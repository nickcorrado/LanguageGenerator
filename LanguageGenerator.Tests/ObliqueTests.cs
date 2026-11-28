using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class ObliqueTests
{
    //only the basic meanings, since coining new ones would add to the shared sample's lexicons

    private static Rendering Walk(Language language, params Oblique[] obliques) =>
        Syntax.Realize(language, Declared(new Event(new Doing("walk", null), BareNoun("dog", Number.Singular), [.. obliques], []), Tense.Past, Aspect.Perfective, false), "x");

    private static Oblique Stone(Role role) => new(role, BareNoun("stone", Number.Singular), Configuration.At);

    [Fact]
    public void GenerateAdpositions_ManyDraws_GiveEachRegionWithoutACaseOneWord()
    {
        var phonology = GeneratedLanguages.Sample[0].Phonology;
        var rand = new Random(1);

        Assert.All(Enumerable.Range(0, 500), _ =>
        {
            var regions = Generator.SelectRegions(rand).Regions;
            var cases = Generator.SelectCases(rand, MorphosyntacticAlignment.NominativeAccusative, regions, 0);
            var words = Generator.GenerateAdpositions(rand, regions, cases, phonology);

            Assert.All(regions, r =>
            {
                if (r.Case is { } @case && cases.Contains(@case))
                {
                    Assert.All(r.Roles, role => Assert.False(words.ContainsKey(role)));
                }
                else
                {
                    Assert.Single(r.Roles.Select(role => words[role]).Distinct());
                    Assert.Equal(r.Gloss, words[r.Head].Root.Meaning);
                }
            });
            Assert.All(words.Values, w => Assert.Equal(1, w.Root.Form.Count(p => p is Vowel)));
            Assert.All(words.Values, w => Assert.Equal(PartOfSpeech.Adposition, w.PartOfSpeech));
        });
    }

    [Fact]
    public void GenerateAdpositions_EachLanguage_RarelyMakesHomophones()
    {
        var apart = RoleMap.Regions([]);
        var homophonous = GeneratedLanguages.Sample.Count(l =>
        {
            var words = Generator.GenerateAdpositions(new Random(1), apart, [], l.Phonology).Values.Distinct().ToList();
            return words.Select(w => string.Concat(w.Root.Form.Select(p => p.IpaSymbol))).Distinct().Count() < words.Count;
        });

        //adpositions are kept from sounding alike, so homophones should turn up in under 1% of languages,
        //failing once in a million at exactly 1%. None of 700 have any now
        Assert.True(homophonous <= Binomial.Most(GeneratedLanguages.Sample.Count, 0.01, 1e-6), $"{homophonous} languages have adpositions that sound alike");
    }

    [Fact]
    public void SelectObliquePosition_VerbBeforeObject_NeverPutsObliquesBetweenThem()
    {
        var rand = new Random(1);

        //WALS 84A has no VXO languages
        Assert.DoesNotContain(ObliquePosition.BetweenObjectAndVerb, Enumerable.Range(0, 1000).Select(_ => Generator.SelectObliquePosition(rand, ConstituentWordOrder.SVO)));
    }

    [Fact]
    public void Generate_AnyLanguage_HasPrepositionsOrPostpositionsByItsAdpositionalPhrases()
    {
        Assert.All(GeneratedLanguages.Sample, l => Assert.All(l.RoleMarkers.Values.Select(m => m.Adposition).OfType<Adposition>(), a =>
            Assert.Equal(l.WordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial ? AdpositionType.Preposition : AdpositionType.Postposition, a.Type)));
    }

    [Fact]
    public void Realize_Oblique_TakesItsRolesCaseOrAdposition()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            foreach (var role in Oblique.Roles.Where(r => r != Role.Similative))
            {
                var sentence = Walk(language, Stone(role));
                var stone = IndexOf(sentence, "stone");
                switch (language.RoleMarkers[role])
                {
                    case { Case: { } c, Adposition: null }:
                        Assert.Contains(sentence.Words, w => w.Gloss.Split('-', '.', '[', ']').Contains(c.Gloss));
                        break;
                    case { Adposition: { } a }:
                        //the adposition is at the phrase's edge, with nothing of the clause's between it and the noun
                        var at = sentence.Words.ToList().FindIndex(w => w.Word.Root == a.Lexeme.Root);
                        Assert.Equal(a.Gloss, sentence.Words[at].Gloss);
                        Assert.True(a.Type == AdpositionType.Preposition ? at < stone : at > stone);
                        var between = sentence.Words.Skip(Math.Min(at, stone) + 1).Take(Math.Abs(at - stone) - 1);
                        Assert.DoesNotContain(between, w => w.Word.Root is not null);
                        break;
                    //a duration marked like the object, in a language that doesn't mark objects
                    case { Case: null, Adposition: null } when role == Role.Duration:
                        break;
                    default:
                        Assert.Fail($"{role} has no marker.");
                        break;
                }
            }
        }
    }

    [Fact]
    public void Realize_Obliques_GoWhereTheLanguagePutsThem()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var order = language.WordOrder;
            var transitive = Syntax.Realize(language, Declared(new Event(new Doing("see", BareNoun("cat", Number.Singular)), BareNoun("dog", Number.Singular), [Stone(Role.Location)], []), Tense.Past, Aspect.Perfective, false), "x");
            var (s, v, o, x) = (IndexOf(transitive, "dog"), IndexOf(transitive, "see"), IndexOf(transitive, "cat"), IndexOf(transitive, "stone"));

            //the core constituents keep their order
            var core = new[] { ('S', s), ('V', v), ('O', o) };
            Assert.Equal(order.ConstituentWordOrder.ToString(), string.Concat(core.OrderBy(p => p.Item2).Select(p => p.Item1)));
            switch (order.ObliquePosition)
            {
                case ObliquePosition.BeforeVerbAndObject:
                    Assert.True(x < v && x < o);
                    break;
                case ObliquePosition.BetweenObjectAndVerb:
                    Assert.True(o < x && x < v);
                    break;
                case ObliquePosition.AfterVerbAndObject:
                    Assert.True(x > v && x > o);
                    break;
            }

            var intransitive = Walk(language, Stone(Role.Location));
            Assert.Equal(order.ObliquePosition == ObliquePosition.AfterVerbAndObject, IndexOf(intransitive, "stone") > IndexOf(intransitive, "walk"));
        }
    }

    [Fact]
    public void Realize_SeveralObliques_StayTogetherInTheOrderGiven()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Walk(l, new Oblique(Role.Source, BareNoun("tree", Number.Singular), Configuration.At), new Oblique(Role.Goal, BareNoun("water", Number.Singular), Configuration.At));
            var (tree, water) = (IndexOf(sentence, "tree"), IndexOf(sentence, "water"));

            Assert.True(tree < water);
            Assert.DoesNotContain(new[] { IndexOf(sentence, "dog"), IndexOf(sentence, "walk") }, i => i > tree && i < water);
        });
    }

    [Fact]
    public void Realize_PronounOblique_IsNeverDropped()
    {
        var dropping = GeneratedLanguages.Sample.Where(l => l.Pronouns.DropsSubjects).ToList();
        var you = new Pronoun(Person.Second, Number.Singular);

        Assert.NotEmpty(dropping);
        Assert.All(dropping, l =>
        {
            var root = l.Pronouns.For(you).Root;
            Assert.Contains(Walk(l, new Oblique(Role.Companion, you, Configuration.At)).Words, w => w.Word.Root == root);
        });
    }

    [Theory]
    [InlineData(Role.DirectObject)]
    [InlineData(Role.Possessor)]
    [InlineData(Role.Addressee)]
    public void Realize_NonObliqueRole_Throws(Role role)
    {
        Assert.Throws<ArgumentException>(() => Walk(GeneratedLanguages.Sample[0], Stone(role)));
    }
}
