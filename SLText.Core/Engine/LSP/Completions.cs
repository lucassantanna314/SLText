using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
    private static readonly string[] CSharpKeywords =
    {
        "abstract", "as", "async", "await", "base", "bool", "break", "byte", "case", "catch",
        "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
        "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed",
        "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal",
        "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "record", "ref", "return", "sbyte",
        "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
        "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
        "using", "var", "virtual", "void", "volatile", "while", "yield",
    };

    private IReadOnlySet<string>? _componentNames;
    private int _componentNamesVersion = -1;

    /// <summary>
    /// Simple names of every Blazor component visible to the project, harvested from the referenced
    /// assemblies. Powers both markup completion (<c>&lt;Mud…</c>) and the suppression of bogus
    /// RZ10012 diagnostics.
    /// </summary>
    /// <remarks>Callers must already hold <see cref="_gate"/> - it is not re-entrant.</remarks>
    private async Task<IReadOnlySet<string>> GetBlazorComponentNamesAsync()
    {
        if (_componentNames != null && _componentNamesVersion == _referencesVersion) return _componentNames;

        var names = new HashSet<string>(StringComparer.Ordinal);
        _componentNamesVersion = _referencesVersion;
        _componentNames = names;

        if (_project == null) return names;

        var compilation = await _project.GetCompilationAsync().ConfigureAwait(false);
        if (compilation == null) return names;

        var componentBase = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.ComponentBase");
        var iComponent = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.IComponent");
        if (componentBase == null && iComponent == null) return names;

        const int maxTypes = 20000;
        int scanned = 0;

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (scanned > maxTypes) break;

            try
            {
                var root = assembly.GlobalNamespace;
                ScanNamespace(root, componentBase, iComponent, names, ref scanned, maxTypes);
            }
            catch
            {
                // A malformed or native reference must not break IntelliSense for the rest.
            }
        }

        return names;
    }

    private static void ScanNamespace(
        INamespaceSymbol namespaceSymbol,
        INamedTypeSymbol? componentBase,
        INamedTypeSymbol? iComponent,
        HashSet<string> names,
        ref int scanned,
        int maxTypes)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            if (scanned++ > maxTypes) return;
            if (!IsComponent(type, componentBase, iComponent)) continue;
            names.Add(type.Name);
        }

        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            if (scanned > maxTypes) return;
            ScanNamespace(child, componentBase, iComponent, names, ref scanned, maxTypes);
        }
    }

    private static bool IsComponent(INamedTypeSymbol type, INamedTypeSymbol? componentBase, INamedTypeSymbol? iComponent)
    {
        if (type.DeclaredAccessibility != Accessibility.Public) return false;
        if (type.IsAbstract || type.TypeKind != TypeKind.Class) return false;

        for (var current = type.BaseType; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, componentBase)) return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, iComponent)) return true;
        }

        return false;
    }

    /// <summary>
    /// Completion at <paramref name="cursorPosition"/>, a 0-based offset into <paramref name="code"/>.
    /// </summary>
    public async Task<IEnumerable<CompletionItem>> GetCompletionsAsync(string code, int cursorPosition, string filePath)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(filePath)) return Enumerable.Empty<CompletionItem>();
        if (cursorPosition < 0 || cursorPosition > code.Length) return Enumerable.Empty<CompletionItem>();

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            bool isRazor = filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);

            if (isRazor)
            {
                var markup = await TryGetMarkupCompletionsAsync(code, cursorPosition).ConfigureAwait(false);
                if (markup != null) return markup;
            }

            string workspacePath = isRazor ? filePath + ".g.cs" : filePath;
            string analysisCode = code.Replace("\r\n", "\n");
            int analysisPosition = cursorPosition;

            if (isRazor)
            {
                var razor = CompileRazor(code, filePath);
                if (razor == null) return Enumerable.Empty<CompletionItem>();

                var mapped = razor.MapToGenerated(cursorPosition);
                if (mapped == null) return Enumerable.Empty<CompletionItem>();

                _razorFiles[filePath] = razor;
                analysisCode = razor.GeneratedCode;
                analysisPosition = mapped.Value;
            }

            var document = UpdateDocument(analysisCode, workspacePath);
            bool isMemberAccess = analysisPosition > 0 && analysisCode[analysisPosition - 1] == '.';

            // CompletionService is only available when the Features assembly took part in the
            // workspace's MEF composition. When it is absent - or yields nothing at this position -
            // fall through to symbol lookup rather than showing the user an empty list.
            var completionService = CompletionService.GetService(document);
            if (completionService != null)
            {
                var trigger = isMemberAccess
                    ? CompletionTrigger.CreateInsertionTrigger('.')
                    : CompletionTrigger.Invoke;

                var completions = await completionService.GetCompletionsAsync(document, analysisPosition, trigger)
                    .ConfigureAwait(false);

                if (completions != null && completions.ItemsList.Count > 0) return completions.ItemsList;
            }

            return await GetFallbackCompletionsAsync(document, analysisCode, analysisPosition, isMemberAccess)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SLText] Completion failed: {ex.Message}");
            return Enumerable.Empty<CompletionItem>();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Offers component names when the caret sits inside a Razor markup tag. Roslyn only ever sees
    /// the generated C#, so it cannot complete <c>&lt;MudButton</c>; that has to come from the
    /// component index instead.
    /// </summary>
    /// <remarks>Callers must already hold <see cref="_gate"/>.</remarks>
    private async Task<List<CompletionItem>?> TryGetMarkupCompletionsAsync(string code, int cursorPosition)
    {
        int searchFrom = Math.Max(0, cursorPosition - 1);
        int lineStart = cursorPosition == 0 ? 0 : code.LastIndexOf('\n', searchFrom) + 1;
        if (lineStart > cursorPosition) return null;

        string linePrefix = code[lineStart..cursorPosition];

        int openAngle = linePrefix.LastIndexOf('<');
        if (openAngle < 0) return null;

        string afterAngle = linePrefix[(openAngle + 1)..];

        // Only complete a bare element name - not attributes, closing tags or expressions.
        if (afterAngle.Length == 0) return null;
        if (afterAngle[0] == '/' || afterAngle[0] == '@') return null;
        if (afterAngle.Any(c => c is ' ' or '>' or '/' or '=' or '"')) return null;

        var names = await GetBlazorComponentNamesAsync().ConfigureAwait(false);
        if (names.Count == 0) return null;

        return names
            .Where(n => n.StartsWith(afterAngle, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(name => CompletionItem.Create(name))
            .ToList();
    }

    private async Task<IEnumerable<CompletionItem>> GetFallbackCompletionsAsync(
        Document document, string analysisCode, int analysisPosition, bool isMemberAccess)
    {
        var semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);
        if (semanticModel == null) return Enumerable.Empty<CompletionItem>();

        if (isMemberAccess)
        {
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var token = root?.FindToken(Math.Max(0, analysisPosition - 1));

            if (token?.Parent is MemberAccessExpressionSyntax memberAccess)
            {
                var type = semanticModel.GetTypeInfo(memberAccess.Expression).Type;
                if (type != null && type.TypeKind != TypeKind.Error)
                {
                    return type.GetMembers()
                        .Where(s => s.DeclaredAccessibility == Accessibility.Public &&
                                    !s.IsStatic &&
                                    !s.IsImplicitlyDeclared &&
                                    s is not IMethodSymbol { MethodKind: not MethodKind.Ordinary })
                        .Select(s => s.Name)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(n => n, StringComparer.Ordinal)
                        .Select(name => CompletionItem.Create(name))
                        .ToList();
                }
            }

            return Enumerable.Empty<CompletionItem>();
        }

        var symbols = semanticModel.LookupSymbols(analysisPosition)
            .Where(s => !s.IsImplicitlyDeclared && s.Name != ".ctor")
            .Select(s => s.Name);

        return symbols
            .Concat(CSharpKeywords)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(name => CompletionItem.Create(name))
            .ToList();
    }
}
