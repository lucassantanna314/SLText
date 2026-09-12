using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
    /// <summary>
    /// Baseline references available before a project folder is opened, so that a loose .cs file
    /// still gets BCL IntelliSense.
    /// </summary>
    private void LoadFrameworkReferences()
    {
        var resolved = ReferenceResolver.Resolve(string.Empty, null);
        ApplyReferences(resolved.References, Array.Empty<string>());
    }

    private void ApplyReferences(IReadOnlyList<MetadataReference> references, IReadOnlyList<string> projectAssemblyNames)
    {
        _references = references;
        _projectAssemblyNames = projectAssemblyNames;
        _referencesVersion++;
        _razorEngines.Clear();
        _projectDirByDirectory.Clear();
    }

    /// <summary>Namespaces that always exist in any .NET compilation.</summary>
    private static readonly string[] SafeGlobalUsings =
    {
        "System",
        "System.Collections.Generic",
        "System.IO",
        "System.Linq",
        "System.Net.Http",
        "System.Threading",
        "System.Threading.Tasks",
    };

    /// <summary>
    /// Emitted only when the matching assembly is actually referenced. Emitting them
    /// unconditionally produced CS0246 "namespace does not exist" errors in every file of every
    /// project that was not a MudBlazor/Blazor app.
    /// </summary>
    private static readonly (string Namespace, string AssemblyFile)[] ConditionalGlobalUsings =
    {
        ("Microsoft.AspNetCore.Components", "Microsoft.AspNetCore.Components.dll"),
        ("Microsoft.AspNetCore.Components.Web", "Microsoft.AspNetCore.Components.Web.dll"),
        ("Microsoft.AspNetCore.Components.Forms", "Microsoft.AspNetCore.Components.Forms.dll"),
        ("Microsoft.AspNetCore.Builder", "Microsoft.AspNetCore.Builder.dll"),
        ("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Hosting.dll"),
        ("Microsoft.AspNetCore.Http", "Microsoft.AspNetCore.Http.dll"),
        ("Microsoft.AspNetCore.Routing", "Microsoft.AspNetCore.Routing.dll"),
        ("Microsoft.Extensions.Configuration", "Microsoft.Extensions.Configuration.dll"),
        ("Microsoft.Extensions.DependencyInjection", "Microsoft.Extensions.DependencyInjection.dll"),
        ("Microsoft.Extensions.Hosting", "Microsoft.Extensions.Hosting.dll"),
        ("Microsoft.Extensions.Logging", "Microsoft.Extensions.Logging.dll"),
        ("Microsoft.JSInterop", "Microsoft.JSInterop.dll"),
        ("MudBlazor", "MudBlazor.dll"),
    };

    private static readonly Regex ImplicitUsingsRegex = new(@"<ImplicitUsings>\s*(enable|true)\s*</ImplicitUsings>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void InitProject(IEnumerable<MetadataReference> refs, string rootPath)
    {
        ThrowIfDisposed();

        var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Create(),
                "SLTextAnalysis",
                "SLTextAnalysis",
                LanguageNames.CSharp)
            .WithMetadataReferences(refs)
            .WithCompilationOptions(new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable,
                // A loose file opened on its own has no entry point; do not nag about it.
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>
                {
                    ["CS5001"] = ReportDiagnostic.Suppress,
                    // Top-level statements are legal in Program.cs and clash with the virtual project.
                    ["CS8802"] = ReportDiagnostic.Suppress,
                }))
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Latest));

        var solution = _workspace.CurrentSolution;
        if (_project != null) solution = solution.RemoveProject(_project.Id);
        solution = solution.AddProject(projectInfo);

        if (!_workspace.TryApplyChanges(solution))
        {
            throw new InvalidOperationException("Failed to apply the analysis project to the workspace.");
        }

        _project = _workspace.CurrentSolution.GetProject(projectInfo.Id)!;
        _projectRoot = rootPath;
    }

    /// <summary>
    /// Builds the implicit-usings document.
    ///
    /// Two passes are required: the project's own namespaces can only be known after its syntax
    /// trees exist, and a <c>global using</c> pointing at a namespace that does not exist is itself
    /// a compile error (CS0246). The old implementation guessed a namespace from every folder that
    /// contained a file, plus a hard-coded ".Shared", which flooded unrelated projects with errors.
    /// </summary>
    private async Task AddImplicitUsingsDocumentAsync(bool implicitUsingsEnabled)
    {
        if (_project == null) return;

        var declared = new HashSet<string>(StringComparer.Ordinal);
        if (implicitUsingsEnabled)
        {
            var compilation = await _project.GetCompilationAsync().ConfigureAwait(false);
            if (compilation != null)
            {
                foreach (var tree in compilation.SyntaxTrees)
                {
                    var root = await tree.GetRootAsync().ConfigureAwait(false);
                    foreach (var ns in root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax>())
                    {
                        declared.Add(ns.Name.ToString());
                    }
                    foreach (var ns in root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax>())
                    {
                        declared.Add(ns.Name.ToString());
                    }
                }
            }
        }

        var referencedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in _references)
        {
            if (reference is PortableExecutableReference { FilePath: not null } per)
                referencedFiles.Add(Path.GetFileName(per.FilePath));
        }

        var sb = new StringBuilder();
        if (implicitUsingsEnabled)
        {
            foreach (var ns in SafeGlobalUsings) sb.Append("global using ").Append(ns).AppendLine(";");

            foreach (var (ns, assemblyFile) in ConditionalGlobalUsings)
            {
                if (referencedFiles.Contains(assemblyFile))
                    sb.Append("global using ").Append(ns).AppendLine(";");
            }

            foreach (var ns in declared.OrderBy(n => n, StringComparer.Ordinal))
                sb.Append("global using ").Append(ns).AppendLine(";");
        }

        var docInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(_project.Id),
            "SLText.ImplicitUsings.g.cs",
            filePath: ImplicitUsingsPath,
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(sb.ToString()), VersionStamp.Create())));

        var solution = _workspace.CurrentSolution.AddDocument(docInfo);
        _workspace.TryApplyChanges(solution);
        _project = _workspace.CurrentSolution.GetProject(_project.Id)!;
    }

    /// <summary>Virtual path of the generated usings document; never a real file.</summary>
    internal const string ImplicitUsingsPath = "/__sltext__/ImplicitUsings.g.cs";

    internal static bool HasImplicitUsingsEnabled(string projectDir)
    {
        try
        {
            var csproj = Directory.EnumerateFiles(projectDir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (csproj == null) return true;
            return ImplicitUsingsRegex.IsMatch(File.ReadAllText(csproj));
        }
        catch
        {
            return true;
        }
    }
}
