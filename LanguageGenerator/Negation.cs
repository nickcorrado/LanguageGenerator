namespace LanguageGenerator;

//WALS 143A: how a language negates a clause, by what kind of marker and where it goes
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

//how a language says not: its markers, each with where it goes. A prefix or suffix goes outside
//the verb's other affixes, and a particle outside its other particles
public record Negation(NegationStrategy Strategy, IReadOnlyList<(ExponentPosition Position, Morpheme Marker)> Markers)
{
    //the verb's words, negated
    public IReadOnlyList<Word> Apply(IReadOnlyList<Word> verb)
    {
        var words = verb.ToList();
        //the verb itself, as opposed to its particles
        var main = words.FindIndex(w => w.Root is not null);
        foreach (var (position, marker) in Markers)
        {
            switch (position)
            {
                case ExponentPosition.Prefix:
                    words[main] = words[main] with { Morphemes = [marker, .. words[main].Morphemes] };
                    break;
                case ExponentPosition.Suffix:
                    words[main] = words[main] with { Morphemes = [.. words[main].Morphemes, marker] };
                    break;
                case ExponentPosition.PrecedingParticle:
                    words.Insert(0, new Word([marker]));
                    main++;
                    break;
                case ExponentPosition.FollowingParticle:
                    words.Add(new Word([marker]));
                    break;
            }
        }
        return words;
    }

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
