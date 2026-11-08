namespace LanguageGenerator;

/// <summary>
/// A language's passive: a marker on the verb, an affix next to its root, as Turkish -il in
/// gör-ül-dü 'was seen', or in an isolating language a word beside it.
/// </summary>
//WALS 107A counts passives without telling them apart by form, and the generator has no
//participles, so an English-style passive with be and a participle is made this way too
public record Passive(Morpheme Marker, ExponentPosition Position)
{
    /// <summary>The verb's words with the passive marked on them.</summary>
    //voice sits closer to the root than tense or agreement, as it nearly always does
    public IReadOnlyList<Word> Apply(IReadOnlyList<Word> verb)
    {
        List<Word> words = [.. verb];
        var at = words.FindIndex(w => w.Root is not null);
        switch (Position)
        {
            case ExponentPosition.PrecedingParticle:
                words.Insert(at, new Word([Marker]));
                return words;
            case ExponentPosition.FollowingParticle:
                words.Insert(at + 1, new Word([Marker]));
                return words;
        }
        var morphemes = words[at].Morphemes.ToList();
        var root = morphemes.FindIndex(m => m.Value is Root);
        morphemes.Insert(Position == ExponentPosition.Prefix ? root : root + 1, Marker);
        words[at] = words[at] with { Morphemes = morphemes };
        return words;
    }
}

/// <summary>
/// A clause about what's done to its patient, as in 'each light is carried by a small figure', with
/// who does it, if anyone says.
/// </summary>
public record PassiveClause(string Verb, Tense Tense, Aspect Aspect, Argument Patient, Argument? Agent, bool Negative, ValueList<Oblique> Obliques);
