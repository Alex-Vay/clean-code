namespace Markdown;

public class Tag(TagType type, PairTokenType pairType, string text)
{
    public TagType Type { get; set; } = type;
    public PairTokenType PairType { get; set; } = pairType;
    public string TagText { get; set; } = text;
}
