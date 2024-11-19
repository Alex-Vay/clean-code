namespace Markdown.Converters;

public class MarkdownToHtmlConverter
{
    public string Convert(List<Token> tokens)
    {
        return tokens.ToString();
    }
}
