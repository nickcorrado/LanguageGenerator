using static LanguageGenerator.Tests.Sentences;

namespace LanguageGenerator.Tests;

public class NameTests
{
    //what a language calls itself is the word it uses in 'the people slept', where an intransitive
    //subject takes the case nouns are cited in
    [Fact]
    public void Name_AnyLanguage_IsItsWordForPeopleCapitalized()
    {
        Assert.All(GeneratedLanguages.Sample, l =>
        {
            var sentence = Syntax.Realize(l, new Clause("sleep", Tense.Past, Aspect.Perfective, new NounPhrase("person", Number.Plural)), "the people slept");
            var people = sentence.Words[IndexOf(sentence, "person")].Spell(l.Orthography);

            Assert.Equal(people, char.ToLowerInvariant(l.Name[0]) + l.Name[1..]);
            Assert.False(char.IsLower(l.Name[0]));
        });
    }

    [Fact]
    public void Name_WhereNounsMarkThePlural_IsPlural()
    {
        var marking = GeneratedLanguages.Sample.Where(l => l.Paradigms[PartOfSpeech.Noun].Categories.Has<Number>()
            && l.Paradigms[PartOfSpeech.Noun].Slots.Any(s => s.Categories.Has<Number>() && s.Position is ExponentPosition.Prefix or ExponentPosition.Suffix)).ToList();

        Assert.NotEmpty(marking);
        Assert.All(marking, l => Assert.Contains("PL", l.Endonym.Gloss.Split('-', '.', '[', ']')));
    }
}
