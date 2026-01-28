using SkiaSharp;

namespace SLText.View.Styles.Languages;

public class CSharpDefinition : LanguageDefinition
{
    public override string Name => "C#";
    public override string[] Extensions => new[] { ".cs" };

    public override List<(string, Func<EditorTheme, SKColor>)> GetRules() => new()
    {
        // 1. Comentários (prioridade alta)
        ( @"//.*", theme => theme.Comment ),
        ( @"(?s)/\*.*?\*/", theme => theme.Comment ),

        // 2. Strings e Caracteres
        ( "\".*?\"", theme => theme.String ),
        ( @"'.*?'", theme => theme.String ),

        // 3. Keywords de Estrutura e Controle
        ( @"\b(using|namespace|class|struct|interface|enum|public|private|internal|protected|static|readonly|void|string|int|float|double|bool|var|new|if|else|for|foreach|while|return|get|set|async|await|task|try|catch|finally|throw|switch|case|default|break|continue)\b", 
            theme => theme.Keyword ),

        // 4. Atributos [Table("Users")]
        ( @"\[[A-Za-z0-9_]+(\(.*?\))?\]", theme => theme.Attribute ),

        // 5. Tipos (Classes, Interfaces - Começam com Letra Maiúscula)
        // Evita pegar métodos usando um lookahead que garante que não há um '(' logo depois
        ( @"\b[A-Z]\w*\b(?!\s*\()", theme => theme.Type ),

        // 6. Métodos (Qualquer palavra seguida por '(' )
        ( @"\b\w+(?=\s*\()", theme => theme.Method ),

        // 7. Números
        ( @"\b\d+(\.\d+)?\b", theme => theme.Number ),

        // 8. Operadores
        ( @"[\+\-\*/%&|!<>=\?:]", theme => theme.Operator )
    };
}