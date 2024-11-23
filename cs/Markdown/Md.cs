using Markdown.Converters;
using System.Text;

namespace Markdown;

public class Md
{
    public static string Render(string text)
    {
        var renderedText = new StringBuilder();
        var paragraphs = text.Split("\n");
        for (var i = 0; i < paragraphs.Count(); i++)
        {
            renderedText.Append(RenderParagraph(paragraphs[i]));
            if (i < paragraphs.Count() - 1) renderedText.Append("\n");
        }  
        return renderedText.ToString();
    }

    private static StringBuilder RenderParagraph(string paragraph)
    {
        var tags = TagsParser.BuildTags(paragraph);
        var conv = new MarkdownToHtmlConverter();
        return conv.Convert(tags);
    }
}
