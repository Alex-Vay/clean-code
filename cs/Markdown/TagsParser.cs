namespace Markdown;

public class TagsParser(string paragraph)
{
    private string Paragraph { get; set; } = paragraph;
    private int currentPos = 0;

    private readonly Dictionary<TagType, bool>  isTagOpened = new()
    { 
        { TagType.H1, false },
        { TagType.Strong, false },
        { TagType.Em, false },
        { TagType.Escaping, true },
    };

    public List<Tag> BuildTags()
    {
        var tags = GetAllTags(Paragraph);
        ProcessCharsInsideWords(tags);
        ProcessTagsBorders(tags, TagType.Em, TagType.Strong);
        CleanEmptyTagsInLine(tags);
        return tags;
    }

    private void ProcessCharsInsideWords(List<Tag> tags)
    {
        var tagInformationByPairType = new Dictionary<TagType, List<TagInWordInformation>>
        {
            [TagType.Strong] = [],
            [TagType.Em] = []
        };
        var wordIndex = 0;
        for (var i = 0; i < tags.Count; i++)
        {
            var currentTag = tags[i];
            if (IsSpaceChar(currentTag)) 
                wordIndex++;
            if (!tagInformationByPairType.ContainsKey(currentTag.Type)) 
                continue;
            var tag = GetTagInWordInformation(tags, wordIndex, i);
            tagInformationByPairType[currentTag.Type].Add(tag);
        }
        foreach (var tagsList in tagInformationByPairType.Keys.Select(tagName => tagInformationByPairType[tagName]))
        {
            while (currentPos < tagsList.Count - 1)
            {
                CheckAndProcessUnderlineOrEmTag(tags, tagsList, currentPos);
                currentPos++;
            }
            currentPos = 0;
        }
    }

    private TagInWordInformation? GetTagInWordInformation(List<Tag> tags, int wordIndex, int pos) =>
        IsTagInListBounds(tags, pos)
        ? new TagInWordInformation(wordIndex, IsTagPartOfWord(tags[pos - 1], tags[pos + 1]), pos)
        : new TagInWordInformation(wordIndex, false, pos);

    private void CheckAndProcessUnderlineOrEmTag(
        List<Tag> tags, 
        List<TagInWordInformation> tagsList, 
        int pos)
    {
        var currentTag = tagsList[currentPos];
        var nextTag = tagsList[currentPos + 1];
        if (IsTagsInOneWord(currentTag, nextTag))
        {
            if (currentTag.IsTagInWord)
                ConvertTagToTextTag(tags[currentTag.TagListIndex]);
            if (nextTag.IsTagInWord)
                ConvertTagToTextTag(tags[nextTag.TagListIndex]);
        }
        else 
            currentPos++;
    }

    private void CleanEmptyTagsInLine(List<Tag> tags) 
    {
        var openTags = tags
            .Select((tag, ind) => (tag, ind))
            .Where(x => x.tag.PairType == PairTokenType.Opening)
            .ToList();
        var closeTags = tags
            .Select((tag, ind) => (tag, ind))
            .Where(x => x.tag.PairType == PairTokenType.Closing)
            .ToList();
        for (var i = 0; i < openTags.Count; i++)
        {
            var stringBetweenTags = tags[openTags[i].ind..closeTags[i].ind];
            if (IsTextInString(stringBetweenTags)) 
                continue;
            ConvertTagToTextTag(openTags[i].tag);
            ConvertTagToTextTag(closeTags[i].tag);
        }
    }

    private List<Tag> GetAllTags(string paragraph)
    {
        var tagList = new List<Tag>();
        while (currentPos < paragraph.Length)
        {
            var tag = GetTagFromChar(paragraph, currentPos);
            GetNextPos(tag);
            tagList.Add(tag);
        }
        currentPos = 0;
        return tagList;
    }

    private void GetNextPos(Tag tag)
    {
        currentPos += tag.TagText.Length;
        if (tag.Type == TagType.Escaping) 
            currentPos++;
    }

    private Tag GetTagFromChar(string paragraph, int pos) => paragraph[pos] switch
    {
        '#' => new Tag(TagType.H1, PairTokenType.Opening, $"{paragraph[pos]} "),
        '_' => ParseEmOrStrongTag(paragraph, pos),
        '\\' => ParseEscapingTag(paragraph, pos),
        '[' => ParseLinkTag(paragraph, pos),
        _ => new Tag(TagType.Text, PairTokenType.None, $"{paragraph[pos]}")
    };
        
    private Tag ParseEmOrStrongTag(string paragraph, int pos) =>
        (IsEndOfParagraph(paragraph, pos) && paragraph[pos + 1] == '_')
        ? new Tag(TagType.Strong, PairTokenType.Opening, paragraph.Substring(pos, 2))
        : new Tag(TagType.Em, PairTokenType.Opening, $"{paragraph[pos]}");

    private Tag ParseEscapingTag(string paragraph, int pos)
    {
        var currentCharInString = $"{paragraph[pos]}";
        if (!IsEndOfParagraph(paragraph, pos))
            return new Tag(TagType.Text, PairTokenType.None, currentCharInString);
        var symb = paragraph[pos + 1].ToString();
        return "_#\\[]".Contains(symb)
            ? new Tag(TagType.Escaping, PairTokenType.Completed, symb)
            : new Tag(TagType.Text, PairTokenType.None, currentCharInString);
    }

    private Tag ParseLinkTag(string paragraph, int pos)
    {
        var linkTextEnd = paragraph.IndexOf(']', pos);
        var linkStart = paragraph.IndexOf('(', pos);
        var linkEnd = paragraph.IndexOf(')', pos);
        var slash = paragraph.IndexOf('\\', pos);
        if (linkTextEnd != -1 && linkStart != -1 && linkEnd != -1 && linkTextEnd + 1 == linkStart && linkEnd > linkStart && slash == -1
            && pos + 1 != linkTextEnd)
            return new Tag(TagType.Link, PairTokenType.Single, paragraph.Substring(pos, linkEnd - pos + 1));
        return new Tag(TagType.Text, PairTokenType.None, $"{paragraph[pos]}");
    }

    private void ProcessTagsBorders(List<Tag> tags, TagType outsideTag, TagType insideTag)
    {
        var stack = new Stack<Tag>();
        for (var i = 0; i < tags.Count; i++)
        {
            var currentTag = tags[i];
            if (IsUnprocessedTag(currentTag)) 
                continue;
            if (stack.Count > 0 && !IsTextTag(currentTag))
            {
                if (IsNotTag(tags, i)) 
                    continue;
                if (isTagOpened[currentTag.Type])
                {
                    var lastTag = stack.Pop();
                    if (IsPairTags(lastTag, currentTag)) 
                        continue;
                    ConvertTagToTextTag(currentTag);
                    while (stack.Count > 0)
                    {
                        lastTag = stack.Pop();
                        ConvertTagToTextTag(lastTag);
                    }
                }
                else if (IsIncorrectTagsNesting(stack, currentTag, outsideTag, insideTag))
                    ConvertTagToTextTag(currentTag);
            }
            if (IsTextTag(currentTag) || IsNotTag(tags, i)) 
                continue;
            stack.Push(currentTag);
            isTagOpened[currentTag.Type] = true;
        }
        CleanStackOfRemainingTags(stack, tags);
    }

    private void CleanStackOfRemainingTags(Stack<Tag> stack, List<Tag> tags)
    {
        while (stack.Count > 0)
        {
            var currentTag = stack.Pop();
            if (currentTag.Type == TagType.H1)
                tags.Add(new Tag(TagType.H1, PairTokenType.Closing, "#"));
            else if (currentTag.PairType != PairTokenType.Single)
            {
                isTagOpened[currentTag.Type] = false;
                ConvertTagToTextTag(currentTag);
            }
        }
    }

    private void ConvertTagToTextTag(Tag tag) 
    {
        tag.Type = TagType.Text;
        tag.PairType = PairTokenType.None;
    }

    private bool IsPairTags(Tag lastTag, Tag currentTag)
    {
        if (lastTag.Type == currentTag.Type)
        {
            currentTag.PairType = PairTokenType.Closing;
            isTagOpened[currentTag.Type] = false;
            return true;
        }
        ConvertTagToTextTag(lastTag);
        isTagOpened[currentTag.Type] = false;
        return false;
    }

    private bool IsNotTag(List<Tag> tags, int pos)
    {
        var currentTag = tags[pos];
        var prevPos = pos - 1;
        var nextPos = pos + 1;
        if (!IsTagInListBounds(tags, pos))
            return false;
        var prevTag = tags[prevPos];
        var nextTag = tags[nextPos];
        if (IsNotFirstOpenTag(currentTag, prevTag, nextTag)
            || IsNotCloseTagAfterOpenTag(currentTag, prevTag, nextTag)
            || IsTagNearNumber(prevTag, nextTag)
            || IsTagBetweenSpaces(prevTag, nextTag))
        {
            ConvertTagToTextTag(currentTag);
            return true;
        }
        return false;
    }

    private bool IsOpenTag(Tag prevTag, Tag nextTag) =>
        IsSpaceChar(prevTag) && !IsSpaceChar(nextTag);

    private bool IsCloseTag(Tag prevTag, Tag nextTag) =>
        IsSpaceChar(nextTag) && !IsSpaceChar(prevTag);

    private bool IsTagNearNumber(Tag prevTag, Tag nextTag) =>
        (char.IsDigit(nextTag.TagText[0]) && char.IsLetter(prevTag.TagText[0])) 
        || (char.IsDigit(prevTag.TagText[0]) && char.IsLetter(nextTag.TagText[0]));

    private bool IsTagPartOfWord(Tag prevTag, Tag nextTag) =>
        char.IsLetter(nextTag.TagText[0]) && char.IsLetter(prevTag.TagText[0]);

    private bool IsTagBetweenSpaces(Tag prevTag, Tag nextTag) =>
        IsSpaceChar(nextTag) && IsSpaceChar(prevTag);
    
    private bool IsSpaceChar(Tag tag) => tag.TagText == " ";

    private bool IsTagInListBounds(List<Tag> tags, int pos) => pos - 1 > 0 && pos + 1 < tags.Count;

    private bool IsTagsInOneWord(TagInWordInformation currentTag, TagInWordInformation nextTag) =>
        currentTag.WordWithTagIndex != nextTag.WordWithTagIndex;

    private bool IsEndOfParagraph(string text, int pos) => pos + 1 < text.Length;

    private bool IsUnprocessedTag(Tag tag) => tag.Type == TagType.Escaping || tag.Type == TagType.Link;
    
    private bool IsTextTag(Tag tag) => tag.Type == TagType.Text;

    private bool IsTextInString(List<Tag> stringBetweenTags) => stringBetweenTags.Any(x =>
        char.IsLetter(x.TagText[0]) || char.IsDigit(x.TagText[0])
                                    || x.TagText.Length > 2);
    
    private bool IsNotFirstOpenTag(Tag currentTag, Tag prevTag, Tag nextTag) =>
        IsOpenTag(prevTag, nextTag) && isTagOpened[currentTag.Type];
    
    private bool IsNotCloseTagAfterOpenTag(Tag currentTag, Tag prevTag, Tag nextTag) =>
        IsCloseTag(prevTag, nextTag) && !isTagOpened[currentTag.Type];

    private bool IsIncorrectTagsNesting(
        Stack<Tag> stack, 
        Tag currentTag,  
        TagType outsideTag, 
        TagType insideTag
        ) => stack.Last().Type == outsideTag && currentTag.Type == insideTag;
}


public class TagInWordInformation(int wordWithTagIndex, bool isTagInWord, int tagListIndex)
{
    public int WordWithTagIndex { get; set; } = wordWithTagIndex;
    public bool IsTagInWord { get; set; } = isTagInWord;
    public int TagListIndex { get; set; } = tagListIndex;
}

