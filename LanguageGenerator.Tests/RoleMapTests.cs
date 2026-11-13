namespace LanguageGenerator.Tests;

public class RoleMapTests
{
    [Fact]
    public void Regions_NoEdges_PutsEachRoleInARegionOfItsOwnInRankOrder() =>
        Assert.Equal(RoleMap.Roles, RoleMap.Regions([]).Select(r => Assert.Single(r.Roles)));

    [Fact]
    public void Regions_JoinedThroughAThirdRole_AreOneRegionNamedForItsHighest()
    {
        var region = Assert.Single(RoleMap.Regions([(Role.Goal, Role.Location), (Role.Recipient, Role.Goal)]), r => r.Roles.Count > 1);

        Assert.Equal([Role.Recipient, Role.Location, Role.Goal], region.Roles);
        Assert.Equal(Case.Dative, region.Case);
        Assert.Equal("to", region.Gloss);
    }

    [Fact]
    public void Gloss_Instrument_IsWithOnlyBesideACompanion()
    {
        Assert.Equal("using", RoleMap.Regions([]).Single(r => r.Head == Role.Instrument).Gloss);
        Assert.Equal("with", RoleMap.Regions([(Role.Companion, Role.Instrument)]).Single(r => r.Head == Role.Instrument).Gloss);
    }

    [Fact]
    public void Edges_JoinOnlyObliqueRolesOnTheMap() =>
        Assert.All(RoleMap.Edges, e =>
        {
            Assert.Contains(e.First, RoleMap.Roles);
            Assert.Contains(e.Second, RoleMap.Roles);
            Assert.InRange(e.Weight, 0, 1);
        });

    private static List<List<RoleRegion>> Draws(int count)
    {
        var rand = new Random(1);
        return [.. Enumerable.Range(0, count).Select(_ => Generator.SelectRegions(rand))];
    }

    private static bool Together(List<RoleRegion> regions, Role first, Role second) => regions.Any(r => r.Contains(first) && r.Contains(second));

    [Fact]
    public void SelectRegions_ManyDraws_AreConnectedAndShareMarkersAboutAsOftenAsTheirEdgesSay()
    {
        var drawn = Draws(4000);

        //every region is connected by the map's own edges, so no marker skips a role between two it covers
        Assert.All(drawn.SelectMany(d => d), region =>
        {
            var reached = new HashSet<Role> { region.Head };
            for (var grew = true; grew;)
            {
                grew = false;
                foreach (var (first, second, _) in RoleMap.Edges.Where(e => region.Contains(e.First) && region.Contains(e.Second)))
                {
                    if (reached.Contains(first) && reached.Add(second))
                    {
                        grew = true;
                    }
                    if (reached.Contains(second) && reached.Add(first))
                    {
                        grew = true;
                    }
                }
            }
            Assert.Equal(region.Roles.Count, reached.Count);
        });
        //roles also meet through a third, so a pair shares a marker at least as often as its edge says,
        //and not much more: WALS 52A has 26% of languages with one word for 'with'
        Assert.InRange(drawn.Count(d => Together(d, Role.Companion, Role.Instrument)) / 4000.0, 0.25, 0.32);
        Assert.InRange(drawn.Count(d => Together(d, Role.Recipient, Role.Goal)) / 4000.0, 0.49, 0.60);
        Assert.InRange(drawn.Count(d => Together(d, Role.Goal, Role.Location)) / 4000.0, 0.34, 0.45);
    }

    [Fact]
    public void SelectCases_ManyDraws_MatchWalsCaseCounts()
    {
        var rand = new Random(1);
        var regions = RoleMap.Regions([]);

        int Obliques(MorphosyntacticAlignment alignment) =>
            Generator.SelectCases(rand, alignment, regions, 0).Count(c => RoleMap.Roles.Any(r => RoleMap.CaseFor(r) == c));
        var withCore = Enumerable.Range(0, 4000).Select(_ => Obliques(MorphosyntacticAlignment.NominativeAccusative)).ToList();
        var withoutCore = Enumerable.Range(0, 4000).Select(_ => Obliques(MorphosyntacticAlignment.None)).ToList();

        //WALS 49A: of 137 languages with two or more cases, 23 have only the core two, and 84 four more
        //at least; of all 237, 100 have none
        Assert.InRange(withCore.Count(n => n == 0) / 4000.0, 0.14, 0.20);
        Assert.InRange(withCore.Count(n => n >= 4) / 4000.0, 0.58, 0.65);
        Assert.InRange(withoutCore.Count(n => n == 0) / 4000.0, 0.39, 0.46);
    }

    [Fact]
    public void SelectCases_ManyDraws_FavorTheTopOfBlakesHierarchy()
    {
        var rand = new Random(1);
        var regions = RoleMap.Regions([]);
        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.SelectCases(rand, MorphosyntacticAlignment.NominativeAccusative, regions, 0))
            .Where(c => c.Count == 3).ToList();

        //a language with one oblique case most often has a genitive, and rarely an allative
        Assert.True(drawn.Count(c => c.Contains(Case.Genitive)) > 3 * drawn.Count(c => c.Contains(Case.Allative)));
        Assert.True(drawn.Count(c => c.Contains(Case.Dative)) > drawn.Count(c => c.Contains(Case.Instrumental)));
    }

    [Fact]
    public void SelectCases_NeverMoreThanTheRegions()
    {
        var rand = new Random(1);
        var one = RoleMap.Regions(RoleMap.Edges.Select(e => (e.First, e.Second)));

        Assert.All(Enumerable.Range(0, 500), _ => Assert.True(Generator.SelectCases(rand, MorphosyntacticAlignment.NominativeAccusative, one, 0).Count <= 3));
    }

    [Fact]
    public void Generate_SomeLanguages_HaveAllativesAndComitatives()
    {
        Assert.Contains(GeneratedLanguages.Sample, l => l.Cases.Contains(Case.Allative));
        Assert.Contains(GeneratedLanguages.Sample, l => l.Cases.Contains(Case.Comitative));
    }

    [Fact]
    public void Generate_AnyLanguage_MarksRolesThatShareACaseOnlyWhereTheMapJoinsThem()
    {
        //two roles with one case share a region, so the map has a path between them through roles
        //that share it too
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var byCase = RoleMap.Roles.Where(r => l.RoleMarkers[r] is { Case: not null, Adposition: null }).GroupBy(r => l.RoleMarkers[r].Case!);
            Assert.All(byCase, group => Assert.Equal(RoleMap.CaseFor(group.OrderBy(RoleMap.Rank).First()), group.Key));
        });
    }
}
