using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class PossessionTests
{
    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static Sentence Slept(Language l, Argument possessor, Number? number = null) =>
        Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective, new NounPhrase("dog", number ?? Number.Singular, Possessor: possessor)) { Adverbs = [] }, "x");

    private static readonly NounPhrase ThePerson = new("person", Number.Singular);
    private static readonly Pronoun Me = new(Person.First, Number.Singular);

    private static IEnumerable<string> Glosses(Sentence sentence) => sentence.Words.SelectMany(w => w.Gloss.Split('-', '.', '[', ']'));

    [Fact]
    public void GeneratePossession_ManyDraws_MatchTheWalsCounts()
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion == Fusion.Concatenative);
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 5000).Select(_ => Generator.GeneratePossession(rand, language.MorphologicalProfile, language.WordOrder, language.Phonology, 0.15)).ToList();

        //WALS 24A: 98, 78, 22 and 32 of 230 languages modeled
        Assert.InRange(drawn.Count(p => p.Marking == PossessiveMarking.Dependent) / 5000.0, 0.40, 0.45);
        Assert.InRange(drawn.Count(p => p.Marking == PossessiveMarking.Head) / 5000.0, 0.32, 0.36);
        Assert.InRange(drawn.Count(p => p.Marking == PossessiveMarking.Double) / 5000.0, 0.08, 0.11);
        Assert.InRange(drawn.Count(p => p.Marking == PossessiveMarking.None) / 5000.0, 0.12, 0.16);
        Assert.All(drawn, p => Assert.Equal(p.MarksPossessed, p.Affixes is not null));
        Assert.All(drawn.Where(p => p.Affixes is not null), p => Assert.Equal(6, p.Affixes!.Count));
    }

    [Fact]
    public void Generate_AnyLanguage_PutsPossessorsWhereItsAdpositionsPutTheirObjects()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
            Assert.Equal(l.WordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Final, l.Possession.PossessorFirst));
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating), l =>
            Assert.False(l.Possession.MarksPossessed));
    }

    [Fact]
    public void Realize_NounPossessor_GoesAndIsMarkedAsTheLanguageSays()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var possession = language.Possession;
            var sentence = Slept(language, ThePerson);
            var (person, dog) = (IndexOf(sentence, "person"), IndexOf(sentence, "dog"));

            Assert.Equal(possession.PossessorFirst, person < dog);
            Assert.Equal(possession.MarksPossessed, sentence.Words[dog].Gloss.Contains("-3SG.POSS") || sentence.Words[dog].Gloss.StartsWith("3SG.POSS-"));
            var marker = language.RoleMarkers[Role.Possessor];
            var marked = marker switch
            {
                { Adposition: { } a } => IndexOf(sentence, a.Lexeme.Root) >= 0,
                { Case: { } c } => Glosses(sentence).Contains(c.Gloss),
                _ => false,
            };
            Assert.Equal(possession.MarksPossessor, marked);
        }
    }

    [Fact]
    public void Realize_PossessorOfAPossessor_NestsOnTheSameSide()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            //'the person's cat's dog', with the cat's owner inside the cat's phrase
            var sentence = Slept(l, new NounPhrase("cat", Number.Singular, Possessor: ThePerson));
            var (person, cat, dog) = (IndexOf(sentence, "person"), IndexOf(sentence, "cat"), IndexOf(sentence, "dog"));

            Assert.True(l.Possession.PossessorFirst ? person < cat && cat < dog : dog < cat && cat < person);
        });
    }

    [Fact]
    public void Realize_PronounPossessor_IsJustTheAffixWhereOnlyTheNounIsMarked()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var sentence = Slept(language, Me);
            var pronoun = IndexOf(sentence, language.Pronouns.For(Me).Root);

            Assert.Equal(language.Possession.Marking != PossessiveMarking.Head, pronoun >= 0);
            Assert.Equal(language.Possession.MarksPossessed, sentence.Words[IndexOf(sentence, "dog")].Gloss.Contains("1SG"));
        }
    }

    [Fact]
    public void Realize_PluralPossessor_TakesThePluralAffixAndADualFallsBackOnIt()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Possession.MarksPossessed), l =>
        {
            foreach (var number in new[] { Number.Plural, Number.Dual })
            {
                var sentence = Slept(l, new NounPhrase("person", number));
                Assert.Contains("3PL", sentence.Words[IndexOf(sentence, "dog")].Gloss);
            }
        });
    }

    [Fact]
    public void Realize_PossessiveAffix_GoesOutsideTheNounsNumber()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Possession.MarksPossessed), l =>
        {
            var word = Slept(l, Me, Number.Plural).Words.Select(w => w.Word).First(w => w.Root?.Meaning == "dog");
            var morphemes = word.Morphemes.ToList();
            var root = morphemes.FindIndex(m => m.Value is Root);
            var possessive = morphemes.FindIndex(m => m.Value is Affix { Grammemes: [Person, Number, Possessive] });
            var plural = morphemes.FindIndex(m => m.Value is Affix { Grammemes: [Number] });

            if (plural >= 0 && (plural < root) == (possessive < root))
            {
                Assert.True(Math.Abs(possessive - root) > Math.Abs(plural - root));
            }
        });
    }
}
