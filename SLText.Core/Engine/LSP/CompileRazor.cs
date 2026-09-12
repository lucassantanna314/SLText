using System.Text;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;

namespace SLText.Core.Engine.LSP;

/// <summary>
/// Holds the generated C# for one .razor file plus the source mappings needed to translate
/// positions between the generated document and the user's file.
/// </summary>
internal sealed class RazorFileInfo
{
    public required string FilePath { get; init; }
    public required string GeneratedPath { get; init; }
    public required string GeneratedCode { get; init; }
    public required RazorCSharpDocument CSharpDocument { get; init; }
    public required IReadOnlyList<RazorDiagnostic> RazorErrors { get; init; }

    private readonly SourceText _sourceText;

    public RazorFileInfo(string sourceText)
    {
        _sourceText = SourceText.From(sourceText);
    }

    /// <summary>
    /// Translates an absolute index in the generated C# back to a position in the .razor file.
    /// Returns null when the position lies in generated boilerplate that has no user-facing origin.
    /// </summary>
    /// <remarks>
    /// Returning null matters: the old code fell back to the *generated* line number, which put
    /// errors ~80 lines past the end of the source file and made every squiggle appear on an
    /// unrelated line.
    /// <para>
    /// <c>Line</c> is 1-based and <c>Column</c> is 0-based, matching the renderer's convention
    /// (it indexes the line text directly with the column).
    /// </para>
    /// </remarks>
    public (int Line, int Column)? MapToSource(int generatedAbsoluteIndex)
    {
        foreach (var mapping in CSharpDocument.SourceMappings)
        {
            int start = mapping.GeneratedSpan.AbsoluteIndex;
            int end = start + mapping.GeneratedSpan.Length;
            if (generatedAbsoluteIndex < start || generatedAbsoluteIndex > end) continue;

            int original = mapping.OriginalSpan.AbsoluteIndex + (generatedAbsoluteIndex - start);
            if (original < 0 || original > _sourceText.Length) return null;

            var position = _sourceText.Lines.GetLinePosition(original);
            return (position.Line + 1, position.Character);
        }

        return null;
    }

    /// <summary>
    /// Translates an absolute index in the .razor file to the equivalent position in the generated
    /// C#, so Roslyn completion/signature-help can be queried at the right offset.
    /// </summary>
    public int? MapToGenerated(int sourceAbsoluteIndex)
    {
        foreach (var mapping in CSharpDocument.SourceMappings)
        {
            int start = mapping.OriginalSpan.AbsoluteIndex;
            int end = start + mapping.OriginalSpan.Length;
            if (sourceAbsoluteIndex < start || sourceAbsoluteIndex > end) continue;

            int candidate = mapping.GeneratedSpan.AbsoluteIndex + (sourceAbsoluteIndex - start);
            return Math.Clamp(candidate, 0, GeneratedCode.Length);
        }

        return null;
    }
}

public partial class LspService
{
    private RazorProjectEngine? _razorEngine;
    private int _razorEngineReferencesVersion = -1;
    private string _razorImportsSignature = string.Empty;

    /// <summary>
    /// Generates C# for a .razor file.
    /// </summary>
    /// <remarks>
    /// The Razor engine is cached and only rebuilt when the reference set changes. Previously a new
    /// engine (plus a re-read of the .csproj and a reflection-based tag-helper dump) was built on
    /// every keystroke, costing ~1.4s on the first call.
    /// </remarks>
    private RazorFileInfo? CompileRazor(string razorCode, string filePath)
    {
        try
        {
            var normalized = razorCode.Replace("\r\n", "\n");
            var virtualPath = ToVirtualPath(filePath);

            var imports = BuildImportsContent();
            var engine = GetOrCreateRazorEngine(imports);

            var fileSystem = new VirtualRazorFileSystem();
            fileSystem.Add(new VirtualProjectItem("/_Imports.razor", imports));

            var projectItem = new VirtualProjectItem(virtualPath, normalized);
            fileSystem.Add(projectItem);

            var codeDocument = engine.Process(projectItem);
            var csharpDocument = codeDocument.GetCSharpDocument();

            return new RazorFileInfo(normalized)
            {
                FilePath = filePath,
                GeneratedPath = filePath + ".g.cs",
                GeneratedCode = csharpDocument.GeneratedCode,
                CSharpDocument = csharpDocument,
                RazorErrors = csharpDocument.Diagnostics.ToList(),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SLText] Razor compile failed for {filePath}: {ex.Message}");
            return null;
        }
    }

    private static string ToVirtualPath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        return normalized.StartsWith('/') ? normalized : "/" + normalized;
    }

    private RazorProjectEngine GetOrCreateRazorEngine(string imports)
    {
        string signature = _referencesVersion.ToString() + "|" + imports.Length;

        if (_razorEngine != null &&
            _razorEngineReferencesVersion == _referencesVersion &&
            _razorImportsSignature == signature)
        {
            return _razorEngine;
        }

        var references = _references;

        _razorEngine = RazorProjectEngine.Create(
            RazorConfiguration.Create(RazorLanguageVersion.Latest, "MVC-3.0", Enumerable.Empty<RazorExtension>()),
            new VirtualRazorFileSystem(),
            builder =>
            {
                builder.SetNamespace("SLText.Razor");

                var referenceFeature = new StaticMetadataReferenceFeature();
                foreach (var reference in references) referenceFeature.References.Add(reference);
                builder.Features.Add(referenceFeature);

                builder.Features.Add(new CompilationTagHelperFeature());
                builder.Features.Add(new DefaultTagHelperDescriptorProvider());
            });

        _razorEngineReferencesVersion = _referencesVersion;
        _razorImportsSignature = signature;
        return _razorEngine;
    }

    /// <summary>
    /// Reads the project's own <c>_Imports.razor</c> files so the component set matches the real
    /// build. A hard-coded "@using MudBlazor" made every non-Blazor project emit CS0246 and made
    /// Blazor projects miss all of their own usings.
    /// </summary>
    private string BuildImportsContent()
    {
        var sb = new StringBuilder();

        sb.AppendLine("@using System");
        sb.AppendLine("@using System.Collections.Generic");
        sb.AppendLine("@using System.Linq");
        sb.AppendLine("@using System.Threading.Tasks");
        sb.AppendLine("@using Microsoft.AspNetCore.Components");
        sb.AppendLine("@using Microsoft.AspNetCore.Components.Web");
        sb.AppendLine("@inherits Microsoft.AspNetCore.Components.ComponentBase");

        if (string.IsNullOrEmpty(_projectRoot) || !Directory.Exists(_projectRoot)) return sb.ToString();

        try
        {
            foreach (var imports in Directory.EnumerateFiles(_projectRoot, "_Imports.razor", SearchOption.AllDirectories))
            {
                if (imports.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    imports.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                sb.AppendLine();
                sb.AppendLine(File.ReadAllText(imports));
            }

            foreach (var assemblyName in _projectAssemblyNames)
            {
                sb.AppendLine($"@addTagHelper *, {assemblyName}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SLText] Could not read _Imports.razor: {ex.Message}");
        }

        return sb.ToString();
    }

    private sealed class StaticMetadataReferenceFeature : IMetadataReferenceFeature
    {
        public List<MetadataReference> References { get; } = new();
        public RazorEngine Engine { get; set; } = null!;
        IReadOnlyList<MetadataReference> IMetadataReferenceFeature.References => References;
    }

    private sealed class VirtualRazorFileSystem : RazorProjectFileSystem
    {
        private readonly Dictionary<string, RazorProjectItem> _items = new(StringComparer.Ordinal);

        public void Add(RazorProjectItem item) => _items[item.FilePath] = item;

        public override IEnumerable<RazorProjectItem> EnumerateItems(string basePath) => _items.Values;

        public override RazorProjectItem GetItem(string path) => GetItem(path, fileKind: null);

        public override RazorProjectItem GetItem(string path, string? fileKind) =>
            _items.TryGetValue(path, out var item)
                ? item
                // A missing item must report Exists == false; claiming it exists made the engine
                // treat absent _ViewImports files as real (empty) inputs.
                : new VirtualProjectItem(path, string.Empty, exists: false);
    }

    private sealed class VirtualProjectItem : RazorProjectItem
    {
        private readonly byte[] _content;
        private readonly bool _exists;

        public VirtualProjectItem(string filePath, string content, bool exists = true)
        {
            FilePath = filePath.Replace('\\', '/');
            _content = Encoding.UTF8.GetBytes(content);
            _exists = exists;
        }

        public override string BasePath => "/";
        public override string FilePath { get; }
        public override string FileKind => "component";
        public override bool Exists => _exists;
        public override string? PhysicalPath => null;

        // Must be an override: hiding the base member (the old code did) meant the engine read the
        // base implementation and resolved the wrong physical path.
        public override string RelativePhysicalPath => FilePath.TrimStart('/');

        public override Stream Read() => new MemoryStream(_content);
    }
}
