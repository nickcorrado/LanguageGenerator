using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class PassiveTests
{
    private static Statement Parsed(string gloss) => Stated(Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Utterance);

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly NounPhrase Fish = BareNoun("fish", Number.Singular);
    private static readonly NounPhrase Cat = BareNoun("cat", Number.Singular);
    private static Clause EatenBy(Argument? agent) => new Clause(new Done("eat", agent), new Declarative(Tense.Past, Aspect.Perfective, Fish), false, [], []);

    private static IEnumerable<string> Glosses(Sentence sentence) => sentence.Words.Select(w => w.Gloss);

    [Theory]
    [InlineData(MorphosyntacticAlignment.NominativeAccusative)]
    [InlineData(MorphosyntacticAlignment.ErgativeAbsolutive)]
    [InlineData(MorphosyntacticAlignment.None)]
    public void GeneratePassive_AnyAlignment_IsAnAffix(MorphosyntacticAlignment alignment)
    {
        var language = GeneratedLanguages.With(s => s.Inflecting());
        var rand = new Random(1);
        var passives = Enumerable.Range(0, 1000).Select(_ => Generator.GeneratePassive(rand, alignment, language.MorphologicalProfile, language.Phonology)).OfType<Passive>().ToList();

        Assert.NotEmpty(passives);
        Assert.All(passives, p => Assert.True(p is { Exponent.Value: Bound }));
    }

    [Fact]
    public void Generate_IsolatingLanguages_MarkThePassiveWithAWord() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating && l.Passive is not null),
            l => Assert.True(l.Passive!.Exponent.Value is Free));

    [Fact]
    public void Parse_Passive_HasItsPatientAsSubjectAndAnAgent()
    {
        var clause = Parsed("carry.PASS.PST  subject:light.DEF  agent:(figure.NDF small)");

        Assert.Equal(new NounPhrase(new Noun("light", Number.Singular, [], null, null, null, Definiteness.Definite, null), null), ForceOf<Declarative>(clause).Subject.Value);
        Assert.Equal("figure", NounOf(PredicateOf<Done>(clause).Agent!.Value).Meaning);
        Assert.Equal(Tense.Past, ForceOf<Declarative>(clause).Tense);
    }

    [Fact]
    public void Parse_PassiveWithoutAnAgent_HasNone() =>
        Assert.Null(PredicateOf<Done>(Parsed("take.PASS.PST  subject:child.PL.DEF")).Agent);

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
            var intransitive = Syntax.Realize(l, new Clause(new Doing("sleep", null), new Declarative(Tense.Past, Aspect.Perfective, Fish), false, [], []), "x");

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
            var marked = l.RoleMarkers[Role.Agent] switch
            {
                { Adposition: { } a } => IndexOf(sentence, a.Lexeme.Root) >= 0,
                { Case: { } c } => sentence.Words.Any(w => w.Gloss.Split('-', '.', '[', ']').Contains(c.Gloss)),
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
            Assert.Equal(Glosses(Syntax.Realize(l, new Clause(new Doing("eat", Fish), new Declarative(Tense.Past, Aspect.Perfective, Cat), false, [], []), "x")), Glosses(Syntax.Realize(l, EatenBy(Cat), "x")));
            Assert.Equal(Glosses(Syntax.Realize(l, new Clause(new Doing("eat", Fish), new Declarative(Tense.Past, Aspect.Perfective, new Pronoun(Person.Third, Number.Plural)), false, [], []), "x")),
                Glosses(Syntax.Realize(l, EatenBy(null), "x")));
        });
    }

    [Fact]
    public void Generate_AgentsCase_IsAlwaysOneItSharesWithAnotherRole()
    {
        //an agent never has a case of its own, so where it has one, another role's region is its own
        Assert.All(GeneratedLanguages.Sample.Where(l => l.RoleMarkers[Role.Agent].Adposition is null && l.RoleMarkers[Role.Agent].Case is not null), l =>
            Assert.Contains(RoleMap.Roles.Where(r => r != Role.Agent), r => l.RoleMarkers[r] == l.RoleMarkers[Role.Agent]));
        Assert.Contains(GeneratedLanguages.Sample, l => l.RoleMarkers[Role.Agent].Adposition is { Lexeme.Root.Meaning: "by" });
    }
}
