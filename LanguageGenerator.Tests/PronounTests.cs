namespace LanguageGenerator.Tests;

public class PronounTests
{
    private static readonly Pronoun I = new(Person.First, Number.Singular);
    private static readonly Pronoun You = new(Person.Second, Number.Singular);
    private static readonly Pronoun We = new(Person.First, Number.Plural);

    //many pronoun systems drawn for one language, which is enough to check the WALS proportions
    private static List<PronounSystem> Draw(Language language, int count)
    {
        var rand = new Random(1);
        return [.. Enumerable.Range(0, count).Select(_ => Generator.GeneratePronouns(rand, language.Paradigms, language.MorphologicalProfile, language.WordOrder, language.Phonology, 0.15))];
    }

    private static bool Agrees(Language language) => language.Paradigms[PartOfSpeech.Verb].Categories.Any(c => c.Name == "Person");

    [Fact]
    public void GeneratePronouns_ManyDraws_MatchTheWalsCountsForWe()
    {
        var systems = Draw(GeneratedLanguages.Sample[0], 4000);
        double Share(FirstPersonPlural we) => systems.Count(s => s.FirstPersonPlural == we) / 4000.0;

        //WALS 39A: 120, 10 and 63 of the 193 languages modeled
        Assert.InRange(Share(FirstPersonPlural.Plain), 0.60, 0.64);
        Assert.InRange(Share(FirstPersonPlural.SameAsSingular), 0.04, 0.07);
        Assert.InRange(Share(FirstPersonPlural.InclusiveExclusive), 0.31, 0.35);
    }

    [Fact]
    public void GeneratePronouns_ManyDraws_DropSubjectsMoreWhereVerbsAgree()
    {
        var agreeing = Draw(GeneratedLanguages.Sample.First(Agrees), 4000);
        var nonAgreeing = Draw(GeneratedLanguages.Sample.First(l => !Agrees(l)), 4000);

        //WALS 101A: 498 of 580 languages drop them, and 61 of 143 where only optional pronouns allow it
        Assert.InRange(agreeing.Count(s => s.DropsSubjects) / 4000.0, 0.84, 0.88);
        Assert.InRange(nonAgreeing.Count(s => s.DropsSubjects) / 4000.0, 0.40, 0.45);
    }

    [Fact]
    public void Generate_AnyLanguage_HasAPronounForEveryPersonAndNumber()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var person in new[] { Person.First, Person.Second, Person.Third })
            {
                foreach (var number in new[] { Number.Singular, Number.Dual, Number.Plural })
                {
                    Assert.Equal(PartOfSpeech.Pronoun, l.Pronouns.For(new(person, number)).PartOfSpeech);
                }
            }
        });
    }

    [Fact]
    public void Generate_Pronouns_AreShortAndNearlyAlwaysDistinct()
    {
        var systems = GeneratedLanguages.Sample.Select(l => l.Pronouns.Forms.Values.Distinct().ToList()).ToList();

        Assert.All(systems.SelectMany(s => s), p => Assert.InRange(p.Root.Form.Count(x => x is Vowel), 1, 2));
        var distinct = systems.Count(s => s.Select(p => string.Join(" ", p.Root.Form.Select(x => x.IpaSymbol))).Distinct().Count() == s.Count);
        Assert.True(distinct >= 0.95 * systems.Count, $"only {distinct} of {systems.Count} languages have all their pronouns distinct");
    }

    [Fact]
    public void For_Dual_IsThePlural()
    {
        var pronouns = GeneratedLanguages.Sample[0].Pronouns;

        Assert.Same(pronouns.For(new(Person.Third, Number.Plural)), pronouns.For(new(Person.Third, Number.Dual)));
    }

    [Fact]
    public void For_We_IsExclusiveUnlessSaidOtherwise()
    {
        var pronouns = GeneratedLanguages.Sample.First(l => l.Pronouns.FirstPersonPlural == FirstPersonPlural.InclusiveExclusive).Pronouns;

        Assert.Equal("1PL.EXCL", pronouns.For(We).Root.Meaning);
        Assert.Equal("1PL.INCL", pronouns.For(We with { Clusivity = Clusivity.Inclusive }).Root.Meaning);
        Assert.NotEqual(pronouns.For(We).Root.Form, pronouns.For(We with { Clusivity = Clusivity.Inclusive }).Root.Form);
    }

    [Fact]
    public void For_InclusiveInALanguageWithOneWe_IsThatWe()
    {
        var pronouns = GeneratedLanguages.Sample.First(l => l.Pronouns.FirstPersonPlural == FirstPersonPlural.Plain).Pronouns;

        Assert.Same(pronouns.For(We), pronouns.For(We with { Clusivity = Clusivity.Inclusive }));
        Assert.Equal("1PL", pronouns.For(We).Root.Meaning);
    }

    [Fact]
    public void For_WeInALanguageWithoutOne_IsI()
    {
        var pronouns = GeneratedLanguages.Sample.First(l => l.Pronouns.FirstPersonPlural == FirstPersonPlural.SameAsSingular).Pronouns;

        Assert.Same(pronouns.For(I), pronouns.For(We));
        Assert.Equal("1", pronouns.For(We).Root.Meaning);
    }

    [Fact]
    public void Generate_NounsWithACaseSlotOfTheirOwn_ShareItWithPronouns()
    {
        var sharing = GeneratedLanguages.Sample.Where(l => l.Paradigms[PartOfSpeech.Noun].Slots.Any(s => s.Categories is [{ Name: "Case" }])).ToList();

        Assert.NotEmpty(sharing);
        Assert.All(sharing, l => Assert.Same(l.Paradigms[PartOfSpeech.Noun].Slots.Single(s => s.Categories is [{ Name: "Case" }]), Assert.Single(l.Pronouns.Paradigm.Slots)));
    }

    [Fact]
    public void Realize_PronounSubject_IsAgreedWithAndDroppedWhereTheLanguageDropsThem()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var sentence = Syntax.Realize(language, new Clause("see", Tense.Present, Aspect.Imperfective, I, You), "I see you");
            var roots = sentence.Words.SelectMany(w => w.Word.Morphemes).Select(m => m.Value).OfType<Root>().ToList();

            //the object pronoun is always there, and the subject only where it isn't dropped
            Assert.Contains(language.Pronouns.For(You).Root, roots);
            Assert.Equal(!language.Pronouns.DropsSubjects, roots.Contains(language.Pronouns.For(I).Root));
            //agreement may be on the verb or a particle of its own, but never on the pronoun's word
            if (Agrees(language))
            {
                var pronoun = language.Pronouns.For(I).Root;
                Assert.Contains(sentence.Words, w => !w.Word.Morphemes.Any(m => m.Value is Root r && r == pronoun) && w.Gloss.Contains("1SG"));
            }
        }
    }
}
