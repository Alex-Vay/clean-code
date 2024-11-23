namespace Markdown;

public class TagsParser
{
    private static Dictionary<TagType, bool>  isTagOpened = new Dictionary<TagType, bool>() 
    { 
        { TagType.H1, false },
        { TagType.Strong, false },
        { TagType.Em, false },
        { TagType.Shield, true },
    };
    private static TagType[] pairTags = { TagType.Strong, TagType.Em };

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
        var tagsTypesTuple = new Dictionary<TagType, List<(int, bool, int)>>(); //в листе, последовательно, номер слова, полностью в слове, индекс в листе тегов
        tagsTypesTuple[TagType.Strong] = new List<(int, bool, int)>();
        tagsTypesTuple[TagType.Em] = new List<(int, bool, int)>();
        var worldCount = 0;
        for (int i = 0; i < tags.Count; i++)
        {
            var currentTag = tags[i];
            if (currentTag.TagText == " ") worldCount++;
            if (tagsTypesTuple.Keys.Contains(currentTag.Type))
            {
                if (i - 1 > 0 && i + 1 < tags.Count)
                    tagsTypesTuple[currentTag.Type].Add((worldCount, IsTagPartOfWord(tags[i - 1], tags[i + 1]), i));
                else
                    tagsTypesTuple[currentTag.Type].Add((worldCount, false, i));
            }
        }
        foreach (var tagName in tagsTypesTuple.Keys)
        {
            var tagsList = tagsTypesTuple[tagName];
            for (var i = 0; i < tagsList.Count - 1; i++)
            {
                var currentTag = tagsList[i];
                var nextTag = tagsList[i + 1];
                if (currentTag.Item1 != nextTag.Item1)
                {
                    if (currentTag.Item2)
                        ConvertTagToTextTag(tags[currentTag.Item3]);
                    if (nextTag.Item2)
                        ConvertTagToTextTag(tags[nextTag.Item3]);
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
            if (!stringBetweenTags.Any(x => char.IsLetter(x.TagText[0]) || char.IsDigit(x.TagText[0])
                || x.TagText.Length > 2))
            {
                ConvertTagToTextTag(openTags[i].tag);
                ConvertTagToTextTag(closeTags[i].tag);
            }
        }
    }

    private static List<Tag> GetAllTags(string paragraph)
    {
        var tagList = new List<Tag>();
        for (int i = 0; i < paragraph.Length; i++)
        {
            var tag = GetTagFromChar(paragraph, i);
            i += tag.TagText.Length - 1;
            if (tag.Type == TagType.Shield) i++;
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
                if (pos + 1 < paragraph.Length && paragraph[pos + 1] == '_')
                    return new Tag(TagType.Strong, PairTokenType.Opening, paragraph.Substring(pos, 2));
                else
                    return new Tag(TagType.Em, PairTokenType.Opening, currentCharInString);
            case '\\':
                string symb = null;
                if (pos + 1 < paragraph.Length)
                {
                    symb = paragraph[pos + 1].ToString();
                    if ("_#\\[]".Contains(symb)) return new Tag(TagType.Shield, PairTokenType.Completed, symb);
                }
                return new Tag(TagType.Text, PairTokenType.None, currentCharInString);
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
        for (int i = 0; i < tags.Count; i++)
        {
            var currentTag = tags[i];
            if (currentTag.Type == TagType.Shield || currentTag.Type == TagType.Link) 
                continue;
            if (stack.Count > 0 && currentTag.Type != TagType.Text)
            {
                if (IsNotTag(tags, i)) continue;
                if (isTagOpened[currentTag.Type])
                {
                    var lastTag = stack.Pop();
                    if (IsPairTags(lastTag, currentTag)) continue;
                    ConvertTagToTextTag(currentTag);
                    while (stack.Count > 0)
                    {
                        lastTag = stack.Pop();
                        ConvertTagToTextTag(lastTag);
                    }
                }
                else if (stack.Last().Type == outsideTag && currentTag.Type == insideTag)
                    ConvertTagToTextTag(currentTag);
            }
            if (currentTag.Type != TagType.Text)
            {
                if (IsNotTag(tags, i)) continue;
                stack.Push(currentTag);
                isTagOpened[currentTag.Type] = true;
            }
        }
        CleanStackOfRemainingTags(stack, tags); ;
        RestoreTagOpenedDictionary();
    }

    private static void CleanStackOfRemainingTags(Stack<Tag> stack, List<Tag> tags)
    {
        while (stack.Count > 0)
        {
            var z = stack.Pop();
            if (z.Type == TagType.H1)
                tags.Add(new Tag(TagType.H1, PairTokenType.Closing, "#"));
            else if (z.PairType != PairTokenType.Single)
                ConvertTagToTextTag(z);
        }
    }

    private static void ConvertTagToTextTag(Tag tag) 
    {
        tag.Type = TagType.Text;
        tag.PairType = PairTokenType.None;
    }

    private static void RestoreTagOpenedDictionary()
    {
        foreach (var curTag in isTagOpened.Keys) isTagOpened[curTag] = false;
        isTagOpened[TagType.Shield] = true;
    }

    private static bool IsPairTags(Tag lastTag, Tag currentTag)
    {
        if (lastTag.Type == currentTag.Type)
        {
            currentTag.PairType = PairTokenType.Closing;
            isTagOpened[currentTag.Type] = false;
            return true;
        }
        else
        {
            ConvertTagToTextTag(lastTag);
            isTagOpened[currentTag.Type] = false;
            return false;
        }
    }

    private static bool IsNotTag(List<Tag> tags, int pos)
    {
        var currentTag = tags[pos];
        var prevPos = pos - 1;
        var nextPos = pos + 1;
        if (prevPos < 0 || nextPos > tags.Count - 1)
            return false;
        var prevTag = tags[prevPos];
        var nextTag = tags[nextPos];
        if (IsOpenTag(prevTag, nextTag) && isTagOpened[currentTag.Type])
        {
            ConvertTagToTextTag(currentTag);
            return true;
        }
        else if (IsCloseTag(prevTag, nextTag) && !isTagOpened[currentTag.Type])
        {
            ConvertTagToTextTag(currentTag);
            return true;
        }
        else if (IsTagNearNumber(prevTag, nextTag) || IsTagBetweenScapes(prevTag, nextTag))
        {
            ConvertTagToTextTag(currentTag);
            return true;
        }
        return false;
    }

    private static bool IsOpenTag(Tag prevTag, Tag nextTag) =>
        prevTag.TagText == " " && nextTag.TagText != " ";

    private static bool IsCloseTag(Tag prevTag, Tag nextTag) =>
        nextTag.TagText == " " && prevTag.TagText != " ";

    private static bool IsTagNearNumber(Tag prevTag, Tag nextTag) =>
        (char.IsDigit(nextTag.TagText[0]) && char.IsLetter(prevTag.TagText[0])) 
        || (char.IsDigit(prevTag.TagText[0]) && char.IsLetter(nextTag.TagText[0]));

    private static bool IsTagPartOfWord(Tag prevTag, Tag nextTag) =>
        char.IsLetter(nextTag.TagText[0]) && char.IsLetter(prevTag.TagText[0]);

    private static bool IsTagBetweenScapes(Tag prevTag, Tag nextTag) =>
        nextTag.TagText == " " && prevTag.TagText == " ";
}
