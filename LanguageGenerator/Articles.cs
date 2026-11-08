namespace LanguageGenerator;

/// <summary>
/// One article: an affix on the noun, as in Swedish hus-et 'the house', or a word at the edge of the
/// noun phrase, as English the is.
/// </summary>
//a word is a Particle, glossed DEF or NDF, or the Root of the language's word for 'one', glossed
//one, where that's the indefinite article
public record Article(Morpheme Marker, ExponentPosition Position);

/// <summary>A language's definite and indefinite articles, either or both of which it may lack.</summary>
//an indefinite article is only for one thing, as the word for 'one' it most often comes from is, so
//'dogs' takes none even where 'a dog' does
public record Articles(Article? Definite, Article? Indefinite)
{
    public Article? For(Definiteness definiteness) => definiteness == Definiteness.Definite ? Definite : Indefinite;

    /// <summary>
    /// A noun phrase's words with its article, if the language has one for it: an affix goes on the
    /// noun, outside its number but inside its case, as in Danish hus-e-ne-s 'house-PL-DEF-GEN', and
    /// a word goes outside everything else in the phrase.
    /// </summary>
    public IReadOnlyList<Word> Apply(IReadOnlyList<Word> phrase, Root noun, Definiteness? definiteness, Number number)
    {
        if (definiteness is null || (definiteness == Definiteness.Indefinite && number != Number.Singular) || For(definiteness) is not { } article)
        {
            return phrase;
        }
        switch (article.Position)
        {
            case ExponentPosition.PrecedingParticle:
                return [new Word([article.Marker]), .. phrase];
            case ExponentPosition.FollowingParticle:
                return [.. phrase, new Word([article.Marker])];
        }

        List<Word> words = [.. phrase];
        var at = words.FindIndex(w => w.Root == noun);
        var morphemes = words[at].Morphemes.ToList();
        static bool IsNumber(Morpheme m) => m.Value is Affix { Grammemes: [Number] };
        var slot = morphemes.FindIndex(m => m.Value is Root);
        if (article.Position == ExponentPosition.Prefix)
        {
            while (slot > 0 && IsNumber(morphemes[slot - 1]))
            {
                slot--;
            }
        }
        else
        {
            slot++;
            while (slot < morphemes.Count && IsNumber(morphemes[slot]))
            {
                slot++;
            }
        }
        morphemes.Insert(slot, article.Marker);
        words[at] = words[at] with { Morphemes = morphemes };
        return words;
    }
}
