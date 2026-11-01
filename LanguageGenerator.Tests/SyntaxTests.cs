namespace LanguageGenerator.Tests;

public class SyntaxTests
{
    private static Sentence DogSawCat(Language l) =>
        Syntax.Realize(l, new(l.Lexicon.Verb("see"), Tense.Past, Aspect.Perfective, new(l.Lexicon.Noun("dog"), Number.Singular), new(l.Lexicon.Noun("cat"), Number.Singular)), "the dog saw the cat");

    private static Sentence BirdsSleep(Language l) =>
        Syntax.Realize(l, new(l.Lexicon.Verb("sleep"), Tense.Present, Aspect.Imperfective, new(l.Lexicon.Noun("bird"), Number.Plural)), "the birds are sleeping");

    private static Sentence PersonSeesDogs(Language l) =>
        Syntax.Realize(l, new(l.Lexicon.Verb("see"), Tense.Present, Aspect.Imperfective, new(l.Lexicon.Noun("person"), Number.Singular), new(l.Lexicon.Noun("dog"), Number.Plural)), "the person sees the dogs");

    //every gloss label in the sentence, including covert ones in brackets
    private static HashSet<string> Glosses(Sentence sentence) => [.. sentence.Words.SelectMany(w => w.Gloss.Split('-', '.', '[', ']'))];

    private static int Position(Sentence sentence, string meaning) =>
        sentence.Words.ToList().FindIndex(w => w.Gloss.Split('-', '.', '[', ']').Contains(meaning));

    private static bool HasCategory(Language l, PartOfSpeech partOfSpeech, string category) =>
        l.Paradigms[partOfSpeech].Categories.Any(c => c.Name == category);

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
        foreach (var language in GeneratedLanguages.Sample.Where(l => HasCategory(l, PartOfSpeech.Noun, "Case")))
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
        var agreeing = GeneratedLanguages.Sample.Where(l => HasCategory(l, PartOfSpeech.Verb, "Person")).ToList();

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
        Assert.All(GeneratedLanguages.Sample.Where(l => HasCategory(l, PartOfSpeech.Verb, "Tense")), l =>
        {
            Assert.Contains("PST", Glosses(DogSawCat(l)));
            Assert.Contains("PRS", Glosses(BirdsSleep(l)));
        });
    }

    [Fact]
    public void Realize_NounsWithNumber_CarryTheirPhrasesNumber()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => HasCategory(l, PartOfSpeech.Noun, "Number")), l =>
        {
            Assert.Contains("SG", Glosses(PersonSeesDogs(l)));
            Assert.Contains("PL", Glosses(PersonSeesDogs(l)));
        });
    }

    [Fact]
    public void Realize_TenseTheLanguageLacks_FallsBackOnTheFirstTense()
    {
        var language = GeneratedLanguages.Sample.First(l => l.Paradigms[PartOfSpeech.Verb].Categories.Any(c => c.Name == "Tense" && c.Values.Count == 2));

        var sentence = Syntax.Realize(language, new(language.Lexicon.Verb("eat"), Tense.Future, Aspect.Perfective, new(language.Lexicon.Noun("dog"), Number.Singular)), "the dog will eat");

        Assert.Contains("PRS", Glosses(sentence));
    }

    [Theory]
    [InlineData(2, "PL")]
    [InlineData(3, "DU")]
    public void Realize_Dual_IsPluralUnlessTheLanguageHasADual(int numbers, string expected)
    {
        var language = GeneratedLanguages.Sample.First(l => l.Paradigms[PartOfSpeech.Noun].Categories.Any(c => c.Name == "Number" && c.Values.Count == numbers));

        var sentence = Syntax.Realize(language, new(language.Lexicon.Verb("sleep"), Tense.Present, Aspect.Imperfective, new(language.Lexicon.Noun("bird"), Number.Dual)), "the two birds are sleeping");

        Assert.Contains(expected, Glosses(sentence));
        Assert.DoesNotContain("SG", Glosses(sentence));
    }

    [Fact]
    public void Interlinear_SegmentedAndGlossLines_StartEachWordInTheSameColumn()
    {
        var sentence = DogSawCat(GeneratedLanguages.Sample[13]);

        var lines = sentence.Interlinear().Split(Environment.NewLine);

        Assert.Equal(4, lines.Length);
        Assert.Equal(sentence.Text, lines[0]);
        Assert.Equal("'the dog saw the cat'", lines[3]);
        var column = 0;
        foreach (var word in sentence.Words)
        {
            Assert.StartsWith(word.Segmented(sentence.Orthography), lines[1][column..]);
            Assert.StartsWith(word.Gloss, lines[2][column..]);
            column += Math.Max(word.Segmented(sentence.Orthography).Length, word.Gloss.Length) + 2;
        }
    }
}
