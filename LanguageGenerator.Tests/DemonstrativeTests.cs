using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

//demonstratives: how they're written, how languages draw them, and how a noun phrase, a
//demonstrative standing alone, and 'here' and 'there' say them
public class DemonstrativeTests
{
    private static Statement Parsed(string text) => Assert.Single(ClauseFile.Parse(text)).Statement;

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    private static Clause ClauseOf(string gloss) => Assert.IsType<Clause>(Parsed($"x\n  {gloss}").Value);

    [Fact]
    public void Parse_DemonstrativeOnANoun_IsItsDistanceWithNoDefiniteness()
    {
        var village = Assert.IsType<NounPhrase>(ClauseOf("sleep  subject:village.PROX").Subject.Value);
        var trees = Assert.IsType<NounPhrase>(ClauseOf("see  subject:1SG  object:(tree.PL.DIST old)").Object.GetValueOrDefault().Value);

        Assert.Equal((Distance.Proximal, (Definiteness?)null), (village.Demonstrative, village.Definiteness));
        Assert.Equal((Distance.Distal, Number.Plural, "old"), (trees.Demonstrative, trees.Number, trees.Adjectives[0].Meaning));
    }

    [Theory]
    [InlineData("PROX", "Proximal", "Singular")]
    [InlineData("DIST.PL", "Distal", "Plural")]
    [InlineData("MED", "Medial", "Singular")]
    public void Parse_DemonstrativeAlone_StandsForANoun(string written, string distance, string number) =>
        Assert.Equal(new DemonstrativePronoun(ClauseFile.ValuesOf<Distance>().Single(d => d.Name == distance), ClauseFile.ValuesOf<Number>().Single(n => n.Name == number)),
            ClauseOf($"see  subject:1SG  object:{written}").Object.GetValueOrDefault().Value);

    [Theory]
    [InlineData("location:here", "Location", "Proximal")]
    [InlineData("goal:here", "Goal", "Proximal")]
    [InlineData("source:there", "Source", "Distal")]
    [InlineData("location:there.MED", "Location", "Medial")]
    public void Parse_HereAndThere_AreAPlaceInTheirRole(string written, string role, string distance)
    {
        var oblique = Assert.Single(ClauseOf($"walk  subject:3PL  {written}").Obliques);

        Assert.Equal(Enum.Parse<Role>(role), oblique.Role);
        Assert.Equal(new DemonstrativePlace(ClauseFile.ValuesOf<Distance>().Single(d => d.Name == distance)), oblique.Argument.Value);
    }

    [Fact]
    public void Parse_HereWithBe_IsWhereItsSubjectIs() =>
        Assert.Equal(new DemonstrativePlace(Distance.Proximal),
            Assert.IsType<Oblique>(Assert.IsType<CopularClause>(Parsed("x\n  be  subject:1SG  location:here").Value).Complement.Value).Argument.Value);

    [Theory]
    [InlineData("sleep  subject:village.PROX.DEF", "not both")]
    [InlineData("sleep  subject:village.NDF.DIST", "not both")]
    [InlineData("sleep  subject:village.PROX.DIST", "more than one demonstrative")]
    [InlineData("sleep  subject:here", "says a place")]
    [InlineData("walk  subject:1SG  route:here", "not route:here")]
    [InlineData("walk  subject:1SG  location.under:here", "can't take a configuration")]
    [InlineData("walk  subject:1SG  location:here.MED", "only there takes")]
    [InlineData("walk  subject:1SG  when:here", "says a place")]
    [InlineData("see  subject:1SG  object:(PROX big)", "goes on its noun")]
    [InlineData("see  subject:1SG  object:PROX.DEF", "standing alone has only")]
    [InlineData("sleep.IMP  addressee:man.PROX", "no demonstrative")]
    [InlineData("walk  subject:1SG  when:inside", "can't be said yet")]
    public void Parse_BadDemonstratives_SayWhy(string gloss, string message) => Assert.Contains(message, Error($"x\n  {gloss}").Message);

    [Fact]
    public void Nouns_SkipDemonstrativesAndPlaces() =>
        Assert.Equal(["key"], ClauseFile.Nouns(ClauseFile.Parse("x\n  give.PST  subject:PROX  object:key.DIST  source:here")).Select(n => n.Noun));

    private static List<Demonstratives> Draws(Func<LanguageSpec, LanguageSpec> fix)
    {
        var language = GeneratedLanguages.With(fix);
        var rand = new Random(1);
        return [.. Enumerable.Range(0, 4000).Select(_ => Generator.GenerateDemonstratives(rand, language.MorphologicalProfile, language.Genders, language.Phonology))];
    }

    private static Language WithGender(Func<LanguageSpec, LanguageSpec> fix) =>
        GeneratedLanguages.With(s => fix(s.Inflecting().Fix(Settings.Genders, [.. Enum.GetValues<NounGenders>().Where(g => g != NounGenders.None)])));

    [Fact]
    public void GenerateDemonstratives_ManyDraws_MatchTheWalsCounts()
    {
        var drawn = Draws(s => s.Inflecting());
        double Share(Func<Demonstratives, bool> holds) => drawn.Count(holds) / 4000.0;

        //WALS 41A: none in 7 of 234, two in 127, and three or more in 100
        Assert.InRange(Share(d => d.Contrast == DistanceContrast.None), 0.02, 0.045);
        Assert.InRange(Share(d => d.Contrast == DistanceContrast.TwoWay), 0.51, 0.57);
        //88A: 542 of 1,141 before the noun, 562 after it, 9 prefixes and 28 suffixes
        Assert.InRange(Share(d => d.Position == ExponentPosition.PrecedingParticle), 0.45, 0.50);
        Assert.InRange(Share(d => d.Position == ExponentPosition.Suffix), 0.017, 0.033);
        //42A: a stem of its own standing alone in 37 of 201, besides every affix's
        var words = drawn.Where(d => !d.IsAffix).ToList();
        Assert.InRange(words.Count(d => d.Pronouns.OwnStem) / (double)words.Count, 0.16, 0.21);
        Assert.All(drawn.Where(d => d.IsAffix), d => Assert.True(d.Pronouns.OwnStem));
    }

    [Fact]
    public void GenerateDemonstratives_ManyDraws_HaveAFormForEachDistanceAndWhatTheyAgreeIn()
    {
        var language = WithGender(s => s);
        var rand = new Random(1);
        var drawn = Enumerable.Range(0, 1000).Select(_ => Generator.GenerateDemonstratives(rand, language.MorphologicalProfile, language.Genders, language.Phonology)).ToList();

        Assert.All(drawn, d =>
        {
            Assert.Equal(d.Distances.Count * (d.AgreesInGender ? language.Genders.Values.Count : 1) * (d.AgreesInNumber ? 2 : 1), d.Forms.Count);
            Assert.All(d.Distances, distance => Assert.Contains(distance, d.Pronouns.Own.Keys));
            Assert.All(d.Distances, distance => Assert.Contains(distance, d.Places.Own.Keys));
        });
        Assert.InRange(drawn.Count(d => d.AgreesInGender) / 1000.0, 0.8, 0.9);
    }

    [Fact]
    public void GenerateDemonstratives_IsolatingLanguage_UsesNoAffix() =>
        Assert.All(Draws(s => s.Fix(Settings.Fusion, Fusion.Isolating)), d => Assert.False(d.IsAffix));

    [Theory]
    [InlineData("Proximal", "Proximal", "Neutral")]
    [InlineData("Medial", "Distal", "Neutral")]
    [InlineData("Neutral", "Distal", "Neutral")]
    public void Nearest_DistanceTheLanguageLacks_FallsBack(string wanted, string twoWay, string none)
    {
        Distance Named(string name) => ClauseFile.ValuesOf<Distance>().Single(d => d.Name == name);
        var two = GeneratedLanguages.With(s => s.Fix(Settings.DistanceContrast, DistanceContrast.TwoWay)).Demonstratives;
        var zero = GeneratedLanguages.With(s => s.Fix(Settings.DistanceContrast, DistanceContrast.None)).Demonstratives;

        Assert.Equal((Named(twoWay), Named(none)), (two.Nearest(Named(wanted)), zero.Nearest(Named(wanted))));
    }

    //'these three big dogs sleep'
    private static Clause TheseDogsSleep(Distance distance) =>
        new("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Paucal, [new("big", false)], Count: 3, Demonstrative: distance)) { Adverbs = [] };

    private static bool IsDemonstrative(Morpheme m) => m.Value is Affix { Grammemes: [Distance, ..] } or Particle { Grammemes: [Distance, ..] };

    [Theory]
    [InlineData("Before")]
    [InlineData("After")]
    public void Realize_DemonstrativeWord_IsOutsideTheAdjectivesAndNumeral(string side)
    {
        var position = side == "Before" ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle;
        var language = GeneratedLanguages.With(s => s.Fix(Settings.DemonstrativePosition, position));
        var sentence = Syntax.Realize(language, TheseDogsSleep(Distance.Proximal), "x");
        var demonstrative = sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(IsDemonstrative));
        var others = new[] { IndexOf(sentence, "three"), IndexOf(sentence, "big"), IndexOf(sentence, "dog") };

        Assert.True(position == ExponentPosition.PrecedingParticle ? demonstrative < others.Min() : demonstrative > others.Max());
    }

    [Fact]
    public void Realize_DemonstrativeSuffix_GoesOnTheNounOutsideItsNumber()
    {
        var language = WithGender(s => s.Fix(Settings.DemonstrativePosition, ExponentPosition.Suffix).Fix(Settings.NounNumber, NounNumber.SingularPlural));
        var sentence = Syntax.Realize(language, new Clause("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Plural, Demonstrative: Distance.Distal)) { Adverbs = [] }, "x");
        var dog = sentence.Words[IndexOf(sentence, "dog")].Word.Morphemes.ToList();
        var marker = dog.FindIndex(IsDemonstrative);

        Assert.True(marker > dog.FindIndex(m => m.Value is Root));
        Assert.DoesNotContain(dog.Skip(marker + 1), m => m.Value is Affix { Grammemes: [Number] });
    }

    [Fact]
    public void Realize_Demonstrative_AgreesWithItsNoun()
    {
        Assert.All(GeneratedLanguages.Sample.Take(100), l =>
        {
            var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Plural, Demonstrative: Distance.Medial)) { Adverbs = [] }, "x");
            var expected = l.Demonstratives.For(Distance.Medial, l.GenderOf(l.Lexicon.Noun("dog")), Number.Plural);

            Assert.Contains(expected, sentence.Words.SelectMany(w => w.Word.Morphemes));
        });
    }

    private static bool IsDefinite(Morpheme m) => m.Value is Affix { Grammemes: [Definiteness, ..] } or Particle { Grammemes: [Definiteness, ..] };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Realize_DefiniteArticle_StaysBesideADemonstrativeOnlyWhereTheLanguageKeepsIt(bool keeps)
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.DefiniteArticle, DefiniteArticle.Word).Fix(Settings.ArticleWithDemonstrative, keeps));
        var morphemes = Syntax.Realize(language, TheseDogsSleep(Distance.Proximal), "x").Words.SelectMany(w => w.Word.Morphemes).ToList();

        Assert.Contains(morphemes, IsDemonstrative);
        Assert.Equal(keeps, morphemes.Any(IsDefinite));
    }

    [Fact]
    public void Generate_DemonstrativeArticle_HasTheFarDemonstrativesFormsAndPlace()
    {
        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, LanguageSpec.Free.Fix(Settings.DefiniteArticle, DefiniteArticle.Demonstrative))), l =>
        {
            var article = l.Articles.Definite!;
            var demonstratives = l.Demonstratives;
            var genders = l.Genders.Values;

            Assert.True(l.Articles.DefiniteFromDemonstrative);
            Assert.False(l.Articles.WithDemonstrative);
            Assert.Equal(demonstratives.Position, article.Position);
            Assert.All(article.Forms, f => Assert.Equal(
                demonstratives.For(demonstratives.Distances[^1], f.Key.Gender ?? genders.FirstOrDefault(), f.Key.Number ?? Number.Singular).Form, f.Value.Form));
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Realize_DemonstrativeAlone_IsItsPronounInflectedAsANoun(bool ownStem)
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.DemonstrativePosition, ExponentPosition.PrecedingParticle).Fix(Settings.DemonstrativePronounsOwnStem, ownStem));
        var sentence = Syntax.Realize(language, new Clause("eat", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular), new DemonstrativePronoun(Distance.Distal, Number.Singular)) { Adverbs = [] }, "x");
        var pronoun = language.Demonstratives.Pronoun(Distance.Distal);

        Assert.True(IndexOf(sentence, pronoun.Root) >= 0);
        Assert.Equal(!ownStem, pronoun.Root.Form.SequenceEqual(language.Demonstratives.For(Distance.Distal, language.Genders.Values.FirstOrDefault(), Number.Singular).Form));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Realize_Here_IsItsPlaceWordMarkedAsALocation(bool fromDemonstratives)
    {
        var language = GeneratedLanguages.With(s => s.Fix(Settings.PlacesFromDemonstratives, fromDemonstratives));
        Clause SleepsAt(Argument place) => new("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("cat", Number.Plural), Obliques: [new(Role.Location, place, Configuration.At)]) { Adverbs = [] };
        var here = Syntax.Realize(language, SleepsAt(new DemonstrativePlace(Distance.Proximal)), "x");
        var house = Syntax.Realize(language, SleepsAt(new NounPhrase("house", Number.Singular)), "x");
        var place = language.Demonstratives.Place(Distance.Proximal);

        Assert.Equal(fromDemonstratives, place == language.Demonstratives.Pronoun(Distance.Proximal));
        //'here' takes the location's case or adposition as 'at the house' does, so it says as many words
        Assert.Equal(house.Words.Count, here.Words.Count);
        Assert.Equal(house.Words[IndexOf(house, "house")].Gloss.Replace("house", "X"), here.Words[IndexOf(here, place.Root)].Gloss.Replace(place.Root.Meaning, "X"));
    }

    [Fact]
    public void Generate_DemonstrativeSettingsFixed_HaveThem()
    {
        var spec = SpecFile.Parse("""
            {
              "distance-contrast": "ThreeWay",
              "demonstrative-position": "Prefix",
              "demonstrative-gender-agreement": true,
              "here-from-demonstratives": false,
              "definite-article": "Affix",
              "article-with-demonstrative": true
            }
            """);

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            var d = l.Demonstratives;
            Assert.True(d is { Contrast: DistanceContrast.ThreeWay, Position: ExponentPosition.Prefix, AgreesInGender: true, Places.FromDemonstratives: false, Pronouns.OwnStem: true });
            Assert.True(l.Articles.WithDemonstrative);
            Assert.NotEqual(Fusion.Isolating, l.MorphologicalProfile.Fusion);
            Assert.NotEmpty(l.Genders.Values);
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    [Theory]
    [InlineData("""{ "demonstrative-position": "Suffix", "fusion": "Isolating" }""", "A demonstrative can only be an affix")]
    [InlineData("""{ "demonstrative-position": "Prefix", "demonstrative-pronouns-own-stem": false }""", "can't stand alone")]
    [InlineData("""{ "demonstrative-gender-agreement": true, "genders": "None" }""", "A demonstrative can only agree in gender")]
    [InlineData("""{ "definite-article": "Demonstrative", "article-with-demonstrative": true }""", "can stay beside a demonstrative")]
    public void Problems_ContradictoryDemonstratives_SayWhy(string json, string message)
    {
        var spec = SpecFile.Parse(json);

        Assert.Contains(spec.Problems(), p => p.Contains(message));
        Assert.Throws<SpecException>(() => Generator.Generate(1, spec));
    }
}
