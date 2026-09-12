using Microsoft.CodeAnalysis;
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _gate.Dispose();
        _workspace.Dispose();
        GC.SuppressFinalize(this);
    }
}
