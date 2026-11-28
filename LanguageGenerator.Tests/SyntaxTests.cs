using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Phrases;
namespace LanguageGenerator.Tests;

public class SyntaxTests
{
    private static Rendering DogSawCat(Language l) =>
        Syntax.Realize(l, Declared(new Event(new Doing("see", BareNoun("cat", Number.Singular)), BareNoun("dog", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), "the dog saw the cat");

    private static Rendering BirdsSleep(Language l) =>
        Syntax.Realize(l, Declared(new Event(new Doing("sleep", null), BareNoun("bird", Number.Plural), [], []), Tense.Present, Aspect.Imperfective, false), "the birds are sleeping");

    private static Rendering PersonSeesDogs(Language l) =>
        Syntax.Realize(l, Declared(new Event(new Doing("see", BareNoun("dog", Number.Plural)), BareNoun("person", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), "the person sees the dogs");

    //every gloss label in the sentence, including covert ones in brackets
    private static HashSet<string> Glosses(Rendering sentence) => [.. sentence.Words.SelectMany(w => w.Gloss.Split('-', '.', '[', ']'))];

    private static int Position(Rendering sentence, string meaning) =>
        sentence.Words.ToList().FindIndex(w => w.Gloss.Split('-', '.', '[', ']').Contains(meaning));

    private static bool HasCategory(Paradigm paradigm, string category) =>
        paradigm.Categories.Any(c => c.Name == category);

    [Fact]
    public void Realize_GeneratedLanguages_PlaceConstituentsInTheirWordOrder()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var order = language.WordOrder.ConstituentWordOrder.ToString();
            var transitive = DogSawCat(language);
            var intransitive = BirdsSleep(language);

            var constituents = new[] { ('S', Position(transitive, "dog")), ('V', Position(transitive, "see")), ('O', Position(transitive, "cat")) };
            Assert.Equal(order, string.Concat(constituents.OrderBy(c => c.Item2).Select(c => c.Item1)));
            var intransitiveConstituents = new[] { ('S', Position(intransitive, "bird")), ('V', Position(intransitive, "sleep")) };
            Assert.Equal(order.Replace("O", ""), string.Concat(intransitiveConstituents.OrderBy(c => c.Item2).Select(c => c.Item1)));
        }
    }

    [Fact]
    public void Realize_GeneratedLanguages_MarkArgumentsWithTheirAlignmentsCases()
    {
        foreach (var language in GeneratedLanguages.Sample.Where(l => HasCategory(l.Paradigms.Nouns, "Case")))
        {
            var (agent, patient, intransitiveSubject) = language.MorphosyntacticAlignment switch
            {
                MorphosyntacticAlignment.NominativeAccusative => ("NOM", "ACC", "NOM"),
                MorphosyntacticAlignment.ErgativeAbsolutive => ("ERG", "ABS", "ABS"),
                _ => ("DIR", "DIR", "DIR"),
            };

            Assert.Contains(agent, Glosses(DogSawCat(language)));
            Assert.Contains(patient, Glosses(DogSawCat(language)));
            Assert.Contains(intransitiveSubject, Glosses(BirdsSleep(language)));
        }
    }

    [Fact]
    public void Realize_ErgativeLanguages_NeverMarkAnIntransitiveSubjectErgative()
    {
        var ergative = GeneratedLanguages.Sample.Where(l => l.MorphosyntacticAlignment == MorphosyntacticAlignment.ErgativeAbsolutive).ToList();

        Assert.NotEmpty(ergative);
        Assert.All(ergative, l => Assert.DoesNotContain("ERG", Glosses(BirdsSleep(l))));
    }

    [Fact]
    public void Realize_VerbsWithAgreement_AgreeWithTheirSubject()
    {
        var agreeing = GeneratedLanguages.Sample.Where(l => HasCategory(l.Paradigms.Verbs, "Person")).ToList();

        Assert.NotEmpty(agreeing);
        Assert.All(agreeing, l =>
        {
            Assert.Contains("3SG", Glosses(DogSawCat(l)));
            Assert.Contains("3PL", Glosses(BirdsSleep(l)));
            Assert.Contains("3SG", Glosses(PersonSeesDogs(l)));
        });
    }

    [Fact]
    public void Realize_VerbsWithTense_CarryTheClausesTense()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => HasCategory(l.Paradigms.Verbs, "Tense")), l =>
        {
            Assert.Contains("PST", Glosses(DogSawCat(l)));
            Assert.Contains("PRS", Glosses(BirdsSleep(l)));
        });
    }

    [Fact]
    public void Realize_NounsWithNumber_CarryTheirPhrasesNumber()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => HasCategory(l.Paradigms.Nouns, "Number")), l =>
        {
            Assert.Contains("SG", Glosses(PersonSeesDogs(l)));
            Assert.Contains("PL", Glosses(PersonSeesDogs(l)));
        });
    }

    [Fact]
    public void Realize_TenseTheLanguageLacks_FallsBackOnTheFirstTense()
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.Tenses, Tenses.PresentPast));

        var sentence = Syntax.Realize(language, Declared(new Event(new Doing("eat", null), BareNoun("dog", Number.Singular), [], []), Tense.Future, Aspect.Perfective, false), "the dog will eat");

        Assert.Contains("PRS", Glosses(sentence));
    }

    [Theory]
    [InlineData(NounNumber.SingularPlural, "PL")]
    [InlineData(NounNumber.SingularDualPlural, "DU")]
    public void Realize_Dual_IsPluralUnlessTheLanguageHasADual(NounNumber numbers, string expected)
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.NounNumber, numbers));

        var sentence = Syntax.Realize(language, Declared(new Event(new Doing("sleep", null), BareNoun("bird", Number.Dual), [], []), Tense.Present, Aspect.Imperfective, false), "the two birds are sleeping");

        Assert.Contains(expected, Glosses(sentence));
        Assert.DoesNotContain("SG", Glosses(sentence));
    }

    [Fact]
    public void Interlinear_SegmentedAndGlossLines_StartEachGroupInTheSameColumn()
    {
        var sentence = DogSawCat(GeneratedLanguages.Sample[13]);

        var lines = sentence.Interlinear(phonetic: false).Split(Environment.NewLine);

        Assert.Equal(4, lines.Length);
        Assert.Equal(sentence.Text, lines[0]);
        Assert.Equal("'the dog saw the cat'", lines[3]);
        var column = 0;
        foreach (var group in sentence.Groups)
        {
            Assert.StartsWith(group.Segmented(sentence.Orthography), lines[1][column..]);
            Assert.StartsWith(group.Gloss, lines[2][column..]);
            column += Math.Max(group.Segmented(sentence.Orthography).Length, group.Gloss.Length) + 2;
        }
    }
}
