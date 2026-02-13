using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
    public void LoadProjectFiles(string rootPath, Action<string>? onProgress = null)
    {
        onProgress?.Invoke($"Loading: {rootPath}");
        _projectRoot = rootPath;
        
        var forbiddenDlls = GetForbiddenAssemblies(rootPath);
        var projectRefs = new Dictionary<string, MetadataReference>(_referencesMap, StringComparer.OrdinalIgnoreCase);
        var potentialDlls = Directory.GetFiles(rootPath, "*.dll", SearchOption.AllDirectories);
        
        foreach (var dllPath in potentialDlls)
        {
            if (dllPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            var fileName = Path.GetFileName(dllPath);
            if (forbiddenDlls.Contains(fileName) || fileName.EndsWith(".Views.dll", StringComparison.OrdinalIgnoreCase)) continue;
            
            if (fileName.Equals("MudBlazor.dll", StringComparison.OrdinalIgnoreCase))
            {
                onProgress?.Invoke($"[FOUND] MudBlazor.dll at: {dllPath}");
            }
            
            if (!projectRefs.ContainsKey(fileName))
            {
                try 
                { 
                    projectRefs[fileName] = MetadataReference.CreateFromFile(dllPath);
                    onProgress?.Invoke($"[LOADED] {fileName}");
                } 
                catch (Exception ex) 
                { 
                    onProgress?.Invoke($"[ERROR] Failed to load {fileName}: {ex.Message}");
                }
            }
        }

        InitProject(projectRefs.Values, rootPath);

        var files = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories);
        onProgress?.Invoke($"Found {files.Length} files .cs. Scanning...");

        int loadedCount = 0;
        
        foreach (var file in files)
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;
            
            bool isObj = file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}");
            
            if (isObj)
            {
                if (!file.EndsWith(".g.cs")) continue;
                if (file.EndsWith(".AssemblyAttributes.cs") || file.EndsWith(".AssemblyInfo.cs")) continue;
            }
            else
            {
                if (file.EndsWith(".AssemblyAttributes.cs")) continue;
                if (file.EndsWith(".g.cs") && !file.EndsWith(".razor.g.cs")) continue;
            }

            try 
            {
                var code = File.ReadAllText(file).Replace("\r\n", "\n"); 
                var fileName = Path.GetFileName(file);

                if (!_project.Documents.Any(d => d.FilePath == file))
                {
                    var docInfo = DocumentInfo.Create(
                        DocumentId.CreateNewId(_project.Id),
                        fileName,
                        filePath: file, 
                        loader: TextLoader.From(TextAndVersion.Create(SourceText.From(code), VersionStamp.Create()))
                    );

                    var solution = _workspace.CurrentSolution.AddDocument(docInfo);
                    _workspace.TryApplyChanges(solution);
                    _project = _workspace.CurrentSolution.GetProject(_project.Id)!;
                    loadedCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading {file}: {ex.Message}");
            }
        }
        
        onProgress?.Invoke($"LSP Ready! {loadedCount} files loaded.");
    }
    
    private HashSet<string> GetForbiddenAssemblies(string rootPath)
    {
        var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
        try
        {
            var projectFiles = Directory.GetFiles(rootPath, "*.csproj", SearchOption.AllDirectories);
        
            foreach (var projPath in projectFiles)
            {
                try 
                {
                    var content = File.ReadAllText(projPath);
                    var match = System.Text.RegularExpressions.Regex.Match(content, @"<AssemblyName>(.*?)</AssemblyName>");
                
                    if (match.Success)
                    {
                        forbidden.Add(match.Groups[1].Value.Trim() + ".dll");
                    }
                    else
                    {
                        forbidden.Add(Path.GetFileNameWithoutExtension(projPath) + ".dll");
                    }
                }
                catch {}
            }
        }
        catch { }
    
        return forbidden;
    }
    
    private void FindTypeInNamespaceRecursively(INamespaceSymbol currentNamespace, string targetTypeName, HashSet<string> results)
    {
        var types = currentNamespace.GetTypeMembers(targetTypeName);
    
        if (types.Any(t => t.DeclaredAccessibility == Accessibility.Public))
        {
            var nsDisplay = currentNamespace.ToDisplayString();
            if (!string.IsNullOrEmpty(nsDisplay) && nsDisplay != "<global namespace>")
            {
                results.Add(nsDisplay);
            }
        }

        foreach (var childNs in currentNamespace.GetNamespaceMembers())
        {
            FindTypeInNamespaceRecursively(childNs, targetTypeName, results);
        }
    }
    
    public async Task<List<string>> GetTypeNamespacesAsync(string typeName)
    {
        if (_project == null) return new List<string>();

        var compilation = await _project.GetCompilationAsync();
        if (compilation == null) return new List<string>();
        
        var results = new HashSet<string>();

        var symbols = compilation.GetSymbolsWithName(typeName, SymbolFilter.Type)
            .Where(s => s.DeclaredAccessibility == Accessibility.Public);

        foreach (var symbol in symbols)
        {
            var ns = symbol.ContainingNamespace.ToDisplayString();
            if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>")
                results.Add(ns);
        }
        
        var globalNs = compilation.GlobalNamespace;
        
        FindTypeInNamespaceRecursively(globalNs, typeName, results);

        return results.ToList();
    }
    
    private Document UpdateDocument(string code, string filePath)
    {
        if (_project == null)
        {
            InitProject(_referencesMap.Values, Path.GetDirectoryName(filePath) ?? "");
        }
        
        if (filePath.EndsWith(".razor"))
        {
            var (generatedCode, razorDoc, razorErrors) = CompileRazorToCSharp(code, filePath);
            code = generatedCode;
            _lastRazorDoc = razorDoc;
            filePath += ".g.cs"; 
        }
        
        var normalizedCode = code.Replace("\r\n", "\n");
        
        string safePath = string.IsNullOrEmpty(filePath) ? "new_file.cs" : filePath;
        string fileName = Path.GetFileName(safePath);

        var document = _workspace.CurrentSolution.Projects
            .SelectMany(p => p.Documents)
            .FirstOrDefault(d => d.FilePath == safePath); 

        if (document == null)
        {
            var docId = DocumentId.CreateNewId(_project.Id);
            var docInfo = DocumentInfo.Create(
                docId,
                fileName,
                filePath: safePath, 
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(normalizedCode), VersionStamp.Create()))
            );
            
            var solution = _workspace.CurrentSolution.AddDocument(docInfo);
            _workspace.TryApplyChanges(solution);
        }
        else
        {
            var solution = _workspace.CurrentSolution.WithDocumentText(document.Id, SourceText.From(normalizedCode));
            _workspace.TryApplyChanges(solution);
        }

        _project = _workspace.CurrentSolution.GetProject(_project.Id)!;
        return _project.Documents.First(d => d.FilePath == safePath);
    }
    
}