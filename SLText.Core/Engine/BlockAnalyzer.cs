using System.Text.RegularExpressions;

namespace SLText.Core.Engine;

public record CodeBlock(int StartLine, int EndLine, int IndentLevel);

public class BlockAnalyzer
{
    public List<CodeBlock> AnalyzeBlocks(TextBuffer buffer, string extension)
    {
        var blocks = new List<CodeBlock>();
        var lines = buffer.GetLines().ToList();
        
        if (extension == ".html" || extension == ".xml")
            return AnalyzeHtmlBlocks(lines);
        
        return AnalyzeCurlyBraceBlocks(lines);
    }

    private List<CodeBlock> AnalyzeCurlyBraceBlocks(List<string> lines)
    {
        var blocks = new List<CodeBlock>();
        var stack = new Stack<(int line, int indent, char type)>(); 

        for (int i = 0; i < lines.Count; i++)
        {
            string text = lines[i];
            for (int col = 0; col < text.Length; col++)
            {
                char c = text[col];
                
                if (c == '{' || c == '[') 
                {
                    stack.Push((i, GetIndentLevel(text), c));
                }
                else if (stack.Count > 0)
                {
                    var start = stack.Peek();
                    
                    if ((c == '}' && start.type == '{') || (c == ']' && start.type == '['))
                    {
                        stack.Pop();
                        if (i > start.line) 
                            blocks.Add(new CodeBlock(start.line, i, start.indent));
                    }
                }
            }
        }
        return blocks;
    }

    private List<CodeBlock> AnalyzeHtmlBlocks(List<string> lines)
    {
        var blocks = new List<CodeBlock>();
        
        var stack = new Stack<(int line, int indent, string tagName)>();

        var tagRegex = new Regex(@"<(/?)([a-zA-Z0-9]+)[^>]*>", RegexOptions.Compiled);

        for (int i = 0; i < lines.Count; i++)
        {
            string text = lines[i];
            var matches = tagRegex.Matches(text);

            foreach (Match match in matches)
            {
                bool isClosing = match.Groups[1].Value == "/";
                string tagName = match.Groups[2].Value;

                if (tagName == "br" || tagName == "hr" || tagName == "img" || tagName == "link") continue;

                if (!isClosing)
                {
                    stack.Push((i, GetIndentLevel(text), tagName));
                }
                else if (stack.Count > 0 && stack.Peek().tagName == tagName)
                {
                    var start = stack.Pop();
                    if (i > start.line)
                    {
                        blocks.Add(new CodeBlock(start.line, i, start.indent));
                    }
                }
            }
        }
        return blocks;
    }

    private int GetIndentLevel(string line)
    {
        int count = 0;
        foreach (var c in line) {
            if (c == ' ') count++;
            else if (c == '\t') count += 4;
            else break;
        }
        return count;
    }
}