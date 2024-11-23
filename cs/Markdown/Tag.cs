namespace Markdown;

public class Tag
{
    public TagType Type { get; set; }
    public PairTokenType PairType { get; set; }
    public string TagText { get; set; }

    public Tag(TagType type, PairTokenType pairType, string text)
    {
        Type = type;
        PairType = pairType;
        TagText = text;
    }
}
