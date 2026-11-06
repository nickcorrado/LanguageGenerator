namespace LanguageGenerator.Tests;

//builders for hand-made phonologies and words, using the full phoneme inventory
internal static class TestData
{
    public static List<Vowel> Vowels { get; } = [.. Vowel.All];
    public static List<Consonant> Consonants { get; } = [.. Consonant.All];

    public static Phonology Phonology(SyllableTemplate template, ClusterRepair repair, params SoundChange[] rules) =>
        Phonology(template, repair, Consonants, rules);

    public static Phonology Phonology(SyllableTemplate template, ClusterRepair repair, List<Consonant> consonants, params SoundChange[] rules) => new()
    {
        Vowels = Vowels,
        Consonants = consonants,
        SyllableRule = new SyllableRule(template, Vowels, consonants),
        SoundChanges = [.. rules],
        ClusterRepair = repair,
        EpentheticVowel = Vowel.ə,
    };

    //the first form is a root meaning "dog"; any after it are plural suffixes
    public static Word Word(params Phoneme[][] morphemes) =>
        new([.. morphemes.Select((form, i) => i == 0 ? (Morpheme)new Root(form, "dog") : new Affix(form, [Number.Plural]))]);
}

//a template with no onsets, which no generated language has, for testing what happens at the edges
internal static class Templates
{
    public static SyllableTemplate Onsetless { get; } = new(Slot.Vowel, Slot.Final);
}

//finding words in a realized sentence, by meaning or by root
internal static class Sentences
{
    public static int IndexOf(Sentence sentence, string meaning) => sentence.Words.ToList().FindIndex(w => w.Word.Root?.Meaning == meaning);
    public static int IndexOf(Sentence sentence, Root root) => sentence.Words.ToList().FindIndex(w => w.Word.Root == root);
}

//generated languages, shared between tests since generating them is the slow part. Seeds are
//fixed, so tests over them are deterministic. Their lexicons are frozen, since a test that coined
//a word into one would change what every other test sees; a test that needs new words generates
//a language of its own
internal static class GeneratedLanguages
{
    private static readonly Lazy<IReadOnlyList<Language>> sample = new(() => [.. Enumerable.Range(0, 300).Select(seed =>
    {
        var language = Generator.Generate(new Random(seed));
        language.Lexicon.Freeze();
        return language;
    })]);

    public static IReadOnlyList<Language> Sample => sample.Value;

    //every lexeme's and pronoun's citation form, and every cell of every paradigm for every lexeme or
    //pronoun it applies to
    public static IEnumerable<Word> AllWords(Language language)
    {
        var lexemes = language.Lexicon.Concat(language.Pronouns.Forms.Values.Distinct()).ToList();
        foreach (var lexeme in lexemes)
        {
            yield return new Word([lexeme.Root]);
        }
        foreach (var paradigm in language.Paradigms.All.Append(language.Pronouns.Paradigm))
        {
            foreach (var lexeme in lexemes.Where(l => l.PartOfSpeech == paradigm.PartOfSpeech))
            {
                foreach (var cell in paradigm.Cells)
                {
                    foreach (var word in paradigm.Inflect(lexeme, cell))
                    {
                        yield return word;
                    }
                }
            }
        }
    }
}
