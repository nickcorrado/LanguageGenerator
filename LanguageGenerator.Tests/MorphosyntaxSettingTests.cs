namespace LanguageGenerator.Tests;

//the settings of negation, questions, the passive, possession, genders and comparison: their rules
//against isolating morphology, and how a value is written in where no draw gave it
public class MorphosyntaxSettingTests
{
    [Fact]
    public void Generate_AffixFixed_DrawsMorphologyThatTakesAffixes()
    {
        var spec = LanguageSpec.Free.Fix(Settings.Negation, NegationStrategy.Prefix).Fix(Settings.PolarQuestions, PolarQuestion.Suffix);

        Assert.All(Enumerable.Range(0, 30).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.NotEqual(Fusion.Isolating, l.MorphologicalProfile.Fusion);
            Assert.Equal(NegationStrategy.Prefix, l.Negation.Strategy);
            Assert.IsType<QuestionAffix>(l.Questions.Polar.Value);
            RelatedStageTests.AssertKeepsTheRules(l);
        });
    }

    public static TheoryData<string, string> Impossible => new()
    {
        { """{ "negation": "Suffix", "fusion": "Isolating" }""", "Negation can only be a prefix or suffix" },
        { """{ "polar-questions": "Prefix", "fusion": "Isolating" }""", "Polar questions can only be marked by a prefix or suffix" },
        { """{ "possession": "Head", "fusion": "Isolating" }""", "Possession can only be marked on the possessed noun" },
        { """{ "comparative": "Affix", "fusion": "Isolating" }""", "A comparative can only be an affix" },
        { """{ "similative": "Affix", "fusion": "Isolating" }""", "A similative can only be an affix" },
        { """{ "possessive-affixes": "Prefix", "possession": "Dependent" }""", "Possessive affixes can only be prefixes" },
        { """{ "ability-modal": "Affix", "fusion": "Isolating" }""", "ability-modal can only be an affix" },
        { """{ "modalities-ability-permission": true, "ability-modal": "Verb", "permission-modal": "Particle" }""", "ability and permission can't be joined" },
    };

    [Fact]
    public void Generate_ModalitiesMarkedDifferently_AreNeverJoined()
    {
        //'can' and 'may' share a marker 60% of the time, but not when they're marked differently
        var spec = LanguageSpec.Free.Fix(Settings.ModalOf(Modality.Ability), ModalKind.Particle).Fix(Settings.ModalOf(Modality.Permission), ModalKind.Verb);

        Assert.All(Enumerable.Range(0, 20).Select(seed => Generator.Generate(seed, spec)), l =>
        {
            Assert.DoesNotContain((Modality.Ability, Modality.Permission), l.Modals.Joined);
            Assert.IsType<Free>(l.Modals.Markers[Modality.Ability].Value);
            Assert.IsType<ModalVerb>(l.Modals.Markers[Modality.Permission].Value);
        });
    }

    [Fact]
    public void Generate_ModalitiesJoined_ShareTheKindFixedForOne()
    {
        var spec = LanguageSpec.Free.Fix(Settings.ModalityEdge(Modality.Ability, Modality.Permission), true).Fix(Settings.ModalOf(Modality.Ability), ModalKind.Particle);

        Assert.All(Enumerable.Range(0, 20).Select(seed => Generator.Generate(seed, spec)), l =>
            Assert.IsType<Free>(l.Modals.Markers[Modality.Permission].Value));
    }

    [Fact]
    public void Write_ModalParticleOverAVerb_KeepsItsFormForTheWholeRegion()
    {
        var drawn = GeneratedLanguages.Sample.Select(l => l.Modals)
            .First(m => m.Joined.Contains((Modality.Ability, Modality.Permission)) && m.Markers[Modality.Ability].Value is ModalVerb);
        var form = ((ModalVerb)drawn.Markers[Modality.Ability].Value).Verb.Root.Form;

        var written = Settings.ModalOf(Modality.Ability).Write(drawn, [ModalKind.Particle]);

        Assert.All([Modality.Ability, Modality.Permission], m => Assert.Equal(form, Assert.IsType<Free>(written.Markers[m].Value).Particle.Form));
    }

    [Theory]
    [MemberData(nameof(Impossible))]
    public void Problems_ContradictorySpec_SaysWhy(string json, string problem)
    {
        var spec = SpecFile.Parse(json);

        Assert.Contains(spec.Problems(), p => p.Contains(problem));
        Assert.Throws<SpecException>(() => Generator.Generate(1, spec));
    }

    [Fact]
    public void Write_DoubleNegationOverOneParticle_UsesItsFormTwice()
    {
        var drawn = GeneratedLanguages.Sample.Select(l => l.Negation).First(n => n.Strategy == NegationStrategy.ParticleBefore);

        var written = Settings.Negation.Write(drawn, [NegationStrategy.Double]);

        Assert.Equal(NegationStrategy.Double, written.Strategy);
        Assert.Equal([ExponentPosition.PrecedingParticle, ExponentPosition.FollowingParticle], written.Markers.Select(m => m.Position));
        Assert.All(written.Markers, m => Assert.Equal(drawn.Markers[0].Marker.Form, m.Marker.Form));
    }

    [Fact]
    public void Write_ThreeGendersOverTwo_ReusesTheMarkersTheyHad()
    {
        var drawn = GeneratedLanguages.Sample.Select(l => l.Genders).First(g => g.Genders == NounGenders.MasculineFeminine && g.Markers is not null);

        var written = Settings.Genders.Write(drawn, [NounGenders.MasculineFeminineNeuter]);

        Assert.Equal([Gender.Masculine, Gender.Feminine, Gender.Neuter], written.Markers!.Keys);
        Assert.Empty(written.Markers[Gender.Masculine].Form);
        Assert.All([Gender.Feminine, Gender.Neuter], g => Assert.Equal(drawn.Markers![Gender.Feminine].Form, written.Markers[g].Form));
    }

    [Fact]
    public void Write_ValueNeedingAFormWhereNoneWasDrawn_Throws()
    {
        //no draw is short of a passive for long, so writing one where none was drawn never happens
        Assert.Throws<NoFormException>(() => Settings.HasPassive.Write(null, [true]));
    }
}
