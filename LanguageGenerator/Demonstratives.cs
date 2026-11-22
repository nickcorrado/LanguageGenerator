namespace LanguageGenerator;

/// <summary>How many distances a language's demonstratives tell apart, as WALS 41A counts them.</summary>
public enum DistanceContrast
{
    /// <summary>None, as French ce is both 'this' and 'that'.</summary>
    None,
    /// <summary>Near and far, as English this and that.</summary>
    TwoWay,
    /// <summary>
    /// Near, middle and far, as Spanish este, ese and aquel, or near me, near you and away from us
    /// both, as Japanese ko-, so- and a-.
    /// </summary>
    ThreeWay,
}

/// <summary>A demonstrative standing for a noun, as 'that' does in 'where did you get that?'.</summary>
public record DemonstrativePronoun(Distance Distance, Number Number);

/// <summary>
/// A demonstrative saying a place, as 'here' and 'there' do, in the role of where, to where or from
/// where, as a question's 'where' is.
/// </summary>
public record DemonstrativePlace(Distance Distance);

/// <summary>
/// What a demonstrative standing alone is, for each distance: the word that goes beside a noun,
/// standing as a noun itself, as English this is both, or a stem of its own, as Hungarian has
/// adnominal ez a ház 'this the house' beside a pronoun inflected like a noun.
/// </summary>
//both are made either way, so that a spec can choose
public record PronominalDemonstratives(bool OwnStem, IReadOnlyDictionary<Distance, Lexeme> Adnominal, IReadOnlyDictionary<Distance, Lexeme> Own)
{
    public Lexeme For(Distance distance) => (OwnStem ? Own : Adnominal)[distance];
}

/// <summary>
/// What a language's words for 'here' and 'there' are: its demonstratives standing alone, marked as
/// a place is, as Japanese koko-ni and Turkish bura-da are 'this place' in the locative, or words of
/// their own, as English here and there are.
/// </summary>
//the words of their own are made either way, so that a spec can choose
public record PlaceDemonstratives(bool FromDemonstratives, IReadOnlyDictionary<Distance, Lexeme> Own);

/// <summary>
/// A language's demonstratives: how many distances they tell apart, and for each a word at the edge
/// of the noun phrase or an affix on the noun, with a form for each gender and number it agrees in,
/// as Spanish has este, esta, estos and estas; what stands alone for a noun; and its words for
/// 'here' and 'there'.
/// </summary>
/*
  a form is a Particle or an Affix glossed with its distance and whatever it agrees in, keyed by
  distance, and by gender or number where it agrees in them, which are null where it doesn't
 */
public record Demonstratives(DistanceContrast Contrast, ExponentPosition Position, IReadOnlyDictionary<(Distance Distance, Gender? Gender, Number? Number), Morpheme> Forms,
    PronominalDemonstratives Pronouns, PlaceDemonstratives Places)
{
    public IReadOnlyList<Distance> Distances => DistancesOf(Contrast);

    public static IReadOnlyList<Distance> DistancesOf(DistanceContrast contrast) => contrast switch
    {
        DistanceContrast.None => [Distance.Neutral],
        DistanceContrast.TwoWay => [Distance.Proximal, Distance.Distal],
        DistanceContrast.ThreeWay => [Distance.Proximal, Distance.Medial, Distance.Distal],
        _ => throw new ArgumentOutOfRangeException(nameof(contrast), contrast, null),
    };

    public bool AgreesInGender => Forms.Keys.Any(k => k.Gender is not null);
    public bool AgreesInNumber => Forms.Keys.Any(k => k.Number is not null);
    public bool IsAffix => Position is ExponentPosition.Prefix or ExponentPosition.Suffix;

    private static readonly Number[] Numbers = [Number.Singular, Number.Plural];

    /// <summary>The distance the language says for this one, which falls back as Syntax.Nearest has it.</summary>
    public Distance Nearest(Distance wanted) => (Distance)Syntax.Nearest(wanted, [.. Distances])!;

    /// <summary>The form beside a noun of this gender and number.</summary>
    //a dual or paucal falls back on the plural, as articles' numbers do
    public Morpheme For(Distance distance, Gender? gender, Number number) => Forms[(
        Nearest(distance),
        AgreesInGender ? gender : null,
        AgreesInNumber ? (Number)(Syntax.Nearest(number, Numbers) ?? Number.Plural) : null)];

    /// <summary>The demonstrative standing alone for a noun.</summary>
    public Lexeme Pronoun(Distance distance) => Pronouns.For(Nearest(distance));

    /// <summary>The word for a place at this distance, as 'here' is for the proximal.</summary>
    public Lexeme Place(Distance distance) => Places.FromDemonstratives ? Pronoun(distance) : Places.Own[Nearest(distance)];

    /// <summary>
    /// A noun phrase's words with its demonstrative, agreeing with the noun: an affix goes on the
    /// noun, outside its number and any article affix but inside its case, and a word goes outside
    /// everything else in the phrase, an article word included, as Greek aftó to spíti 'this the
    /// house' has it.
    /// </summary>
    public IReadOnlyList<Word> Apply(IReadOnlyList<Word> phrase, Root noun, Distance distance, Number number, Gender? gender) =>
        Articles.Mark(phrase, noun, For(distance, gender, number), Position);
}
