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

    private static List<IReadOnlyList<RoleRegion>> Draws(int count)
    {
        var rand = new Random(1);
        return [.. Enumerable.Range(0, count).Select(_ => Generator.SelectRegions(rand).Regions)];
    }


    [Fact]
    public void SelectRegions_ManyDraws_AreConnected()
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
