using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
    private AdhocWorkspace _workspace;
    private Project _project;
    private readonly Dictionary<string, MetadataReference> _referencesMap = new();
    private RazorCSharpDocument? _lastRazorDoc;
    private string _projectRoot = string.Empty;
    public LspService()
    {
        _workspace = new AdhocWorkspace();
        LoadMetadataReferences();
    }
}







