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

    private static bool Agrees(Language language) => language.Paradigms[PartOfSpeech.Verb].Categories.Has<Person>();

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
    public void GeneratePronouns_ManyDraws_MatchTheWalsCountsForGender()
    {
        var systems = Draw(GeneratedLanguages.Sample[0], 4000);
        double Share(PronounGender gender) => systems.Count(s => s.GenderedPronouns == gender) / 4000.0;

        //WALS 44A: 254, 61, 42 and 18 of the 375 languages modeled
        Assert.InRange(Share(PronounGender.None), 0.66, 0.70);
        Assert.InRange(Share(PronounGender.ThirdSingular), 0.15, 0.18);
        Assert.InRange(Share(PronounGender.Third), 0.10, 0.13);
        Assert.InRange(Share(PronounGender.ThirdAndSecond), 0.04, 0.06);
        Assert.All(systems, s => Assert.Equal(s.GenderedPronouns == PronounGender.None, s.Genders.Count == 0));
    }

    [Theory]
    [InlineData(PronounGender.ThirdSingular, "3SG")]
    [InlineData(PronounGender.Third, "3PL")]
    [InlineData(PronounGender.ThirdAndSecond, "2SG")]
    public void For_GenderedPronouns_HaveADistinctFormForEachGender(PronounGender gendered, string gloss)
    {
        var language = GeneratedLanguages.Sample.First(l => l.Pronouns.GenderedPronouns == gendered);
        var pronoun = new Pronoun(gloss[0] == '2' ? Person.Second : Person.Third, gloss.EndsWith("PL") ? Number.Plural : Number.Singular);

        //only the third person has a neuter
        var genders = language.Pronouns.Genders.Where(g => g != Gender.Neuter || pronoun.Person == Person.Third).ToList();
        var forms = genders.Select(g => language.Pronouns.For(pronoun with { Gender = g })).ToList();

        Assert.Equal(forms.Count, forms.Select(f => string.Join(" ", f.Root.Form.Select(p => p.IpaSymbol))).Distinct().Count());
        Assert.Equal(genders.Select(g => $"{gloss}.{g.Gloss}"), forms.Select(f => f.Root.Meaning));
    }

    [Fact]
    public void For_GenderedPronoun_IsMasculineUnlessSaidOtherwise()
    {
        var pronouns = GeneratedLanguages.Sample.First(l => l.Pronouns.GenderedPronouns != PronounGender.None).Pronouns;
        var he = new Pronoun(Person.Third, Number.Singular, Gender: Gender.Masculine);

        Assert.Same(pronouns.For(he), pronouns.For(he with { Gender = null }));
    }

    [Fact]
    public void For_NeuterWithoutANeuter_IsMasculine()
    {
        var pronouns = GeneratedLanguages.Sample.First(l => l.Pronouns.Genders is [_, _]).Pronouns;
        var it = new Pronoun(Person.Third, Number.Singular, Gender: Gender.Neuter);

        Assert.Same(pronouns.For(it with { Gender = Gender.Masculine }), pronouns.For(it));
    }

    [Fact]
    public void Generate_SecondPerson_NeverHasANeuter()
    {
        var you = GeneratedLanguages.Sample.Where(l => l.Pronouns.GenderedPronouns == PronounGender.ThirdAndSecond && l.Pronouns.Genders.Contains(Gender.Neuter)).ToList();

        Assert.NotEmpty(you);
        Assert.All(you, l =>
        {
            Assert.DoesNotContain(l.Pronouns.Forms.Keys, p => p.Person == Person.Second && p.Gender == Gender.Neuter);
            Assert.Same(l.Pronouns.For(new(Person.Second, Number.Singular, Gender: Gender.Masculine)), l.Pronouns.For(new(Person.Second, Number.Singular, Gender: Gender.Neuter)));
        });
    }

    [Fact]
    public void For_GenderWhereTheLanguageHasNone_IsIgnored()
    {
        var english = GeneratedLanguages.Sample.First(l => l.Pronouns.GenderedPronouns == PronounGender.ThirdSingular).Pronouns;
        var none = GeneratedLanguages.Sample.First(l => l.Pronouns.GenderedPronouns == PronounGender.None).Pronouns;

        //English-like languages have one 'they', whatever its gender, and some have no gender at all
        Assert.Same(english.For(new(Person.Third, Number.Plural)), english.For(new(Person.Third, Number.Plural, Gender: Gender.Feminine)));
        Assert.Same(none.For(new(Person.Third, Number.Singular)), none.For(new(Person.Third, Number.Singular, Gender: Gender.Feminine)));
    }

    [Fact]
    public void Generate_NounsWithACaseSlotOfTheirOwn_ShareItWithPronouns()
    {
        var sharing = GeneratedLanguages.Sample.Where(l => l.Paradigms[PartOfSpeech.Noun].Slots.Any(s => s.Categories is [var only] && only.Is<Case>())).ToList();

        Assert.NotEmpty(sharing);
        Assert.All(sharing, l => Assert.Same(l.Paradigms[PartOfSpeech.Noun].Slots.Single(s => s.Categories is [var only] && only.Is<Case>()), Assert.Single(l.Pronouns.Paradigm.Slots)));
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
                Assert.Contains(sentence.Words, w => w.Word.Root != pronoun && w.Gloss.Contains("1SG"));
            }
        }
    }
}
