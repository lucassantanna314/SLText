using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Host.Mef;

namespace SLText.Core.Engine.LSP;

/// <summary>
/// Roslyn-backed language service for the editor: diagnostics, completion and signature help
/// for C# and Razor/Blazor files.
///
/// Threading: every public entry point mutates the same <see cref="AdhocWorkspace"/>. Roslyn
/// workspaces tolerate concurrent <c>TryApplyChanges</c>, but a completion request for one tab
/// must not overwrite the document another tab is compiling. All mutation therefore runs under
/// <see cref="_gate"/>, and per-file Razor state is keyed by path instead of held in a single field.
/// </summary>
public partial class LspService : IDisposable
{
    /// <summary>
    /// Host services with the full Roslyn feature set composed in.
    /// </summary>
    /// <remarks>
    /// <c>new AdhocWorkspace()</c> composes MEF from whatever Roslyn assemblies happen to be loaded
    /// at that moment. Without <c>Microsoft.CodeAnalysis.CSharp.Features</c> in the composition,
    /// <see cref="Microsoft.CodeAnalysis.Completion.CompletionService.GetService"/> returns nothing
    /// usable and C# completion silently degrades to a raw symbol dump - which is why the editor
    /// used to offer ~500 alphabetically sorted type names instead of context-aware items.
    /// </remarks>
    private static readonly string[] FeatureAssemblies =
    {
        "Microsoft.CodeAnalysis.Workspaces",
        "Microsoft.CodeAnalysis.CSharp.Workspaces",
        "Microsoft.CodeAnalysis.Features",
        "Microsoft.CodeAnalysis.CSharp.Features",
    };

    private static readonly Lazy<MefHostServices> HostServices = new(() =>
    {
        foreach (var name in FeatureAssemblies)
        {
            try { System.Reflection.Assembly.Load(name); }
            catch { /* only the assemblies that are actually referenced will load */ }
        }

        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic &&
                        !a.IsCollectible &&
                        !string.IsNullOrEmpty(a.Location) &&
                        (a.GetName().Name?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) ?? false))
            .ToArray();

        return MefHostServices.Create(assemblies);
    });

    private readonly AdhocWorkspace _workspace = new(HostServices.Value);
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Project? _project;
    private string _projectRoot = string.Empty;

    private IReadOnlyList<MetadataReference> _references = Array.Empty<MetadataReference>();
    private IReadOnlyList<string> _projectAssemblyNames = Array.Empty<string>();

    /// <summary>Bumped whenever the reference set changes; invalidates derived caches.</summary>
    private int _referencesVersion;

    /// <summary>Generated-code state per Razor file, so switching tabs cannot cross-map diagnostics.</summary>
    private readonly Dictionary<string, RazorFileInfo> _razorFiles = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    public LspService()
    {
        LoadFrameworkReferences();
    }

    /// <summary>True once a project folder has been loaded and references resolved.</summary>
    public bool IsProjectLoaded => _projectRoot.Length > 0;

    public string ProjectRoot => _projectRoot;

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(LspService));
    }

    public async Task<(string filePath, int line, int column)?> GetDefinitionAsync(string filePath, int line, int column)
    {
        ThrowIfDisposed();

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            // --- Normal .cs / non-Razor path ---
            var normalDocId = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
            if (normalDocId != null)
            {
                return await ResolveFromNormalDocument(filePath, line, column).ConfigureAwait(false);
            }

            // --- .razor path: compile and use generated code ---
            if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            {
                return await ResolveFromRazorFile(filePath, line, column).ConfigureAwait(false);
            }

            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Resolve go-to-definition for a standard workspace document (.cs, etc.).</summary>
    private async Task<(string filePath, int line, int column)?> ResolveFromNormalDocument(
        string filePath, int line, int column)
    {
        var documentId = _workspace.CurrentSolution.GetDocumentIdsWithFilePath(filePath).FirstOrDefault();
        if (documentId == null) return null;

        var document = _workspace.CurrentSolution.GetDocument(documentId);
        if (document == null) return null;

        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null) return null;

        var text = await syntaxTree.GetTextAsync();
        if (line >= text.Lines.Count) return null;

        int position = text.Lines[line].Start + Math.Min(column, text.Lines[line].Span.Length);
        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return null;

        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, position, _workspace);
        if (symbol == null)
        {
            var token = (await syntaxTree.GetRootAsync()).FindToken(position);
            symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, token.SpanStart, _workspace);
        }

        return ExtractDefinition(symbol);
    }

    /// <summary>Resolve go-to-definition for a .razor file by compiling its generated C#.</summary>
    private async Task<(string filePath, int line, int column)?> ResolveFromRazorFile(
        string razorPath, int sourceLine, int sourceCol)
    {
        // 1. Read raw .razor source
        string rawCode;
        try
        {
            rawCode = File.ReadAllText(razorPath).Replace("\r\n", "\n");
        }
        catch
        {
            return null;
        }

        // 2. Compile Razor → generate C# + position mappings
        var razorInfo = CompileRazor(rawCode, razorPath);
        if (razorInfo == null) return null;

        // 3. Store for future calls (diagnostics, completion share this)
        _razorFiles[razorPath] = razorInfo;

        // 4. Convert source (line, col) → absolute index in .razor file
        var lines = rawCode.Split('\n');
        if (sourceLine < 0 || sourceLine >= lines.Length) return null;

        int sourceAbsoluteIndex = 0;
        for (int i = 0; i < sourceLine && i < lines.Length; i++)
        {
            sourceAbsoluteIndex += lines[i].Length + 1; // +1 for newline
        }
        sourceAbsoluteIndex += Math.Min(sourceCol, lines[sourceLine].Length);

        // 5. Map source position → generated code position
        int? generatedPos = razorInfo.MapToGenerated(sourceAbsoluteIndex);
        if (generatedPos == null || generatedPos.Value < 0 || generatedPos.Value > razorInfo.GeneratedCode.Length)
            return null;

        int analysisPosition = generatedPos.Value;

        // 6. Create/update virtual document at filePath+".g.cs"
        string workspacePath = razorPath + ".g.cs";
        var document = UpdateDocument(razorInfo.GeneratedCode, workspacePath);

        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree == null) return null;

        var semanticModel = await document.GetSemanticModelAsync();
        if (semanticModel == null) return null;

        // 7. Find symbol at the generated-code position
        var symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, analysisPosition, _workspace);
        if (symbol == null)
        {
            var root = await syntaxTree.GetRootAsync();
            var token = root.FindToken(analysisPosition);
            symbol = await SymbolFinder.FindSymbolAtPositionAsync(semanticModel, token.SpanStart, _workspace);
        }

        return ExtractDefinitionMapped(symbol, razorInfo);
    }

    /// <summary>Extract a definition location from a Roslyn ISymbol.</summary>
    private static (string filePath, int line, int column)? ExtractDefinition(ISymbol? symbol)
    {
        if (symbol == null) return null;

        var targetSymbol = symbol.IsDefinition ? symbol : symbol.OriginalDefinition;
        var syntaxRef = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault();

        if (syntaxRef != null)
        {
            var targetTree = syntaxRef.SyntaxTree;
            var lineSpan = targetTree.GetLineSpan(syntaxRef.Span);
            return (targetTree.FilePath, lineSpan.StartLinePosition.Line, lineSpan.StartLinePosition.Character);
        }

        return null;
    }

    /// <summary>
    /// Like <see cref="ExtractDefinition"/> but maps returned paths back through the
    /// Razor source-mapping so callers get real file paths and source coordinates.
    /// </summary>
    private (string filePath, int line, int column)? ExtractDefinitionMapped(ISymbol? symbol, RazorFileInfo razorInfo)
    {
        if (symbol == null) return null;

        var targetSymbol = symbol.IsDefinition ? symbol : symbol.OriginalDefinition;
        var syntaxRef = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault();

        if (syntaxRef == null) return null;

        var targetTree = syntaxRef.SyntaxTree;
        var span = syntaxRef.Span;

        // Case 1: definition lives in a .cs code-behind file — return path as-is.
        if (targetTree.FilePath != null && !targetTree.FilePath.EndsWith(".g.cs", StringComparison.Ordinal))
        {
            var lineSpan = targetTree.GetLineSpan(span);
            return (targetTree.FilePath, lineSpan.StartLinePosition.Line, lineSpan.StartLinePosition.Character);
        }

        // Case 2: definition is in the generated .g.cs virtual document.
        // Attempt to map back to the original .razor source file.
        var start = razorInfo.MapToSource(span.Start);
        if (!start.HasValue)
        {
            var end = razorInfo.MapToSource(span.End);
            if (end.HasValue)
            {
                return (razorInfo.FilePath, end.Value.Line - 1, end.Value.Column);
            }
            // Neither start nor end could be mapped — return the .razor path
            // with approximate coordinates so the editor can at least open it.
            return (razorInfo.FilePath, 0, 0);
        }

        return (razorInfo.FilePath, start.Value.Line - 1, start.Value.Column);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gate.Dispose();
        _workspace.Dispose();
        GC.SuppressFinalize(this);
    }
}
