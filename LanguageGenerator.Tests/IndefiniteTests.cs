using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

//quantifiers and indefinite pronouns: how they're written, how languages draw them, and how a noun
//phrase and a clause say them, with the verb's negation beside a negative one where the language
//has it
public class IndefiniteTests
{
    private static Sentence Parsed(string text) => Stated(Assert.Single(ClauseFile.Parse(text)).Utterance);

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    private static Clause ClauseOf(string gloss) => Assert.IsType<Clause>(Parsed($"x\n  {gloss}").Value);

    private static NounPhrase Indefinite(Quantifier series, Ontology kind) => new(new IndefinitePronoun(series, kind), null);

    [Theory]
    [InlineData("someone", "Some", "Person")]
    [InlineData("nothing", "No", "Thing")]
    [InlineData("everyone", "Every", "Person")]
    public void Parse_IndefinitePronoun_IsAnIndefinitePhraseOfItsKind(string written, string series, string kind) =>
        Assert.Equal(Indefinite(Enum.Parse<Quantifier>(series), Enum.Parse<Ontology>(kind)), PredicateOf<Doing>(ClauseOf($"see  subject:1SG  object:{written}")).Object.GetValueOrDefault().Value);

    [Theory]
    [InlineData("location:somewhere", "Location", "Some")]
    [InlineData("goal:nowhere", "Goal", "No")]
    [InlineData("source:everywhere", "Source", "Every")]
    [InlineData("time:sometime", "Time", "Some")]
    [InlineData("time:never", "Time", "No")]
    [InlineData("time:always", "Time", "Every")]
    public void Parse_IndefinitePlaceOrTime_IsInItsRole(string written, string role, string series)
    {
        var oblique = Assert.Single(ClauseOf($"walk  subject:3PL  {written}").Event.Obliques);

        Assert.Equal(Enum.Parse<Role>(role), oblique.Role);
        Assert.Equal(Indefinite(Enum.Parse<Quantifier>(series), oblique.Role == Role.Time ? Ontology.Time : Ontology.Place), oblique.Argument.Value);
    }

    [Fact]
    public void Parse_IndefiniteWithARelativeClause_KeepsIt()
    {
        var nobody = Assert.IsType<NounPhrase>(ClauseOf("return  subject:(nobody relative:[go  subject:_  goal.in:forest.DEF])").Event.Subject.Value);

        Assert.Equal(new IndefinitePronoun(Quantifier.No, Ontology.Person), nobody.Head.Value);
        Assert.NotNull(nobody.Relative);
    }

    [Theory]
    [InlineData("(traveler.PL few)", "Few", "Plural", null)]
    [InlineData("(light every)", "Every", "Singular", null)]
    [InlineData("(man.PL.DEF most)", "Most", "Plural", "Definite")]
    [InlineData("(traveler no)", "No", "Singular", null)]
    public void Parse_Quantifier_IsInItsNounPhrase(string written, string quantifier, string number, string? definiteness)
    {
        var phrase = NounOf(ClauseOf($"come  subject:{written}").Event.Subject);

        Assert.Equal((Enum.Parse<Quantifier>(quantifier), number, definiteness), (phrase.Quantifier, phrase.Number.Name, (phrase.Determiner?.Value as Definiteness)?.Name));
    }

    [Fact]
    public void Parse_AllWithANumber_KeepsBoth()
    {
        var dogs = NounOf(ClauseOf("sleep  subject:(dog.PL.DEF all 3)").Event.Subject);

        Assert.Equal((Quantifier.All, 3), (dogs.Quantifier, dogs.Count));
    }

    [Theory]
    [InlineData("sleep  subject:nobody.PL", "takes no glosses")]
    [InlineData("sleep  subject:somewhere", "says a place")]
    [InlineData("walk  subject:1SG  route:somewhere", "not route:somewhere")]
    [InlineData("walk  subject:1SG  location.under:somewhere", "can't take a configuration")]
    [InlineData("walk  subject:1SG  when:somewhere", "says a place")]
    [InlineData("walk  subject:1SG  when:nobody", "isn't an adverb")]
    [InlineData("sleep  subject:anyone", "Write nobody in a negated clause")]
    [InlineData("sleep  subject:3SG  time:ever", "Write never in a negated clause")]
    [InlineData("sleep  subject:always", "says a time")]
    [InlineData("walk  subject:1SG  location:never", "never says a time, so it's time:never")]
    [InlineData("walk  subject:1SG  often:never", "so it's time:never")]
    [InlineData("walk  subject:1SG  when:always", "so it's time:always")]
    [InlineData("sleep.NEG  subject:3SG  time:never", "takes no NEG")]
    [InlineData("sleep  subject:somebody", "Write someone")]
    [InlineData("sleep.NEG  subject:nobody", "takes no NEG")]
    [InlineData("see.NEG  subject:1SG  object:(dog no)", "takes no NEG")]
    [InlineData("swim.ABIL.NEG  subject:nobody", "takes no NEG")]
    [InlineData("sleep  subject:(dog each)", "Write every")]
    [InlineData("sleep  subject:(dog.NDF some)", "not both")]
    [InlineData("sleep  subject:(dog.DEF every)", "picks out no particular one")]
    [InlineData("sleep  subject:(dog.PROX no)", "picks out no particular one")]
    [InlineData("sleep  subject:(dog.PL some 3)", "Only all goes with a number")]
    [InlineData("sleep  subject:(dog.PL all most)", "more than one quantifier")]
    [InlineData("sleep  subject:(dog all.PL)", "takes no glosses")]
    [InlineData("sleep  subject:(nobody old)", "takes only a relative clause")]
    [InlineData("sleep  subject:dog.INDF", "write NDF")]
    public void Parse_BadIndefinites_SayWhy(string gloss, string message) => Assert.Contains(message, Error($"x\n  {gloss}").Message);

    [Fact]
    public void Nouns_SkipIndefinitesButNotTheirRelativeClauses() =>
        Assert.Equal(["forest"], ClauseFile.Nouns(ClauseFile.Parse("x\n  return  subject:(nobody relative:[go  subject:_  goal.in:forest.DEF])  location:somewhere")).Select(n => n.Noun));

    private static List<Indefinites> Draws(Func<LanguageSpec, LanguageSpec> fix)
    {
        var language = GeneratedLanguages.With(fix);
        var rand = new Random(1);
        return [.. Enumerable.Range(0, 4000).Select(_ => Generator.GenerateIndefinites(rand, language.MorphologicalProfile, language.Numerals, language.Phonology))];
    }

    [Fact]
    public void GenerateIndefinites_Mixed_MakePersonsAndThingsEachFromQuestionWordsOrNouns()
    {
        var mixed = Draws(s => s).Where(i => i.Type == IndefinitePronouns.Mixed).ToList();

        Assert.NotEmpty(mixed);
        Assert.All(mixed, i => Assert.True(i is { Persons.Value: InterrogativeIndefinites, Things.Value: GenericNounIndefinites }
            or { Persons.Value: GenericNounIndefinites, Things.Value: InterrogativeIndefinites }));
    }

    [Fact]
    public void GenerateIndefinites_ManyDraws_HaveTheFormsTheirSourcesUseAndKeepThemApart()
    {
        Assert.All(Draws(s => s).Take(500), i =>
        {
            var markers = Markers(i).ToList();
            Assert.Equal(markers.Count > 0 ? Indefinites.Series.Count : 0, markers.Count);
            Assert.Equal(i.Type == IndefinitePronouns.Special ? 12 : 0, i.Own.Count());
            Assert.Equal(Enum.GetValues<Quantifier>(), i.Quantifiers.Keys.Order());
            var forms = markers.Select(m => m.Marker.Form).Concat(i.Own.Concat(i.Quantifiers.Values).Select(l => l.Root.Form)).Select(f => string.Concat(f.Select(p => p.IpaSymbol))).ToList();
            Assert.Equal(forms.Count, forms.Distinct().Count());
        });
    }

    //the markers for question words, which persons and things share where both are made from them
    private static IEnumerable<Exponent> Markers(Indefinites indefinites) =>
        new[] { indefinites.Persons.Value, indefinites.Things.Value }.OfType<InterrogativeIndefinites>().Distinct().SelectMany(m => m.Markers.Values);

    private static InterrogativeIndefinites FromQuestionWords(Indefinites indefinites) =>
        Assert.IsType<InterrogativeIndefinites>(indefinites.Persons.Value as InterrogativeIndefinites ?? indefinites.Things.Value);

    private static SpecialIndefinites Special(Indefinites indefinites, Ontology kind) => Assert.IsType<SpecialIndefinites>(indefinites.SourceOf(kind).Value);

    [Fact]
    public void GenerateIndefinites_IsolatingLanguage_MarksQuestionWordsWithWords() =>
        Assert.All(Draws(s => s.Fix(Settings.Fusion, Fusion.Isolating)), i => Assert.DoesNotContain(Markers(i), m => m.Value is Bound));

    [Fact]
    public void GenerateIndefinites_RestrictedNumerals_SayManyWithTheNumeralsWord() =>
        Assert.All(Draws(s => s.Fix(Settings.NumeralBase, NumeralBase.Restricted)), i => Assert.Equal("many", i.Quantifiers[Quantifier.Many].Root.Meaning));

    private static Clause Sees(Argument subject, Argument @object) => Declared(new Event(new Doing("see", @object), subject, [], []), Tense.Present, Aspect.Imperfective, false);

    private static Language With(IndefinitePronouns type) => GeneratedLanguages.With(s => s.Fix(Settings.IndefinitePronouns, type));

    [Fact]
    public void Realize_InterrogativeBased_IsTheQuestionWordWithItsSeriesMarker()
    {
        var language = With(IndefinitePronouns.InterrogativeBased);
        var morphemes = Syntax.Realize(language, Sees(Indefinite(Quantifier.Some, Ontology.Person), BareNoun("dog", Number.Singular)), "x")
            .Words.SelectMany(w => w.Word.Morphemes).ToList();

        Assert.Contains(language.Questions.Words[InterrogativeWord.Who].Root, morphemes);
        Assert.Contains(FromQuestionWords(language.Indefinites).Markers[Quantifier.Some].Marker, morphemes);
    }

    [Fact]
    public void Realize_Special_IsItsWordOfItsOwn()
    {
        var language = With(IndefinitePronouns.Special);
        var sentence = Syntax.Realize(language, Sees(BareNoun("dog", Number.Singular), Indefinite(Quantifier.Every, Ontology.Thing)), "x");

        Assert.True(IndexOf(sentence, Special(language.Indefinites, Ontology.Thing).Words[(Quantifier.Every, Ontology.Thing)].Root) >= 0);
    }

    [Fact]
    public void Realize_GenericNounBased_IsTheNounWithItsQuantifier()
    {
        var language = With(IndefinitePronouns.GenericNounBased);
        var sentence = Syntax.Realize(language, Sees(Indefinite(Quantifier.Some, Ontology.Person), BareNoun("dog", Number.Singular)), "x");

        Assert.True(IndexOf(sentence, language.Lexicon.Noun("person").Root) >= 0);
        Assert.True(IndexOf(sentence, language.Indefinites.Quantifiers[Quantifier.Some].Root) >= 0);
    }

    [Fact]
    public void Realize_Mixed_MakesEachKindAsItsBaseSays()
    {
        var language = With(IndefinitePronouns.Mixed);
        var sentence = Syntax.Realize(language, Sees(Indefinite(Quantifier.Some, Ontology.Person), Indefinite(Quantifier.Some, Ontology.Thing)), "x");
        var (fromNoun, fromQuestion) = language.Indefinites.Persons.Value is GenericNounIndefinites
            ? (language.Lexicon.Noun("person"), language.Questions.Words[InterrogativeWord.What])
            : (language.Lexicon.Noun("thing"), language.Questions.Words[InterrogativeWord.Who]);

        Assert.True(IndexOf(sentence, fromNoun.Root) >= 0);
        Assert.True(IndexOf(sentence, fromQuestion.Root) >= 0);
    }

    [Fact]
    public void Realize_EveryIndefiniteInEveryRole_SaysItsWords()
    {
        var relative = Declared(new Event(new Doing("sleep", null), new Gap(null), [], []), Tense.Present, Aspect.Imperfective, false);
        Assert.All(GeneratedLanguages.Sample.Take(60), l => Assert.All(Indefinites.Series, series =>
        {
            var sentence = Syntax.Realize(l, Declared(new Event(new Doing("see", Indefinite(series, Ontology.Thing)), Indefinite(series, Ontology.Person) with { Relative = Relating(relative) }, [new(Role.Location, Indefinite(series, Ontology.Place), Configuration.At)], []), Tense.Past, Aspect.Perfective, false), "x");

            Assert.True(sentence.Words.Count >= 4);
        }));
    }

    //whether a clause's verb is negated, by the language's own negation marker
    private static bool Negated(Language language, Sentence statement) =>
        Syntax.Realize(language, statement, "x").Words.SelectMany(w => w.Word.Morphemes).Any(m => language.Negation.Markers.Any(n => n.Marker.Equals(m)));

    private static Language Concording(NegativeIndefiniteNegation negation) => GeneratedLanguages.With(s => s
        .Fix(Settings.NegativeIndefiniteNegation, negation).Fix(Settings.ConstituentOrder, ConstituentWordOrder.SVO).Fix(Settings.IndefinitePronouns, IndefinitePronouns.Special));

    [Theory]
    [InlineData("Always", true, true)]
    [InlineData("Never", false, false)]
    [InlineData("AfterVerb", false, true)]
    public void Realize_NegativeIndefinite_NegatesTheVerbAsTheLanguageHasIt(string negation, bool beforeVerb, bool afterVerb)
    {
        var language = Concording(Enum.Parse<NegativeIndefiniteNegation>(negation));
        var nobody = Indefinite(Quantifier.No, Ontology.Person);
        var dog = new NounPhrase(new Noun("dog", Number.Singular, [], null, null, null, Definiteness.Definite, null), null);

        //'nobody sees the dog' has nobody before the verb, and 'the dog sees nobody' after it
        Assert.Equal((beforeVerb, afterVerb), (Negated(language, Sees(nobody, dog)), Negated(language, Sees(dog, nobody))));
        Assert.Equal(afterVerb, Negated(language, Sees(dog, new NounPhrase(new Noun("cat", Number.Singular, [], null, null, Quantifier.No, null, null), null))));
        Assert.False(Negated(language, Sees(Indefinite(Quantifier.Some, Ontology.Person), dog)));
    }

    [Theory]
    [InlineData("Always", true)]
    [InlineData("Never", false)]
    public void Realize_Never_NegatesTheVerbAsNobodyDoes(string negation, bool negated)
    {
        var language = Concording(Enum.Parse<NegativeIndefiniteNegation>(negation));
        var sleeps = Declared(new Event(new Doing("sleep", null), BareNoun("dog", Number.Singular), [new(Role.Time, Indefinite(Quantifier.No, Ontology.Time), Configuration.At)], []), Tense.Present, Aspect.Imperfective, false);

        Assert.Equal(negated, Negated(language, sleeps));
        Assert.True(IndexOf(Syntax.Realize(language, sleeps, "x"), Special(language.Indefinites, Ontology.Time).Words[(Quantifier.No, Ontology.Time)].Root) >= 0);
    }

    [Fact]
    public void Realize_InterrogativeBasedTime_IsWhenWithItsSeriesMarker()
    {
        var language = With(IndefinitePronouns.InterrogativeBased);
        var sleeps = Declared(new Event(new Doing("sleep", null), BareNoun("dog", Number.Singular), [new(Role.Time, Indefinite(Quantifier.Every, Ontology.Time), Configuration.At)], []), Tense.Present, Aspect.Imperfective, false);
        var morphemes = Syntax.Realize(language, sleeps, "x").Words.SelectMany(w => w.Word.Morphemes).ToList();

        Assert.Contains(language.Questions.Words[InterrogativeWord.When].Root, morphemes);
        Assert.Contains(FromQuestionWords(language.Indefinites).Markers[Quantifier.Every].Marker, morphemes);
    }

    [Fact]
    public void Realize_NegativeIndefiniteWithAModal_NegatesItOnce()
    {
        var language = Concording(NegativeIndefiniteNegation.Always);
        var swims = Declared(new Event(new Doing("swim", null), Indefinite(Quantifier.No, Ontology.Person), [], []), Tense.Present, Aspect.Imperfective, false);
        var morphemes = Syntax.Realize(language, WithModal(swims, SituationalModality.Ability, false), "x").Words.SelectMany(w => w.Word.Morphemes).ToList();

        Assert.Equal(language.Negation.Markers.Count, morphemes.Count(m => language.Negation.Markers.Any(n => n.Marker.Equals(m))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Realize_Quantifier_GoesOutsideTheNumeralOnItsSide(bool before)
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.QuantifiersBeforeNoun, before));
        var dogs = new NounPhrase(new Noun("dog", Number.Plural, [new("big", null)], 3, null, Quantifier.All, null, null), null);
        var sentence = Syntax.Realize(language, Declared(new Event(new Doing("sleep", null), dogs, [], []), Tense.Present, Aspect.Imperfective, false), "x");
        var all = IndexOf(sentence, language.Indefinites.Quantifiers[Quantifier.All].Root);
        var others = new[] { IndexOf(sentence, "three"), IndexOf(sentence, "big"), IndexOf(sentence, "dog") };

        Assert.True(before ? all < others.Min() : all > others.Max());
    }

    [Fact]
    public void Generate_IndefiniteSettingsFixed_HaveThem()
    {
        var spec = SpecFile.Parse("""
            {
              "indefinite-pronouns": "Mixed",
              "negative-indefinite-negation": "AfterVerb",
              "quantifiers-before-noun": false
            }
            """);

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.True(l.Indefinites is { Type: IndefinitePronouns.Mixed, Negation: NegativeIndefiniteNegation.AfterVerb, QuantifiersBeforeNoun: false });
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }
}
