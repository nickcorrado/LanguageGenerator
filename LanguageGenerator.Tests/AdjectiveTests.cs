using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class AdjectiveTests
{
    private static readonly NounPhrase BigOldDogs = new("dog", Number.Plural, [new("big"), new("old")]);

    //only the basic meanings, since coining new ones would add to the shared sample's lexicons
    private static Sentence Sleep(Language l, NounPhrase subject) =>
        Syntax.Realize(l, new Clause("sleep", Tense.Present, Aspect.Imperfective, subject), "the big old dogs are sleeping");

    private static Word WordOf(Sentence sentence, string meaning) => sentence.Words[IndexOf(sentence, meaning)].Word;

    //a word's affixes, by gloss, on either side of its root, as in PL-ACC
    private static (string Prefixes, string Suffixes) Affixes(Word word)
    {
        var root = word.Morphemes.ToList().FindIndex(m => m.Value is Root);
        return (string.Join("-", word.Morphemes.Take(root).Select(m => m.Gloss)), string.Join("-", word.Morphemes.Skip(root + 1).Select(m => m.Gloss)));
    }

    [Fact]
    public void SelectAdjectiveAgreement_VerbalAdjectives_NeverAgree()
    {
        var rand = new Random(1);
        var verbal = GeneratedLanguages.Sample.First(l => l.Predication.Adjectives == PredicativeAdjectives.Verbal).Predication;
        var nonverbal = GeneratedLanguages.Sample.First(l => l.Predication.Adjectives == PredicativeAdjectives.Nonverbal).Predication;

        var verbalDraws = Enumerable.Range(0, 2000).Select(_ => Generator.SelectAdjectiveAgreement(rand, verbal)).ToList();
        var nonverbalDraws = Enumerable.Range(0, 2000).Select(_ => Generator.SelectAdjectiveAgreement(rand, nonverbal)).ToList();

        Assert.DoesNotContain(AdjectiveAgreement.Agreement, verbalDraws);
        Assert.Equal(3, nonverbalDraws.Distinct().Count());
    }

    [Fact]
    public void Realize_Adjectives_GoOnTheNounPhrasesHeadSideInTheirGivenOrder()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var sentence = Sleep(language, BigOldDogs);
            var (dog, big, old) = (IndexOf(sentence, "dog"), IndexOf(sentence, "big"), IndexOf(sentence, "old"));

            Assert.True(big < old);
            if (language.WordOrder.NounPhraseDirectionality == HeadDirectionality.Initial)
            {
                Assert.Equal([dog, dog + 1, dog + 2], [dog, big, old]);
            }
            else
            {
                Assert.Equal([dog - 2, dog - 1, dog], [big, old, dog]);
            }
        }
    }

    [Fact]
    public void Realize_NounWithoutAdjectives_IsUnchanged()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
            Assert.Equal(Sleep(l, new NounPhrase("dog", Number.Plural)).Text, Sleep(l, BigOldDogs with { Adjectives = [] }).Text));
    }

    [Fact]
    public void Realize_AgreeingAdjectives_TakeTheNounsAffixes()
    {
        var agreeing = GeneratedLanguages.Sample.Where(l => l.AdjectiveAgreement == AdjectiveAgreement.Agreement).ToList();

        Assert.NotEmpty(agreeing);
        Assert.All(agreeing, l =>
        {
            //besides any gender marker, which GenderTests checks
            var sentence = Sleep(l, BigOldDogs);
            Word WithoutGender(Word w) => w with { Morphemes = [.. w.Morphemes.Where(m => m.Value is not Affix { Tagmemes: [Gender] })] };
            Assert.Equal(Affixes(WordOf(sentence, "dog")), Affixes(WithoutGender(WordOf(sentence, "big"))));
            Assert.Equal(Affixes(WordOf(sentence, "dog")), Affixes(WithoutGender(WordOf(sentence, "old"))));
        });
    }

    [Fact]
    public void Realize_NonagreeingAdjectives_AreBare()
    {
        Assert.All(GeneratedLanguages.Sample.Where(l => l.AdjectiveAgreement == AdjectiveAgreement.None), l =>
        {
            var sentence = Sleep(l, BigOldDogs);
            Assert.Single(WordOf(sentence, "big").Morphemes);
            Assert.Single(WordOf(sentence, "old").Morphemes);
            //and the noun keeps what it would have had alone
            Assert.Equal(Affixes(WordOf(Sleep(l, new NounPhrase("dog", Number.Plural)), "dog")), Affixes(WordOf(sentence, "dog")));
        });
    }

    [Fact]
    public void Realize_PhrasalMarking_MovesTheNounsAffixesToThePhrasesEdges()
    {
        var phrasal = GeneratedLanguages.Sample.Where(l => l.AdjectiveAgreement == AdjectiveAgreement.Phrasal).ToList();

        Assert.NotEmpty(phrasal);
        Assert.All(phrasal, l =>
        {
            var alone = WordOf(Sleep(l, new NounPhrase("dog", Number.Plural)), "dog");
            var (prefixes, suffixes) = Affixes(alone);
            var sentence = Sleep(l, BigOldDogs);
            var phrase = new[] { "dog", "big", "old" }.Select(m => IndexOf(sentence, m)).Order().Select(i => sentence.Words[i].Word).ToList();

            Assert.Equal(prefixes, Affixes(phrase[0]).Prefixes);
            Assert.Equal(suffixes, Affixes(phrase[^1]).Suffixes);
            Assert.Equal(alone.Morphemes.Count - 1, phrase.Sum(w => w.Morphemes.Count - 1));
        });
    }

    [Fact]
    public void Realize_ParticlesOfTheNoun_StayOutsideTheWholePhrase()
    {
        //in a language with case or number particles, nothing comes between the noun and its adjectives
        var withParticles = GeneratedLanguages.Sample.Where(l => l.Paradigms[PartOfSpeech.Noun].Slots.Any(s => s.Position is ExponentPosition.PrecedingParticle or ExponentPosition.FollowingParticle)).ToList();

        Assert.NotEmpty(withParticles);
        Assert.All(withParticles, l =>
        {
            var sentence = Sleep(l, BigOldDogs);
            var positions = new[] { "dog", "big", "old" }.Select(m => IndexOf(sentence, m)).Order().ToList();
            Assert.Equal(positions[0] + 2, positions[2]);
        });
    }

    [Fact]
    public void Realize_ModifiedNounPhrases_FollowThePhonology()
    {
        foreach (var language in GeneratedLanguages.Sample)
        {
            var rule = language.Phonology.SyllableRule;
            Assert.All(Sleep(language, BigOldDogs).Words.SelectMany(w => w.Syllables), s =>
            {
                Assert.True(rule.IsLegalOnset([.. s.Onset]));
                Assert.True(rule.IsLegalCoda(s.Coda is null ? [] : [s.Coda]));
            });
        }
    }
}
