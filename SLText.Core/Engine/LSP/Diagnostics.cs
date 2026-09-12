using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SLText.Core.Engine.LSP;

public partial class LspService
{
    /// <summary>
    /// Diagnostics that originate in generated Razor boilerplate rather than user code. They cannot
    /// be mapped back to a source line, and surfacing them produced pure noise.
    /// </summary>
    private static readonly HashSet<string> SuppressedDiagnosticIds = new(StringComparer.Ordinal)
    {
        "CS8802", // Only one compilation unit can have top-level statements
        "CS8805", // Top-level statements must precede other members
        "CS5001", // Program does not contain a static 'Main' method
    };

    public async Task<List<MappedDiagnostic>> GetDiagnosticsAsync(string code, string filePath)
    {
        ThrowIfDisposed();

        var results = new List<MappedDiagnostic>();
        if (string.IsNullOrEmpty(filePath) || code == null) return results;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            bool isRazor = filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
            string workspacePath = isRazor ? filePath + ".g.cs" : filePath;

            string analysisCode = code.Replace("\r\n", "\n");
            RazorFileInfo? razor = null;

            if (isRazor)
            {
                razor = CompileRazor(code, filePath);
                if (razor == null) return results;

                _razorFiles[filePath] = razor;
                analysisCode = razor.GeneratedCode;

                var componentNames = await GetBlazorComponentNamesAsync().ConfigureAwait(false);
                foreach (var error in razor.RazorErrors)
                {
                    if (IsSuppressableRazorDiagnostic(error, componentNames)) continue;

                    results.Add(new MappedDiagnostic
                    {
                        Message = error.GetMessage(),
                        Line = error.Span.LineIndex + 1,
                        CharacterStart = error.Span.CharacterIndex,
                        CharacterEnd = error.Span.CharacterIndex + error.Span.Length,
                        Severity = DiagnosticSeverity.Error,
                        Id = error.Id,
                    });
                }
            }

            var document = UpdateDocument(analysisCode, workspacePath);
            var semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);
            if (semanticModel == null) return results;

            foreach (var diagnostic in semanticModel.GetDiagnostics())
            {
                if (diagnostic.Severity != DiagnosticSeverity.Error) continue;
                if (SuppressedDiagnosticIds.Contains(diagnostic.Id)) continue;
                if (!diagnostic.Location.IsInSource) continue;

                var tree = diagnostic.Location.SourceTree;
                if (tree == null || !string.Equals(tree.FilePath, workspacePath, StringComparison.Ordinal)) continue;

                int line, columnStart, columnEnd;

                if (razor != null)
                {
                    var start = razor.MapToSource(diagnostic.Location.SourceSpan.Start);
                    if (start == null) continue; // generated boilerplate - not actionable by the user

                    var end = razor.MapToSource(diagnostic.Location.SourceSpan.End) ?? start.Value;
                    line = start.Value.Line;
                    columnStart = start.Value.Column;
                    columnEnd = end.Line == line ? end.Column : columnStart + 1;
                }
                else
                {
                    var span = diagnostic.Location.GetLineSpan();
                    line = span.StartLinePosition.Line + 1;
                    columnStart = span.StartLinePosition.Character;
                    columnEnd = span.EndLinePosition.Character;
                }

                results.Add(new MappedDiagnostic
                {
                    Message = diagnostic.GetMessage(),
                    Line = line,
                    CharacterStart = columnStart,
                    CharacterEnd = columnEnd,
                    Severity = diagnostic.Severity,
                    Id = diagnostic.Id,
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SLText] Diagnostics failed for {filePath}: {ex.Message}");
            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// RZ10012 ("unexpected markup element") is raised whenever Razor's tag-helper discovery does not
    /// know the component. The in-process Razor engine does not discover tag helpers from referenced
    /// assemblies, so without this filter every MudBlazor component in every file is flagged as an
    /// error even though <c>dotnet build</c> accepts it. If the name resolves to a real component type
    /// in the compilation, the diagnostic is wrong and is dropped.
    /// </summary>
    private static bool IsSuppressableRazorDiagnostic(
        Microsoft.AspNetCore.Razor.Language.RazorDiagnostic diagnostic,
        IReadOnlySet<string> componentNames)
    {
        if (!string.Equals(diagnostic.Id, "RZ10012", StringComparison.Ordinal)) return false;
        if (componentNames.Count == 0) return false;

        var message = diagnostic.GetMessage();
        foreach (var name in componentNames)
        {
            if (message.Contains("'" + name + "'", StringComparison.Ordinal)) return true;
        }

        return false;
    }

    public async Task<SignatureHelpResult?> GetSignatureHelpAsync(string code, int cursorPosition, string filePath)
    {
        ThrowIfDisposed();
        if (string.IsNullOrEmpty(filePath)) return null;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            bool isRazor = filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);
            string workspacePath = isRazor ? filePath + ".g.cs" : filePath;
            string analysisCode = code.Replace("\r\n", "\n");
            int analysisPosition = cursorPosition;

            if (isRazor)
            {
                var razor = CompileRazor(code, filePath);
                if (razor == null) return null;

                // The caller's offset is in the .razor file; Roslyn needs the generated-code offset.
                var mapped = razor.MapToGenerated(cursorPosition);
                if (mapped == null) return null;

                _razorFiles[filePath] = razor;
                analysisCode = razor.GeneratedCode;
                analysisPosition = mapped.Value;
            }

            var document = UpdateDocument(analysisCode, workspacePath);
            var root = await document.GetSyntaxRootAsync().ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync().ConfigureAwait(false);
            if (root == null || semanticModel == null) return null;
            if (analysisPosition < 0 || analysisPosition > analysisCode.Length) return null;

            var token = root.FindToken(analysisPosition);
            var argumentList = token.Parent?.AncestorsAndSelf().OfType<ArgumentListSyntax>().FirstOrDefault();
            if (argumentList?.Parent is not InvocationExpressionSyntax invocation) return null;

            int openParenEnd = argumentList.OpenParenToken.Span.End;
            if (openParenEnd > analysisPosition || analysisPosition > analysisCode.Length) return null;

            int activeParameter = 0;
            foreach (var argument in argumentList.Arguments)
            {
                if (argument.Span.End < analysisPosition) activeParameter++;
            }

            // Nested parentheses / lambdas make a raw comma count wrong, but it is still a better
            // approximation than the argument index when the caret sits after a trailing comma.
            string beforeCursor = analysisCode[openParenEnd..analysisPosition];
            if (beforeCursor.TrimEnd().EndsWith(','))
            {
                activeParameter = Math.Max(activeParameter, CountTopLevelCommas(beforeCursor) );
            }

            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            IReadOnlyList<ISymbol> symbols = symbolInfo.Symbol != null
                ? new ISymbol[] { symbolInfo.Symbol }
                : symbolInfo.CandidateSymbols;

            if (symbols.Count == 0) return null;

            var result = new SignatureHelpResult { ActiveParameter = activeParameter };

            foreach (var symbol in symbols)
            {
                if (symbol is not IMethodSymbol method) continue;

                result.Signatures.Add(new SignatureItem
                {
                    Label = method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    Documentation = method.GetDocumentationCommentXml(),
                    Parameters = method.Parameters.Select(p => new ParameterItem
                    {
                        Name = p.Name,
                        Type = p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        Display = $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}",
                    }).ToList(),
                });
            }

            return result.Signatures.Count > 0 ? result : null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[SLText] Signature help failed: {ex.Message}");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static int CountTopLevelCommas(string text)
    {
        int depth = 0, commas = 0;
        foreach (char c in text)
        {
            switch (c)
            {
                case '(': case '[': case '{': case '<': depth++; break;
                case ')': case ']': case '}': case '>': depth--; break;
                case ',' when depth == 0: commas++; break;
            }
        }
        return commas;
    }

    public class MappedDiagnostic
    {
        public string Message { get; set; } = "";

        /// <summary>1-based line in the user's source file.</summary>
        public int Line { get; set; }

        /// <summary>1-based start column in the user's source file.</summary>
        public int CharacterStart { get; set; }

        /// <summary>1-based end column in the user's source file.</summary>
        public int CharacterEnd { get; set; }

        public DiagnosticSeverity Severity { get; set; }
        public string Id { get; set; } = "";
    }
}
