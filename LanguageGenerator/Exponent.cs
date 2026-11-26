namespace LanguageGenerator;

/// <summary>Which side of what it marks a marker goes on.</summary>
public enum Side
{
    Before,
    After,
}

/// <summary>
/// How far out a marker goes: to its root, as an affix next to the root or a word right beside
/// the root's word, or to the edge, as an affix outside everything else on the word or a word
/// outside every other word marking it.
/// </summary>
/*
  a passive affix goes to the root, inside tense and agreement, as Turkish gör-ül-dü 'was seen'
  has it, and a negative one to the edge, outside them, as Swahili ha-ku-ona 'didn't see' has it
 */
public enum Reach
{
    Root,
    Edge,
}

/// <summary>A marker as an affix on the word it marks, before or after the root.</summary>
public record Bound(Affix Affix, Side Side)
{
    public Word On(Word word, Reach reach)
    {
        List<Morpheme> morphemes = [.. word.Morphemes];
        var root = morphemes.FindIndex(m => m.Value is Root);
        var at = (Side, reach) switch
        {
            (Side.Before, Reach.Root) => root,
            (Side.After, Reach.Root) => root + 1,
            (Side.Before, Reach.Edge) => 0,
            (Side.After, Reach.Edge) => morphemes.Count,
            _ => throw new ArgumentOutOfRangeException(nameof(reach), (Side, reach), null),
        };
        morphemes.Insert(at, Affix);
        return word with { Morphemes = morphemes };
    }
}

/// <summary>
/// A marker as a word of its own beside the word it marks, leaning on it as a clitic, as Mandarin
/// zài does.
/// </summary>
public record Free(Particle Particle, Side Side);

/// <summary>
/// How a marker is said: an affix on the word it marks or a word of its own beside it, on one side
/// of it.
/// </summary>
public union Exponent(Bound, Free)
{
    public Morpheme Marker => this switch
    {
        Bound b => b.Affix,
        Free f => f.Particle,
    };

    public Side Side => this switch
    {
        Bound b => b.Side,
        Free f => f.Side,
    };

    /// <summary>Where the marker goes, as a paradigm's slots say it.</summary>
    public ExponentPosition Position => this switch
    {
        Bound { Side: Side.Before } => ExponentPosition.Prefix,
        Bound { Side: Side.After } => ExponentPosition.Suffix,
        Free { Side: Side.Before } => ExponentPosition.PrecedingParticle,
        Free { Side: Side.After } => ExponentPosition.FollowingParticle,
        _ => throw new InvalidOperationException($"{Side} isn't a side."),
    };

    /// <summary>Which of a verb's words its markers go on: the verb itself, as opposed to its particles.</summary>
    public static int Host(IReadOnlyList<Word> words) => words.ToList().FindIndex(w => w.Root is not null);

    /// <summary>
    /// The words with this marker on the one at host, or a particle beside it or at the edge of
    /// them all.
    /// </summary>
    public IReadOnlyList<Word> Place(IReadOnlyList<Word> words, int host, Reach reach)
    {
        List<Word> placed = [.. words];
        switch (this)
        {
            case Bound b:
                placed[host] = b.On(placed[host], reach);
                break;
            case Free f:
                var lean = f.Side == Side.Before ? Lean.Forward : Lean.Back;
                var at = (f.Side, reach) switch
                {
                    (Side.Before, Reach.Root) => host,
                    (Side.After, Reach.Root) => host + 1,
                    (Side.Before, Reach.Edge) => 0,
                    (Side.After, Reach.Edge) => placed.Count,
                    _ => throw new ArgumentOutOfRangeException(nameof(reach), (f.Side, reach), null),
                };
                placed.Insert(at, Word.Marker(f.Particle, lean));
                break;
        }
        return placed;
    }

    /// <summary>The marker at the position a paradigm's slots would name, as an affix or a particle.</summary>
    public static Exponent At(ExponentPosition position, IReadOnlyList<Phoneme> form, IReadOnlyList<Grammeme> grammemes, AffixAccent accent) => position switch
    {
        ExponentPosition.Prefix => new Bound(new Affix(form, grammemes, accent), Side.Before),
        ExponentPosition.Suffix => new Bound(new Affix(form, grammemes, accent), Side.After),
        ExponentPosition.PrecedingParticle => new Free(new Particle(form, grammemes), Side.Before),
        ExponentPosition.FollowingParticle => new Free(new Particle(form, grammemes), Side.After),
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
    };
}
