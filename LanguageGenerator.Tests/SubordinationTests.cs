using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class SubordinationTests
{
    private static Statement Parsed(string text) => Assert.Single(ClauseFile.Parse(text)).Statement;

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    [Fact]
    public void Parse_LineStartingWithASubordinator_PutsItsClauseUnderTheMainOne()
    {
        var subordinated = Assert.IsType<Subordinated>(Parsed("x\n  cover.IMP  object:ear.PL\n  if  hear  subject:2SG  object:song.DEF").Value);

        Assert.IsType<Command>(subordinated.Main.Value);
        Assert.Equal(Subordinator.If, subordinated.Subordinator);
        Assert.Equal("hear", Assert.IsType<Clause>(subordinated.Clause.Value).Verb);
        Assert.False(subordinated.SharesSubject);
    }

    [Fact]
    public void Parse_PurposeWithoutASubject_TakesTheMainClauses()
    {
        var subordinated = Assert.IsType<Subordinated>(Parsed("x\n  need  subject:2SG  object:rope.NDF\n  purpose  cross  object:river.DEF").Value);

        Assert.True(subordinated.SharesSubject);
        Assert.Equal(new Pronoun(Person.Second, Number.Singular), Assert.IsType<Clause>(subordinated.Clause.Value).Subject.Value);
    }

    [Fact]
    public void Parse_PurposeWithASubject_KeepsIt() =>
        Assert.False(Assert.IsType<Subordinated>(Parsed("x\n  shout  subject:1SG\n  purpose  hear  subject:3SG.M").Value).SharesSubject);

    [Fact]
    public void Parse_JoinedAndSubordinateLines_NestInTheOrderWritten()
    {
        var subordinated = Assert.IsType<Subordinated>(Parsed("x\n  sit  subject:dog\n  and  eat  subject:dog\n  until  sleep  subject:cat").Value);

        Assert.IsType<Coordinated>(subordinated.Main.Value);
        Assert.Equal(Subordinator.Until, subordinated.Subordinator);
    }

    [Theory]
    [InlineData("x\n  sleep  subject:dog\n  if  go.IMP", "can't be a command")]
    [InlineData("x\n  sleep  subject:dog\n  when  sleep  subject:cat  addressee:marta", "in the main clause")]
    [InlineData("x\n  sleep  subject:dog\n  because  sleep", "needs a subject")]
    [InlineData("x\n  if  sleep  subject:dog", "can't come first")]
    public void Parse_BadSubordinateClauses_SayWhy(string text, string message) => Assert.Contains(message, Error(text).Message);

    [Fact]
    public void Nouns_IncludeTheSubordinateClauses() =>
        Assert.Equal(["dog", "cat"], ClauseFile.Nouns(ClauseFile.Parse("x\n  sleep  subject:dog\n  because  eat  subject:cat")).Select(n => n.Noun));

    private static List<Subordination> Draws(Func<Language, bool> keep)
    {
        var language = GeneratedLanguages.Sample.First(keep);
        var rand = new Random(1);
        return [.. Enumerable.Range(0, 3000).Select(_ => Generator.GenerateSubordination(rand, language.MorphologicalProfile, language.WordOrder, language.Phonology, 0.15))];
    }

    private static bool ObjectFirst(Language l) => l.WordOrder.VerbPhraseDirectionality == HeadDirectionality.Final;

    [Fact]
    public void GenerateSubordination_ManyDraws_MarkClausesAsWals94ASaysForTheWordOrder()
    {
        var vo = Draws(l => !ObjectFirst(l)).SelectMany(s => s.Markers.Values).ToList();
        var ov = Draws(l => ObjectFirst(l) && l.MorphologicalProfile.Fusion != Fusion.Isolating).SelectMany(s => s.Markers.Values).ToList();

        //of 307 VO languages that don't mix them, 304 start the clause with a word; of 208 OV ones, 61
        //start it with a word, 91 end it with one and 56 mark its verb
        Assert.InRange(vo.Count(m => m.Position == ExponentPosition.PrecedingParticle) / (double)vo.Count, 0.97, 1);
        Assert.InRange(ov.Count(m => m.Position == ExponentPosition.PrecedingParticle) / (double)ov.Count, 0.26, 0.33);
        Assert.InRange(ov.Count(m => m.Position == ExponentPosition.FollowingParticle) / (double)ov.Count, 0.40, 0.48);
        Assert.InRange(ov.Count(m => m.Position == ExponentPosition.Suffix) / (double)ov.Count, 0.24, 0.30);
        Assert.All(ov.Concat(vo), m => Assert.Equal(m.Position == ExponentPosition.Suffix, m.Marker.Value is Affix));
    }

    [Fact]
    public void GenerateSubordination_ManyDraws_DerankAsCristofaroCounts()
    {
        var drawn = Draws(_ => true);
        double Share(Subordinator s, Balance b) => drawn.Count(d => d.Markers[s].Balance == b) / 3000.0;

        //WALS 125A to 127A: 102 of 170 deranking purpose clauses, 84 of 174 balancing 'when' clauses, 90 of
        //169 balancing reason clauses
        Assert.InRange(Share(Subordinator.Purpose, Balance.Deranked), 0.57, 0.63);
        Assert.InRange(Share(Subordinator.When, Balance.Balanced), 0.45, 0.52);
        Assert.InRange(Share(Subordinator.Because, Balance.Balanced), 0.50, 0.57);
        Assert.All(drawn, d => Assert.All(new[] { Subordinator.If, Subordinator.Before, Subordinator.After, Subordinator.Until },
            s => Assert.Equal(d.Markers[Subordinator.When].Balance, d.Markers[s].Balance)));
    }

    [Theory]
    [InlineData(true, Subordinator.If, 0.87, 0.93)]
    [InlineData(true, Subordinator.Because, 0.87, 0.93)]
    [InlineData(false, Subordinator.If, 0.82, 0.88)]
    [InlineData(false, Subordinator.Purpose, 0.12, 0.18)]
    public void GenerateSubordination_ManyDraws_OrderClausesAsDiesselFinds(bool objectFirst, Subordinator subordinator, double least, double most)
    {
        var drawn = Draws(l => ObjectFirst(l) == objectFirst);

        Assert.InRange(drawn.Count(d => d.Markers[subordinator].BeforeMain) / 3000.0, least, most);
    }

    [Fact]
    public void GenerateSubordination_ManyDraws_ShareMarkersAcrossTheMapsEdges()
    {
        var drawn = Draws(_ => true);
        bool Share(Subordination s, Subordinator a, Subordinator b) =>
            s.Markers[a].Marker.Form.SequenceEqual(s.Markers[b].Marker.Form) && s.Markers[a].Position == s.Markers[b].Position;

        //'when' and 'if' share a marker by their own edge 40% of the time, and a little more through others
        Assert.InRange(drawn.Count(d => Share(d, Subordinator.When, Subordinator.If)) / 3000.0, 0.39, 0.50);
        Assert.InRange(drawn.Count(d => Share(d, Subordinator.Before, Subordinator.Until)) / 3000.0, 0.17, 0.25);
        //'if' and 'before' have no path between them but through 'when' and 'after' and 'because'... and
        //none at all to 'until', so those never share
        Assert.DoesNotContain(drawn, d => Share(d, Subordinator.If, Subordinator.Until));
        //each keeps its own meaning in its gloss
        Assert.All(drawn, d => Assert.All(d.Markers, m => Assert.Equal(Linkage.For(m.Key), Assert.Single(m.Value.Marker.Value switch
        {
            Affix a => a.Grammemes,
            Particle p => p.Grammemes,
            _ => [],
        }))));
    }

    [Fact]
    public void GenerateSubordination_ManyDraws_MarkReducedClausesLikeRolesAsGuessed()
    {
        var drawn = Draws(_ => true);
        double Share(Subordinator s, Role? role) => drawn.Count(d => d.Markers[s].MarkedAs == role) / 3000.0;

        Assert.InRange(Share(Subordinator.Purpose, Role.Goal), 0.22, 0.28);
        Assert.InRange(Share(Subordinator.Purpose, Role.Recipient), 0.12, 0.18);
        Assert.InRange(Share(Subordinator.Because, Role.Source), 0.22, 0.28);
        Assert.Equal(1, Share(Subordinator.If, null));
        Assert.Equal(1, Share(Subordinator.Before, null));
    }

    [Fact]
    public void Realize_DerankedClauseMarkedLikeARole_TakesItsCaseOrAdposition()
    {
        var dogEats = CatEats with { Subject = new NounPhrase("dog", Number.Singular) };
        var marked = GeneratedLanguages.Sample.Where(l => l.Subordination.Markers[Subordinator.Purpose] is { Balance: not Balance.Balanced, MarkedAs: not null }).ToList();

        Assert.NotEmpty(marked);
        Assert.All(marked, l =>
        {
            var sentence = Syntax.Realize(l, new Subordinated(dogEats, Subordinator.Purpose, DogSleeps, true), "x");
            var sleep = IndexOf(sentence, "sleep");

            Assert.DoesNotContain(sentence.Words, w => w.Word.Morphemes.Any(IsMarker));
            switch (l.RoleMarkers[l.Subordination.Markers[Subordinator.Purpose].MarkedAs!.Value].Value)
            {
                //a case particle can come before the verb, and the main clause has only core cases
                case Case c:
                    Assert.Contains(c.Gloss, string.Join(" ", sentence.Words.Select(w => w.Gloss)).Split('-', '.', ' ', '[', ']'));
                    break;
                case Adposition a:
                    Assert.InRange(IndexOf(sentence, a.Lexeme.Root), sleep - 1, sleep + 1);
                    break;
            }
        });
    }

    [Fact]
    public void Generate_IsolatingLanguages_MarkClausesWithWords() =>
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating),
            l => Assert.DoesNotContain(l.Subordination.Markers.Values, m => m.Position == ExponentPosition.Suffix));

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static readonly Clause CatEats = new("eat", Tense.Present, Aspect.Imperfective, new NounPhrase("cat", Number.Singular), new NounPhrase("fish", Number.Singular)) { Adverbs = [] };
    private static readonly Clause DogSleeps = new("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("dog", Number.Singular)) { Adverbs = [] };

    private static bool IsMarker(Morpheme m) => m.Value is Affix { Grammemes: [Linkage] } or Particle { Grammemes: [Linkage] };

    [Fact]
    public void Realize_SubordinateClause_GoesWhereTheLanguagePutsItWithItsMarker()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            foreach (var subordinator in Enum.GetValues<Subordinator>())
            {
                var marking = l.Subordination.Markers[subordinator];
                var sentence = Syntax.Realize(l, new Subordinated(CatEats, subordinator, DogSleeps, false), "x");
                var (dog, cat, sleep) = (IndexOf(sentence, "dog"), IndexOf(sentence, "cat"), IndexOf(sentence, "sleep"));
                var marker = sentence.Words.ToList().FindIndex(w => w.Word.Morphemes.Any(IsMarker));

                Assert.Equal(marking.BeforeMain, dog < cat);
                //the clauses don't share a subject, so only a clause the language always deranks can be
                //marked like a role in place of its subordinator
                if (marking is { Balance: Balance.Deranked, MarkedAs: not null })
                {
                    Assert.Equal(-1, marker);
                    continue;
                }
                Assert.True(marking.Position switch
                {
                    ExponentPosition.PrecedingParticle => marker < Math.Min(dog, sleep),
                    ExponentPosition.FollowingParticle => marker > Math.Max(dog, sleep),
                    _ => marker == sleep,
                });
            }
        });
    }

    [Fact]
    public void Realize_DerankedClause_HasABareVerb()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Subordination.Markers[Subordinator.If].Balance == Balance.Deranked), l =>
        {
            var sentence = Syntax.Realize(l, new Subordinated(CatEats, Subordinator.If, DogSleeps, false), "x");
            var verb = sentence.Words[IndexOf(sentence, "sleep")].Word;

            Assert.All(verb.Morphemes, m => Assert.True(m.Value is Root || IsMarker(m)));
            Assert.Null(verb.Covert);
        });
    }

    [Fact]
    public void Realize_SharedSubject_IsSaidOnlyOnce()
    {
        var dogEats = CatEats with { Subject = new NounPhrase("dog", Number.Singular) };

        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Subordinated(dogEats, Subordinator.Purpose, DogSleeps, true), "x");

            Assert.Single(sentence.Words, w => w.Word.Root?.Meaning == "dog");
        });
    }

    [Fact]
    public void Realize_BalancedClause_IsAsItWouldBeAlone()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Subordination.Markers[Subordinator.When] is { Balance: Balance.Balanced, Position: not ExponentPosition.Suffix }), l =>
        {
            var sentence = Syntax.Realize(l, new Subordinated(CatEats, Subordinator.When, DogSleeps, false), "x");
            var alone = Syntax.Realize(l, DogSleeps, "x").Words.Select(w => w.Gloss);

            Assert.Contains(string.Join(" ", alone), string.Join(" ", sentence.Words.Select(w => w.Gloss)));
        });
    }
}
