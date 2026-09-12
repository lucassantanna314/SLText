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
    /// <summary>
    /// One engine per root namespace. The namespace is baked into the engine, so a solution with
    /// several projects needs one each; every file of the same project shares its engine.
    /// </summary>
    private readonly Dictionary<string, RazorProjectEngine> _razorEngines = new(StringComparer.Ordinal);
    private int _razorEngineReferencesVersion = -1;
    private string _razorImportsContent = string.Empty;

    /// <summary>Owning project directory per source directory, so the .csproj is not re-read per keystroke.</summary>
    private readonly Dictionary<string, string> _projectDirByDirectory = new(StringComparer.Ordinal);

    /// <summary>Virtual path of the synthesized imports file; the engine looks for it at the root.</summary>
    private const string ImportsVirtualPath = "/_Imports.razor";

    /// <summary>Root namespace used for a .razor file that belongs to no project.</summary>
    private const string FallbackRootNamespace = "SLText.Razor";

    /// <summary>
    /// Features that make the engine understand Blazor rather than only MVC.
    /// </summary>
    /// <remarks>
    /// <see cref="DefaultTagHelperDescriptorProvider"/> discovers MVC tag helpers - types published
    /// through a tag-helper assembly attribute. Blazor components are found by
    /// <c>ComponentTagHelperDescriptorProvider</c>, and the directive attributes that only mean
    /// something on a component (<c>@bind-</c>, <c>@onclick</c>, <c>@key</c>, <c>@ref</c>,
    /// <c>@attributes</c>) by the remaining providers. <c>DefaultTypeNameFeature</c> is not a
    /// provider at all: <c>ComponentGenericTypePass</c> refuses to run without it, so a single
    /// generic component such as <c>&lt;MudTable&gt;</c> aborts the whole compilation.
    /// <para>
    /// Registering only the two public features left zero tag helpers discovered. Every component
    /// then degraded to an unknown markup element: <c>&lt;MudTable&gt;</c> produced RZ10012, its
    /// <c>&lt;RowTemplate&gt;</c> child was never bound to a <c>RenderFragment&lt;T&gt;</c>
    /// parameter, and <c>context</c> inside it became CS0103.
    /// </para>
    /// <para>
    /// All of these are internal to the Razor tooling packages, hence the reflection below. The
    /// package versions are pinned in the .csproj, so a name that stops resolving means the pin
    /// moved and is reported once instead of silently regressing every .razor file.
    /// </para>
    /// </remarks>
    private static readonly string[] ComponentFeatureTypeNames =
    {
        "Microsoft.CodeAnalysis.Razor.ComponentTagHelperDescriptorProvider",
        "Microsoft.CodeAnalysis.Razor.BindTagHelperDescriptorProvider",
        "Microsoft.CodeAnalysis.Razor.EventHandlerTagHelperDescriptorProvider",
        "Microsoft.CodeAnalysis.Razor.KeyTagHelperDescriptorProvider",
        "Microsoft.CodeAnalysis.Razor.RefTagHelperDescriptorProvider",
        "Microsoft.CodeAnalysis.Razor.SplatTagHelperDescriptorProvider",
        "Microsoft.CodeAnalysis.Razor.DefaultTypeNameFeature",
    };

    private static readonly System.Reflection.Assembly RazorFeatureAssembly = typeof(CompilationTagHelperFeature).Assembly;

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
            var (virtualPath, rootNamespace) = ToVirtualPath(filePath);

            var engine = GetOrCreateRazorEngine(rootNamespace, BuildImportsContent());

            // Registering the file on the engine's own file system is what lets the engine find
            // _Imports.razor for it; a separate file system instance is never consulted.
            var projectItem = new VirtualProjectItem(virtualPath, normalized);
            ((VirtualRazorFileSystem)engine.FileSystem).Add(projectItem);

            // Design-time, not runtime, code generation. Runtime mode exists to emit an executable
            // BuildRenderTree and carries only a handful of source mappings (~7 for a 100-line
            // file), so most Roslyn diagnostics fell outside every mapping and MapToSource returned
            // null - they were silently dropped. Design-time mode is what the Razor tooling uses for
            // an editor: it assigns every user expression to a throwaway __o and maps all of them
            // back (~68 for the same file), so errors, completion and signature help all have a
            // position to resolve against.
            var codeDocument = engine.ProcessDesignTime(projectItem);
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

    /// <summary>
    /// Maps <paramref name="filePath"/> onto the virtual project the engine sees: a path relative to
    /// the owning project, plus that project's root namespace.
    /// </summary>
    /// <remarks>
    /// Both halves matter and neither is cosmetic. The engine derives the generated namespace from
    /// the path relative to <c>BasePath</c>, so handing it an absolute path produced
    /// <c>__GeneratedComponent.AspNetCore_&lt;checksum&gt;</c> instead of
    /// <c>ASSync.View.Pages.Clients</c>. A generated class under that name never merges with its
    /// <c>.razor.cs</c> code-behind, which left every field declared there - <c>_table</c> behind
    /// <c>@ref="_table"</c>, for instance - unresolved as CS0103.
    /// <para>
    /// A file that belongs to no project keeps a flat path under <see cref="FallbackRootNamespace"/>.
    /// </para>
    /// </remarks>
    private (string VirtualPath, string RootNamespace) ToVirtualPath(string filePath)
    {
        var normalized = filePath.Replace('\\', '/');
        var directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? string.Empty;
        var projectDir = FindProjectDirectory(directory);

        if (projectDir == null)
        {
            return ("/" + Path.GetFileName(normalized), FallbackRootNamespace);
        }

        var relative = normalized[(projectDir.Length + 1)..];
        var rootNamespace = ReferenceResolver.GetProjectAssemblyName(projectDir);

        return ("/" + relative, rootNamespace.Length > 0 ? rootNamespace : FallbackRootNamespace);
    }

    /// <summary>
    /// Walks up from <paramref name="directory"/> to the nearest directory holding a .csproj.
    /// </summary>
    private string? FindProjectDirectory(string directory)
    {
        if (directory.Length == 0) return null;

        if (_projectDirByDirectory.TryGetValue(directory, out var cached))
            return cached.Length > 0 ? cached : null;

        string? found = null;
        for (var current = directory; ; current = Path.GetDirectoryName(current)?.Replace('\\', '/'))
        {
            if (string.IsNullOrEmpty(current)) break;

            if (Directory.EnumerateFiles(current, "*.csproj", SearchOption.TopDirectoryOnly).Any())
            {
                found = current;
                break;
            }

            var parent = Path.GetDirectoryName(current)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent) || parent == current) break;
        }

        _projectDirByDirectory[directory] = found ?? string.Empty;
        return found;
    }

    private RazorProjectEngine GetOrCreateRazorEngine(string rootNamespace, string imports)
    {
        if (_razorEngineReferencesVersion != _referencesVersion || _razorImportsContent != imports)
        {
            _razorEngines.Clear();
            _razorEngineReferencesVersion = _referencesVersion;
            _razorImportsContent = imports;
        }

        if (_razorEngines.TryGetValue(rootNamespace, out var cached)) return cached;

        var references = _references;

        // The engine resolves _Imports.razor through the file system it was created with, so the
        // synthesized imports must live on that instance.
        var fileSystem = new VirtualRazorFileSystem();
        fileSystem.Add(new VirtualProjectItem(ImportsVirtualPath, imports));

        var engine = RazorProjectEngine.Create(
            RazorConfiguration.Create(RazorLanguageVersion.Latest, "MVC-3.0", Enumerable.Empty<RazorExtension>()),
            fileSystem,
            builder =>
            {
                builder.SetRootNamespace(rootNamespace);

                var referenceFeature = new StaticMetadataReferenceFeature();
                foreach (var reference in references) referenceFeature.References.Add(reference);
                builder.Features.Add(referenceFeature);

                builder.Features.Add(new CompilationTagHelperFeature());
                builder.Features.Add(new DefaultTagHelperDescriptorProvider());

                foreach (var feature in CreateComponentFeatures()) builder.Features.Add(feature);

                DisableClassNameMangling(builder);
            });

        _razorEngines[rootNamespace] = engine;
        return engine;
    }

    /// <summary>
    /// Turns off the MVC-style class-name mangling that the component classifier defaults to.
    /// </summary>
    /// <remarks>
    /// Left on, the generated type is <c>__generated__Clients</c> rather than <c>Clients</c>, which
    /// is fine for MVC views compiled at runtime but breaks Blazor: the <c>.razor.cs</c> code-behind
    /// declares <c>partial class Clients</c>, and only an identically named partial merges with it.
    /// The Blazor SDK builds with mangling off, so matching it also keeps the generated code
    /// comparable to what <c>dotnet build</c> produces.
    /// </remarks>
    private static void DisableClassNameMangling(RazorProjectEngineBuilder builder)
    {
        foreach (var feature in builder.Features)
        {
            var property = feature.GetType().GetProperty(
                "MangleClassNames", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            if (property is { CanWrite: true } && property.PropertyType == typeof(bool))
            {
                property.SetValue(feature, false);
            }
        }
    }

    /// <summary>
    /// Instantiates <see cref="ComponentFeatureTypeNames"/>. A type that cannot be resolved is
    /// reported rather than skipped quietly: the failure mode is every component in every .razor
    /// file being flagged as an unknown element, which reads like a problem in the user's code.
    /// </summary>
    private static IEnumerable<IRazorEngineFeature> CreateComponentFeatures()
    {
        foreach (var typeName in ComponentFeatureTypeNames)
        {
            var type = RazorFeatureAssembly.GetType(typeName);

            if (type != null && Activator.CreateInstance(type, nonPublic: true) is IRazorEngineFeature feature)
            {
                yield return feature;
                continue;
            }

            Console.Error.WriteLine(
                $"[SLText] Razor feature '{typeName}' is unavailable; Blazor components will be reported as unknown markup elements.");
        }
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
