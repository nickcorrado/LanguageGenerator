namespace LanguageGenerator;

//coordination
public static partial class Generator
{
    //WALS 63A: 'and' between nouns is the word for 'with' in 103 of 234 languages
    private static readonly (bool Value, int Languages)[] AndIsWithCounts = [(true, 103), (false, 131)];

    private enum ClauseAndType { SameAsNouns, OwnWord, Juxtaposition }

    //WALS 64A: of 301 languages, 161 join nouns and clauses alike, 125 differently, and 15 set both
    //side by side. Where 'and' between nouns is 'with', clauses get their own word or none, since
    //'with' doesn't join clauses
    private static readonly (ClauseAndType Value, int Languages)[] ClauseAndCounts =
        [(ClauseAndType.SameAsNouns, 161), (ClauseAndType.OwnWord, 125), (ClauseAndType.Juxtaposition, 15)];

    //short words of their own for 'and', 'or' and 'but', glossed in English and kept from sounding
    //alike. Nearly every language has a word for 'or' and one for 'but', so every language here does
    public static Conjunctions GenerateConjunctions(Random rand, Phonology phonology, double dropoff)
    {
        var andIsWith = Sampling.Categorical(rand, AndIsWithCounts);
        var clauseAndType = Sampling.Categorical(rand, ClauseAndCounts);
        var forms = new DistinctForms();
        Lexeme Word(string meaning) =>
            new(new Root(forms.Draw(() => GenerateForm(rand, phonology, dropoff, 0, maxSyllables: 1)), meaning), PartOfSpeech.Conjunction);

        //where clauses are set side by side, nouns are too, unless they're joined with 'with'
        NounAnd nounAnd = andIsWith ? new AndWith()
            : clauseAndType == ClauseAndType.Juxtaposition ? new AndJuxtaposed()
            : new AndWord(Word("and"));
        var clauseAnd = clauseAndType switch
        {
            ClauseAndType.Juxtaposition => null,
            ClauseAndType.SameAsNouns when nounAnd.Value is AndWord shared => shared.Word,
            _ => Word("and"),
        };
        return new Conjunctions(nounAnd, clauseAnd, Word("or"), Word("but"));
    }
}
