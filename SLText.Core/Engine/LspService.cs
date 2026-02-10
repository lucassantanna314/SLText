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
    private readonly MetadataReference[] _references;
    private readonly List<MetadataReference> _baseReferences;
    
    public LspService()
    {
        _workspace = new AdhocWorkspace();
        _baseReferences = new List<MetadataReference>();
        var loadedAssemblies = new HashSet<string>();
        
        var coreDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        foreach (var file in Directory.GetFiles(coreDir, "*.dll"))        {
            var fileName = Path.GetFileName(file);
        
            if (fileName.StartsWith("System.") || 
                fileName.StartsWith("Microsoft.") || 
                fileName == "mscorlib.dll" || 
                fileName == "netstandard.dll")
            {
                try 
                {
                    if (loadedAssemblies.Add(fileName)) 
                    {
                        _baseReferences.Add(MetadataReference.CreateFromFile(file));
                    }
                }
                catch { /*  */ }
            }
        }
        
        //Dlls local
        var appDir = AppContext.BaseDirectory;
        foreach (var file in Directory.GetFiles(appDir, "*.dll"))
        {
            var fileName = Path.GetFileName(file);
            if(fileName.Contains("SLText") || loadedAssemblies.Contains(fileName))
                continue;
            try
            {
                if (loadedAssemblies.Add(fileName))
                {
                    _baseReferences.Add(MetadataReference.CreateFromFile(file));
                }
            }
            catch { }
            
        }
        Console.WriteLine($"[LSP] Base References carregadas: {_baseReferences.Count}");
        InitProject(_baseReferences);
    }
    
    private void InitProject(IEnumerable<MetadataReference> refs)
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
        
        var globalUsingsCode = 
            @"global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
";
        
        var docInfo = DocumentInfo.Create(
            DocumentId.CreateNewId(_project.Id),
            "GlobalUsings.g.cs", 
            filePath: "GlobalUsings.g.cs",
            loader: TextLoader.From(TextAndVersion.Create(SourceText.From(globalUsingsCode), VersionStamp.Create()))
        );
        
        var solution = _workspace.CurrentSolution.AddDocument(docInfo);
        _workspace.TryApplyChanges(solution);
        
        _project = _workspace.CurrentSolution.GetProject(_project.Id)!;
    }
    
    public async Task<List<Diagnostic>> GetDiagnosticsAsync(string code, string filePath)
    {
        var document = UpdateDocument(code, filePath);
        var semanticModel = await document.GetSemanticModelAsync();
        
        if (semanticModel == null) return new List<Diagnostic>();

        var diagnostics = semanticModel.GetDiagnostics()
            .Where(d => d.Location.IsInSource && 
                        d.Location.SourceTree?.FilePath == filePath && 
                        d.Severity == DiagnosticSeverity.Error)
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

    public void LoadProjectFiles(string rootPath)
    {
        Console.WriteLine($"[LSP] Escaneando projeto em: {rootPath}");
        var projectReferences = new List<MetadataReference>(_baseReferences);
        var loadedNames = new HashSet<string>();
        
        var potentialDlls = Directory.GetFiles(rootPath, "*.dll", SearchOption.AllDirectories);
        
        foreach (var dllPath in potentialDlls)
        {
            if (dllPath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || 
                dllPath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")) // Às vezes refs ficam no obj
            {
                var fileName = Path.GetFileName(dllPath);
                
                bool alreadyLoaded = _baseReferences.Any(r => r.Display != null && Path.GetFileName(r.Display) == fileName);
                
                if (!alreadyLoaded && loadedNames.Add(fileName))
                {
                    try 
                    {
                        projectReferences.Add(MetadataReference.CreateFromFile(dllPath)); 
                        Console.WriteLine($"[LSP] Referência de projeto encontrada: {fileName}");
                    }
                    catch { }
                }
            }
        }
        
        Console.WriteLine($"[LSP] Total de Referências (Base + Projeto): {projectReferences.Count}");
        
        InitProject(projectReferences);
        
        var files = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            {
                continue;
            }

            var code = File.ReadAllText(file).Replace("\r\n", "\n"); 
            var fileName = Path.GetFileName(file);
            
            var docId = DocumentId.CreateNewId(_project.Id);
            var docInfo = DocumentInfo.Create(
                docId,
                fileName,
                filePath: file, 
                loader: TextLoader.From(TextAndVersion.Create(SourceText.From(code), VersionStamp.Create()))
            );

            var solution = _workspace.CurrentSolution.AddDocument(docInfo);
            _workspace.TryApplyChanges(solution);
            _project = _workspace.CurrentSolution.GetProject(_project.Id)!;
        }
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
    var textBeforeCursor = code.Substring(argumentList.OpenParenToken.Span.End, cursorPosition - argumentList.OpenParenToken.Span.End);
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
    public string Label { get; set; }       
    public string Documentation { get; set; } 
    public List<ParameterItem> Parameters { get; set; } = new();
}

public class ParameterItem
{
    public string Name { get; set; }  
    public string Type { get; set; }  
    public string Display { get; set; } 
}