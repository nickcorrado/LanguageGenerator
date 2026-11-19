namespace LanguageGenerator.Tests;

//the stages drawn together, from word order to how configurations are marked, and the rules that
//keep what a spec writes in from contradicting what was drawn before it
public class RelatedStageTests
{
    //every rule's conditions hold of the language, as the spec locking all its settings implies them
    internal static void AssertKeepsTheRules(Language language)
    {
        var implied = SpecRules.Close(LanguageSpec.Lock(language, Settings.All));
        Assert.Empty(implied.Problems);
        Assert.All(implied.Conditions, c => Assert.True(c.HoldsIn(language), c.Says));
    }

    private static void AssertMeets(Language language, LanguageSpec spec) =>
        Assert.All(spec.Fixed, kv => Assert.Contains(kv.Value, allowed => allowed.Equals(kv.Key.Read(language))));

    [Fact]
    public void Lock_FreeLanguages_KeepEveryRule()
    {
        Assert.All(GeneratedLanguages.Sample, AssertKeepsTheRules);
    }

    [Fact]
    public void Generate_SpatialCaseFixed_DrawsMorphologyThatInflects()
    {
        //drawn together, the morphology is drawn given the case, rather than left isolating and the
        //case written in
        var spec = LanguageSpec.Free.Fix(Settings.MarkerOf(Configuration.In), ConfigurationStrategy.Case);
        var languages = Enumerable.Range(0, 60).Select(seed => Generator.Generate(seed, spec)).ToList();

        Assert.Contains(GeneratedLanguages.Sample, l => l.MorphologicalProfile.Fusion == Fusion.Isolating);
        Assert.All(languages, l =>
        {
            Assert.NotEqual(Fusion.Isolating, l.MorphologicalProfile.Fusion);
            Assert.Contains(Localization.In, l.Configurations.Localizations);
            AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Generate_IsolatingFixed_MakesVerbBeforeObjectLikelier()
    {
        //isolating languages are mostly VO, so drawing word order with the morphology makes VO
        //likelier than it is in languages at large
        static double VerbFirst(IEnumerable<Language> languages) =>
            languages.Average(l => WordOrder.ObjectPrecedesVerb(l.WordOrder.ConstituentWordOrder) ? 0.0 : 1.0);
        var spec = LanguageSpec.Free.Fix(Settings.Fusion, Fusion.Isolating);

        Assert.True(VerbFirst(Enumerable.Range(0, 150).Select(seed => Generator.Generate(seed, spec))) > VerbFirst(GeneratedLanguages.Sample) + 0.15);
    }

    [Fact]
    public void Generate_AlignmentFixed_KeepsTheSeedsRoleRegions()
    {
        //role regions come after alignment, the only stage with a setting fixed, so they're drawn as
        //the seed drew them
        var spec = LanguageSpec.Free.Fix(Settings.Alignment, MorphosyntacticAlignment.ErgativeAbsolutive);
        var changed = Enumerable.Range(0, 30).Select(seed => (Free: Generator.Generate(seed, LanguageSpec.Free), Fixed: Generator.Generate(seed, spec)))
            .Where(p => p.Free.MorphosyntacticAlignment != MorphosyntacticAlignment.ErgativeAbsolutive).ToList();

        Assert.NotEmpty(changed);
        Assert.All(changed, p => Assert.Equal(p.Free.RoleRegions.Joined, p.Fixed.RoleRegions.Joined));
    }

    [Fact]
    public void Generate_RoleEdgeFixed_KeepsTheSeedsOtherEdges()
    {
        var edge = Settings.RoleEdge(Role.Recipient, Role.Goal);
        foreach (var seed in Enumerable.Range(0, 20))
        {
            var free = Generator.Generate(seed, LanguageSpec.Free);
            var flipped = Generator.Generate(seed, LanguageSpec.Free.Fix(edge, !edge.Get(free.RoleRegions)));

            Assert.NotEqual(edge.Get(free.RoleRegions), edge.Get(flipped.RoleRegions));
            Assert.Equal(free.RoleRegions.Joined.Where(e => e != (Role.Recipient, Role.Goal)), flipped.RoleRegions.Joined.Where(e => e != (Role.Recipient, Role.Goal)));
        }
    }

    [Fact]
    public void Generate_CaseFixed_HasARegionHeadedByItsRole()
    {
        var spec = LanguageSpec.Free.Fix(Settings.HasCase(Case.Dative), true);

        Assert.All(Enumerable.Range(0, 30).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.Contains(Case.Dative, l.Cases);
            Assert.Equal(Role.Recipient, l.RoleRegions.Regions.First(r => r.Contains(Role.Recipient)).Head);
        });
    }

    [Fact]
    public void Generate_ManyRelatedSettingsFixed_MeetsThemAllAndKeepsTheRules()
    {
        //every case, every role apart, and spatial cases for in, on and under in a language without
        //core cases: too rare together for any attempt to draw, so it falls back to drawing the
        //stages one at a time, and writes in what none of those draws gives either
        var spec = Settings.CaseSettings.Aggregate(LanguageSpec.Free, (s, setting) => s.Fix(setting, true));
        spec = Settings.RoleEdges.Aggregate(spec, (s, setting) => s.Fix(setting, false))
            .Fix(Settings.Alignment, MorphosyntacticAlignment.None)
            .Fix(Settings.MarkerOf(Configuration.In), ConfigurationStrategy.Case)
            .Fix(Settings.MarkerOf(Configuration.On), ConfigurationStrategy.Case)
            .Fix(Settings.MarkerOf(Configuration.Under), ConfigurationStrategy.Case)
            .Fix(Settings.MarkerOf(Configuration.Behind), ConfigurationStrategy.RelationalNoun)
            .Fix(Settings.ConfigurationMarking, ConfigurationStrategy.Adposition);

        Assert.Empty(spec.Problems());
        foreach (var seed in new[] { 3, 4 })
        {
            var language = Generator.Generate(seed, spec);
            AssertMeets(language, spec);
            AssertKeepsTheRules(language);
        }
    }

    [Fact]
    public void Generate_MarkerFixedToEitherOfTwo_HasOneItCanHave()
    {
        //on said like at, or by a case; with isolating morphology it can't be a case, so it has to
        //be joined to at
        var spec = LanguageSpec.Free
            .Fix(Settings.MarkerOf(Configuration.On), ConfigurationStrategy.At, ConfigurationStrategy.Case)
            .Fix(Settings.Fusion, Fusion.Isolating);

        Assert.All(Enumerable.Range(0, 20).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.Equal(ConfigurationStrategy.At, l.Configurations.Plan.StrategyFor(Configuration.On));
            AssertKeepsTheRules(l);
        });
    }

    [Fact]
    public void Generate_CaseImpliedByTheMap_DrawsWhatACaseNeeds()
    {
        //against reaches at only through on, which has a marker of its own, so of a case or at it can
        //only be a case, though the spec never says so; the rules work that out, so the morphology
        //and cases are drawn to allow it, even where the stages fall back to being drawn one by one
        var spec = SpecFile.Parse("""{ "on-marker": "RelationalNoun", "against-marker": ["Case", "At"], "ablative-case": false, "genitive-case": [false, true] }""");

        Assert.All(new[] { 1, 2, 258130253 }.Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.Equal(ConfigurationStrategy.Case, l.Configurations.Plan.StrategyFor(Configuration.Against));
            AssertKeepsTheRules(l);
        });
    }

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "dative-case": true, "roles-possessor-recipient": true }""", "dative case needs the recipient to head its region" },
        { """{ "in-marker": "Case", "fusion": "Isolating" }""", "in-marker can only be Case where nouns inflect" },
        { """{ "configuration-marking": "Case", "fusion": "Isolating" }""", "configuration-marking can only be Case where nouns inflect" },
        { """{ "in-marker": "At", "configurations-at-in": false, "configurations-in-on": false }""", "in is said like at" },
        { """{ "on-marker": "Adposition", "configurations-at-on": true }""", "on has a marker of its own" },
        { """{ "in-marker": "Adposition", "on-marker": "RelationalNoun", "configurations-in-on": true }""", "share its marker" },
        { """{ "in-marker": "Case", "genitive-case": false, "dative-case": false, "locative-case": false }""", "case on the location's region" },
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
    public void Parse_AtForAConfigurationTheMapNeverJoinsToAt_SaysWhatItCanBe()
    {
        //nothing on the map joins under to anything, so it always has a marker of its own
        var e = Assert.Throws<SpecException>(() => SpecFile.Parse("""{ "under-marker": "At" }"""));

        Assert.Contains("Adposition, RelationalNoun, Case", e.Message);
    }
}
