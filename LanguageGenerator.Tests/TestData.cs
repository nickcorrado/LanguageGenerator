namespace LanguageGenerator.Tests;

//builders for hand-made phonologies and words, using the full phoneme inventory. A phonology may say
//every sound it lacks as an allophone, as a generated language may
internal static class TestData
{
    public static List<Vowel> Vowels { get; } = [.. Vowel.All];
    public static List<Consonant> Consonants { get; } = [.. Consonant.All];
    public static StressSystem Stress { get; } = new(LanguageGenerator.Stress.Penultimate, false);

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
        Stress = Stress,
        Dropoff = new Dropoff(0.15, 0.15),
        SyllableContact = SyllableContact.Any,
        Allophones = Generator.SelectAllophones(Vowels, consonants),
        Sandhi = NoSandhi,
    };

    //nothing happens where words meet, and clitics are stressed with their host by the rule
    public static Sandhi NoSandhi { get; } = new([], false, CliticStress.Group);

    //the first form is a root meaning "dog"; any after it are plural suffixes
    public static Word Word(params Phoneme[][] morphemes) =>
        new([.. morphemes.Select((form, i) => i == 0 ? (Morpheme)new Root(form, "dog", null) : new Affix(form, [Number.Plural], AffixAccent.None))]);
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

//a statement's predicate or force, where it's a clause with one of that kind
internal static class Clauses
{
    public static T PredicateOf<T>(Statement statement) => Assert.IsType<T>(Assert.IsType<Clause>(statement.Value).Predicate.Value);
    public static T ForceOf<T>(Statement statement) => Assert.IsType<T>(Assert.IsType<Clause>(statement.Value).Force.Value);
}

//a noun on its own, with no modifiers, determiner, possessor or relative clause
internal static class Phrases
{
    public static NounPhrase BareNoun(string meaning, Number number) => new(new Noun(meaning, number, [], null, null, null, null, null), null);

    //the noun a phrase is of, where it's a noun phrase of a noun
    public static Noun NounOf(Argument argument) => NounOf(Assert.IsType<NounPhrase>(argument.Value));
    public static Noun NounOf(NounPhrase phrase) => Assert.IsType<Noun>(phrase.Head.Value);
}

//what the tests' people and animals are, as a noun file would say
internal static class TestNouns
{
    public static IReadOnlyDictionary<string, SemanticClass> All { get; } = NounFile.Parse("woman F\nking M\nperson HUM\nhunter HUM\ndog ANIM\ncat ANIM");

    public static SemanticClass Of(string meaning) => All.GetValueOrDefault(meaning, SemanticClass.Thing);
}

/*
  generated languages, shared between tests since generating them is the slow part. Seeds are
  fixed, so tests over them are deterministic. Their lexicons are frozen, since a test that coined
  a word into one would change what every other test sees; a test that needs new words generates
  a language of its own
 */
internal static class GeneratedLanguages
{
    private static readonly Lazy<IReadOnlyList<Language>> sample = new(() => [.. Enumerable.Range(0, 300).Select(seed =>
    {
        var language = Generator.Generate(seed, LanguageSpec.Free);
        language.Lexicon.Classify(TestNouns.All);
        language.Lexicon.Freeze();
        return language;
    })]);

    public static IReadOnlyList<Language> Sample => sample.Value;

    /*
      a language with some settings fixed, for a test that needs one with some feature, so it doesn't
      depend on which of the sample happen to have it. It's the test's own, so it's neither classified
      nor frozen
     */
    public static Language With(Func<LanguageSpec, LanguageSpec> fix) => Generator.Generate(1, fix(LanguageSpec.Free));

    //a spec that inflects, by any fusion but isolating
    public static LanguageSpec Inflecting(this LanguageSpec spec) => spec.Fix(Settings.Fusion, Fusion.Concatenative, Fusion.Mixed);

    //a spec whose object goes before the verb, or after it, by any constituent order that does
    public static LanguageSpec ObjectFirst(this LanguageSpec spec, bool objectFirst) =>
        spec.Fix(Settings.ConstituentOrder, [.. Enum.GetValues<ConstituentWordOrder>().Where(o => WordOrder.ObjectPrecedesVerb(o) == objectFirst)]);

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
