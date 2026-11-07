namespace LanguageGenerator;

/// <summary>
/// One sound of a word's surface form and the morpheme it belongs to. A word never has the same
/// morpheme twice, so that morpheme also says where the sound sits among the word's morphemes.
/// </summary>
public record Segment(Phoneme Phoneme, Morpheme Morpheme);

/// <summary>What a language does with a consonant cluster its syllables can't hold.</summary>
public enum ClusterRepair
{
    //insert a vowel to break the cluster up (kabak-s → kabakəs)
    Epenthesis,
    //drop consonants until the cluster fits (kabak-s → kabas)
    Deletion,
}

/// <summary>
/// How a language's words sound: the inventory, what a syllable may be, and the rules that turn a
/// word's underlying morphemes into its surface form.
/// </summary>
public record Phonology
{
    public required IReadOnlyList<Vowel> Vowels { get; init; }
    public required IReadOnlyList<Consonant> Consonants { get; init; }
    //TODO syllable contact: any legal coda can sit beside any legal onset, so nothing stops a word
    //like as.hoka, which real languages mostly avoid inside a simple word (English has it in compounds
    //like "mishap"). A per-language rule about what meets at a boundary would keep such words out of
    //roots. Suffixes, sound changes and cluster repair make new boundaries too, so it has to decide
    //how far to police those. It leaves less room for roots, which root length will have to measure
    public required SyllableRule SyllableRule { get; init; }
    /// <summary>Applied in order across the whole word, before clusters are repaired.</summary>
    public required IReadOnlyList<SoundChange> SoundChanges { get; init; }
    //TODO stress: syllables don't have any yet. WALS 14A counts fixed stress positions, and 15A what
    //weight does to them. It would bring unstressed vowel reduction and syncope, as more changes here
    //that run after harmony, and the homophone check should then look at inflected forms too
    /// <summary>Changes that depend on syllables, so they apply once a word has them.</summary>
    public IReadOnlyList<SoundChange> SyllableChanges { get; init; } = [];
    public required ClusterRepair ClusterRepair { get; init; }
    public required Vowel EpentheticVowel { get; init; }
    /// <summary>Which syllable of a word is stressed.</summary>
    public Stress Stress { get; init; } = Stress.Penultimate;

    /// <summary>
    /// The consonant in this inventory with these features, and the given one's for any not given.
    /// </summary>
    //aspiration and labialization always carry over, so a pʰ has no voiced counterpart unless the
    //language has a voiced aspirate, and never turns into a plain b
    public Consonant? Counterpart(Consonant consonant, ConsonantPlace? place = null, ConsonantManner? manner = null, bool? voiced = null) =>
        Consonants.FirstOrDefault(c => c.Place == (place ?? consonant.Place) && c.Manner == (manner ?? consonant.Manner) && c.Voiced == (voiced ?? consonant.Voiced)
            && c.Aspirated == consonant.Aspirated && c.Labialized == consonant.Labialized);

    //underlying morphemes, run together → sound changes → cluster repair → syllables and stress →
    //changes that depend on syllables, each followed by repair, syllables and stress again, since it
    //can leave a word that needs them: a syllable lost to syncope moves the stress, as it would in a
    //language that had lost it. Every segment keeps track of its morpheme throughout, so the result
    //can still be segmented
    public SurfaceForm Realize(Word word)
    {
        var segments = new List<Segment>();
        foreach (var morpheme in word.Morphemes)
        {
            foreach (var phoneme in morpheme.Form)
            {
                segments.Add(new Segment(phoneme, morpheme));
            }
        }
        foreach (var change in SoundChanges)
        {
            segments = change.Apply(segments, this);
        }

        var syllables = Repaired(word, ref segments);
        foreach (var change in SyllableChanges)
        {
            segments = change.Apply(segments, this, Places(syllables, this.Stress.StressedSyllable(syllables)));
            syllables = Repaired(word, ref segments);
        }
        return new SurfaceForm(word, segments, syllables) { StressedSyllable = this.Stress.StressedSyllable(syllables) };
    }

    //the word's syllables, once it's been repaired until it has legal ones
    private List<Syllable> Repaired(Word word, ref List<Segment> segments)
    {
        List<Syllable>? syllables;
        for (int repairs = 0; (syllables = Syllabify(segments, out var badCluster)) is null; repairs++)
        {
            //repair always terminates (see Repair); this only guards against a bug in that reasoning
            if (repairs > 100)
            {
                throw new InvalidOperationException($"Couldn't repair /{word.Segmented(Orthography.Ipa)}/ into legal syllables.");
            }
            segments = Repair(segments, badCluster);
        }
        return syllables;
    }

    //where each sound sits, in the order of the word's segments
    private static List<SyllablePlace> Places(IReadOnlyList<Syllable> syllables, int stressed) =>
    [
        .. syllables.SelectMany((syllable, index) =>
        {
            var closed = syllable.Coda is not null;
            var isStressed = index == stressed;
            return syllable.Onset.Select(_ => new SyllablePlace(SyllablePosition.Onset, closed, isStressed))
                .Append(new SyllablePlace(SyllablePosition.Nucleus, closed, isStressed))
                .Concat(closed ? [new SyllablePlace(SyllablePosition.Coda, closed, isStressed)] : []);
        }),
    ];

    //every vowel is a nucleus. Consonants before the first vowel are its onset and those after the
    //last are the final coda; between two vowels, the onset takes as many as it legally can and the
    //coda the rest. Returns null, with the offending cluster's [Start, End), when that's impossible
    private List<Syllable>? Syllabify(List<Segment> segments, out (int Start, int End) badCluster)
    {
        badCluster = default;
        var nuclei = Enumerable.Range(0, segments.Count).Where(i => segments[i].Phoneme is Vowel).ToList();
        if (nuclei.Count == 0)
        {
            throw new InvalidOperationException("A word needs at least one vowel to be syllabified.");
        }

        List<Consonant> ConsonantsBetween(int start, int end) => [.. segments[start..end].Select(s => (Consonant)s.Phoneme)];

        var onsets = new List<Consonant>[nuclei.Count];
        var codas = new List<Consonant>[nuclei.Count];

        onsets[0] = ConsonantsBetween(0, nuclei[0]);
        if (!SyllableRule.IsLegalOnset(onsets[0]))
        {
            badCluster = (0, nuclei[0]);
            return null;
        }
        for (int k = 0; k < nuclei.Count - 1; k++)
        {
            var cluster = ConsonantsBetween(nuclei[k] + 1, nuclei[k + 1]);
            var split = Enumerable.Range(0, cluster.Count + 1)
                .FirstOrDefault(c => SyllableRule.IsLegalCoda(cluster[..c]) && SyllableRule.IsLegalOnset(cluster[c..]), -1);
            if (split < 0)
            {
                badCluster = (nuclei[k] + 1, nuclei[k + 1]);
                return null;
            }
            codas[k] = cluster[..split];
            onsets[k + 1] = cluster[split..];
        }
        codas[^1] = ConsonantsBetween(nuclei[^1] + 1, segments.Count);
        if (!SyllableRule.IsLegalCoda(codas[^1]))
        {
            badCluster = (nuclei[^1] + 1, segments.Count);
            return null;
        }

        //templates allow at most one coda consonant, which legality has already checked
        return [.. nuclei.Select((n, k) => new Syllable([.. onsets[k]], (Vowel)segments[n].Phoneme, codas[k].SingleOrDefault()))];
    }

    //one edit to the bad cluster: the first one that fixes it if any does, and otherwise the first
    //one at all. Every edit leaves shorter clusters behind, so repeating this always ends: a lone
    //consonant between vowels or at either edge is legal as an onset or a coda, or else it's legal
    //nowhere (a coda restriction in a template with no onsets), and then it's deleted
    private List<Segment> Repair(List<Segment> segments, (int Start, int End) cluster)
    {
        var atStart = cluster.Start == 0;
        var atEnd = cluster.End == segments.Count;
        var candidates = new List<(List<Segment> Segments, int ClusterEnd)>();
        if (ClusterRepair == ClusterRepair.Epenthesis)
        {
            //at the edges of the word the vowel can also go outside the cluster
            for (int at = atStart ? cluster.Start : cluster.Start + 1; at <= (atEnd ? cluster.End : cluster.End - 1); at++)
            {
                //an inserted vowel belongs with the morpheme after it, or the last one at the end of a word
                var neighbor = at < segments.Count ? segments[at] : segments[^1];
                candidates.Add(([.. segments[..at], neighbor with { Phoneme = EpentheticVowel }, .. segments[at..]], cluster.End + 1));
            }
        }
        //deletion, or the only fix left for a consonant no vowel can save
        if (candidates.Count == 0)
        {
            for (int i = cluster.Start; i < cluster.End; i++)
            {
                candidates.Add(([.. segments[..i], .. segments[(i + 1)..]], cluster.End - 1));
            }
        }

        //fixed means the word now syllabifies, or at least the first bad cluster is somewhere later
        bool Fixes((List<Segment> Segments, int ClusterEnd) candidate) =>
            Syllabify(candidate.Segments, out var next) is not null || next.Start >= candidate.ClusterEnd;
        return candidates.FirstOrDefault(Fixes, candidates[0]).Segments;
    }
}

/// <summary>A word as it's actually pronounced.</summary>
public record SurfaceForm(Word Word, IReadOnlyList<Segment> Segments, IReadOnlyList<Syllable> Syllables)
{
    public IEnumerable<Phoneme> Phonemes => Segments.Select(s => s.Phoneme);

    /// <summary>The index of the stressed syllable.</summary>
    public int StressedSyllable { get; init; }

    public string Spell(Orthography orthography) => orthography.Spell(Phonemes);
    public string Syllabified(Orthography orthography) => string.Join(".", Syllables.Select(s => s.Spell(orthography)));

    /// <summary>
    /// The IPA by syllable, with ˈ before the stressed one in place of the dot, as in [paˈta.ma]. A
    /// word of one syllable needs no mark.
    /// </summary>
    public string Transcribed() => Syllables.Count == 1
        ? Syllables[0].Spell(Orthography.Ipa)
        : string.Concat(Syllables.Select((s, i) => (i == StressedSyllable ? "ˈ" : i == 0 ? "" : ".") + s.Spell(Orthography.Ipa)));

    //the surface form split back into its morphemes, lining up with Gloss. A morpheme that lost
    //all of its sounds to a sound change or cluster repair shows as ∅
    public string Segmented(Orthography orthography) => string.Join("-", Word.Morphemes.Select(morpheme =>
        Segments.Any(s => s.Morpheme.Equals(morpheme)) ? orthography.Spell(Segments.Where(s => s.Morpheme.Equals(morpheme)).Select(s => s.Phoneme)) : "∅"));

    public string Gloss => Word.Gloss;
}
