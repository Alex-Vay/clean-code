namespace Markdown;

public class Token
{
    public TokenType Type { get; }
    public PairTokenType PairType { get; }
    public string TokenText { get; }

    public Token(TokenType type, PairTokenType pairType, string text)
    {
        Type = type;
        PairType = pairType;
        TokenText = text;
    }
}
