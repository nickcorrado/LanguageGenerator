using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class SpatialTests
{
    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly NounPhrase Stone = new("stone", Number.Singular);

    private static Sentence Walk(Language language, Role role, Configuration configuration) =>
        Syntax.Realize(language, new Clause("walk", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular), Obliques: [new(role, Stone, configuration)]) { Adverbs = [] }, "x");

    private static Statement Parsed(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    private static ClauseFormatException Error(string gloss) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}"));

    private static string[] Glosses(Sentence sentence) => string.Join(" ", sentence.Words.Select(w => w.Gloss)).Split('-', '.', ' ', '[', ']');

    [Theory]
    [InlineData("goal.in", Role.Goal, Configuration.In)]
    [InlineData("source.under", Role.Source, Configuration.Under)]
    [InlineData("location.front", Role.Location, Configuration.InFront)]
    [InlineData("route.over", Role.Route, Configuration.Over)]
    [InlineData("route", Role.Route, Configuration.At)]
    public void Parse_SpatialRole_TakesItsConfiguration(string written, Role role, Configuration configuration)
    {
        var clause = Assert.IsType<Clause>(Parsed($"walk.PST  subject:3SG.M  {written}:river.DEF").Value);

        Assert.Equal(new Oblique(role, new NounPhrase("river", Number.Singular, Definiteness: Definiteness.Definite), configuration), Assert.Single(clause.Obliques));
    }

    [Fact]
    public void Parse_BeWithAConfiguration_SaysWhereItsSubjectIs()
    {
        var clause = Assert.IsType<CopularClause>(Parsed("be  subject:key.DEF  location.under:table.DEF").Value);

        Assert.Equal(new Oblique(Role.Location, new NounPhrase("table", Number.Singular, Definiteness: Definiteness.Definite), Configuration.Under), clause.Complement.Value);
    }

    [Theory]
    [InlineData("walk  subject:3SG.M  recipient.in:river", "Only a role saying where")]
    [InlineData("walk  subject.in:3SG.M", "Only a role saying where")]
    [InlineData("walk  subject:3SG.M  location.at:river", "already means at")]
    [InlineData("walk  subject:3SG.M  location.inside:river", "isn't a configuration")]
    [InlineData("walk  subject:3SG.M  goal.in.under:river", "one configuration")]
    [InlineData("walk  subject:3SG.M  goal.in:where", "can't take a configuration")]
    public void Parse_BadConfigurations_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Error(gloss).Message);

    [Fact]
    public void Generate_Configurations_HaveOneMarkerARegionUnlessTheyreSaidLikeAt() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var configurations = l.Configurations;
            Assert.Equal(ConfigurationMap.Configurations.Count, configurations.Regions.Sum(r => r.Configurations.Count));
            Assert.DoesNotContain(Configuration.At, configurations.Markers.Keys);
            Assert.All(configurations.Regions, r =>
            {
                if (r.Contains(Configuration.At))
                {
                    Assert.All(r.Configurations, c => Assert.False(configurations.Markers.ContainsKey(c)));
                }
                else
                {
                    Assert.Single(r.Configurations.Select(c => configurations.Markers[c]).Distinct());
                }
            });
        });

    [Fact]
    public void Generate_AnyStrategy_IsSomeLanguagesAndMostConfigurationsAreMarked()
    {
        var markers = GeneratedLanguages.Sample.SelectMany(l => l.Configurations.Markers.Values.Select(m => m.Value)).ToList();

        Assert.Contains(markers, m => m is SpatialAdposition);
        Assert.Contains(markers, m => m is RelationalNoun);
        Assert.Contains(markers, m => m is SpatialCase);
        //ten configurations besides At, of which only a few are ever said like it
        Assert.InRange(markers.Count / (double)GeneratedLanguages.Sample.Count, 7, 10);
    }

    [Fact]
    public void Generate_SpatialCases_OnlyWhereNounsInflectAndLocationsHaveACase() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var localizations = l.Configurations.Localizations;
            var category = l.Paradigms.Nouns.Categories.FirstOrDefault(c => c.Is<Localization>());
            if (localizations.Count == 0)
            {
                Assert.Null(category);
                return;
            }
            Assert.NotEqual(Fusion.Isolating, l.MorphologicalProfile.Fusion);
            Assert.NotNull(l.RoleMarkers[Role.Location].Case);
            Assert.Null(l.RoleMarkers[Role.Location].Adposition);
            Assert.Equal([Localization.None, .. localizations], Assert.IsType<InflectionalCategory>(category).Values);
        });

    [Theory]
    [InlineData(Role.Location)]
    [InlineData(Role.Goal)]
    [InlineData(Role.Source)]
    [InlineData(Role.Route)]
    public void Realize_Configuration_IsMarkedAsTheLanguageMarksIt(Role role)
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var path = language.RoleMarkers[role];
            foreach (var (configuration, marker) in language.Configurations.Markers)
            {
                var sentence = Walk(language, role, configuration);
                var stone = IndexOf(sentence, "stone");
                Assert.True(stone >= 0);
                switch (marker.Value)
                {
                    case RelationalNoun noun:
                        //the noun is said, and the place is its possessor
                        Assert.True(IndexOf(sentence, noun.Meaning) >= 0, $"{noun.Meaning} is missing.");
                        break;
                    case SpatialAdposition adposition:
                    {
                        var at = IndexOf(sentence, adposition.Adposition.Lexeme.Root);
                        Assert.True(adposition.Adposition.Type == AdpositionType.Preposition ? at < stone : at > stone);
                        //a role's own adposition goes outside it, but a location's 'at' gives way to it
                        if (path.Adposition is { } own && own.Lexeme != adposition.Adposition.Lexeme)
                        {
                            var outside = IndexOf(sentence, own.Lexeme.Root);
                            if (role == Role.Location)
                            {
                                Assert.Equal(-1, outside);
                            }
                            else
                            {
                                Assert.True(own.Type == AdpositionType.Preposition ? outside < at : outside > at);
                            }
                        }
                        break;
                    }
                    case SpatialCase spatial:
                        Assert.Contains(spatial.Localization.Gloss, Glosses(sentence));
                        break;
                }
            }
        }
    }

    [Fact]
    public void Realize_ConfigurationSaidLikeAt_IsThePlainRole() =>
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var plain = Walk(l, Role.Location, Configuration.At);
            foreach (var configuration in ConfigurationMap.Configurations.Where(c => !l.Configurations.Markers.ContainsKey(c)))
            {
                Assert.Equal(plain.Text, Walk(l, Role.Location, configuration).Text);
            }
        });

    [Fact]
    public void Realize_PronounWithASpatialCase_KeepsItsLocalization()
    {
        var languages = GeneratedLanguages.Sample.Where(l => l.Configurations.Localizations.Count > 0).ToList();
        Assert.NotEmpty(languages);
        Assert.All(languages, l =>
        {
            var (configuration, marker) = l.Configurations.Markers.First(m => m.Value.Value is SpatialCase);
            var sentence = Syntax.Realize(l, new Clause("walk", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular),
                Obliques: [new(Role.Location, new Pronoun(Person.Third, Number.Singular), configuration)]) { Adverbs = [] }, "x");

            Assert.Contains(((SpatialCase)marker.Value!).Localization.Gloss, Glosses(sentence));
        });
    }

    [Fact]
    public void Realize_NonSpatialRoleWithAConfiguration_Throws() =>
        Assert.Throws<ArgumentException>(() => Walk(GeneratedLanguages.Sample[0], Role.Companion, Configuration.In));

    [Fact]
    public void Generate_Adpositions_GovernACaseWhereTheLanguageHasCoreCases() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphosyntacticAlignment != MorphosyntacticAlignment.None), l =>
            Assert.All(l.RoleMarkers.Values.Where(m => m.Adposition is not null), m => Assert.NotNull(m.Case)));
}
