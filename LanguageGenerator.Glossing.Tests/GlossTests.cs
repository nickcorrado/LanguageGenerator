using System.Text.RegularExpressions;
namespace LanguageGenerator.Glossing.Tests;

public partial class GlossTests
{
    //answers each numbered sentence in a request with whatever gloss answer gives it, and records
    //the requests. Throws on the request numbered failOn, counting from 1
    private sealed partial class FakeModel(Func<string, int, string> answer, int? failOn = null) : IChatModel
    {
        public List<string> Requests { get; } = [];
        public string Name => "fake";

        public Task<ChatReply> Complete(string system, string user, CancellationToken cancellationToken = default)
        {
            Requests.Add(user);
            if (Requests.Count == failOn)
            {
                throw new ChatModelException("The server went away.");
            }
            var reply = string.Join("\n", Numbered().Matches(user).Select(m => $"[{m.Groups[1].Value}]\n{answer(m.Groups[2].Value, Requests.Count)}"));
            return Task.FromResult(new ChatReply(reply, new TokenUsage(100, 10, 20)));
        }

        [GeneratedRegex(@"^\[(\d+)\] (.*)$", RegexOptions.Multiline)]
        private static partial Regex Numbered();
    }

    //a gloss for sentences like "The dog slept.": the noun, then the verb
    private static string Good(string sentence)
    {
        var words = sentence.TrimEnd('.').ToLowerInvariant().Split(' ');
        return $"{sentence}\n  sleep.PST  subject:{words[1]}";
    }

    private static readonly string Text = "The dog slept. The cat slept. The bird slept. The fish slept. The wolf slept.";

    //replies to noun requests in turn, and records them
    private sealed class NounModel(params string[] replies) : IChatModel
    {
        public List<string> Requests { get; } = [];
        public string Name => "fake";

        public Task<ChatReply> Complete(string system, string user, CancellationToken cancellationToken = default)
        {
            Requests.Add(user);
            return Task.FromResult(new ChatReply(replies[Requests.Count - 1], new TokenUsage(100, 0, 20)));
        }
    }

    private static IReadOnlyList<ClauseFile.Entry> Clauses(params string[] nouns) =>
        ClauseFile.Parse(string.Join("\n", nouns.Select(n => $"the {n} slept\n  sleep.PST  subject:{n}")));

    private static readonly Dictionary<string, SemanticClass> NoNouns = [];

    [Fact]
    public async Task ClassifyNouns_TagsWhatTheModelListsAndTheRestAreThings()
    {
        var model = new NounModel("marta F\nmiller HUM");

        var result = await new Glosser(model).ClassifyNouns(Clauses("marta", "miller", "river"), NoNouns);

        Assert.Null(result.Error);
        Assert.Equal(SemanticClass.Female, result.Nouns["marta"]);
        Assert.Equal(SemanticClass.Human, result.Nouns["miller"]);
        Assert.Equal(SemanticClass.Thing, result.Nouns["river"]);
        Assert.Contains("marta: the marta slept", model.Requests[0]);
    }

    [Fact]
    public async Task ClassifyNouns_KnownNouns_AreKeptAndNotAskedAbout()
    {
        var model = new NounModel("miller HUM");
        var known = new Dictionary<string, SemanticClass> { ["marta"] = SemanticClass.Male, ["old"] = SemanticClass.Thing };

        var result = await new Glosser(model).ClassifyNouns(Clauses("marta", "miller"), known);

        Assert.DoesNotContain("marta", model.Requests[0]);
        Assert.Equal(SemanticClass.Male, result.Nouns["marta"]);
        Assert.Equal(SemanticClass.Human, result.Nouns["miller"]);
        Assert.Contains("old", result.Nouns.Keys);
    }

    [Fact]
    public async Task ClassifyNouns_NothingNew_AsksNothing()
    {
        var model = new NounModel();
        var known = new Dictionary<string, SemanticClass> { ["marta"] = SemanticClass.Female };

        var result = await new Glosser(model).ClassifyNouns(Clauses("marta"), known);

        Assert.Empty(model.Requests);
        Assert.Equal(known, result.Nouns);
    }

    [Fact]
    public async Task ClassifyNouns_None_MeansThingsAndFences_AreIgnored()
    {
        Assert.Equal(SemanticClass.Thing, (await new Glosser(new NounModel("NONE")).ClassifyNouns(Clauses("river"), NoNouns)).Nouns["river"]);
        Assert.Equal(SemanticClass.Female, (await new Glosser(new NounModel("```\nmarta F\n```")).ClassifyNouns(Clauses("marta"), NoNouns)).Nouns["marta"]);
    }

    [Theory]
    [InlineData("marta WOMAN")]
    [InlineData("marta is female")]
    [InlineData("river THING\nsomeone F")]
    public async Task ClassifyNouns_ABadReply_IsRetriedWithWhatsWrong(string bad)
    {
        var model = new NounModel(bad, "marta F");

        var result = await new Glosser(model).ClassifyNouns(Clauses("marta", "river"), NoNouns);

        Assert.Null(result.Error);
        Assert.Equal(2, model.Requests.Count);
        Assert.Contains(bad, model.Requests[1]);
        Assert.Equal(SemanticClass.Female, result.Nouns["marta"]);
    }

    [Fact]
    public async Task ClassifyNouns_RepliesThatNeverParse_GiveUpAndKeepWhatWasKnown()
    {
        var known = new Dictionary<string, SemanticClass> { ["king"] = SemanticClass.Male };

        var result = await new Glosser(new NounModel("what", "what", "what"), retries: 2).ClassifyNouns(Clauses("marta"), known);

        Assert.NotNull(result.Error);
        Assert.Equal(known, result.Nouns);
    }

    [Fact]
    public void NounPrompt_ShowsEveryTagButThing()
    {
        Assert.All(NounFile.TagNames.Where(t => t != "THING"), tag => Assert.Contains(tag, GlossPrompt.NounSystem));
    }

    [Fact]
    public void Sentences_SplitAtSentenceEndsAndParagraphs()
    {
        Assert.Equal(["The dog slept.", "Did the cat run?", "\"Yes,\" she said.", "\"It ran!\"", "Then", "A new paragraph"],
            Glosser.Sentences("The dog slept.  Did the cat run?\n\"Yes,\" she said. \"It ran!\" Then\n\n  A new\nparagraph"));
    }

    [Fact]
    public void Prompt_DescribesEveryRoleAndFeatureTheFormatHas()
    {
        var prompt = GlossPrompt.System;

        Assert.All(ClauseFile.RoleNames, role => Assert.Contains(role, prompt));
        Assert.All(ClauseFile.ValuesOf<Tense>().Concat<Grammeme>(ClauseFile.ValuesOf<Aspect>()).Concat(ClauseFile.ValuesOf<Polarity>()).Concat(ClauseFile.ValuesOf<Number>()),
            value => Assert.Contains($"{value.Gloss} ({value.Name.ToLowerInvariant()})", prompt));
    }

    [Fact]
    public void Prompt_SaysHowToGiveCommandsAndNotToDropModals()
    {
        Assert.Contains(".IMP", GlossPrompt.System);
        Assert.Contains("Never drop a modal", GlossPrompt.System);
    }

    [Fact]
    public void Prompt_ItsOwnExamples_Parse()
    {
        var examples = Regex.Matches(GlossPrompt.System, @"^ {2}(\S.*)\n {4}(\S.*)$", RegexOptions.Multiline);

        Assert.NotEmpty(examples);
        Assert.All(examples, m => Assert.Single(ClauseFile.Parse($"{m.Groups[1].Value}\n  {m.Groups[2].Value}")));
    }

    [Fact]
    public async Task Gloss_SentencesInBatches_WritesAFileThatParses()
    {
        var model = new FakeModel((s, _) => Good(s));

        var result = await new Glosser(model, batchSize: 2).Gloss(Text);

        Assert.Equal(3, model.Requests.Count);
        Assert.Equal(5, ClauseFile.Parse(result.Text).Count);
        Assert.Equal(5, result.Text.Split('\n').Count(l => l.StartsWith(Glosser.SentenceMarker)));
        Assert.Equal(new TokenUsage(300, 30, 60), result.Usage);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Gloss_AGlossThatDoesntParse_IsSentBackAloneWithTheError()
    {
        var model = new FakeModel((s, request) => s.Contains("cat") && request == 1 ? $"{s}\n  sleep.PAST  subject:cat" : Good(s));

        var result = await new Glosser(model).Gloss(Text);

        Assert.Equal(2, model.Requests.Count);
        Assert.Contains("Did you mean PST", model.Requests[1]);
        Assert.Contains("[1] The cat slept.", model.Requests[1]);
        Assert.DoesNotContain("The dog slept.", model.Requests[1].Split("These glosses")[1]);
        Assert.Equal(5, ClauseFile.Parse(result.Text).Count);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task Gloss_AGlossThatNeverParses_IsLeftAsAComment()
    {
        var model = new FakeModel((s, _) => s.Contains("cat") ? $"{s}\n  sleep.PAST  subject:cat" : Good(s));

        var result = await new Glosser(model, retries: 2).Gloss(Text);

        Assert.Equal(3, model.Requests.Count);
        Assert.Equal(1, result.Failed);
        Assert.Contains($"{Glosser.CouldNotGloss} (Line 2:", result.Text);
        Assert.Equal(4, ClauseFile.Parse(result.Text).Count);
    }

    [Fact]
    public async Task Gloss_AnAnswerLeftOut_IsAskedForAgain()
    {
        var model = new FakeModel((s, request) => s.Contains("bird") && request == 1 ? "" : Good(s));

        var result = await new Glosser(model).Gloss(Text);

        Assert.Equal(2, model.Requests.Count);
        Assert.Contains("no gloss", model.Requests[1]);
        Assert.Equal(5, ClauseFile.Parse(result.Text).Count);
    }

    [Fact]
    public async Task Gloss_ASentenceThatCantBeExpressed_IsKeptAsSuch()
    {
        var model = new FakeModel((s, _) => s.EndsWith('?') ? $"{GlossPrompt.CantExpress} (question): {s}" : Good(s));

        var result = await new Glosser(model).Gloss("The dog slept. Where did it sleep?");

        Assert.Single(model.Requests);
        Assert.Equal(1, result.Unexpressible);
        Assert.Single(ClauseFile.Parse(result.Text));
    }

    [Fact]
    public async Task Gloss_AgainWithTheEarlierOutput_SendsOnlyWhatsNewOrFailed()
    {
        var edited = "the dog slept soundly\n  sleep.PST.PFV  subject:dog";
        var previous = $"# header\n\n{Glosser.SentenceMarker}The dog slept.\n{edited}\n\n{Glosser.SentenceMarker}The cat slept.\n{Glosser.CouldNotGloss} (whatever)\n";
        var model = new FakeModel((s, _) => Good(s));

        var result = await new Glosser(model).Gloss("The dog slept. The cat slept. The bird slept.", previous);

        var asked = Assert.Single(model.Requests).Split("Gloss these:")[1];
        Assert.DoesNotContain("dog", asked);
        Assert.Contains("[1] The cat slept.", asked);
        Assert.Contains("[2] The bird slept.", asked);
        Assert.Contains(edited, result.Text);
        Assert.Equal(1, result.Reused);
    }

    [Fact]
    public async Task Gloss_LaterBatches_SeeTheSentencesBeforeThem()
    {
        var model = new FakeModel((s, _) => Good(s));

        await new Glosser(model, batchSize: 1, contextSentences: 2).Gloss(Text);

        Assert.DoesNotContain("Earlier in the text", model.Requests[0]);
        Assert.Contains("Earlier in the text, for context only:\n  The dog slept.\n  The cat slept.\n", model.Requests[2]);
    }

    [Fact]
    public async Task Gloss_ARequestThatFails_KeepsWhatWasDoneForTheNextRun()
    {
        var failing = new FakeModel((s, _) => Good(s), failOn: 2);

        var partial = await new Glosser(failing, batchSize: 2).Gloss(Text);

        Assert.IsType<ChatModelException>(partial.Error);
        Assert.Equal(3, partial.Missing);
        Assert.Equal(2, ClauseFile.Parse(partial.Text).Count);

        var model = new FakeModel((s, _) => Good(s));
        var result = await new Glosser(model, batchSize: 10).Gloss(Text, partial.Text);
        Assert.Single(model.Requests);
        Assert.Equal(2, result.Reused);
        Assert.Equal(5, ClauseFile.Parse(result.Text).Count);
    }

    [Theory]
    [InlineData("", "no gloss")]
    [InlineData("the dog slept", "needs an indented clause")]
    [InlineData("# just a comment", "no clauses")]
    [InlineData("x\n  sleep subject:dog\n# > sneaky", "can't start with")]
    public void Check_UnusableGlosses_SayWhy(string block, string message)
    {
        Assert.Contains(message, Glosser.Check(block));
    }

    [Fact]
    public void Numbered_ReadsBlocksByNumber_IgnoringFencesAndBlankEdges()
    {
        var answers = Glosser.Numbered("Sure!\n```\n[2]\n\nb\n  sleep subject:b\n\n[1]\na\n  sleep subject:a\n```\n");

        Assert.Equal("b\n  sleep subject:b", answers[2]);
        Assert.Equal("a\n  sleep subject:a", answers[1]);
    }
}
