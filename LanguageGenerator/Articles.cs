namespace LanguageGenerator;

/// <summary>
/// One article: an affix on the noun, as in Swedish hus-et 'the house', or a word at the edge of the
/// noun phrase, as English the is, with a form for each gender and number it agrees in, as Spanish
/// has el, la, los and las.
/// </summary>
//a form is a Particle or an Affix, glossed DEF or NDF and whatever it agrees in, or the Root of the
//language's word for 'one', glossed one, where that's the indefinite article, which agrees in
//nothing. A key's gender or number is null when the article doesn't agree in it
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

/// <summary>A language's definite and indefinite articles, either or both of which it may lack.</summary>
//an indefinite article is only for one thing, as the word for 'one' it most often comes from is, so
//'dogs' takes none even where 'a dog' does
public record Articles(Article? Definite, Article? Indefinite)
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
        var marker = article.For(gender, number);
        switch (article.Position)
        {
            case ExponentPosition.PrecedingParticle:
                return [new Word([marker]), .. phrase];
            case ExponentPosition.FollowingParticle:
                return [.. phrase, new Word([marker])];
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
        morphemes.Insert(slot, marker);
        words[at] = words[at] with { Morphemes = morphemes };
        return words;
    }
}
