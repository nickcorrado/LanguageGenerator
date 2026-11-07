namespace LanguageGenerator.Tests;

public class GenderTests
{
    private static Lexeme Noun(string meaning, params Phoneme[] form) => new(new Root(form, meaning), PartOfSpeech.Noun);

    private static GenderSystem System(NounGenders genders, GenderAssignment assignment) =>
        new() { Genders = genders, Assignment = assignment, Markers = null, MarkerPosition = ExponentPosition.Suffix, VerbsAgree = false };

    private static Language WithGenders(NounGenders genders, AdjectiveAgreement? agreement = null, bool verbsAgree = false) =>
        GeneratedLanguages.Sample.First(l => l.Genders.Genders == genders && l.Genders.Markers is not null
            && (agreement is null || l.AdjectiveAgreement == agreement)
            && (!verbsAgree || l.Genders.VerbsAgree));

    //which gender markers a word has, by gloss
    private static List<string> GenderGlosses(Word word) =>
        [.. word.Morphemes.Where(m => m.Value is Affix { Grammemes: [Gender] }).Select(m => m.Gloss)];

    [Fact]
    public void GenerateGenders_ManyDraws_MatchTheWalsCounts()
    {
        var language = GeneratedLanguages.Sample.First(l => l.MorphologicalProfile.Fusion != Fusion.Isolating);
        var rand = new Random(1);

        var systems = Enumerable.Range(0, 5000).Select(_ => Generator.GenerateGenders(rand, language.MorphologicalProfile, language.Paradigms, language.Phonology, 0.15)).ToList();
        double Share(NounGenders g) => systems.Count(s => s.Genders == g) / 5000.0;

        //WALS 30A: 145 of the 221 languages modeled have none, 50 have two and 26 three
        Assert.InRange(Share(NounGenders.None), 0.63, 0.68);
        Assert.InRange(Share(NounGenders.MasculineFeminine) + Share(NounGenders.AnimateInanimate), 0.20, 0.25);
        Assert.InRange(Share(NounGenders.MasculineFeminineNeuter), 0.10, 0.14);
        //WALS 32A: 59 of 112 gendered languages assign by form as well as meaning
        var gendered = systems.Where(s => s.Genders != NounGenders.None).ToList();
        Assert.InRange(gendered.Count(s => s.Assignment == GenderAssignment.SemanticAndFormal) / (double)gendered.Count, 0.49, 0.56);
    }

    [Theory]
    [InlineData("woman", "F")]
    [InlineData("king", "M")]
    [InlineData("person", "M")]
    public void Of_People_AreBySex(string meaning, string gloss)
    {
        foreach (var assignment in Enum.GetValues<GenderAssignment>())
        {
            foreach (var genders in new[] { NounGenders.MasculineFeminine, NounGenders.MasculineFeminineNeuter })
            {
                Assert.Equal(gloss, System(genders, assignment).Of(Noun(meaning, Consonant.t, Vowel.i))!.Gloss);
            }
        }
    }

    [Theory]
    [InlineData("stone", "NANM")]
    [InlineData("dog", "ANM")]
    [InlineData("hunter", "ANM")]
    public void Of_SemanticAnimacy_MakesLivingThingsAnimate(string meaning, string gloss)
    {
        Assert.Equal(gloss, System(NounGenders.AnimateInanimate, GenderAssignment.Semantic).Of(Noun(meaning, Consonant.t, Vowel.a, Consonant.k))!.Gloss);
    }

    [Fact]
    public void Of_SemanticAssignment_PutsEverythingButPeopleInOneGender()
    {
        var three = System(NounGenders.MasculineFeminineNeuter, GenderAssignment.Semantic);
        var two = System(NounGenders.MasculineFeminine, GenderAssignment.Semantic);

        Assert.All(new[] { Noun("stone", Vowel.i), Noun("dog", Vowel.a), Noun("tree", Consonant.k) }, n =>
        {
            Assert.Equal(Gender.Neuter, three.Of(n));
            Assert.Equal(Gender.Masculine, two.Of(n));
        });
    }

    [Theory]
    [InlineData(NounGenders.MasculineFeminineNeuter, "F", "M", "N")]
    [InlineData(NounGenders.MasculineFeminine, "F", "M", "M")]
    [InlineData(NounGenders.AnimateInanimate, "NANM", "NANM", "ANM")]
    public void Of_FormalAssignment_GoesByTheRootsLastSound(NounGenders genders, string frontVowel, string otherVowel, string consonant)
    {
        var system = System(genders, GenderAssignment.SemanticAndFormal);

        Assert.Equal(frontVowel, system.Of(Noun("stone", Consonant.t, Vowel.e))!.Gloss);
        Assert.Equal(otherVowel, system.Of(Noun("stone", Consonant.t, Vowel.o))!.Gloss);
        Assert.Equal(consonant, system.Of(Noun("stone", Consonant.t, Vowel.a, Consonant.k))!.Gloss);
    }

    [Fact]
    public void Of_LanguageWithoutGender_IsNothing()
    {
        Assert.Null(System(NounGenders.None, GenderAssignment.SemanticAndFormal).Of(Noun("woman", Vowel.a)));
    }

    [Fact]
    public void Generate_GenderMarkers_AreDistinctWithTheFirstGenderUnmarked()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Genders.Markers is not null), l =>
        {
            var markers = l.Genders.Values.Select(g => l.Genders.Markers![g]).ToList();
            Assert.Empty(markers[0].Form);
            Assert.All(markers.Skip(1), m => Assert.NotEmpty(m.Form));
            Assert.Equal(markers.Count, markers.Select(m => string.Join(" ", m.Form.Select(p => p.IpaSymbol))).Distinct().Count());
        });
    }

    [Fact]
    public void Generate_IsolatingLanguages_HaveNoGenderMarkers()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.MorphologicalProfile.Fusion == Fusion.Isolating), l => Assert.Null(l.Genders.Markers));
    }

    [Fact]
    public void Realize_AgreeingAdjective_TakesItsNounsGender()
    {
        //sun, as a thing, is neuter in a semantic system; whatever the gender, the adjective shows it
        foreach (var language in GeneratedLanguages.Sample.Where(l => l.Genders.Markers is not null && l.AdjectiveAgreement == AdjectiveAgreement.Agreement))
        {
            foreach (var noun in new[] { "sun", "person", "dog" })
            {
                var gender = language.GenderOf(language.Lexicon.Noun(noun))!;
                var sentence = Syntax.Realize(language, new Clause("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase(noun, Number.Singular, [new("big")])), "x");
                var big = sentence.Words.Single(w => w.Word.Root?.Meaning == "big").Word;

                Assert.Equal(language.Genders.Markers![gender].Form.Count == 0 ? [] : [gender.Gloss], GenderGlosses(big));
            }
        }
    }

    [Fact]
    public void Realize_NonagreeingAdjectives_ShowNoGender()
    {
        foreach (var language in GeneratedLanguages.Sample.Where(l => l.Genders.Markers is not null && l.AdjectiveAgreement != AdjectiveAgreement.Agreement))
        {
            var sentence = Syntax.Realize(language, new Clause("sleep", Tense.Present, Aspect.Imperfective, new NounPhrase("sun", Number.Singular, [new("big")])), "x");

            //the verb may agree in gender, but the adjective doesn't
            var big = sentence.Words.Single(w => w.Word.Root?.Meaning == "big").Word;
            Assert.Empty(GenderGlosses(big));
        }
    }

    [Fact]
    public void Realize_VerbThatAgreesInGender_TakesItsSubjects()
    {
        var agreeing = GeneratedLanguages.Sample.Where(l => l.Genders.VerbsAgree).ToList();

        Assert.NotEmpty(agreeing);
        Assert.All(agreeing, l =>
        {
            foreach (var subject in new Argument[] { new NounPhrase("person", Number.Singular), new NounPhrase("sun", Number.Singular), new Pronoun(Person.Third, Number.Singular, Gender: l.Genders.Values[^1]) })
            {
                var gender = subject.Value is NounPhrase n ? l.GenderOf(l.Lexicon.Noun(n.Noun))! : l.Genders.Values[^1];
                var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Present, Aspect.Imperfective, subject), "x");
                var sleep = sentence.Words.Single(w => w.Word.Root?.Meaning == "sleep").Word;

                Assert.Equal(l.Genders.Markers![gender].Form.Count == 0 ? [] : [gender.Gloss], GenderGlosses(sleep));
            }
        });
    }

    [Fact]
    public void Generate_VerbsAgreeInGender_OnlyWhereTheyAgreeWithTheirSubject()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.Genders.VerbsAgree), l =>
            Assert.Contains(l.Paradigms.Verbs.Categories, c => c.Is<Person>()));
    }
}

//what these tests mostly ask: the gender of a noun for what the generator knows of it
internal static class GenderSystemExtensions
{
    public static Gender? Of(this GenderSystem system, Lexeme noun) => system.Of(noun, Semantics.Of(noun.Root.Meaning));
}
