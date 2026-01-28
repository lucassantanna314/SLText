using SkiaSharp;

namespace SLText.View.Styles.Languages;

public class GCodeDefinition : LanguageDefinition
{
    public override string Name => "G-Code";
    public override string[] Extensions => new[] { ".gcode", ".nc", ".cnc", ".tap" };
    public override List<(string Pattern, Func<EditorTheme, SKColor> ColorSelector)> GetRules() => new()
    {
        ( @"\(.*?\)", theme => theme.Comment ),                // Comentários entre parênteses
        ( @";.*", theme => theme.Comment ),                    // Comentários com ponto e vírgula
        ( @"\b[GM]\d+\b", theme => theme.Keyword ),            // Comandos G e M (ex: G01, M03)
        ( @"\b[XYZFSE][+-]?\d*(\.\d+)?\b", theme => theme.Type ), // Eixos e parâmetros (X10.5, F1200)
        ( @"\b[TN]\d+\b", theme => theme.Method ),             // Ferramentas e Números de linha
        ( @"\d+(\.\d+)?", theme => theme.Number )              // Números puros
    };
}