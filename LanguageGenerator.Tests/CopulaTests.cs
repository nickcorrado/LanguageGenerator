namespace LanguageGenerator.Tests;

public class CopulaTests
{
    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static Sentence DogIsPerson(Language l, Tense tense, bool negative = false) =>
        Syntax.Realize(l, new CopularClause(new NounPhrase("person", Number.Singular), tense, Aspect.Imperfective, new NounPhrase("dog", Number.Singular), negative), "the dog is a person");

    private static Sentence DogIsBig(Language l, Tense tense, bool negative = false) =>
        Syntax.Realize(l, new CopularClause(new Adjective("big"), tense, Aspect.Imperfective, new NounPhrase("dog", Number.Singular), negative), "the dog is big");

    private static int IndexOf(Sentence sentence, string meaning) =>
        sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Root r && r.Meaning == meaning));

    private static bool HasCopula(Sentence sentence) => IndexOf(sentence, "COP") >= 0;

    [Fact]
    public void GeneratePredication_ManyDraws_MatchTheWalsCounts()
    {
        var phonology = GeneratedLanguages.Sample[0].Phonology;
        var rand = new Random(1);

        var predications = Enumerable.Range(0, 5000).Select(_ => Generator.GeneratePredication(rand, phonology, 0.15)).ToList();

        //WALS 118A: 151 of 283 languages modeled; 120A: 175 of 386
        Assert.InRange(predications.Count(p => p.Adjectives == PredicativeAdjectives.Verbal) / 5000.0, 0.51, 0.56);
        Assert.InRange(predications.Count(p => p.ZeroCopula) / 5000.0, 0.43, 0.48);
        Assert.All(predications, p => Assert.Equal(1, p.Copula.Root.Form.Count(x => x is Vowel)));
    }

    [Fact]
    public void Realize_PredicateNoun_DropsTheCopulaOnlyInTheAffirmativePresentOfZeroCopulaLanguages()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var zero = language.Predication.ZeroCopula;

            Assert.Equal(!zero, HasCopula(DogIsPerson(language, Tense.Present)));
            Assert.True(HasCopula(DogIsPerson(language, Tense.Past)));
            Assert.True(HasCopula(DogIsPerson(language, Tense.Present, negative: true)));
        }
    }

    [Fact]
    public void Realize_PredicateNoun_TakesTheIntransitiveSubjectsCase()
    {
        foreach (var language in GeneratedLanguages.Sample.Where(l => l.Paradigms[PartOfSpeech.Noun].Categories.Any(c => c.Name == "Case")))
        {
            var sentence = DogIsPerson(language, Tense.Past);
            string Cases(int word) => string.Join(" ", sentence.Words[word].Gloss.Split('-', '.', '[', ']').Where(g => language.Cases.Any(c => c.Gloss == g) || g == "DIR"));

            Assert.Equal(Cases(IndexOf(sentence, "dog")), Cases(IndexOf(sentence, "person")));
        }
    }

    [Fact]
    public void Realize_PredicateNoun_GoesWhereAnObjectWould()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var order = language.WordOrder.ConstituentWordOrder.ToString();
            var sentence = DogIsPerson(language, Tense.Past);
            var positions = new[] { ('S', IndexOf(sentence, "dog")), ('V', IndexOf(sentence, "COP")), ('O', IndexOf(sentence, "person")) };

            Assert.Equal(order, string.Concat(positions.OrderBy(p => p.Item2).Select(p => p.Item1)));
        }
    }

    [Fact]
    public void Realize_VerbalAdjective_InflectsLikeAVerbWithNoCopula()
    {
        var verbal = GeneratedLanguages.Sample.Where(l => l.Predication.Adjectives == PredicativeAdjectives.Verbal).ToList();

        Assert.NotEmpty(verbal);
        Assert.All(verbal, l =>
        {
            var sentence = DogIsBig(l, Tense.Past);
            Assert.False(HasCopula(sentence));
            //it carries the verb's tense, wherever the language marks tense
            if (l.Paradigms[PartOfSpeech.Verb].Categories.Any(c => c.Name == "Tense"))
            {
                Assert.Contains(sentence.Words, w => w.Gloss.Contains("PST"));
            }
            Assert.Contains(DogIsBig(l, Tense.Past, negative: true).Words, w => w.Gloss.Contains("NEG"));
        });
    }

    [Fact]
    public void Realize_NonverbalAdjective_TakesTheCopulaLikeANoun()
    {
        var nonverbal = GeneratedLanguages.Sample.Where(l => l.Predication.Adjectives == PredicativeAdjectives.Nonverbal).ToList();

        Assert.NotEmpty(nonverbal);
        Assert.All(nonverbal, l =>
        {
            Assert.Equal(!l.Predication.ZeroCopula, HasCopula(DogIsBig(l, Tense.Present)));
            Assert.True(HasCopula(DogIsBig(l, Tense.Past)));
        });
    }

    [Fact]
    public void Realize_PredicateAdjectiveWhereAdjectivesDontAgree_IsBare()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Predication.Adjectives == PredicativeAdjectives.Nonverbal && l.AdjectiveAgreement != AdjectiveAgreement.Agreement), l =>
            Assert.Equal("big", DogIsBig(l, Tense.Past).Words[IndexOf(DogIsBig(l, Tense.Past), "big")].Gloss));
    }

    //a word's affixes, by gloss, leaving out gender markers
    private static string Affixes(Word word) =>
        string.Join("-", word.Morphemes.Where(m => m.Value is not Root and not Affix { Tagmemes: [Gender] }).Select(m => m.Gloss));

    private static List<string> GenderGlosses(Word word) =>
        [.. word.Morphemes.Where(m => m.Value is Affix { Tagmemes: [Gender] }).Select(m => m.Gloss)];

    private static List<Language> Agreeing() =>
        [.. GeneratedLanguages.Sample.Where(l => l.Predication.Adjectives == PredicativeAdjectives.Nonverbal && l.AdjectiveAgreement == AdjectiveAgreement.Agreement)];

    [Fact]
    public void Realize_AgreeingPredicateAdjective_TakesItsSubjectsNumberCaseAndGender()
    {
        Assert.NotEmpty(Agreeing());
        Assert.All(Agreeing(), l =>
        {
            foreach (var (noun, number) in new[] { ("dog", Number.Plural), ("sun", Number.Singular), ("person", Number.Plural) })
            {
                var sentence = Syntax.Realize(l, new CopularClause(new Adjective("big"), Tense.Past, Aspect.Imperfective, new NounPhrase(noun, number)), "x");
                var subject = sentence.Words[IndexOf(sentence, noun)].Word;
                var big = sentence.Words[IndexOf(sentence, "big")].Word;
                var gender = l.Genders.Of(l.Lexicon.Noun(noun));

                Assert.Equal(Affixes(subject), Affixes(big));
                Assert.Equal(gender is not null && l.Genders.Markers?[gender].Form.Count > 0 ? [gender.Gloss] : [], GenderGlosses(big));
            }
        });
    }

    [Fact]
    public void Realize_PredicateAdjectiveWithAPronounSubject_AgreesInWhatThePronounSays()
    {
        Assert.All(Agreeing(), l =>
        {
            //number from the pronoun, and gender only if it gives one the language has
            var feminine = l.Genders.Values.FirstOrDefault(g => g == Gender.Feminine);
            var they = new Pronoun(Person.Third, Number.Plural, Gender: Gender.Feminine);
            var sentence = Syntax.Realize(l, new CopularClause(new Adjective("old"), Tense.Past, Aspect.Imperfective, they), "they were old");
            var plural = Syntax.Realize(l, new CopularClause(new Adjective("old"), Tense.Past, Aspect.Imperfective, new NounPhrase("stone", Number.Plural)), "x");
            var old = sentence.Words[IndexOf(sentence, "old")].Word;

            Assert.Equal(Affixes(plural.Words[IndexOf(plural, "old")].Word), Affixes(old));
            Assert.Equal(feminine is not null && l.Genders.Markers?[feminine].Form.Count > 0 ? ["F"] : [], GenderGlosses(old));
        });
    }

    [Fact]
    public void Realize_Copula_AgreesWithItsSubject()
    {
        foreach (var language in GeneratedLanguages.Sample.Where(l => l.Paradigms[PartOfSpeech.Verb].Categories.Any(c => c.Name == "Person")))
        {
            var we = new Pronoun(Person.First, Number.Plural);
            var sentence = Syntax.Realize(language, new CopularClause(new NounPhrase("person", Number.Plural), Tense.Past, Aspect.Imperfective, we), "we were people");

            //agreement may be on the copula or a particle of its own, but never on the pronoun's word
            var pronoun = language.Pronouns.For(we).Root;
            Assert.Contains(sentence.Words, w => !w.Word.Morphemes.Any(m => m.Value is Root r && r == pronoun) && w.Gloss.Contains("1PL"));
        }
    }
}
