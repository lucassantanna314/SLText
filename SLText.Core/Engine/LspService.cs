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
    
    public LspService()
    {
        _workspace = new AdhocWorkspace();

        // 1. Localiza a pasta onde o .NET está instalado
        var coreDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var references = new List<MetadataReference>();

        // 2. TÁTICA "SHOTGUN": Carrega TODAS as DLLs do sistema disponíveis
        // Isso resolve o problema do "int" não ser reconhecido
        foreach (var file in Directory.GetFiles(coreDir, "*.dll"))
        {
            var fileName = Path.GetFileName(file);
        
            // Carrega apenas DLLs do sistema para não pesar a memória com coisas inúteis
            if (fileName.StartsWith("System.") || 
                fileName.StartsWith("Microsoft.") || 
                fileName == "mscorlib.dll" || 
                fileName == "netstandard.dll")
            {
                try 
                {
                    references.Add(MetadataReference.CreateFromFile(file));
                }
                catch { /* Ignora DLLs que não podem ser carregadas */ }
            }
        }

        _references = references.ToArray();

        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
                projectId, 
                VersionStamp.Create(), 
                "VirtualProject", 
                "VirtualProject", 
                LanguageNames.CSharp)
            .WithMetadataReferences(_references)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)) 
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Latest));
        
        _project = _workspace.AddProject(projectInfo);
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
            // Identifica se é acesso a membro (tem ponto antes)
            bool isMemberAccess = cursorPosition > 0 && code[cursorPosition - 1] == '.';

            var document = UpdateDocument(code, filePath);

            // --- 1. TENTATIVA PADRÃO (CompletionService do Roslyn) ---
            // O Roslyn é inteligente. Se ele funcionar, usamos ele.
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

            // --- 2. PLANO B: FALLBACK MANUAL (Se o Roslyn falhar por erro de sintaxe) ---

            var semanticModel = await document.GetSemanticModelAsync();

            // CASO A: ACESSO COM PONTO (x.|) -> Busca membros do tipo
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
            // CASO B: SEM PONTO (nom|) -> Busca Variáveis, Classes e Keywords
            else
            {
                // 1. Busca Símbolos do Escopo (Variáveis, Parâmetros, Classes Locais)
                // O LookupSymbols(position) é a mágica aqui!
                var symbols = semanticModel.LookupSymbols(cursorPosition);

                var symbolItems = symbols
                    .Where(s => !s.IsImplicitlyDeclared && s.Name != ".ctor") // Filtra lixo
                    .Select(s => s.Name);

                // 2. Mistura com as Keywords do C#
                var allItems = symbolItems
                    .Concat(_csharpKeywords) // Junta variáveis + keywords
                    .Distinct() // Remove duplicatas
                    .OrderBy(n => n) // Ordena
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
        var projectInfo = ProjectInfo.Create(
                ProjectId.CreateNewId(), 
                VersionStamp.Create(), 
                "VirtualProject", 
                "VirtualProject", 
                LanguageNames.CSharp)
            .WithMetadataReferences(_references)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)) 
            .WithParseOptions(new CSharpParseOptions(LanguageVersion.Latest));

        var newSolution = _workspace.CurrentSolution
            .RemoveProject(_project.Id)
            .AddProject(projectInfo);
            
        _workspace.TryApplyChanges(newSolution);
        _project = _workspace.CurrentSolution.GetProject(projectInfo.Id)!;

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

    public async Task DebugRoslynState(string code, int cursorPosition, string filePath)
    {
        Console.WriteLine("--- INÍCIO DO DIAGNÓSTICO ROSLYN ---");

        // 1. Verificar Referências
        Console.WriteLine($"[1] Referências Carregadas: {_project.MetadataReferences.Count}");
        if (!_project.MetadataReferences.Any(r => r.Display != null && r.Display.Contains("System.Runtime")))
        {
            Console.WriteLine("    [CRÍTICO] System.Runtime.dll NÃO encontrado!");
        }
        else
        {
            Console.WriteLine("    [OK] System.Runtime.dll encontrado.");
        }

        // 2. Verificar Documento Atual
        var doc = UpdateDocument(code, filePath);
        var textObj = await doc.GetTextAsync();
        string roslynText = textObj.ToString();

        Console.WriteLine($"[2] Tamanho do Texto:");
        Console.WriteLine($"    Editor (Input): {code.Length}");
        Console.WriteLine($"    Roslyn (Doc):   {roslynText.Length}");

        if (code.Length != roslynText.Length)
        {
            Console.WriteLine("    [CRÍTICO] O texto do Editor e do Roslyn têm tamanhos diferentes!");
            Console.WriteLine("    Isso confirma problema de \\r\\n vs \\n.");
        }

        // 3. Verificar Posição do Cursor (A Prova Real)
        Console.WriteLine($"[3] Teste de Posição (Cursor: {cursorPosition}):");
        if (cursorPosition > 0 && cursorPosition <= roslynText.Length)
        {
            char charAtRoslyn = roslynText[cursorPosition - 1];
            char charAtInput = code[cursorPosition - 1];

            Console.WriteLine($"    Caractere no Editor: '{charAtInput}' (Int: {(int)charAtInput})");
            Console.WriteLine($"    Caractere no Roslyn: '{charAtRoslyn}' (Int: {(int)charAtRoslyn})");

            if (charAtRoslyn != charAtInput)
            {
                Console.WriteLine("    [CRÍTICO] O Roslyn está olhando para o caractere errado!");
            }
            else
            {
                Console.WriteLine("    [OK] Sincronia de cursor confirmada.");
            }
        }
        else
        {
            Console.WriteLine(
                $"    [ERRO] Cursor {cursorPosition} está fora dos limites do texto ({roslynText.Length})");
        }

        // 4. Verificar Semântica Básica
        var semanticModel = await doc.GetSemanticModelAsync();
        var root = await doc.GetSyntaxRootAsync();

        var node = root?.FindToken(cursorPosition > 0 ? cursorPosition - 1 : 0).Parent;
        Console.WriteLine($"[4] Nó no cursor: {node?.Kind()} -> '{node?.ToString()}'");

        if (node is MemberAccessExpressionSyntax memberAccess)
        {
            // Pega o que está à esquerda do ponto (o "x")
            var expression = memberAccess.Expression;
            var typeInfo = semanticModel.GetTypeInfo(expression);
        
            Console.WriteLine($"[5] Análise da Expressão '{expression}':");
            Console.WriteLine($"    Tipo Detectado: {typeInfo.Type?.ToDisplayString() ?? "DESCONHECIDO"}");
        
            if (typeInfo.Type == null || typeInfo.Type.Kind == SymbolKind.ErrorType)
            {
                Console.WriteLine("    [CRÍTICO] O Roslyn não sabe o tipo da variável. Autocomplete impossível.");
            }
            else
            {
                Console.WriteLine("    [SUCESSO] O Roslyn sabe o tipo! O autocomplete DEVE funcionar.");
            }
        }
        else 
        {
            Console.WriteLine("[5] Não é um acesso de membro (x.).");
        }
        
        var allDiagnostics = semanticModel.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Console.WriteLine($"[6] Erros Ativos ({allDiagnostics.Count}):");
        foreach (var diag in allDiagnostics)
        {
            Console.WriteLine($"    [{diag.Id}] {diag.GetMessage()}");
        }

        Console.WriteLine("--- FIM DO DIAGNÓSTICO ---");
    }
}