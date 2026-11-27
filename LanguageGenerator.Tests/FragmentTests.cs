using static LanguageGenerator.Tests.Phrases;
using static LanguageGenerator.Tests.Clauses;
using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class FragmentTests
{
    private static Fragment Parse(string gloss) => Assert.IsType<Fragment>(Assert.Single(ClauseFile.Parse($"x\n  {gloss}")).Utterance.Value);

    private static ClauseFormatException Error(string text) => Assert.Throws<ClauseFormatException>(() => ClauseFile.Parse(text));

    private static NounPhrase Definite(string meaning) => new(new Noun(meaning, Number.Singular, [], null, null, null, Definiteness.Definite, null), null);

    [Fact]
    public void Parse_NounAlone_StandsAlone()
    {
        var fragment = Parse("noun:(key.NDF old iron)");

        var standing = Assert.IsType<Standalone>(fragment.Part.GetValueOrDefault().Value);
        Assert.Equal(["old", "iron"], NounOf(standing.Argument).Adjectives.Select(a => a.Meaning));
        Assert.Null(fragment.Addressee);
    }

    [Fact]
    public void Parse_ObliqueAlone_KeepsItsRoleAndConfiguration()
    {
        var fragment = Parse("goal.in:river.DEF  addressee:marta");

        Assert.Equal(new Oblique(Role.Goal, Definite("river"), Configuration.In), fragment.Part.GetValueOrDefault().Value);
        Assert.Equal(BareNoun("marta", Number.Singular), fragment.Addressee);
    }

    [Fact]
    public void Parse_LabelAndNoun_IsAHeading()
    {
        var heading = Assert.IsType<Heading>(Parse("label:item  noun:(key rusty)").Part.GetValueOrDefault().Value);

        Assert.Equal(BareNoun("item", Number.Singular), heading.Label.Value);
        Assert.Equal("key", NounOf(heading.Title).Meaning);
    }

    [Fact]
    public void Parse_AddresseeAlone_CallsThem()
    {
        var fragment = Parse("addressee:stranger");

        Assert.Null(fragment.Part);
        Assert.Equal(BareNoun("stranger", Number.Singular), fragment.Addressee);
    }

    [Fact]
    public void Parse_Interjection_IsItsMeaning()
    {
        var fragment = Parse("interjection:hello  addressee:marta");

        Assert.Equal(new Interjection("hello"), fragment.Part.GetValueOrDefault().Value);
        Assert.Equal(BareNoun("marta", Number.Singular), fragment.Addressee);
    }

    [Theory]
    [InlineData("yes", "Affirmative")]
    [InlineData("no", "Negative")]
    public void Parse_Answer_IsItsPolarity(string word, string polarity)
    {
        var answer = Assert.IsType<Answer>(Parse($"answer:{word}").Part.GetValueOrDefault().Value);

        Assert.Equal(polarity, answer.Polarity.Name);
        Assert.Equal(word, answer.Meaning);
    }

    [Fact]
    public void Parse_QuotedAnswer_IsWhatWasSaid()
    {
        var said = Said<Clause>(Assert.Single(ClauseFile.Parse("x\n  say  subject:3SG.F  quote:[answer:no]  manner:quiet")).Utterance);

        var quotation = Assert.IsType<Quotation>(PredicateOf<Doing>(said).Object.GetValueOrDefault().Value);
        Assert.Equal(new Answer(Polarity.Negative), Assert.IsType<Fragment>(quotation.Utterance.Value).Part.GetValueOrDefault().Value);
    }

    [Fact]
    public void Parse_QuotedFragment_IsWhatWasSaid()
    {
        var said = Said<Clause>(Assert.Single(ClauseFile.Parse("x\n  say  subject:3SG.M  quote:[beneficiary:(ear.PL possessor:2SG)]")).Utterance);

        var quotation = Assert.IsType<Quotation>(PredicateOf<Doing>(said).Object.GetValueOrDefault().Value);
        var fragment = Assert.IsType<Fragment>(quotation.Utterance.Value);
        Assert.Equal(Role.Beneficiary, Assert.IsType<Oblique>(fragment.Part.GetValueOrDefault().Value).Role);
    }

    [Fact]
    public void Parse_FragmentNouns_AreTheTextsNouns()
    {
        var entries = ClauseFile.Parse("x\n  label:item  noun:(key rusty)\ny\n  beneficiary:ear.PL  addressee:stranger");

        Assert.Equal(["item", "key", "stranger", "ear"], ClauseFile.Nouns(entries).Select(n => n.Noun));
    }

    [Theory]
    [InlineData("x\n  subject:dog", "not subject:")]
    [InlineData("x\n  noun:dog  goal:house", "not both")]
    [InlineData("x\n  label:item", "needs its title")]
    [InlineData("x\n  label:item  noun:key  addressee:marta", "said to no one")]
    [InlineData("x\n  noun:dog  noun:cat", "more than one noun")]
    [InlineData("x\n  goal:house  source:river", "one oblique")]
    [InlineData("x\n  beneficiary.in:house", "Only a role saying where")]
    [InlineData("x\n  noun:what", "can't ask")]
    [InlineData("x\n  noun:(dog relative:[see  subject:_  object:cat])  goal:_", "_ marks")]
    [InlineData("x\n  say  subject:3SG  quote:[label:item  noun:key]", "can't be quoted")]
    [InlineData("x\n  noun:dog.DEF\n  and  sleep  subject:cat", "which a fragment with no verb isn't")]
    [InlineData("x\n  interjection:oh  noun:dog", "said on its own")]
    [InlineData("x\n  interjection:hello.PL", "with no glosses")]
    [InlineData("x\n  interjection:(hello big)", "with no glosses")]
    [InlineData("x\n  interjection:oh  interjection:hello", "one interjection: or answer:")]
    [InlineData("x\n  interjection:oh  answer:no", "one interjection: or answer:")]
    [InlineData("x\n  interjection:no", "so it's answer:no")]
    [InlineData("x\n  answer:maybe", "answer:yes or answer:no")]
    [InlineData("x\n  answer:no  goal:house", "said on its own")]
    public void Parse_BadFragments_SayWhy(string text, string message) => Assert.Contains(message, Error(text).Message);

    [Fact]
    public void Realize_Standalone_IsThePhraseAsItsCited()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var dog = Definite("dog");
            var sentence = Syntax.Realize(l, new Fragment(new Standalone(dog), null), "the dog");

            Assert.Equal(Syntax.Cited(l, dog).Select(w => w.Gloss), sentence.Words.Select(w => w.Gloss));
            Assert.Empty(sentence.Label);
        });
    }

    [Fact]
    public void Realize_Oblique_IsMarkedAsInAClause()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var oblique = new Oblique(Role.Beneficiary, Definite("dog"), Configuration.At);
            var clause = Syntax.Realize(l, new Clause(new Doing("sleep", null), new Declarative(Tense.Present, Aspect.Imperfective, BareNoun("person", Number.Singular)), false, [oblique], []), "x");
            var fragment = Syntax.Realize(l, new Fragment(oblique, null), "x");

            //the fragment is the oblique's words, together in the clause as they are on their own
            var (said, alone) = (clause.Words.Select(w => w.Gloss).ToList(), fragment.Words.Select(w => w.Gloss).ToList());
            Assert.Contains(Enumerable.Range(0, said.Count - alone.Count + 1), at => said.Skip(at).Take(alone.Count).SequenceEqual(alone));
            Assert.Contains(fragment.Words, w => w.Word.Root?.Meaning == "dog");
        });
    }

    [Fact]
    public void Realize_AddresseeAlone_IsTheVocative()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Fragment(null, BareNoun("person", Number.Singular)), "x");

            Assert.Equal(l.RoleMarkers[Role.Addressee].Case is { Name: "Vocative" }, sentence.Words.Any(w => w.Gloss.Contains("VOC")));
            Assert.Equal("person", Assert.Single(sentence.Words, w => w.Word.Root is not null).Word.Root!.Meaning);
        });
    }

    [Fact]
    public void Realize_AddresseeWithAPhrase_ComesFirst()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Fragment(new Standalone(Definite("dog")), BareNoun("person", Number.Singular)), "x");

            Assert.True(IndexOf(sentence, "person") < IndexOf(sentence, "dog"));
        });
    }

    [Fact]
    public void Realize_Heading_IsItsLabelThenItsTitleWithAColon()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var (item, dog) = (BareNoun("house", Number.Singular), Definite("dog"));
            var sentence = Syntax.Realize(l, new Fragment(new Heading(item, dog), null), "house: the dog");

            Assert.Equal(Syntax.Cited(l, item).Select(w => w.Gloss), sentence.Label.SelectMany(g => g.Words).Select(w => w.Gloss));
            Assert.Equal(Syntax.Cited(l, dog).Select(w => w.Gloss), sentence.Said.SelectMany(g => g.Words).Select(w => w.Gloss));
            Assert.Contains(": ", sentence.Text);
            Assert.Contains(" | ", sentence.Transcribed());
            Assert.Contains(":", sentence.Interlinear(false).Split(Environment.NewLine)[1]);
        });
    }

    [Fact]
    public void Realize_Interjection_IsAShortWordBeforeWhoItsSaidTo()
    {
        //languages of the test's own, since the sample's have no interjections
        Assert.All(Enumerable.Range(0, 30).Select(seed => Generator.Generate(seed, LanguageSpec.Free)), l =>
        {
            var sentence = Syntax.Realize(l, new Fragment(new Interjection("hello"), BareNoun("person", Number.Singular)), "x");

            var hello = l.Lexicon.Interjection("hello");
            Assert.True(IndexOf(sentence, hello.Root) < IndexOf(sentence, "person"));
            Assert.InRange(l.Phonology.Realize(new Word([hello.Root])).Syllables.Count, 1, 2);
        });
    }

    [Fact]
    public void Realize_Answer_IsAWordOfItsOwnBeforeWhoItsSaidTo()
    {
        //languages of the test's own, since the sample's have no answers
        Assert.All(Enumerable.Range(0, 30).Select(seed => Generator.Generate(seed, LanguageSpec.Free)), l =>
        {
            var sentence = Syntax.Realize(l, new Fragment(new Answer(Polarity.Negative), BareNoun("person", Number.Singular)), "no, person");

            Assert.True(IndexOf(sentence, "no") < IndexOf(sentence, "person"));
        });
    }

    [Fact]
    public void Realize_QuotedFragment_IsSaid()
    {
        //languages of the test's own, since the sample's have no word for 'say'
        Assert.All(Enumerable.Range(0, 10).Select(seed => Generator.Generate(seed, LanguageSpec.Free)), l =>
        {
            var said = new Quotation(new Fragment(new Oblique(Role.Goal, Definite("house"), Configuration.At), null));
            var sentence = Syntax.Realize(l, new Clause(new Doing("say", said), new Declarative(Tense.Past, Aspect.Perfective, BareNoun("person", Number.Singular)), false, [], []), "x");

            Assert.True(IndexOf(sentence, "house") >= 0);
        });
    }
}
