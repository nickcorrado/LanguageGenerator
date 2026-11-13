using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class PassiveTests
{
    private static Statement Parsed(string gloss) => Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Statement;

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly NounPhrase Fish = new("fish", Number.Singular);
    private static readonly NounPhrase Cat = new("cat", Number.Singular);
    private static PassiveClause EatenBy(Argument? agent) => new("eat", Tense.Past, Aspect.Perfective, Fish, agent, false, []) { Adverbs = [] };

    private static IEnumerable<string> Glosses(Sentence sentence) => sentence.Words.Select(w => w.Gloss);

    [Fact]
    public void SelectAlignment_ManyDraws_MatchWals98A()
    {
        var rand = new Random(1);
        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.SelectAlignment(rand)).ToList();

        //98 neutral, 52 nominative-accusative and 32 ergative-absolutive, of the 182 modeled
        Assert.InRange(drawn.Count(a => a == MorphosyntacticAlignment.None) / 4000.0, 0.51, 0.57);
        Assert.InRange(drawn.Count(a => a == MorphosyntacticAlignment.NominativeAccusative) / 4000.0, 0.26, 0.31);
        Assert.InRange(drawn.Count(a => a == MorphosyntacticAlignment.ErgativeAbsolutive) / 4000.0, 0.16, 0.20);
    }

    [Theory]
    [InlineData(MorphosyntacticAlignment.NominativeAccusative, 0.67, 0.75)]
    [InlineData(MorphosyntacticAlignment.ErgativeAbsolutive, 0.19, 0.25)]
    [InlineData(MorphosyntacticAlignment.None, 0.40, 0.47)]
    public void GeneratePassive_ManyDraws_FollowWals107AForTheAlignment(MorphosyntacticAlignment alignment, double least, double most)
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion != Fusion.Isolating);
        var rand = new Random(1);
        var drawn = Enumerable.Range(0, 4000).Select(_ => Generator.GeneratePassive(rand, alignment, language.MorphologicalProfile, language.Phonology, 0.15)).ToList();

        Assert.InRange(drawn.Count(p => p is not null) / 4000.0, least, most);
        Assert.All(drawn.OfType<Passive>(), p => Assert.True(p is { Position: ExponentPosition.Prefix or ExponentPosition.Suffix, Marker.Value: Affix }));
    }

    [Fact]
    public void Generate_IsolatingLanguages_MarkThePassiveWithAWord() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating && l.Passive is not null),
            l => Assert.True(l.Passive!.Marker.Value is Particle));

    [Fact]
    public void Parse_Passive_HasItsPatientAsSubjectAndAnAgent()
    {
        var clause = Assert.IsType<PassiveClause>(Parsed("carry.PASS.PST  subject:light.DEF  agent:(figure.NDF small)").Value);

        Assert.Equal(new NounPhrase("light", Number.Singular, Definiteness: Definiteness.Definite), clause.Patient.Value);
        Assert.Equal("figure", Assert.IsType<NounPhrase>(clause.Agent!.Value.Value).Noun);
        Assert.Equal(Tense.Past, clause.Tense);
    }

    [Fact]
    public void Parse_PassiveWithoutAnAgent_HasNone() =>
        Assert.Null(Assert.IsType<PassiveClause>(Parsed("take.PASS.PST  subject:child.PL.DEF").Value).Agent);

    [Theory]
    [InlineData("carry  subject:light  agent:figure", "Only a passive verb takes an agent:")]
    [InlineData("carry.PASS  subject:light  object:box", "A passive has no object")]
    [InlineData("carry.PASS.IMP  object:box", "A command can't be passive")]
    [InlineData("be.PASS  subject:light  adjective:big", "be can't be passive")]
    public void Parse_BadPassives_SayWhy(string gloss, string message) =>
        Assert.Contains(message, Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse($"x\n  {gloss}")).Message);

    [Fact]
    public void Realize_Passive_MarksTheVerbAndPutsThePatientInTheIntransitiveSubjectsCase()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Passive is not null), l =>
        {
            var passive = Syntax.Realize(l, EatenBy(null), "x");
            var intransitive = Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective, Fish) { Adverbs = [] }, "x");

            Assert.Contains(passive.Words.SelectMany(w => w.Word.Morphemes), m => m.Value is Affix { Grammemes: [Voice] } or Particle { Grammemes: [Voice] });
            Assert.Equal(intransitive.Words[IndexOf(intransitive, "fish")].Gloss, passive.Words[IndexOf(passive, "fish")].Gloss);
        });
    }

    [Fact]
    public void Realize_PassiveAgent_IsMarkedForItsRole()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Passive is not null), l =>
        {
            var sentence = Syntax.Realize(l, EatenBy(Cat), "x");
            var marked = l.RoleMarkers[Role.Agent].Value switch
            {
                Case c => sentence.Words.Any(w => w.Gloss.Split('-', '.', '[', ']').Contains(c.Gloss)),
                Adposition a => IndexOf(sentence, a.Lexeme.Root) >= 0,
                _ => false,
            };

            Assert.True(marked);
        });
    }

    [Fact]
    public void Realize_NoPassive_SaysTheActive()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Passive is null), l =>
        {
            Assert.Equal(Glosses(Syntax.Realize(l, new Clause("eat", Tense.Past, Aspect.Perfective, Cat, Fish) { Adverbs = [] }, "x")), Glosses(Syntax.Realize(l, EatenBy(Cat), "x")));
            Assert.Equal(Glosses(Syntax.Realize(l, new Clause("eat", Tense.Past, Aspect.Perfective, new Pronoun(Person.Third, Number.Plural), Fish) { Adverbs = [] }, "x")),
                Glosses(Syntax.Realize(l, EatenBy(null), "x")));
        });
    }

    [Fact]
    public void Generate_AgentsCase_IsAlwaysOneItSharesWithAnotherRole()
    {
        //an agent never has a case of its own, so where it has one, another role's region is its own
        Assert.All(GeneratedLanguages.Sample.Where(l => l.RoleMarkers[Role.Agent].Value is Case), l =>
            Assert.Contains(RoleMap.Roles.Where(r => r != Role.Agent), r => l.RoleMarkers[r].Value is Case c && c == (Case)l.RoleMarkers[Role.Agent].Value!));
        Assert.Contains(GeneratedLanguages.Sample, l => l.RoleMarkers[Role.Agent].Value is Adposition { Lexeme.Root.Meaning: "by" });
    }
}
