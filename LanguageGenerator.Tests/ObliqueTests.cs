namespace LanguageGenerator.Tests;

public class ObliqueTests
{
    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static int IndexOf(Sentence sentence, string meaning) =>
        sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Root r && r.Meaning == meaning));

    private static Sentence Walk(Language language, params Oblique[] obliques) =>
        Syntax.Realize(language, new Clause("walk", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular), Obliques: obliques), "x");

    private static Oblique Stone(CaseUse role) => new(role, new NounPhrase("stone", Number.Singular));

    [Fact]
    public void GenerateAdpositions_ManyDraws_AreOneSyllableAndShareWordsAsWalsSays()
    {
        var phonology = GeneratedLanguages.Sample[0].Phonology;
        var rand = new Random(1);

        var drawn = Enumerable.Range(0, 3000).Select(_ => Generator.GenerateAdpositions(rand, phonology, 0.15)).ToList();

        //WALS 52A: 76 of 289 languages modeled use one word for 'with a friend' and 'with a knife'
        Assert.InRange(drawn.Count(a => a[CaseUse.Instrument] == a[CaseUse.Accompaniment]) / 3000.0, 0.23, 0.30);
        Assert.All(drawn, a =>
        {
            Assert.Same(a[CaseUse.IndirectObject], a[CaseUse.MovementTo]);
            Assert.All(a.Values, w => Assert.Equal(1, w.Root.Form.Count(p => p is Vowel)));
            Assert.All(a.Values, w => Assert.Equal(PartOfSpeech.Adposition, w.PartOfSpeech));
        });
    }

    [Fact]
    public void GenerateAdpositions_EachLanguage_RarelyMakesHomophones()
    {
        var homophonous = GeneratedLanguages.Sample.Count(l =>
        {
            var words = Generator.GenerateAdpositions(new Random(1), l.Phonology, 0.15).Values.Distinct().ToList();
            return words.Select(w => string.Concat(w.Root.Form.Select(p => p.IpaSymbol))).Distinct().Count() < words.Count;
        });

        Assert.InRange(homophonous / (double)GeneratedLanguages.Sample.Count, 0, 0.05);
    }

    [Fact]
    public void SelectObliquePosition_ManyDraws_MatchTheWalsCounts()
    {
        var rand = new Random(1);
        var vo = Enumerable.Range(0, 5000).Select(_ => Generator.SelectObliquePosition(rand, ConstituentWordOrder.SVO)).ToList();
        var ov = Enumerable.Range(0, 5000).Select(_ => Generator.SelectObliquePosition(rand, ConstituentWordOrder.SOV)).ToList();

        //WALS 84A: VOX 210 and XVO 3; XOV 48, OXV 45 and OVX 27
        Assert.InRange(vo.Count(p => p == ObliquePosition.AfterVerbAndObject) / 5000.0, 0.97, 1);
        Assert.DoesNotContain(ObliquePosition.BetweenObjectAndVerb, vo);
        Assert.InRange(ov.Count(p => p == ObliquePosition.BeforeVerbAndObject) / 5000.0, 0.37, 0.43);
        Assert.InRange(ov.Count(p => p == ObliquePosition.BetweenObjectAndVerb) / 5000.0, 0.35, 0.40);
        Assert.InRange(ov.Count(p => p == ObliquePosition.AfterVerbAndObject) / 5000.0, 0.20, 0.25);
    }

    [Fact]
    public void Generate_AnyLanguage_HasPrepositionsOrPostpositionsByItsAdpositionalPhrases()
    {
        Assert.All(GeneratedLanguages.Sample, l => Assert.All(l.RoleMarkers.Values.Select(m => m.Value).OfType<Adposition>(), a =>
            Assert.Equal(l.WordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial ? AdpositionType.Preposition : AdpositionType.Postposition, a.Type)));
    }

    [Fact]
    public void Realize_Oblique_TakesItsRolesCaseOrAdposition()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            foreach (var role in Oblique.Roles)
            {
                var sentence = Walk(language, Stone(role));
                var stone = IndexOf(sentence, "stone");
                switch (language.RoleMarkers[role].Value)
                {
                    case Case c:
                        Assert.Contains(sentence.Words, w => w.Gloss.Split('-', '.', '[', ']').Contains(c.Gloss));
                        break;
                    case Adposition a:
                        //the adposition is at the phrase's edge, with nothing of the clause's between it and the noun
                        var at = sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(m => m.Value is Root r && r == a.Lexeme.Root));
                        Assert.Equal(a.Gloss, sentence.Words[at].Gloss);
                        Assert.True(a.Type == AdpositionType.Preposition ? at < stone : at > stone);
                        var between = sentence.Words.Skip(Math.Min(at, stone) + 1).Take(Math.Abs(at - stone) - 1);
                        Assert.DoesNotContain(between, w => w.Word.Morphemes.Any(m => m.Value is Root));
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
            var transitive = Syntax.Realize(language, new Clause("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular),
                new NounPhrase("cat", Number.Singular), Obliques: [Stone(CaseUse.Location)]), "x");
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

            var intransitive = Walk(language, Stone(CaseUse.Location));
            Assert.Equal(order.ObliquePosition == ObliquePosition.AfterVerbAndObject, IndexOf(intransitive, "stone") > IndexOf(intransitive, "walk"));
        }
    }

    [Fact]
    public void Realize_SeveralObliques_StayTogetherInTheOrderGiven()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Walk(l, new Oblique(CaseUse.MovementFrom, new NounPhrase("tree", Number.Singular)), new Oblique(CaseUse.MovementTo, new NounPhrase("water", Number.Singular)));
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
            Assert.Contains(Walk(l, new Oblique(CaseUse.Accompaniment, you)).Words, w => w.Word.Morphemes.Any(m => m.Value is Root r && r == root));
        });
    }

    [Theory]
    [InlineData(CaseUse.DirectObject)]
    [InlineData(CaseUse.Possessive)]
    [InlineData(CaseUse.Address)]
    public void Realize_NonObliqueRole_Throws(CaseUse role)
    {
        Assert.Throws<ArgumentException>(() => Walk(GeneratedLanguages.Sample[0], Stone(role)));
    }
}
