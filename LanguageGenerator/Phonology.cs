namespace LanguageGenerator;

//one sound of a word's surface form, and the index of the morpheme it belongs to
public record Segment(Phoneme Phoneme, int Morpheme);

//what a language does with a consonant cluster its syllables can't hold
public enum ClusterRepair
{
    //insert a vowel to break the cluster up (kabak-s → kabakəs)
    Epenthesis,
    //drop consonants until the cluster fits (kabak-s → kabas)
    Deletion,
}

//how a language's words sound: the inventory, what a syllable may be, and the rules that
//turn a word's underlying morphemes into its surface form
public class Phonology
{
    public required List<Vowel> Vowels { get; init; }
    public required List<Consonant> Consonants { get; init; }
    //only one rule now!
    public required SyllableRule SyllableRule { get; init; }
    //applied in order across the whole word, before clusters are repaired
    public required List<SandhiRule> SandhiRules { get; init; }
    public required ClusterRepair ClusterRepair { get; init; }
    public required Vowel EpentheticVowel { get; init; }

    //underlying morphemes, run together → sandhi → cluster repair → syllables. Every
    //segment keeps track of its morpheme throughout, so the result can still be segmented
    public SurfaceForm Realize(Word word)
    {
        var segments = new List<Segment>();
        for (int i = 0; i < word.Morphemes.Count; i++)
        {
            foreach (var phoneme in word.Morphemes[i].Form)
            {
                segments.Add(new Segment(phoneme, i));
            }
        }
        foreach (var rule in SandhiRules)
        {
            segments = rule.Apply(segments, Consonants);
        }

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
        return new SurfaceForm(word, segments, syllables);
    }

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
        return [.. nuclei.Select((n, k) => new Syllable(new ConsonantCluster(onsets[k]), (Vowel)segments[n].Phoneme, codas[k].SingleOrDefault()))];
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
                var morpheme = at < segments.Count ? segments[at].Morpheme : segments[^1].Morpheme;
                candidates.Add(([.. segments[..at], new Segment(EpentheticVowel, morpheme), .. segments[at..]], cluster.End + 1));
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

//a word as it's actually pronounced
public record SurfaceForm(Word Word, IReadOnlyList<Segment> Segments, IReadOnlyList<Syllable> Syllables)
{
    public IEnumerable<Phoneme> Phonemes => Segments.Select(s => s.Phoneme);

    public string Spell(Orthography orthography) => orthography.Spell(Phonemes);
    public string Syllabified(Orthography orthography) => string.Join(".", Syllables.Select(s => s.Spell(orthography)));

    //the surface form split back into its morphemes, lining up with Gloss. A morpheme that lost
    //all of its sounds to sandhi or cluster repair shows as ∅
    public string Segmented(Orthography orthography) => string.Join("-", Enumerable.Range(0, Word.Morphemes.Count).Select(i =>
        Segments.Any(s => s.Morpheme == i) ? orthography.Spell(Segments.Where(s => s.Morpheme == i).Select(s => s.Phoneme)) : "∅"));

    public string Gloss => Word.Gloss;
}
