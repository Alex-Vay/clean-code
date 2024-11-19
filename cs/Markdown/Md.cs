using Markdown.Converters;

namespace Markdown;

public class Md
{
    public static string Render(string text)
    {
        var htmlConverter = new MarkdownToHtmlConverter();
        var htmlText = htmlConverter.Convert(GetTextTokens(text);
        return htmlText;
    }

    public static List<Token> GetTextTokens(string text)
    {
        return new List<Token>();
    }
}
