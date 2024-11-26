namespace Markdown;

public static class TagsParser
{
    private static readonly Dictionary<TagType, bool>  IsTagOpened = new()
    { 
        { TagType.H1, false },
        { TagType.Strong, false },
        { TagType.Em, false },
        { TagType.Escaping, true },
    };

    public static List<Tag> BuildTags(string paragraph)
    {
        var tags = GetAllTags(paragraph);
        ProcessCharsInsideWords(tags);
        ProcessTagsBorders(tags, TagType.Em, TagType.Strong);
        CleanEmptyTagsInLine(tags);
        return tags;
    }

    private static void ProcessCharsInsideWords(List<Tag> tags)
    {
        var tagInformationByPairType = new Dictionary<TagType, List<TagInWordInformation>>
        {
            [TagType.Strong] = [],
            [TagType.Em] = []
        };
        var worldIndex = 0;
        for (var i = 0; i < tags.Count; i++)
        {
            var currentTag = tags[i];
            if (IsSpaceChar(currentTag)) 
                worldIndex++;
            if (tagInformationByPairType.ContainsKey(currentTag.Type))
            {
                tagInformationByPairType[currentTag.Type].Add(
                    IsTagInListBounds(tags, i)
                    ? new TagInWordInformation(worldIndex, IsTagPartOfWord(tags[i - 1], tags[i + 1]), i)
                    : new TagInWordInformation(worldIndex, false, i));
            }
        }
        foreach (var tagsList in tagInformationByPairType.Keys.Select(tagName => tagInformationByPairType[tagName]))
        {
            for (var i = 0; i < tagsList.Count - 1; i++)
            {
                var currentTag = tagsList[i];
                var nextTag = tagsList[i + 1];
                if (IsTagsInOneWord(currentTag, nextTag))
                {
                    if (currentTag.IsTagInWord)
                        ConvertTagToTextTag(tags[currentTag.TagListIndex]);
                    if (nextTag.IsTagInWord)
                        ConvertTagToTextTag(tags[nextTag.TagListIndex]);
                }
                else i++;
            }
        }
    }

    private static void CleanEmptyTagsInLine(List<Tag> tags) 
    {
        //плохо, очень плохо
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

    private static List<Tag> GetAllTags(string paragraph)
    {
        var tagList = new List<Tag>();
        for (var i = 0; i < paragraph.Length; i++)
        {
            var tag = GetTagFromChar(paragraph, i);
            i += tag.TagText.Length - 1;
            if (tag.Type == TagType.Escaping) i++;
            tagList.Add(tag);
        }
        return tagList;
    }

    private static Tag GetTagFromChar(string paragraph, int pos)
    {
        var currentCharInString = paragraph[pos].ToString();
        switch (paragraph[pos])
        {
            case '#':
                return new Tag(TagType.H1, PairTokenType.Opening, currentCharInString + " ");
            case '_':
                if (IsSymbolAfterCurrent(paragraph, pos) && paragraph[pos + 1] == '_')
                    return new Tag(TagType.Strong, PairTokenType.Opening, paragraph.Substring(pos, 2));
                return new Tag(TagType.Em, PairTokenType.Opening, currentCharInString);
            case '\\':
                if (!IsSymbolAfterCurrent(paragraph, pos)) 
                    return new Tag(TagType.Text, PairTokenType.None, currentCharInString);
                var symb = paragraph[pos + 1].ToString();
                return "_#\\[]".Contains(symb) 
                    ? new Tag(TagType.Escaping, PairTokenType.Completed, symb) 
                    : new Tag(TagType.Text, PairTokenType.None, currentCharInString);
            case '[':
                var linkTextEnd = paragraph.IndexOf(']', pos);
                var linkStart = paragraph.IndexOf('(', pos);
                var linkEnd = paragraph.IndexOf(')', pos);
                var slash = paragraph.IndexOf('\\', pos);
                if (linkTextEnd != -1 && linkStart != -1 && linkEnd != -1 && linkTextEnd + 1 == linkStart && linkEnd > linkStart && slash == -1
                    && pos + 1 != linkTextEnd)
                    return new Tag(TagType.Link, PairTokenType.Single, paragraph.Substring(pos, linkEnd - pos + 1));
                return new Tag(TagType.Text, PairTokenType.None, currentCharInString);
            default:
                return new Tag(TagType.Text, PairTokenType.None, currentCharInString);
        }
    }

    private static void ProcessTagsBorders(List<Tag> tags, TagType outsideTag, TagType insideTag)
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
                if (IsTagOpened[currentTag.Type])
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
            IsTagOpened[currentTag.Type] = true;
        }
        CleanStackOfRemainingTags(stack, tags);
        RestoreTagOpenedDictionary();
    }

    private static void CleanStackOfRemainingTags(Stack<Tag> stack, List<Tag> tags)
    {
        while (stack.Count > 0)
        {
            var currentTag = stack.Pop();
            if (currentTag.Type == TagType.H1)
                tags.Add(new Tag(TagType.H1, PairTokenType.Closing, "#"));
            else if (currentTag.PairType != PairTokenType.Single)
                ConvertTagToTextTag(currentTag);
        }
    }

    private static void ConvertTagToTextTag(Tag tag) 
    {
        tag.Type = TagType.Text;
        tag.PairType = PairTokenType.None;
    }

    private static void RestoreTagOpenedDictionary()
    {
        foreach (var curTag in IsTagOpened.Keys) IsTagOpened[curTag] = false;
        IsTagOpened[TagType.Escaping] = true;
    }

    private static bool IsPairTags(Tag lastTag, Tag currentTag)
    {
        if (lastTag.Type == currentTag.Type)
        {
            currentTag.PairType = PairTokenType.Closing;
            IsTagOpened[currentTag.Type] = false;
            return true;
        }
        ConvertTagToTextTag(lastTag);
        IsTagOpened[currentTag.Type] = false;
        return false;
    }

    private static bool IsNotTag(List<Tag> tags, int pos)
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

    private static bool IsOpenTag(Tag prevTag, Tag nextTag) =>
        IsSpaceChar(prevTag) && !IsSpaceChar(nextTag);

    private static bool IsCloseTag(Tag prevTag, Tag nextTag) =>
        IsSpaceChar(nextTag) && !IsSpaceChar(prevTag);

    private static bool IsTagNearNumber(Tag prevTag, Tag nextTag) =>
        (char.IsDigit(nextTag.TagText[0]) && char.IsLetter(prevTag.TagText[0])) 
        || (char.IsDigit(prevTag.TagText[0]) && char.IsLetter(nextTag.TagText[0]));

    private static bool IsTagPartOfWord(Tag prevTag, Tag nextTag) =>
        char.IsLetter(nextTag.TagText[0]) && char.IsLetter(prevTag.TagText[0]);

    private static bool IsTagBetweenSpaces(Tag prevTag, Tag nextTag) =>
        IsSpaceChar(nextTag) && IsSpaceChar(prevTag);
    
    private static bool IsSpaceChar(Tag tag) => tag.TagText == " ";

    private static bool IsTagInListBounds(List<Tag> tags, int pos) => pos - 1 > 0 && pos + 1 < tags.Count;

    private static bool IsTagsInOneWord(TagInWordInformation currentTag, TagInWordInformation nextTag) =>
        currentTag.WordWithTagIndex != nextTag.WordWithTagIndex;

    private static bool IsSymbolAfterCurrent(string text, int pos) => pos + 1 < text.Length;

    private static bool IsUnprocessedTag(Tag tag) => tag.Type == TagType.Escaping || tag.Type == TagType.Link;
    
    private static bool IsTextTag(Tag tag) => tag.Type == TagType.Text;

    private static bool IsTextInString(List<Tag> stringBetweenTags) => stringBetweenTags.Any(x =>
        char.IsLetter(x.TagText[0]) || char.IsDigit(x.TagText[0])
                                    || x.TagText.Length > 2);
    
    private static bool IsNotFirstOpenTag(Tag currentTag, Tag prevTag, Tag nextTag) =>
        IsOpenTag(prevTag, nextTag) && IsTagOpened[currentTag.Type];
    
    private static bool IsNotCloseTagAfterOpenTag(Tag currentTag, Tag prevTag, Tag nextTag) =>
        IsCloseTag(prevTag, nextTag) && !IsTagOpened[currentTag.Type];

    private static bool IsIncorrectTagsNesting(
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

