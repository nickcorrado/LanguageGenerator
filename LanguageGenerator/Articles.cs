namespace LanguageGenerator;

/// <summary>What a language's definite article is, if it has one.</summary>
//WALS 37A
public enum DefiniteArticle
{
    None,
    //a word of its own, like English the
    Word,
    //the distal demonstrative, or the only one, used as an article too, as in 69 of the languages
    //WALS counts, so its forms are the demonstrative's
    Demonstrative,
    //an affix on the noun, like Swedish hus-et 'the house'
    Affix,
}

/// <summary>What a language's indefinite article is, if it has one.</summary>
//WALS 38A
public enum IndefiniteArticle
{
    None,
    //a word of its own, apart from 'one'
    Word,
    //the word for 'one', as German ein is
    One,
    //an affix on the noun
    Affix,
}

/// <summary>
/// One article: an affix on the noun, as in Swedish hus-et 'the house', or a word at the edge of the
/// noun phrase, as English the is, with a form for each gender and number it agrees in, as Spanish
/// has el, la, los and las.
/// </summary>
/*
  a form is a Particle or an Affix, glossed DEF or NDF and whatever it agrees in, or the Root of the
  language's word for 'one', glossed one, where that's the indefinite article, which agrees in
  nothing. A key's gender or number is null when the article doesn't agree in it
 */
public record Article(ExponentPosition Position, IReadOnlyDictionary<(Gender? Gender, Number? Number), Morpheme> Forms)
{
    public bool AgreesInGender => Forms.Keys.Any(k => k.Gender is not null);
    public bool AgreesInNumber => Forms.Keys.Any(k => k.Number is not null);

    private static readonly Number[] Numbers = [Number.Singular, Number.Plural];

    /// <summary>The form for a noun of this gender and number.</summary>
    //a dual or paucal falls back on the plural, as pronouns' numbers do
    public Morpheme For(Gender? gender, Number number) => Forms[(
        AgreesInGender ? gender : null,
        AgreesInNumber ? (Number)(Syntax.Nearest(number, Numbers) ?? Number.Plural) : null)];
}

/// <summary>
/// A language's definite and indefinite articles, either or both of which it may lack, whether its
/// definite article is its demonstrative, and whether its definite article stays beside a
/// demonstrative, as Greek aftó to spíti 'this the house' keeps it and English drops it.
/// </summary>
//an indefinite article is only for one thing, as the word for 'one' it most often comes from is, so
//'dogs' takes none even where 'a dog' does
public record Articles(Article? Definite, Article? Indefinite, bool DefiniteFromDemonstrative, bool WithDemonstrative)
{
    public Article? For(Definiteness definiteness) => definiteness == Definiteness.Definite ? Definite : Indefinite;

    /// <summary>
    /// A noun phrase's words with its article, if the language has one for it, agreeing with the
    /// noun: an affix goes on the noun, outside its number but inside its case, as in Danish
    /// hus-e-ne-s 'house-PL-DEF-GEN', and a word goes outside everything else in the phrase.
    /// </summary>
    //the article agrees with how many things there are, not with the noun's own form, which some
    //languages leave singular after a numeral
    public IReadOnlyList<Word> Apply(IReadOnlyList<Word> phrase, Root noun, Definiteness? definiteness, Number number, Gender? gender)
    {
        if (definiteness is null || (definiteness == Definiteness.Indefinite && number != Number.Singular) || For(definiteness) is not { } article)
        {
            return phrase;
        }
        return Mark(phrase, noun, article.For(gender, number), article.Position);
    }

    /*
      a noun phrase's words with a marker of the whole phrase: a word outside everything else in it,
      or an affix on the noun, outside its number and an article's affix but inside its case, so a
      demonstrative's affix goes outside an article's
     */
    public static IReadOnlyList<Word> Mark(IReadOnlyList<Word> phrase, Root noun, Morpheme marker, ExponentPosition position)
    {
        switch (position)
        {
            case ExponentPosition.PrecedingParticle:
                return [Word.Marker(marker, Lean.Forward), .. phrase];
            case ExponentPosition.FollowingParticle:
                return [.. phrase, Word.Marker(marker, Lean.Back)];
        }

        List<Word> words = [.. phrase];
        var at = words.FindIndex(w => w.Root == noun);
        var morphemes = words[at].Morphemes.ToList();
        static bool IsInner(Morpheme m) => m.Value is Affix { Grammemes: [Number] } or Affix { Grammemes: [Definiteness, ..] };
        var slot = morphemes.FindIndex(m => m.Value is Root);
        if (position == ExponentPosition.Prefix)
        {
            while (slot > 0 && IsInner(morphemes[slot - 1]))
            {
                slot--;
            }
        }
        else
        {
            slot++;
            while (slot < morphemes.Count && IsInner(morphemes[slot]))
            {
                slot++;
            }
        }
        morphemes.Insert(slot, marker);
        words[at] = words[at] with { Morphemes = morphemes };
        return words;
    }
}
