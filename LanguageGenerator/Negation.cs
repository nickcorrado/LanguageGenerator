namespace LanguageGenerator;

/// <summary>How a language negates a clause: what kind of marker, and where it goes.</summary>
//WALS 143A
public enum NegationStrategy
{
    //a word before the verb, like English not or Mandarin bù
    ParticleBefore,
    //a word after the verb, like German nicht in ich sehe ihn nicht
    ParticleAfter,
    //a prefix on the verb, like Swahili ha-
    Prefix,
    //a suffix on the verb, like Turkish -me-
    Suffix,
    //a word on each side of the verb, both required, like French ne ... pas
    Double,
}

/// <summary>How a language says not: its markers, each with where it goes.</summary>
//a prefix or suffix goes outside the verb's other affixes, and a particle outside its other
//particles
public record Negation(NegationStrategy Strategy, IReadOnlyList<Exponent> Markers)
{
    public override string ToString() => Strategy switch
    {
        NegationStrategy.ParticleBefore => "a particle before the verb",
        NegationStrategy.ParticleAfter => "a particle after the verb",
        NegationStrategy.Prefix => "a prefix on the verb",
        NegationStrategy.Suffix => "a suffix on the verb",
        NegationStrategy.Double => "particles before and after the verb",
        _ => Strategy.ToString(),
    };
}
