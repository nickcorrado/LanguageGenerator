namespace LanguageGenerator.Tests;

public class ParadigmTests
{
    private static readonly Lexeme Dog = new(new Root([Consonant.k, Vowel.a, Consonant.b, Vowel.a], "dog"), PartOfSpeech.Noun);
    private static readonly Lexeme See = new(new Root([Consonant.s, Vowel.i], "see"), PartOfSpeech.Verb);
    private static readonly InflectionalCategory NumberCategory = new("Number", [Number.Singular, Number.Plural]);
    private static readonly InflectionalCategory CaseCategory = new("Case", [Case.Nominative, Case.Accusative]);

    //every slot's exponents are ti, noh, sei, muh in order, one per combination of its values
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
        (string.Join(" ", words.Select(w => w.Segmented)), string.Join(" ", words.Select(w => w.Gloss)));

    [Fact]
    public void Inflect_FusedSlot_ExpressesBothCategoriesInOneAffix()
    {
        var paradigm = Nouns(Slot(ExponentPosition.Suffix, NumberCategory, CaseCategory));

        Assert.Equal(("kaba-muh", "dog-PL.ACC"), Show(paradigm.Inflect(Dog, [Number.Plural, Case.Accusative])));
    }

    [Fact]
    public void Inflect_SlotPerCategory_StacksSuffixesOutward()
    {
        var paradigm = Nouns(Slot(ExponentPosition.Suffix, NumberCategory), Slot(ExponentPosition.Suffix, CaseCategory));

        Assert.Equal(("kaba-noh-ti", "dog-PL-NOM"), Show(paradigm.Inflect(Dog, [Number.Plural, Case.Nominative])));
    }

    [Fact]
    public void Inflect_PrefixSlot_GoesBeforeTheRoot()
    {
        var paradigm = Nouns(Slot(ExponentPosition.Prefix, NumberCategory), Slot(ExponentPosition.Suffix, CaseCategory));

        Assert.Equal(("noh-kaba-ti", "PL-dog-NOM"), Show(paradigm.Inflect(Dog, [Number.Plural, Case.Nominative])));
    }

    [Fact]
    public void Inflect_FollowingParticles_AreSeparateWordsInnermostFirst()
    {
        var paradigm = Nouns(Slot(ExponentPosition.FollowingParticle, NumberCategory), Slot(ExponentPosition.FollowingParticle, CaseCategory));

        Assert.Equal(("kaba ti noh", "dog SG ACC"), Show(paradigm.Inflect(Dog, [Number.Singular, Case.Accusative])));
    }

    [Fact]
    public void Inflect_PrecedingParticles_AreSeparateWordsInnermostNearest()
    {
        var paradigm = Nouns(Slot(ExponentPosition.PrecedingParticle, NumberCategory), Slot(ExponentPosition.PrecedingParticle, CaseCategory));

        Assert.Equal(("noh ti kaba", "ACC SG dog"), Show(paradigm.Inflect(Dog, [Number.Singular, Case.Accusative])));
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

    [Fact]
    public void GenerateParadigm_ByMorphologicalType_FusesAndUsesParticlesAsTheTypeTends()
    {
        var slots = GeneratedLanguages.Sample
            .SelectMany(l => l.Paradigms.Values.SelectMany(p => p.Slots).Select(s => (l.Morphology, Slot: s)))
            .GroupBy(x => x.Morphology)
            .ToDictionary(g => g.Key, g => (
                CategoriesPerSlot: g.Average(x => x.Slot.Categories.Count),
                Particles: g.Average(x => x.Slot.Position is ExponentPosition.PrecedingParticle or ExponentPosition.FollowingParticle ? 1.0 : 0.0)));

        Assert.True(slots[Morphology.Fusional].CategoriesPerSlot > slots[Morphology.Analytic].CategoriesPerSlot);
        Assert.True(slots[Morphology.Analytic].CategoriesPerSlot > slots[Morphology.Agglutinative].CategoriesPerSlot);
        Assert.True(slots[Morphology.Analytic].Particles > 0.5);
        Assert.True(slots[Morphology.Fusional].Particles < 0.15);
        Assert.True(slots[Morphology.Agglutinative].Particles < 0.15);
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
        var repeats = slots.Sum(s => s.Exponents.Count - s.Exponents.Select(e => e.Exponent.Transcription).Distinct().Count());
        Assert.True(repeats < exponents / 100.0, $"{repeats} of {exponents} exponents repeat a form in their slot");
    }

    [Fact]
    public void SelectNounCategories_OnlyPeripheralCases_AddsADirectCase()
    {
        var categories = Generator.SelectNounCategories(new Random(0), MorphosyntacticAlignment.None, [Case.Dative]);

        Assert.Equal(["DIR", "DAT"], categories.Single(c => c.Name == "Case").Values.Select(v => v.Gloss));
    }
}
