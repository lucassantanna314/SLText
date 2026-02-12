using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SLText.Core.Engine;

public class LspService
{
    private AdhocWorkspace _workspace;
    private Project _project;
    private readonly Dictionary<string, MetadataReference> _referencesMap = new();
    public LspService()
    {
        _workspace = new AdhocWorkspace();
    
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
        
        if (runtimeDir != null)
        {
            string[] coreLibraries = {
                "System.Runtime.dll",
                "mscorlib.dll",
                "System.Console.dll",
                "System.Collections.dll",
                "System.Linq.dll",
                "netstandard.dll",
                "System.Private.CoreLib.dll"
            };

            foreach (var lib in coreLibraries)
            {
                var path = Path.Combine(runtimeDir, lib);
                if (File.Exists(path))
                {
                    var reference = MetadataReference.CreateFromFile(path);
                    _referencesMap[lib] = reference;
                }
            }
            
            LoadReferencesFromDirectory(runtimeDir);
        
            var coreParent = Directory.GetParent(runtimeDir); 
            var sharedParent = coreParent?.Parent;         
            var dotnetRoot = sharedParent?.Parent?.FullName; 

            if (dotnetRoot != null)
            {
                var aspNetCoreRoot = Path.Combine(dotnetRoot, "Microsoft.AspNetCore.App");
                if (Directory.Exists(aspNetCoreRoot))
                {
                    var latestVersion = Directory.GetDirectories(aspNetCoreRoot)
                        .OrderByDescending(d => d)
                        .FirstOrDefault();

                    if (latestVersion != null)
                    {
                        LoadReferencesFromDirectory(latestVersion);
                        Console.WriteLine($"[LSP] ASP.NET Core Framework carregado de: {latestVersion}");
                    }
                }
            }
        }
    }
    
    private string GetProjectAssemblyName(string rootPath)
    {
        try
        {
            var csprojPath = Directory.GetFiles(rootPath, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();

            if (csprojPath == null) 
                return string.Empty; 

            var csprojContent = File.ReadAllText(csprojPath);
        
            var match = System.Text.RegularExpressions.Regex.Match(csprojContent, @"<AssemblyName>(.*?)</AssemblyName>");
            if (match.Success)
            {
                return match.Groups[1].Value.Trim() + ".dll";
            }

            return Path.GetFileNameWithoutExtension(csprojPath) + ".dll";
        }
        catch
        {
            return string.Empty;
        }
    }
    
    private void LoadReferencesFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;

        foreach (var file in Directory.GetFiles(directoryPath, "*.dll"))
        {
            var fileName = Path.GetFileName(file);
            
            if (!_referencesMap.ContainsKey(fileName))
            {
                try
                {
                    var reference = MetadataReference.CreateFromFile(file);
                    _referencesMap.Add(fileName, reference);
                }
                catch 
                { 
                    //
                }
            }
        }
    }
    
    private void InitProject(IEnumerable<MetadataReference> refs, string rootPath)
    {
        var projectId = ProjectId.CreateNewId();
        
        var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            
        var projectInfo = ProjectInfo.Create(
                projectId, 
                VersionStamp.Create(), 
                "VirtualProject", 
                "VirtualProject", 
                LanguageNames.CSharp)
            .WithMetadataReferences(refs)
            .WithCompilationOptions(compilationOptions)
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Latest));
        
        if (_project != null)
        {
            var newSolution = _workspace.CurrentSolution.RemoveProject(_project.Id).AddProject(projectInfo);
            _workspace.TryApplyChanges(newSolution);
            _project = _workspace.CurrentSolution.GetProject(projectId)!;
        }
        else
        {
            _project = _workspace.AddProject(projectInfo);
        }
        
        const string baseGlobalUsings = @"global using System;
              global using System.Collections.Generic;
              global using System.IO;
              global using System.Linq;
              global using System.Net.Http;
              global using System.Threading;
              global using System.Threading.Tasks;
              global using Microsoft.AspNetCore.Builder;
              global using Microsoft.AspNetCore.Hosting;
              global using Microsoft.AspNetCore.Http;
              global using Microsoft.AspNetCore.Routing;
              global using Microsoft.Extensions.Configuration;
              global using Microsoft.Extensions.DependencyInjection;
              global using Microsoft.Extensions.Hosting;
              global using Microsoft.Extensions.Logging;
         
              global using Microsoft.AspNetCore.Components;
              global using Microsoft.AspNetCore.Components.Web;
              global using Microsoft.AspNetCore.Components.Forms;
              global using Microsoft.JSInterop;
              global using MudBlazor; 
              global using MudBlazor.Services;";
        
        var projectFolderUsings = GenerateProjectNamespaces(rootPath);
        var finalGlobalUsings = baseGlobalUsings + Environment.NewLine + projectFolderUsings;
        
        var docInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(_project.Id),
            "GlobalUsings.g.cs", 
            filePath: "GlobalUsings.g.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(finalGlobalUsings), VersionStamp.Create()))
        );
        
        var solution = _workspace.CurrentSolution.AddDocument(docInfo);
        _workspace.TryApplyChanges(solution);
        
        _project = _workspace.CurrentSolution.GetProject(_project.Id)!;
    }
    
    private string GenerateProjectNamespaces(string rootPath)
    {
        var sb = new StringBuilder();
        var rootNamespace = GetProjectAssemblyName(rootPath).Replace(".dll", ""); 

        if (string.IsNullOrEmpty(rootNamespace)) return "";

        sb.AppendLine("global using " + rootNamespace + ";");

        var directories = Directory.GetDirectories(rootPath, "*", SearchOption.AllDirectories);
    
        foreach (var dir in directories)
        {
            if (dir.Contains($"{Path.DirectorySeparatorChar}obj") || 
                dir.Contains($"{Path.DirectorySeparatorChar}bin") || 
                dir.Contains(".git"))
                continue;

            if (Directory.GetFiles(dir, "*.cs").Any() || Directory.GetFiles(dir, "*.razor").Any())
            {
                var relativePath = Path.GetRelativePath(rootPath, dir);
            
                var namespaceSuffix = relativePath
                    .Replace(Path.DirectorySeparatorChar, '.')
                    .Replace(" ", "_")
                    .Replace("-", "_");

                if (!string.IsNullOrEmpty(namespaceSuffix))
                {
                    sb.AppendLine("global using " + rootNamespace + "." + namespaceSuffix + ";");
                }
            }
        }
    
        sb.AppendLine("global using " + rootNamespace + ".Shared;");
       // Console.WriteLine("[AUTO-IMPORT] Namespaces gerados:\n" + sb.ToString());
    
        return sb.ToString();
    }
    
    public async Task<List<Diagnostic>> GetDiagnosticsAsync(string code, string filePath)
    {
        var document = UpdateDocument(code, filePath);
        var semanticModel = await document.GetSemanticModelAsync();
        
        if (semanticModel == null) return new List<Diagnostic>();

        var diagnostics = semanticModel.GetDiagnostics()
            .Where(d => d.Location.IsInSource && 
                        d.Location.SourceTree?.FilePath == filePath && 
                        d.Severity == DiagnosticSeverity.Error &&
                        d.Id != "CS8802" && 
                        d.Id != "CS8805")   
            .ToList();

        return diagnostics;
    }
    
    private static readonly List<string> _csharpKeywords = new()
    {
        "public", "private", "protected", "internal",
        "class", "interface", "struct", "enum", "record",
        "void", "int", "string", "bool", "double", "float", "decimal", "var", "object",
        "return", "if", "else", "for", "foreach", "while", "do", "switch", "case",
        "using", "namespace", "new", "try", "catch", "finally", "async", "await",
        "static", "readonly", "const", "override", "virtual", "abstract"
    };

    public async Task<IEnumerable<CompletionItem>> GetCompletionsAsync(string code, int cursorPosition, string filePath)
    {
        try
        {
            bool isMemberAccess = cursorPosition > 0 && code[cursorPosition - 1] == '.';

            var document = UpdateDocument(code, filePath);

            var completionService = CompletionService.GetService(document);
            if (completionService != null)
            {
                var trigger = isMemberAccess
                    ? CompletionTrigger.CreateInsertionTrigger('.')
                    : CompletionTrigger.Invoke;

                var completions = await completionService.GetCompletionsAsync(document, cursorPosition, trigger);

                if (completions != null && completions.ItemsList.Count > 0)
                {
                    return completions.ItemsList;
                }
            }

            var semanticModel = await document.GetSemanticModelAsync();

            if (isMemberAccess)
            {
                var root = await document.GetSyntaxRootAsync();
                var token = root?.FindToken(cursorPosition - 1);

                if (token?.Parent is MemberAccessExpressionSyntax memberAccess)
                {
                    var expression = memberAccess.Expression;
                    var typeInfo = semanticModel?.GetTypeInfo(expression);

                    if (typeInfo?.Type != null && typeInfo.Value.Type.Kind != SymbolKind.ErrorType)
                    {
                        var uniqueNames = typeInfo.Value.Type.GetMembers()
                            .Where(s => s.DeclaredAccessibility == Accessibility.Public &&
                                        !s.IsStatic &&
                                        !s.IsImplicitlyDeclared &&
                                        (s is IMethodSymbol m ? m.MethodKind == MethodKind.Ordinary : true))
                            .Select(s => s.Name)
                            .Distinct()
                            .OrderBy(n => n)
                            .ToList();

                        return uniqueNames.Select(n => CompletionItem.Create(n)).ToList();
                    }
                }
            }
            
            else
            {
                var symbols = semanticModel.LookupSymbols(cursorPosition);

                var symbolItems = symbols
                    .Where(s => !s.IsImplicitlyDeclared && s.Name != ".ctor")
                    .Select(s => s.Name);

                var allItems = symbolItems
                    .Concat(_csharpKeywords) 
                    .Distinct() 
                    .OrderBy(n => n) 
                    .ToList();

                return allItems.Select(text => CompletionItem.Create(text));
            }

            return Enumerable.Empty<CompletionItem>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LSP Error: {ex.Message}");
            return Enumerable.Empty<CompletionItem>();
        }
    }

    private Document UpdateDocument(string code, string filePath)
    {
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

    public void LoadProjectFiles(string rootPath, Action<string>? onProgress = null)
    {
        onProgress?.Invoke($"Loading: {rootPath}");
        
        var forbiddenDlls = GetForbiddenAssemblies(rootPath);
        var projectRefs = new Dictionary<string, MetadataReference>(_referencesMap, StringComparer.OrdinalIgnoreCase);
        var potentialDlls = Directory.GetFiles(rootPath, "*.dll", SearchOption.AllDirectories);
        
        foreach (var dllPath in potentialDlls)
        {
            if (dllPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) continue;
            var fileName = Path.GetFileName(dllPath);
            if (forbiddenDlls.Contains(fileName) || fileName.EndsWith(".Views.dll", StringComparison.OrdinalIgnoreCase)) continue;
            
            if (!projectRefs.ContainsKey(fileName))
            {
                try { projectRefs[fileName] = MetadataReference.CreateFromFile(dllPath); } catch { }
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
    public async Task<SignatureHelpResult?> GetSignatureHelpAsync(string code, int cursorPosition, string filePath)
    {
        var document = UpdateDocument(code, filePath);
        var root = await document.GetSyntaxRootAsync();
        var semanticModel = await document.GetSemanticModelAsync();

        var token = root?.FindToken(cursorPosition);
        var node = token?.Parent;

        var argumentList = node?.AncestorsAndSelf()
            .OfType<ArgumentListSyntax>()
            .FirstOrDefault();

        if (argumentList == null) return null;

        if (argumentList.Parent is not InvocationExpressionSyntax invocation) return null;

        int activeParameter = 0;
        foreach (var arg in argumentList.Arguments)
        {
            if (arg.Span.End < cursorPosition)
            {
                activeParameter++;
            }
        }

        var textBeforeCursor = code.Substring(argumentList.OpenParenToken.Span.End,
            cursorPosition - argumentList.OpenParenToken.Span.End);
        if (textBeforeCursor.Trim().EndsWith(","))
        {
            activeParameter = textBeforeCursor.Count(c => c == ',');
        }

        var symbolInfo = semanticModel.GetSymbolInfo(invocation);
        var symbols = new List<ISymbol>();

        if (symbolInfo.Symbol != null) symbols.Add(symbolInfo.Symbol);
        else if (symbolInfo.CandidateSymbols.Any()) symbols.AddRange(symbolInfo.CandidateSymbols);

        if (!symbols.Any()) return null;

        var result = new SignatureHelpResult { ActiveParameter = activeParameter };

        foreach (var symbol in symbols)
        {
            if (symbol is IMethodSymbol method)
            {
                var sigItem = new SignatureItem
                {
                    Label = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    Documentation = method.GetDocumentationCommentXml(), // Pega docs XML se houver
                    Parameters = method.Parameters.Select(p => new ParameterItem
                    {
                        Name = p.Name,
                        Type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        Display = $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"
                    }).ToList()
                };
                result.Signatures.Add(sigItem);
            }
        }

        return result;
    }
}

public class SignatureHelpResult
{
    public int ActiveParameter { get; set; }
    public List<SignatureItem> Signatures { get; set; } = new();
}

public class SignatureItem
{
    public string? Label { get; set; }       
    public string? Documentation { get; set; } 
    public List<ParameterItem> Parameters { get; set; } = new();
}

public class ParameterItem
{
    public string? Name { get; set; }  
    public string? Type { get; set; }  
    public string? Display { get; set; } 
}