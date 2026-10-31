namespace LanguageGenerator.Tests;

public class ParadigmTests
{
    private static readonly Lexeme Dog = new(new Root([Consonant.k, Vowel.a, Consonant.b, Vowel.a], "dog"), PartOfSpeech.Noun);
    private static readonly Lexeme See = new(new Root([Consonant.s, Vowel.i], "see"), PartOfSpeech.Verb);
    private static readonly InflectionalCategory NumberCategory = new("Number", [Number.Singular, Number.Plural]);
    private static readonly InflectionalCategory CaseCategory = new("Case", [Case.Nominative, Case.Accusative]);

    //every slot's exponents are ti, no, se, mə in order, one per combination of its values
    private static ExponentSlot Slot(ExponentPosition position, params InflectionalCategory[] categories)
    {
        var forms = new Queue<Phoneme[]>([[Consonant.t, Vowel.i], [Consonant.n, Vowel.o], [Consonant.s, Vowel.e], [Consonant.m, Vowel.ə]]);
        return new ExponentSlot
        {
            Categories = categories,
            Position = position,
            Exponents = [.. Paradigm.Combinations(categories).Select(values => (values, position is ExponentPosition.Prefix or ExponentPosition.Suffix
                ? (Morpheme)new Affix(forms.Dequeue(), values)
                : new Particle(forms.Dequeue(), values)))],
        };
    }

    private static Paradigm Nouns(params ExponentSlot[] slots) =>
        new() { PartOfSpeech = PartOfSpeech.Noun, Categories = [NumberCategory, CaseCategory], Slots = slots };

    private static (string Segmented, string Gloss) Show(IReadOnlyList<Word> words) =>
        (string.Join(" ", words.Select(w => w.Segmented(Orthography.Ipa))), string.Join(" ", words.Select(w => w.Gloss)));

    [Fact]
    public void Inflect_FusedSlot_ExpressesBothCategoriesInOneAffix()
    {
        var paradigm = Nouns(Slot(ExponentPosition.Suffix, NumberCategory, CaseCategory));

        Assert.Equal(("kaba-mə", "dog-PL.ACC"), Show(paradigm.Inflect(Dog, [Number.Plural, Case.Accusative])));
    }

    [Fact]
    public void Inflect_SlotPerCategory_StacksSuffixesOutward()
    {
        var paradigm = Nouns(Slot(ExponentPosition.Suffix, NumberCategory), Slot(ExponentPosition.Suffix, CaseCategory));

        Assert.Equal(("kaba-no-ti", "dog-PL-NOM"), Show(paradigm.Inflect(Dog, [Number.Plural, Case.Nominative])));
    }

    [Fact]
    public void Inflect_PrefixSlot_GoesBeforeTheRoot()
    {
        var paradigm = Nouns(Slot(ExponentPosition.Prefix, NumberCategory), Slot(ExponentPosition.Suffix, CaseCategory));

        Assert.Equal(("no-kaba-ti", "PL-dog-NOM"), Show(paradigm.Inflect(Dog, [Number.Plural, Case.Nominative])));
    }

    [Fact]
    public void Inflect_FollowingParticles_AreSeparateWordsInnermostFirst()
    {
        var paradigm = Nouns(Slot(ExponentPosition.FollowingParticle, NumberCategory), Slot(ExponentPosition.FollowingParticle, CaseCategory));

        Assert.Equal(("kaba ti no", "dog SG ACC"), Show(paradigm.Inflect(Dog, [Number.Singular, Case.Accusative])));
    }

    [Fact]
    public void Inflect_PrecedingParticles_AreSeparateWordsInnermostNearest()
    {
        var paradigm = Nouns(Slot(ExponentPosition.PrecedingParticle, NumberCategory), Slot(ExponentPosition.PrecedingParticle, CaseCategory));

        Assert.Equal(("no ti kaba", "ACC SG dog"), Show(paradigm.Inflect(Dog, [Number.Singular, Case.Accusative])));
    }

    [Fact]
    public void Inflect_PersonAndNumberInOneSlot_GlossesThemTogether()
    {
        InflectionalCategory person = new("Person", [Person.Third]), number = new("Number", [Number.Singular]), tense = new("Tense", [Tense.Past]);
        var paradigm = new Paradigm { PartOfSpeech = PartOfSpeech.Verb, Categories = [person, number, tense], Slots = [Slot(ExponentPosition.Suffix, person, number, tense)] };

        Assert.Equal(("si-ti", "see-3SG.PST"), Show(paradigm.Inflect(See, [Person.Third, Number.Singular, Tense.Past])));
    }

    [Fact]
    public void Cells_AreEveryCombinationOfValues()
    {
        Assert.Equal(4, Nouns().Cells.Count());
    }

    [Fact]
    public void Inflect_MissingACategoryValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => Nouns().Inflect(Dog, [Number.Plural]));
    }

    [Fact]
    public void Inflect_TwoValuesForOneCategory_Throws()
    {
        Assert.Throws<ArgumentException>(() => Nouns().Inflect(Dog, [Number.Plural, Number.Singular, Case.Nominative]));
    }

    [Fact]
    public void Inflect_WrongPartOfSpeech_Throws()
    {
        Assert.Throws<ArgumentException>(() => Nouns().Inflect(See, [Number.Plural, Case.Nominative]));
    }

    private static bool IsParticle(ExponentSlot slot) => slot.Position is ExponentPosition.PrecedingParticle or ExponentPosition.FollowingParticle;

    private static ExponentSlot SlotWith(Paradigm paradigm, string category) => paradigm.Slots.Single(s => s.Categories.Any(c => c.Name == category));

    [Theory]
    [InlineData(Fusion.Isolating, true)]
    [InlineData(Fusion.Concatenative, false)]
    public void GenerateParadigm_ByFusion_UsesParticlesOnlyWhenIsolating(Fusion fusion, bool particles)
    {
        var slots = GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == fusion)
            .SelectMany(l => l.Paradigms.Values.SelectMany(p => p.Slots)).ToList();

        Assert.NotEmpty(slots);
        Assert.All(slots, s => Assert.Equal(particles, IsParticle(s)));
    }

    [Fact]
    public void GenerateParadigm_MixedFusion_UsesBothParticlesAndAffixes()
    {
        var slots = GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Mixed)
            .SelectMany(l => l.Paradigms.Values.SelectMany(p => p.Slots)).ToList();

        Assert.Contains(slots, IsParticle);
        Assert.Contains(slots, s => !IsParticle(s));
    }

    [Fact]
    public void GenerateParadigm_CaseWithNumber_DecidesWhetherTheyShareASlot()
    {
        var nouns = GeneratedLanguages.Sample.Select(l => (l.MorphologicalProfile.Exponence.CaseWithNumber, Paradigm: l.Paradigms[PartOfSpeech.Noun]))
            .Where(x => x.Paradigm.Categories.Any(c => c.Name == "Case") && x.Paradigm.Categories.Any(c => c.Name == "Number"))
            .ToList();

        Assert.Contains(nouns, x => x.CaseWithNumber);
        Assert.All(nouns, x => Assert.Equal(x.CaseWithNumber, SlotWith(x.Paradigm, "Case") == SlotWith(x.Paradigm, "Number")));
    }

    [Fact]
    public void GenerateParadigm_TamWithAgreement_DecidesWhetherTheyShareASlot()
    {
        var verbs = GeneratedLanguages.Sample.Select(l => (l.MorphologicalProfile.Exponence.TamWithAgreement, Paradigm: l.Paradigms[PartOfSpeech.Verb]))
            .Where(x => x.Paradigm.Categories.Any(c => c.Name == "Person") && x.Paradigm.Categories.Any(c => c.Name is "Tense" or "Aspect"))
            .ToList();

        Assert.Contains(verbs, x => x.TamWithAgreement);
        Assert.All(verbs, x =>
        {
            //agreement fuses with whichever TAM category sits just inside it
            var tam = x.Paradigm.Categories.Last(c => c.Name is "Tense" or "Aspect").Name;
            Assert.Equal(x.TamWithAgreement, SlotWith(x.Paradigm, "Person") == SlotWith(x.Paradigm, tam));
        });
    }

    [Fact]
    public void Morphology_ReadsOffWhatTheParadigmsDo()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var multiCategory = l.Paradigms.Values.SelectMany(p => p.Slots)
                .Any(s => s.Categories.Select(c => c.Name).Except(["Number"]).Count() + (s.Categories.Any(c => c.Name == "Number") && !s.Categories.Any(c => c.Name == "Person") ? 1 : 0) > 1);
            var expected = l.MorphologicalProfile.Fusion == Fusion.Isolating ? Morphology.Analytic
                : multiCategory ? Morphology.Fusional
                : Morphology.Agglutinative;
            Assert.Equal(expected, l.Morphology);
        });
    }

    [Fact]
    public void GenerateParadigm_VerbAgreement_AlwaysFusesPersonAndNumber()
    {
        var agreementSlots = GeneratedLanguages.Sample
            .SelectMany(l => l.Paradigms[PartOfSpeech.Verb].Slots)
            .Where(s => s.Categories.Any(c => c.Name == "Person"))
            .ToList();

        Assert.NotEmpty(agreementSlots);
        Assert.All(agreementSlots, s => Assert.Contains(s.Categories, c => c.Name == "Number"));
    }

    [Fact]
    public void GenerateParadigm_GeneratedLanguages_HaveNoSingleValuedCategory()
    {
        var categories = GeneratedLanguages.Sample.SelectMany(l => l.Paradigms.Values.SelectMany(p => p.Categories));

        Assert.All(categories, c => Assert.True(c.Values.Count >= 2, $"{c.Name} has only {c.Values.Count} value"));
    }

    [Fact]
    public void GenerateParadigm_GeneratedLanguages_KeepExponentsAlmostAlwaysDistinctWithinASlot()
    {
        var slots = GeneratedLanguages.Sample.SelectMany(l => l.Paradigms.Values.SelectMany(p => p.Slots)).ToList();

        //syncretism is allowed when the phonology runs out of distinct forms, which happens in big
        //fused slots whose affixes are bare consonants, but it should stay rare
        var exponents = slots.Sum(s => s.Exponents.Count);
        var repeats = slots.Sum(s => s.Exponents.Count - s.Exponents.Select(e => string.Join(" ", e.Exponent.Form.Select(p => p.IpaSymbol))).Distinct().Count());
        Assert.True(repeats < exponents / 100.0, $"{repeats} of {exponents} exponents repeat a form in their slot");
    }

    [Fact]
    public void SelectNounCategories_OnlyPeripheralCases_AddsADirectCase()
    {
        var categories = Generator.SelectNounCategories(new Random(0), MorphosyntacticAlignment.None, [Case.Dative]);

        Assert.Equal(["DIR", "DAT"], categories.Single(c => c.Name == "Case").Values.Select(v => v.Gloss));
    }
}
