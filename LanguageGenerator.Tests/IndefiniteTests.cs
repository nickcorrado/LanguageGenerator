using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

//quantifiers and indefinite pronouns: how they're written, how languages draw them, and how a noun
//phrase and a clause say them, with the verb's negation beside a negative one where the language
//has it
public class IndefiniteTests
{
    private static Statement Parsed(string text) => Assert.Single(ClauseFile.Parse(text)).Statement;

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    private static Clause ClauseOf(string gloss) => Assert.IsType<Clause>(Parsed($"x\n  {gloss}").Value);

    private static NounPhrase Indefinite(Quantifier series, Ontology kind) => Indefinites.Pronoun(series, kind);

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
        var oblique = Assert.Single(ClauseOf($"walk  subject:3PL  {written}").Obliques);

        Assert.Equal(Enum.Parse<Role>(role), oblique.Role);
        Assert.Equal(Indefinite(Enum.Parse<Quantifier>(series), oblique.Role == Role.Time ? Ontology.Time : Ontology.Place), oblique.Argument.Value);
    }

    [Fact]
    public void Parse_IndefiniteWithARelativeClause_KeepsIt()
    {
        var nobody = Assert.IsType<NounPhrase>(ClauseOf("return  subject:(nobody relative:[go  subject:_  goal.in:forest.DEF])").Force.Subject.Value);

        Assert.Equal(("person", Quantifier.No, Ontology.Person), (nobody.Noun, nobody.Quantifier, nobody.Indefinite));
        Assert.NotNull(nobody.Relative);
    }

    [Theory]
    [InlineData("(traveler.PL few)", "Few", "Plural", null)]
    [InlineData("(light every)", "Every", "Singular", null)]
    [InlineData("(man.PL.DEF most)", "Most", "Plural", "Definite")]
    [InlineData("(traveler no)", "No", "Singular", null)]
    public void Parse_Quantifier_IsInItsNounPhrase(string written, string quantifier, string number, string? definiteness)
    {
        var phrase = Assert.IsType<NounPhrase>(ClauseOf($"come  subject:{written}").Force.Subject.Value);

        Assert.Equal((Enum.Parse<Quantifier>(quantifier), number, definiteness, (Ontology?)null), (phrase.Quantifier, phrase.Number.Name, phrase.Definiteness?.Name, phrase.Indefinite));
    }

    [Fact]
    public void Parse_AllWithANumber_KeepsBoth()
    {
        var dogs = Assert.IsType<NounPhrase>(ClauseOf("sleep  subject:(dog.PL.DEF all 3)").Force.Subject.Value);

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
        Assert.All(mixed, i =>
            Assert.Equal([IndefiniteBase.Interrogative, IndefiniteBase.GenericNoun], new[] { i.PersonBase, i.ThingBase }.Order()));
    }

    [Fact]
    public void GenerateIndefinites_ManyDraws_HaveEveryFormAndKeepThemApart()
    {
        Assert.All(Draws(s => s).Take(500), i =>
        {
            Assert.Equal(Indefinites.Series.Order(), i.Markers.Keys.Order());
            Assert.Equal(12, i.Own.Count);
            Assert.Equal(Enum.GetValues<Quantifier>(), i.Quantifiers.Keys.Order());
            var forms = i.Markers.Values.Select(m => m.Form).Concat(i.Own.Values.Concat(i.Quantifiers.Values).Select(l => l.Root.Form)).Select(f => string.Concat(f.Select(p => p.IpaSymbol))).ToList();
            Assert.Equal(forms.Count, forms.Distinct().Count());
        });
    }

    [Fact]
    public void GenerateIndefinites_IsolatingLanguage_MarksQuestionWordsWithWords() =>
        Assert.All(Draws(s => s.Fix(Settings.Fusion, Fusion.Isolating)), i => Assert.False(i.MarkersAreAffixes));

    [Fact]
    public void GenerateIndefinites_RestrictedNumerals_SayManyWithTheNumeralsWord() =>
        Assert.All(Draws(s => s.Fix(Settings.NumeralBase, NumeralBase.Restricted)), i => Assert.Equal("many", i.Quantifiers[Quantifier.Many].Root.Meaning));

    private static Clause Sees(Argument subject, Argument @object) => new Clause(new Doing("see", @object), new Declarative(Tense.Present, Aspect.Imperfective, subject), false, [], []);

    private static Language With(IndefinitePronouns type) => GeneratedLanguages.With(s => s.Fix(Settings.IndefinitePronouns, type));

    [Fact]
    public void Realize_InterrogativeBased_IsTheQuestionWordWithItsSeriesMarker()
    {
        var language = With(IndefinitePronouns.InterrogativeBased);
        var morphemes = Syntax.Realize(language, Sees(Indefinite(Quantifier.Some, Ontology.Person), new NounPhrase("dog", Number.Singular)), "x")
            .Words.SelectMany(w => w.Word.Morphemes).ToList();

        Assert.Contains(language.Questions.Words[Interrogative.Who].Root, morphemes);
        Assert.Contains(language.Indefinites.Markers[Quantifier.Some], morphemes);
    }

    [Fact]
    public void Realize_Special_IsItsWordOfItsOwn()
    {
        var language = With(IndefinitePronouns.Special);
        var sentence = Syntax.Realize(language, Sees(new NounPhrase("dog", Number.Singular), Indefinite(Quantifier.Every, Ontology.Thing)), "x");

        Assert.True(IndexOf(sentence, language.Indefinites.Own[(Quantifier.Every, Ontology.Thing)].Root) >= 0);
    }

    [Fact]
    public void Realize_GenericNounBased_IsTheNounWithItsQuantifier()
    {
        var language = With(IndefinitePronouns.GenericNounBased);
        var sentence = Syntax.Realize(language, Sees(Indefinite(Quantifier.Some, Ontology.Person), new NounPhrase("dog", Number.Singular)), "x");

        Assert.True(IndexOf(sentence, language.Lexicon.Noun("person").Root) >= 0);
        Assert.True(IndexOf(sentence, language.Indefinites.Quantifiers[Quantifier.Some].Root) >= 0);
    }

    [Fact]
    public void Realize_Mixed_MakesEachKindAsItsBaseSays()
    {
        var language = With(IndefinitePronouns.Mixed);
        var sentence = Syntax.Realize(language, Sees(Indefinite(Quantifier.Some, Ontology.Person), Indefinite(Quantifier.Some, Ontology.Thing)), "x");
        var (fromNoun, fromQuestion) = language.Indefinites.PersonBase == IndefiniteBase.GenericNoun
            ? (language.Lexicon.Noun("person"), language.Questions.Words[Interrogative.What])
            : (language.Lexicon.Noun("thing"), language.Questions.Words[Interrogative.Who]);

        Assert.True(IndexOf(sentence, fromNoun.Root) >= 0);
        Assert.True(IndexOf(sentence, fromQuestion.Root) >= 0);
    }

    [Fact]
    public void Realize_EveryIndefiniteInEveryRole_SaysItsWords()
    {
        var relative = new Clause(new Doing("sleep", null), new Declarative(Tense.Present, Aspect.Imperfective, new Gap(null)), false, [], []);
        Assert.All(GeneratedLanguages.Sample.Take(60), l => Assert.All(Indefinites.Series, series =>
        {
            var sentence = Syntax.Realize(l, new Clause(new Doing("see", Indefinite(series, Ontology.Thing)), new Declarative(Tense.Past, Aspect.Perfective, Indefinite(series, Ontology.Person) with { Relative = relative }), false, [new(Role.Location, Indefinite(series, Ontology.Place), Configuration.At)], []), "x");

            Assert.True(sentence.Words.Count >= 4);
        }));
    }

    //whether a clause's verb is negated, by the language's own negation marker
    private static bool Negated(Language language, Statement statement) =>
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
        var dog = new NounPhrase("dog", Number.Singular, Definiteness: Definiteness.Definite);

        //'nobody sees the dog' has nobody before the verb, and 'the dog sees nobody' after it
        Assert.Equal((beforeVerb, afterVerb), (Negated(language, Sees(nobody, dog)), Negated(language, Sees(dog, nobody))));
        Assert.Equal(afterVerb, Negated(language, Sees(dog, new NounPhrase("cat", Number.Singular, Quantifier: Quantifier.No))));
        Assert.False(Negated(language, Sees(Indefinite(Quantifier.Some, Ontology.Person), dog)));
    }

    [Theory]
    [InlineData("Always", true)]
    [InlineData("Never", false)]
    public void Realize_Never_NegatesTheVerbAsNobodyDoes(string negation, bool negated)
    {
        var language = Concording(Enum.Parse<NegativeIndefiniteNegation>(negation));
        var sleeps = new Clause(new Doing("sleep", null), new Declarative(Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular)), false, [new(Role.Time, Indefinite(Quantifier.No, Ontology.Time), Configuration.At)], []);

        Assert.Equal(negated, Negated(language, sleeps));
        Assert.True(IndexOf(Syntax.Realize(language, sleeps, "x"), language.Indefinites.Own[(Quantifier.No, Ontology.Time)].Root) >= 0);
    }

    [Fact]
    public void Realize_InterrogativeBasedTime_IsWhenWithItsSeriesMarker()
    {
        var language = With(IndefinitePronouns.InterrogativeBased);
        var sleeps = new Clause(new Doing("sleep", null), new Declarative(Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular)), false, [new(Role.Time, Indefinite(Quantifier.Every, Ontology.Time), Configuration.At)], []);
        var morphemes = Syntax.Realize(language, sleeps, "x").Words.SelectMany(w => w.Word.Morphemes).ToList();

        Assert.Contains(language.Questions.Words[Interrogative.When].Root, morphemes);
        Assert.Contains(language.Indefinites.Markers[Quantifier.Every], morphemes);
    }

    [Fact]
    public void Realize_NegativeIndefiniteWithAModal_NegatesItOnce()
    {
        var language = Concording(NegativeIndefiniteNegation.Always);
        var swims = new Clause(new Doing("swim", null), new Declarative(Tense.Present, Aspect.Imperfective, Indefinite(Quantifier.No, Ontology.Person)), false, [], []);
        var morphemes = Syntax.Realize(language, new Modalized(swims, Modality.Ability, false), "x").Words.SelectMany(w => w.Word.Morphemes).ToList();

        Assert.Equal(language.Negation.Markers.Count, morphemes.Count(m => language.Negation.Markers.Any(n => n.Marker.Equals(m))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Realize_Quantifier_GoesOutsideTheNumeralOnItsSide(bool before)
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.QuantifiersBeforeNoun, before));
        var dogs = new NounPhrase("dog", Number.Plural, [new("big", false)], Count: 3, Quantifier: Quantifier.All);
        var sentence = Syntax.Realize(language, new Clause(new Doing("sleep", null), new Declarative(Tense.Present, Aspect.Imperfective, dogs), false, [], []), "x");
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
