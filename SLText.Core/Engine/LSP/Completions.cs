using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
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
}