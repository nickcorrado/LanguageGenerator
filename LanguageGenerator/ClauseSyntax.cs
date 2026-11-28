namespace LanguageGenerator;

/*
  a gloss line as it's written, before anything in it is understood: what's nested in what, and
  which role each part is written with, which ClauseFile then builds into a Sentence. Each part
  keeps its text, for errors to quote
 */

/// <summary>
/// One part of a clause or group: a value, written with the role it fills, as in subject:dog, or
/// with none, as a verb, an adjective or why is.
/// </summary>
internal record SyntaxItem(string Text, string? Role, SyntaxValue Value);

/// <summary>A word with any glosses after it, as in dog.PL.DEF, 3SG.F or 2nd.</summary>
internal record SyntaxWord(string Text);

/// <summary>Parts in parentheses, as a noun phrase or a coordination is written: (dog.PL big old).</summary>
internal record SyntaxGroup(string Text, IReadOnlyList<SyntaxItem> Items);

/// <summary>Parts in brackets, as a clause inside another is written, or a whole gloss line.</summary>
internal record SyntaxClause(string Text, IReadOnlyList<SyntaxItem> Items);

internal union SyntaxValue(SyntaxWord, SyntaxGroup, SyntaxClause)
{
    public string Text => this switch
    {
        SyntaxWord w => w.Text,
        SyntaxGroup g => g.Text,
        SyntaxClause c => c.Text,
    };
}

internal static class ClauseSyntax
{
    /// <summary>A gloss line, or the inside of a clause in brackets, as its parts.</summary>
    public static SyntaxClause Read(string line, int number) => new(line.Trim(), new Reader(line, number).Items(null));

    //reads parts separated by spaces, a group or clause as one part however many spaces it holds
    private sealed class Reader(string text, int number)
    {
        private int at;

        //parts up to the bracket that closes what's open, which the caller steps past, or to the end
        //of the line where nothing is
        public List<SyntaxItem> Items(char? closing)
        {
            var items = new List<SyntaxItem>();
            while (true)
            {
                while (at < text.Length && char.IsWhiteSpace(text[at]))
                {
                    at++;
                }
                if (at == text.Length)
                {
                    return closing is { } close ? throw new ClauseFormatException($"There's a {Opening(close)} that isn't closed.", number) : items;
                }
                if (text[at] is ')' or ']')
                {
                    if (text[at] == closing)
                    {
                        return items;
                    }
                    throw new ClauseFormatException(closing is { } expected
                        ? $"There's a {text[at]} where a {expected} should close what's open."
                        : $"There's a {text[at]} without a {Opening(text[at])} before it.", number);
                }
                items.Add(Item());
            }
        }

        //a value, after its role and a colon if it has one: a role is the word before the first colon
        private SyntaxItem Item()
        {
            var start = at;
            var end = at;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not ('(' or '[' or ')' or ']' or ':'))
            {
                end++;
            }
            string? role = null;
            if (end < text.Length && text[end] == ':')
            {
                role = text[at..end];
                at = end + 1;
            }
            var value = Value();
            return new SyntaxItem(text[start..at], role, value);
        }

        private SyntaxValue Value()
        {
            var start = at;
            if (at < text.Length && text[at] is '(' or '[')
            {
                var open = text[at];
                var close = open == '(' ? ')' : ']';
                at++;
                var items = Items(close);
                at++;
                if (at < text.Length && !char.IsWhiteSpace(text[at]) && text[at] is not (')' or ']'))
                {
                    throw new ClauseFormatException($"'{Rest(start)}' has to end at its {close}, as in {(open == '(' ? "(dog big)" : "object:[that  sleep  subject:dog]")}.", number);
                }
                return open == '(' ? new SyntaxGroup(text[start..at], items) : new SyntaxClause(text[start..at], items);
            }
            while (at < text.Length && !char.IsWhiteSpace(text[at]) && text[at] is not (')' or ']'))
            {
                if (text[at] is '(' or '[')
                {
                    throw new ClauseFormatException($"'{Rest(start)}' has a {text[at]} in the middle of it: put a space before it.", number);
                }
                at++;
            }
            return new SyntaxWord(text[start..at]);
        }

        //the text from where a value started up to the next space after here, for an error to quote
        private string Rest(int start)
        {
            var end = at;
            while (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }
            return text[start..end];
        }

        private static char Opening(char closing) => closing switch
        {
            ')' => '(',
            ']' => '[',
            _ => throw new ArgumentOutOfRangeException(nameof(closing), closing, "Only ) and ] close what's open."),
        };
    }
}
