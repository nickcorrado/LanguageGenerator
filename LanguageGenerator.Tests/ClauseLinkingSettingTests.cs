namespace LanguageGenerator.Tests;

//the settings of 'and' and of subordinate clauses: drawn to match, the rules that keep 'with' from
//joining clauses and joined subordinators sharing a marker, and how values are written in
public class ClauseLinkingSettingTests
{
    [Fact]
    public void Generate_CoordinationSettingsFixed_HaveThem()
    {
        var spec = SpecFile.Parse("""{ "and-is-with": true, "clause-and": "OwnWord" }""");

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.IsType<AndWith>(l.Conjunctions.NounAnd.Value);
            Assert.NotNull(l.Conjunctions.ClauseAnd);
        });
    }

    [Fact]
    public void Generate_ClausesSideBySide_SetNounsSideBySideToo()
    {
        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, LanguageSpec.Free.Fix(Settings.ClauseAnd, ClauseAnd.Juxtaposition))), l =>
        {
            Assert.Null(l.Conjunctions.ClauseAnd);
            Assert.True(l.Conjunctions.NounAnd.Value is AndWith or AndJuxtaposed);
        });
    }

    [Fact]
    public void Generate_SubordinationSettingsFixed_HaveThem()
    {
        //in an OV language, where draws give a word at the clause's end and a suffix often enough to
        //match; a VO language nearly always has to have them written in, which the test below checks
        var spec = SpecFile.Parse("""
            {
              "subordinators-when-if": true,
              "if-subordinator": "Final",
              "purpose-subordinator": "Suffix",
              "until-clause-first": true,
              "purpose-balance": "Balanced",
              "temporal-balance": "Deranked"
            }
            """).ObjectFirst(true);

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            var markers = l.Subordination.Markers;
            Assert.Contains(Subordinator.When, l.Subordination.RegionOf(Subordinator.If));
            Assert.Equal(markers[Subordinator.If].Marker.Form, markers[Subordinator.When].Marker.Form);
            Assert.Equal(ExponentPosition.FollowingParticle, markers[Subordinator.When].Position);
            Assert.True(markers[Subordinator.Purpose] is { Position: ExponentPosition.Suffix, Marker.Value: Affix, Balance: Balance.Balanced });
            Assert.NotEqual(Fusion.Isolating, l.MorphologicalProfile.Fusion);
            Assert.True(markers[Subordinator.Until].BeforeMain);
            Assert.All([Subordinator.If, Subordinator.When, Subordinator.Before, Subordinator.After, Subordinator.Until],
                s => Assert.Equal(Balance.Deranked, markers[s].Balance));
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    /*
      the edges between them free, and positions no VO draw gives all at once, so they're written
      in; where 'if', 'when' and 'because' are drawn joined, their region has no position all three
      allow, and is parted rather than left with the last one written
     */
    [Fact]
    public void Generate_JoinedSubordinatorsWithNoPositionInCommon_EachKeepTheirs()
    {
        var spec = SpecFile.Parse("""
            {
              "constituent-order": "VOS",
              "if-subordinator": ["Initial", "Suffix"],
              "when-subordinator": ["Final", "Suffix"],
              "because-subordinator": "Final",
              "before-subordinator": "Suffix"
            }
            """);

        Assert.All(Enumerable.Range(0, 20).Select(seed => Generator.Generate(seed, spec)), l => SpecTests.AssertMeets(l, spec));
    }

    /*
      'when' marked at the clause's end is rare in a VO language, and with the rest no draw meets
      the whole spec, so values are written in. The draw kept must have 'before' and 'until' apart
      already, since parting them would need a form for the second that a write can't coin. Seed 6
      once kept one that had them joined, though it was no closer to the spec than others
     */
    [Fact]
    public void Generate_RareSubordinatorsBesideAnEdgeParted_KeepADrawThatHasItParted()
    {
        var spec = SpecFile.Parse("""
            {
              "constituent-order": "SVO",
              "subordinators-purpose-because": true,
              "subordinators-before-until": false,
              "when-subordinator": "Final",
              "purpose-subordinator": "Initial",
              "when-clause-first": false,
              "purpose-balance": "Balanced",
              "reason-balance": "Either"
            }
            """);

        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, spec)), l => SpecTests.AssertMeets(l, spec));
    }

    [Fact]
    public void Generate_SuffixInAVerbObjectLanguage_IsWrittenIn()
    {
        //only 1 of WALS's 338 VO languages marks its subordinate clauses with a suffix
        var spec = LanguageSpec.Free.ObjectFirst(false).Fix(Settings.SubordinatorOf(Subordinator.Because), ExponentPosition.Suffix);

        Assert.All(Enumerable.Range(0, 5).Select(seed => Generator.Generate(seed, spec)), l =>
            Assert.True(l.Subordination.Markers[Subordinator.Because] is { Position: ExponentPosition.Suffix, Marker.Value: Affix }));
    }

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "and-is-with": true, "clause-and": "SameAsNouns" }""", "Clauses can only be joined like nouns" },
        { """{ "if-subordinator": "Suffix", "fusion": "Isolating" }""", "if-subordinator can only be a suffix" },
        { """{ "subordinators-when-if": true, "if-subordinator": "Initial", "when-subordinator": "Final" }""", "when and if can't be joined" },
        { """{ "subordinators-when-if": true, "subordinators-when-after": true, "if-subordinator": "Initial", "after-subordinator": "Final" }""", "can't be joined" },
    };

    [Theory]
    [MemberData(nameof(Impossible))]
    public void Problems_ContradictorySpec_SaysWhy(string json, string problem)
    {
        var spec = SpecFile.Parse(json);

        Assert.Contains(spec.Problems(), p => p.Contains(problem));
        Assert.Throws<SpecException>(() => Generator.Generate(1, spec));
    }

    [Fact]
    public void Write_JoiningTwoSubordinators_GivesTheSecondTheFirstsMarker()
    {
        var edge = (Subordinator.When, Subordinator.If);
        var drawn = GeneratedLanguages.Sample.Select(l => l.Subordination).First(s => !s.RegionOf(Subordinator.When).Contains(Subordinator.If));

        var written = Settings.SubordinatorEdge(edge.When, edge.If).Write(drawn, [true]);

        Assert.Contains(edge, written.Joined);
        Assert.All(written.RegionOf(Subordinator.If), s =>
        {
            Assert.Equal(drawn.Markers[Subordinator.When].Marker.Form, written.Markers[s].Marker.Form);
            Assert.Equal(drawn.Markers[Subordinator.When].Position, written.Markers[s].Position);
            Assert.Equal(drawn.Markers[s].Balance, written.Markers[s].Balance);
        });
    }

    [Fact]
    public void Write_SubordinatorPosition_MovesItsWholeRegion()
    {
        var drawn = GeneratedLanguages.Sample.Select(l => l.Subordination)
            .First(s => s.RegionOf(Subordinator.When).Count > 1 && s.Markers[Subordinator.When].Position != ExponentPosition.Suffix);

        var written = Settings.SubordinatorOf(Subordinator.When).Write(drawn, [ExponentPosition.Suffix]);

        Assert.All(drawn.RegionOf(Subordinator.When), s =>
        {
            Assert.True(written.Markers[s] is { Position: ExponentPosition.Suffix, Marker.Value: Affix });
            Assert.Equal(drawn.Markers[s].Marker.Form, written.Markers[s].Marker.Form);
            Assert.Equal(drawn.Markers[s].Marker.Gloss, written.Markers[s].Marker.Gloss);
        });
    }

    [Fact]
    public void Write_AndIsntWith_TakesTheClausesWord()
    {
        var drawn = GeneratedLanguages.Sample.Select(l => l.Conjunctions).First(c => c.NounAnd.Value is AndWith && c.ClauseAnd is not null);

        var written = Settings.AndIsWith.Write(drawn, [false]);

        Assert.Equal(drawn.ClauseAnd, Assert.IsType<AndWord>(written.NounAnd.Value).Word);
        Assert.Equal(ClauseAnd.SameAsNouns, Settings.ClauseAnd.Get(written));
    }
}
