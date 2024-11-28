namespace Markdown;

public class TagsParser(string paragraph)
{
    private int currentPos = 0;
    private List<Tag> tags = new List<Tag>();

    private readonly Dictionary<TagType, bool>  isTagOpened = new()
    { 
        { TagType.H1, false },
        { TagType.Strong, false },
        { TagType.Em, false },
        { TagType.Escaping, true },
    };

    public List<Tag> BuildTags()
    {
        GetAllTags();
        ProcessCharsInsideWords();
        ProcessTagsBorders(TagType.Em, TagType.Strong);
        CleanEmptyTagsInLine();
        return tags;
    }

    private void ProcessCharsInsideWords()
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
            var tag = GetTagInWordInformation(wordIndex, i);
            tagInformationByPairType[currentTag.Type].Add(tag);
        }
        foreach (var tagsList in tagInformationByPairType.Keys.Select(tagName => tagInformationByPairType[tagName]))
        {
            while (currentPos < tagsList.Count - 1)
            {
                CheckAndProcessUnderlineOrEmTag(tagsList);
                currentPos++;
            }
            currentPos = 0;
        }
    }

    private TagInWordInformation? GetTagInWordInformation(int wordIndex, int pos) =>
        IsTagInListBounds(pos)
        ? new TagInWordInformation(wordIndex, IsTagPartOfWord(tags[pos - 1], tags[pos + 1]), pos)
        : new TagInWordInformation(wordIndex, false, pos);

    private void CheckAndProcessUnderlineOrEmTag(List<TagInWordInformation> tagsList)
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

    private void CleanEmptyTagsInLine() 
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

    private void GetAllTags()
    {
        while (currentPos < paragraph.Length)
        {
            var tag = GetTagFromChar();
            GetNextPos(tag);
            tags.Add(tag);
        }
        currentPos = 0;
    }

    private void GetNextPos(Tag tag)
    {
        currentPos += tag.TagText.Length;
        if (tag.Type == TagType.Escaping) 
            currentPos++;
    }

    private bool IsNextCharIs(params char[] chars)
    {
        var nenxChar = paragraph[currentPos + 1];
        return chars.Contains(nenxChar);
    }

    private Tag GetTagFromChar() => paragraph[currentPos] switch
    {
        '#' => Tag.Create(TagType.H1, PairTokenType.Opening, paragraph[currentPos] + " "),
        '_' => ParseEmOrStrongTag(),
        '\\' => ParseEscapingTag(),
        '[' => ParseLinkTag(),
        _ => Tag.Create(TagType.Text, PairTokenType.None, paragraph[currentPos])
    };
        
    private Tag ParseEmOrStrongTag() =>
        (IsEndOfParagraph() && IsNextCharIs('_'))
        ? Tag.Create(TagType.Strong, PairTokenType.Opening, paragraph.Substring(currentPos, 2))
        : Tag.Create(TagType.Em, PairTokenType.Opening, paragraph[currentPos]);

    private Tag ParseEscapingTag()
    {
        if (!IsEndOfParagraph())
            return Tag.Create(TagType.Text, PairTokenType.None, paragraph[currentPos]);
        
        return IsNextCharIs('_', '#', '\\', '[', ']')
            ? Tag.Create(TagType.Escaping, PairTokenType.Completed, paragraph[currentPos + 1])
            : Tag.Create(TagType.Text, PairTokenType.None, paragraph[currentPos]);
    }

    private Tag ParseLinkTag()
    {
        var linkTextEnd = paragraph.IndexOf(']', currentPos);
        var linkStart = paragraph.IndexOf('(', currentPos);
        var linkEnd = paragraph.IndexOf(')', currentPos);
        var slash = paragraph.IndexOf('\\', currentPos);
        if (linkTextEnd != -1 && linkStart != -1 && linkEnd != -1 && linkTextEnd + 1 == linkStart && linkEnd > linkStart && slash == -1
            && currentPos + 1 != linkTextEnd)
            return Tag.Create(TagType.Link, PairTokenType.Single, paragraph.Substring(currentPos, linkEnd - currentPos + 1));
        
        return Tag.Create(TagType.Text, PairTokenType.None, paragraph[currentPos]);
    }

    private void ProcessTagsBorders(TagType outsideTag, TagType insideTag)
    {
        var stack = new Stack<Tag>();
        for (var i = 0; i < tags.Count; i++)
        {
            var currentTag = tags[i];
            if (IsUnprocessedTag(currentTag)) 
                continue;
            if (stack.Count > 0 && !IsTextTag(currentTag))
            {
                if (IsNotTag(i)) 
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
            if (IsTextTag(currentTag) || IsNotTag(i)) 
                continue;
            stack.Push(currentTag);
            isTagOpened[currentTag.Type] = true;
        }
        CleanStackOfRemainingTags(stack);
    }

    private void CleanStackOfRemainingTags(Stack<Tag> stack)
    {
        while (stack.Count > 0)
        {
            var currentTag = stack.Pop();
            if (currentTag.Type == TagType.H1)
                tags.Add(Tag.Create(TagType.H1, PairTokenType.Closing, "#"));
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

    private bool IsNotTag(int pos)
    {
        var currentTag = tags[pos];
        var prevPos = pos - 1;
        var nextPos = pos + 1;
        if (!IsTagInListBounds(pos))
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

    private bool IsTagInListBounds(int pos) => pos - 1 > 0 && pos + 1 < tags.Count;

    private bool IsTagsInOneWord(TagInWordInformation currentTag, TagInWordInformation nextTag) =>
        currentTag.WordWithTagIndex != nextTag.WordWithTagIndex;

    private bool IsEndOfParagraph() => currentPos + 1 < paragraph.Length;

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

