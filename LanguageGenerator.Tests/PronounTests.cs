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
        return [.. Enumerable.Range(0, count).Select(_ => Generator.GeneratePronouns(rand, language.Paradigms, language.MorphologicalProfile, language.WordOrder, language.Phonology))];
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
        var alike = systems.Count(s => s.Select(p => string.Join(" ", p.Root.Form.Select(x => x.IpaSymbol))).Distinct().Count() < s.Count);
        //pronouns are kept from sounding alike, so homophones should turn up in under 1% of languages,
        //failing once in a million at exactly 1%. None of 700 have any now
        Assert.True(alike <= Binomial.Most(systems.Count, 0.01, 1e-6), $"{alike} of {systems.Count} languages have pronouns that sound alike");
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
        var pronouns = GeneratedLanguages.With(s => s.Fix(Settings.FirstPersonPlural, FirstPersonPlural.InclusiveExclusive)).Pronouns;

        Assert.Equal("1PL.EXCL", pronouns.For(We).Root.Meaning);
        Assert.Equal("1PL.INCL", pronouns.For(We with { Clusivity = Clusivity.Inclusive }).Root.Meaning);
        Assert.NotEqual(pronouns.For(We).Root.Form, pronouns.For(We with { Clusivity = Clusivity.Inclusive }).Root.Form);
    }

    [Fact]
    public void For_InclusiveInALanguageWithOneWe_IsThatWe()
    {
        var pronouns = GeneratedLanguages.With(s => s.Fix(Settings.FirstPersonPlural, FirstPersonPlural.Plain)).Pronouns;

        Assert.Same(pronouns.For(We), pronouns.For(We with { Clusivity = Clusivity.Inclusive }));
        Assert.Equal("1PL", pronouns.For(We).Root.Meaning);
    }

    [Fact]
    public void For_WeInALanguageWithoutOne_IsI()
    {
        var pronouns = GeneratedLanguages.With(s => s.Fix(Settings.FirstPersonPlural, FirstPersonPlural.SameAsSingular)).Pronouns;

        Assert.Same(pronouns.For(I), pronouns.For(We));
        Assert.Equal("1", pronouns.For(We).Root.Meaning);
    }

    [Fact]
    public void GeneratePronouns_ManyDraws_HaveGendersExactlyWhenGendered()
    {
        var systems = Draw(GeneratedLanguages.Sample[0], 1000);

        Assert.Contains(systems, s => s.GenderedPronouns != PronounGender.None);
        Assert.All(systems, s => Assert.Equal(s.GenderedPronouns == PronounGender.None, s.Genders.Count == 0));
    }

    [Theory]
    [InlineData(PronounGender.ThirdSingular, "3SG")]
    [InlineData(PronounGender.Third, "3PL")]
    [InlineData(PronounGender.ThirdAndSecond, "2SG")]
    public void For_GenderedPronouns_HaveADistinctFormForEachGender(PronounGender gendered, string gloss)
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.GenderedPronouns, gendered));
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
        var pronouns = GeneratedLanguages.With(s => s.Fix(Settings.GenderedPronouns, PronounGender.ThirdSingular, PronounGender.Third, PronounGender.ThirdAndSecond)).Pronouns;
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
        //gendered pronouns have a neuter 40% of the time, which has no setting of its own
        var spec = LanguageSpec.Free.Fix(Settings.GenderedPronouns, PronounGender.ThirdAndSecond);
        var you = Enumerable.Range(0, 30).Select(seed => Generator.Generate(seed, spec)).Where(l => l.Pronouns.Genders.Contains(Gender.Neuter)).Take(3).ToList();

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
        var english = GeneratedLanguages.With(s => s.Fix(Settings.GenderedPronouns, PronounGender.ThirdSingular)).Pronouns;
        var none = GeneratedLanguages.With(s => s.Fix(Settings.GenderedPronouns, PronounGender.None)).Pronouns;

        //English-like languages have one 'they', whatever its gender, and some have no gender at all
        Assert.Same(english.For(new(Person.Third, Number.Plural)), english.For(new(Person.Third, Number.Plural, Gender: Gender.Feminine)));
        Assert.Same(none.For(new(Person.Third, Number.Singular)), none.For(new(Person.Third, Number.Singular, Gender: Gender.Feminine)));
    }

    [Fact]
    public void Generate_NounsWithACaseSlotOfTheirOwn_ShareItWithPronouns()
    {
        //a localization goes with case, in a slot of its own or fused with it
        static bool Spatial(InflectionalCategory c) => c.Is<Case>() || c.Is<Localization>();
        var sharing = GeneratedLanguages.Sample.Where(l => l.Paradigms.Nouns.Categories.Has<Case>()
            && l.Paradigms.Nouns.Slots.Where(s => s.Categories.Any(Spatial)).All(s => s.Categories.All(Spatial))).ToList();

        Assert.NotEmpty(sharing);
        Assert.Contains(sharing, l => l.Paradigms.Nouns.Categories.Has<Localization>());
        Assert.All(sharing, l => Assert.Equal(l.Paradigms.Nouns.Slots.Where(s => s.Categories.Any(Spatial)), l.Pronouns.Paradigm.Slots));
    }

    [Fact]
    public void Realize_PronounSubject_IsAgreedWithAndDroppedWhereTheLanguageDropsThem()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var sentence = Syntax.Realize(language, new Clause("see", Tense.Present, Aspect.Imperfective, I, You) { Adverbs = [] }, "I see you");
            var roots = sentence.Words.SelectMany(w => w.Word.Morphemes).Select(m => m.Value).OfType<Root>().ToList();

            //the object pronoun is always there, and the subject only where it isn't dropped
            Assert.Contains(language.Pronouns.For(You).Root, roots);
            Assert.Equal(!language.Pronouns.DropsSubjects, roots.Contains(language.Pronouns.For(I).Root));
            //agreement may be on the verb or a particle of its own, but never on the pronoun's word
            if (language.Paradigms.Verbs.Categories.Has<Person>())
            {
                var pronoun = language.Pronouns.For(I).Root;
                Assert.Contains(sentence.Words, w => w.Word.Root != pronoun && w.Gloss.Contains("1SG"));
            }
        }
    }
}
