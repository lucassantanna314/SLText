using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
    public async Task<List<MappedDiagnostic>> GetDiagnosticsAsync(string code, string filePath)
    {
        bool isRazor = filePath.EndsWith(".razor");
        string workspacePath = isRazor ? filePath + ".g.cs" : filePath;

        RazorCSharpDocument? razorDoc = null;
        IEnumerable<RazorDiagnostic> razorErrors = Enumerable.Empty<RazorDiagnostic>();

        if (isRazor)
        {
            var result = CompileRazorToCSharp(code, filePath);
            code = result.code;
            razorDoc = result.doc;
            razorErrors = result.razorErrors;
        }

        var document = UpdateDocument(code, workspacePath);
        var semanticModel = await document.GetSemanticModelAsync();
        var results = new List<MappedDiagnostic>();

        foreach (var error in razorErrors)
        {
            results.Add(new MappedDiagnostic
            {
                Message = error.GetMessage(),
                Line = error.Span.LineIndex + 1,
                Severity = DiagnosticSeverity.Error,
                Id = error.Id
            });
        }

        if (semanticModel != null)
        {
            var diagnostics = semanticModel.GetDiagnostics()
                .Where(d => d.Location.IsInSource &&
                            d.Location.SourceTree?.FilePath == workspacePath &&
                            d.Severity == DiagnosticSeverity.Error &&
                            d.Id != "CS8802" && d.Id != "CS8805");

            foreach (var diag in diagnostics)
            {
                int mappedLine = diag.Location.GetLineSpan().StartLinePosition.Line;

                if (isRazor && razorDoc != null)
                {
                    mappedLine = MapGeneratedLineToSource(razorDoc, mappedLine) - 1;
                }

                var lineSpan = diag.Location.GetLineSpan();
                results.Add(new MappedDiagnostic {
                    Message = diag.GetMessage(),
                    Line = mappedLine + 1,
                    CharacterStart = lineSpan.StartLinePosition.Character,
                    CharacterEnd = lineSpan.EndLinePosition.Character,
                    Severity = diag.Severity,
                    Id = diag.Id
                });
            }
        }

        return results;
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
    
    public class MappedDiagnostic
    {
        public string Message { get; set; } = "";
        public int Line { get; set; } 
        public int CharacterStart { get; set; }
        public int CharacterEnd { get; set; }
        public DiagnosticSeverity Severity { get; set; }
        public string Id { get; set; } = "";
    }
}