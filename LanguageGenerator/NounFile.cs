namespace LanguageGenerator;

/// <summary>
/// A list of what nouns name, one noun to a line, as in "marta F" or "crow ANIM": the format between
/// the front end and the generator, like <see cref="ClauseFile"/>.
/// </summary>
//it says what a noun means, not what gender it has: a clause file can be translated into any
//language, and each language decides what to make of this, if anything. It's a list of its own
//because a noun's meaning belongs to the word, not to any one sentence, and several texts can share
//one. Tags say what the word itself entails, so 'mother' is F but 'hunter' is HUM, even of a woman.
//Lines starting with # are comments
public static class NounFile
{
    private static readonly Dictionary<string, SemanticClass> Tags = new()
    {
        ["M"] = SemanticClass.Male,
        ["F"] = SemanticClass.Female,
        ["HUM"] = SemanticClass.Human,
        ["ANIM"] = SemanticClass.Animal,
        ["THING"] = SemanticClass.Thing,
    };

    public static IReadOnlyCollection<string> TagNames => Tags.Keys;

    public static Dictionary<string, SemanticClass> Parse(string text)
    {
        var nouns = new Dictionary<string, SemanticClass>();
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var number = i + 1;
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts is not [var written, var tag])
            {
                throw new ClauseFormatException($"'{line}' should be a noun and a tag, as in crow ANIM.", number);
            }
            if (!Tags.TryGetValue(tag, out var meaning))
            {
                throw new ClauseFormatException($"'{tag}' isn't a tag. The tags are {string.Join(", ", TagNames)}.", number);
            }
            //as in a clause file, case doesn't matter
            var noun = written.ToLowerInvariant();
            if (nouns.TryGetValue(noun, out var earlier) && earlier != meaning)
            {
                throw new ClauseFormatException($"'{noun}' is listed as both {earlier} and {meaning}.", number);
            }
            nouns[noun] = meaning;
        }
        return nouns;
    }

    //alphabetical, so a file changes only where its nouns do
    public static string Write(IReadOnlyDictionary<string, SemanticClass> nouns) =>
        string.Concat(nouns.OrderBy(n => n.Key, StringComparer.Ordinal).Select(n => $"{n.Key} {Tags.First(t => t.Value == n.Value).Key}\n"));
}
