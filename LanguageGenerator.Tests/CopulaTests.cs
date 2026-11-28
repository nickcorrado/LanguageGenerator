using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class CopulaTests
{
    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static Rendering DogIsPerson(Language l, Tense tense, bool negative = false) =>
        Syntax.Realize(l, Declared(new Event(new Being(BareNoun("person", Number.Singular)), BareNoun("dog", Number.Singular), [], []), tense, Aspect.Imperfective, negative), "the dog is a person");

    private static Rendering DogIsBig(Language l, Tense tense, bool negative = false) =>
        Syntax.Realize(l, Declared(new Event(new Being(new Adjective("big", null)), BareNoun("dog", Number.Singular), [], []), tense, Aspect.Imperfective, negative), "the dog is big");

    private static bool HasCopula(Rendering sentence) => IndexOf(sentence, "COP") >= 0;

    [Fact]
    public void GeneratePredication_Copula_IsOneSyllable()
    {
        var phonology = GeneratedLanguages.Sample[0].Phonology;
        var rand = new Random(1);

        var predications = Enumerable.Range(0, 1000).Select(_ => Generator.GeneratePredication(rand, phonology)).ToList();

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
        foreach (var language in GeneratedLanguages.Sample.Where(l => l.Paradigms.Nouns.Categories.Has<Case>()))
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
            if (l.Paradigms.Verbs.Categories.Has<Tense>())
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
        string.Join("-", word.Morphemes.Where(m => m.Value is not Root and not Affix { Grammemes: [Gender] }).Select(m => m.Gloss));

    private static List<string> GenderGlosses(Word word) =>
        [.. word.Morphemes.Where(m => m.Value is Affix { Grammemes: [Gender] }).Select(m => m.Gloss)];

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
                var sentence = Syntax.Realize(l, Declared(new Event(new Being(new Adjective("big", null)), BareNoun(noun, number), [], []), Tense.Past, Aspect.Imperfective, false), "x");
                var subject = sentence.Words[IndexOf(sentence, noun)].Word;
                var big = sentence.Words[IndexOf(sentence, "big")].Word;
                var gender = l.GenderOf(l.Lexicon.Noun(noun));

                Assert.Equal(Affixes(subject), Affixes(big));
                Assert.Equal(gender is not null && l.Genders.Markers?.Affixes[gender].Form.Count > 0 ? [gender.Gloss] : [], GenderGlosses(big));
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
            var sentence = Syntax.Realize(l, Declared(new Event(new Being(new Adjective("old", null)), they, [], []), Tense.Past, Aspect.Imperfective, false), "they were old");
            var plural = Syntax.Realize(l, Declared(new Event(new Being(new Adjective("old", null)), BareNoun("stone", Number.Plural), [], []), Tense.Past, Aspect.Imperfective, false), "x");
            var old = sentence.Words[IndexOf(sentence, "old")].Word;

            Assert.Equal(Affixes(plural.Words[IndexOf(plural, "old")].Word), Affixes(old));
            Assert.Equal(feminine is not null && l.Genders.Markers?.Affixes[feminine].Form.Count > 0 ? ["F"] : [], GenderGlosses(old));
        });
    }

    private static Rendering DogIsInTheHouse(Language l, Tense tense, bool negative = false) =>
        Syntax.Realize(l, Declared(new Event(new Being(new Oblique(Role.Location, BareNoun("house", Number.Singular), Configuration.At)), BareNoun("dog", Number.Singular), [], []), tense, Aspect.Imperfective, negative), "the dog is in the house");

    [Fact]
    public void GenerateLocationalPredication_ManyDraws_AddOnlyAOneSyllableCopula()
    {
        var phonology = GeneratedLanguages.Sample[0].Phonology;
        var rand = new Random(1);
        var predication = Generator.GeneratePredication(rand, phonology);

        var drawn = Enumerable.Range(0, 1000).Select(_ => Generator.GenerateLocationalPredication(rand, predication, phonology)).ToList();

        Assert.Contains(drawn, p => p.LocationalCopula is not null);
        Assert.All(drawn.Select(p => p.LocationalCopula).OfType<Lexeme>(), l =>
        {
            Assert.Equal("be.at", l.Root.Meaning);
            Assert.Equal(1, l.Root.Form.Count(x => x is Vowel));
        });
        //the rest of the predication is as it was
        Assert.All(drawn, p => Assert.Equal(predication, p with { LocationalCopula = null }));
    }

    [Fact]
    public void Realize_Location_TakesTheLocationalCopulaAndTheLocationsMarker()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var sentence = DogIsInTheHouse(language, Tense.Past);
            var locational = language.Predication.LocationalCopula;

            Assert.Equal(locational is null, HasCopula(sentence));
            Assert.Equal(locational is not null, IndexOf(sentence, "be.at") >= 0);
            switch (language.RoleMarkers[Role.Location])
            {
                case { Adposition: { } a }:
                    Assert.Contains(sentence.Words, w => w.Word.Root == a.Lexeme.Root);
                    break;
                case { Case: { } c }:
                    Assert.Contains(sentence.Words, w => w.Gloss.Split('-', '.', '[', ']').Contains(c.Gloss));
                    break;
            }
        }
    }

    [Fact]
    public void Realize_Location_DropsOnlyTheCopulaAndNeverALocationalVerb()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var predication = language.Predication;
            var present = DogIsInTheHouse(language, Tense.Present);
            var be = IndexOf(present, predication.LocationalCopula is null ? "COP" : "be.at");

            Assert.Equal(!predication.ZeroCopula || predication.LocationalCopula is not null, be >= 0);
            Assert.True(IndexOf(DogIsInTheHouse(language, Tense.Present, negative: true), predication.CopulaFor(locational: true).Root.Meaning) >= 0);
        }
    }

    [Fact]
    public void Realize_Location_GoesWhereObliquesGo()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var sentence = DogIsInTheHouse(language, Tense.Past);
            var be = IndexOf(sentence, language.Predication.CopulaFor(locational: true).Root.Meaning);

            Assert.Equal(language.WordOrder.ObliquePosition == ObliquePosition.AfterVerbAndObject, IndexOf(sentence, "house") > be);
        }
    }

    [Fact]
    public void Realize_CopularClauseWithAnotherOblique_Throws()
    {
        var clause = Declared(new Event(new Being(new Oblique(Role.Goal, BareNoun("house", Number.Singular), Configuration.At)), BareNoun("dog", Number.Singular), [], []), Tense.Past, Aspect.Imperfective, false);

        Assert.Throws<ArgumentException>(() => Syntax.Realize(GeneratedLanguages.Sample[0], clause, "x"));
    }

    [Fact]
    public void Realize_Copula_AgreesWithItsSubject()
    {
        foreach (var language in GeneratedLanguages.Sample.Where(l => l.Paradigms.Verbs.Categories.Has<Person>()))
        {
            var we = new Pronoun(Person.First, Number.Plural);
            var sentence = Syntax.Realize(language, Declared(new Event(new Being(BareNoun("person", Number.Plural)), we, [], []), Tense.Past, Aspect.Imperfective, false), "we were people");

            //agreement may be on the copula or a particle of its own, but never on the pronoun's word
            var pronoun = language.Pronouns.For(we).Root;
            Assert.Contains(sentence.Words, w => w.Word.Root != pronoun && w.Gloss.Contains("1PL"));
        }
    }
}
