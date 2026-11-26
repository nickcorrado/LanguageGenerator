namespace LanguageGenerator;

/// <summary>
/// One sound of a word's surface form and the morpheme it belongs to. A word never has the same
/// morpheme twice, so that morpheme also says where the sound sits among the word's morphemes.
/// Accented marks a vowel a morpheme's accent falls on, where stress is free: a root's or affix's
/// own, or the one before an affix that stresses it.
/// </summary>
/*
  Phoneme is the sound as it's said, and Realizes the phoneme it's said for: the same, unless a
  sound change made it an allophone, a sound the language has only as a form of one of its
  phonemes, as Spanish says the b of lobo as β. A sound change that makes another of the
  language's phonemes merges the two, so the sound realizes that one. Spelling and syllables go by
  the phoneme, so an allophone is written as its phoneme is and fills the slots its phoneme does.
  A silent sound is one a language's final consonant loss took from what's said, which is still
  spelled and may be said again before a vowel (see Phonology.Silenced)
 */
public record Segment(Phoneme Phoneme, Phoneme Realizes, Morpheme Morpheme, bool Accented, bool Silent);

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
    public required SyllableRule SyllableRule { get; init; }
    /// <summary>
    /// What may meet across a syllable boundary. Roots keep to it; where morphemes meet, only a
    /// contact repair among the syllable changes enforces it.
    /// </summary>
    public required SyllableContact SyllableContact { get; init; }
    /// <summary>Applied in order across the whole word, before clusters are repaired.</summary>
    public required IReadOnlyList<SoundChange> SoundChanges { get; init; }
    /// <summary>Changes that depend on syllables, so they apply once a word has them.</summary>
    public IReadOnlyList<SoundChange> SyllableChanges { get; init; } = [];
    public required ClusterRepair ClusterRepair { get; init; }
    public required Vowel EpentheticVowel { get; init; }
    /// <summary>Which syllable of a word is stressed, by rule or as its root stores it.</summary>
    public required StressSystem Stress { get; init; }
    /// <summary>
    /// How steeply sounds get rarer down the inventory, which is ordered commonest first, as every
    /// word and affix is coined.
    /// </summary>
    public required Dropoff Dropoff { get; init; }
    /// <summary>
    /// The sounds outside the inventory its sound changes may make, as allophones of its phonemes:
    /// those the spec doesn't forbid, and those no language has as phonemes.
    /// </summary>
    public required IReadOnlyList<Phoneme> Allophones { get; init; }
    /// <summary>What happens to sounds where words meet in a sentence.</summary>
    public required Sandhi Sandhi { get; init; }

    /// <summary>Whether a sound change may make this sound, as a phoneme or an allophone.</summary>
    public bool CanSay(Phoneme sound) => sound switch
    {
        Vowel vowel => Vowels.Contains(vowel) || Allophones.Contains(vowel),
        Consonant consonant => Consonants.Contains(consonant) || Allophones.Contains(consonant),
    };

    /// <summary>
    /// The consonant with these features, and the given one's for any not given, that a sound change
    /// may make: a phoneme if there is one, and otherwise an allophone.
    /// </summary>
    //aspiration and labialization always carry over, so a pʰ has no voiced counterpart unless the
    //language may say a voiced aspirate, and never turns into a plain b
    public Consonant? Counterpart(Consonant consonant, ConsonantPlace? place = null, ConsonantManner? manner = null, bool? voiced = null) =>
        Consonants.Concat(Allophones.OfType<Consonant>()).FirstOrDefault(c => c.Place == (place ?? consonant.Place) && c.Manner == (manner ?? consonant.Manner)
            && c.Voiced == (voiced ?? consonant.Voiced) && c.Aspirated == consonant.Aspirated && c.Labialized == consonant.Labialized);

    /// <summary>
    /// A segment said as this sound instead: merged into that phoneme if the language has it, and
    /// otherwise an allophone of the phoneme it already realized.
    /// </summary>
    public Segment Changed(Segment segment, Phoneme sound)
    {
        if (IsPhoneme(sound))
        {
            return segment with { Phoneme = sound, Realizes = sound };
        }
        if (!CanSay(sound) || sound.GetType() != segment.Realizes.GetType())
        {
            throw new ArgumentException($"/{segment.Realizes.IpaSymbol}/ can't be said as [{sound.IpaSymbol}].", nameof(sound));
        }
        return segment with { Phoneme = sound };
    }

    /// <summary>Whether this sound is one of the language's phonemes.</summary>
    public bool IsPhoneme(Phoneme sound) => sound switch
    {
        Vowel vowel => Vowels.Contains(vowel),
        Consonant consonant => Consonants.Contains(consonant),
    };

    /*
      underlying morphemes, run together → sound changes → cluster repair → syllables and stress →
      changes that depend on syllables, each followed by repair, syllables and stress again, since it
      can leave a word that needs them: a syllable lost to syncope moves stress that falls by rule, as
      it would in a language that had lost it, though a stored accent stays on its vowel. Every
      segment keeps track of its morpheme throughout, so the result can still be segmented. Last, a
      language that loses final consonants silences them (see Silenced)
     */
    public SurfaceForm Realize(Word word) => Realize([word], 0);

    /*
      a word with a root and the clitics leaning on it, said as one word, which is how a clitic comes
      to lose its vowel before a host that starts with one, as French le ami is l'ami, or to take the
      host's harmony, as Turkish question mI does. The host is the one with a root
     */
    private SurfaceForm Realize(IReadOnlyList<Word> words, int host)
    {
        var word = words.Count == 1 ? words[0] : new Word([.. words.SelectMany(w => w.Morphemes)], words[host].Covert);
        var hostMorphemes = words[host].Morphemes;
        var segments = new List<Segment>();
        foreach (var morpheme in word.Morphemes)
        {
            var accent = morpheme.Value switch
            {
                Root root => root.Accent ?? -1,
                Affix affix => affix.Accent.Kind == AffixAccentKind.Own ? affix.Accent.Vowel : -1,
                Particle => -1,
            };
            //an affix that stresses the syllable before it marks the last vowel before it, if any
            if (morpheme.Value is Affix { Accent.Kind: AffixAccentKind.Before } && segments.FindLastIndex(s => s.Phoneme is Vowel) is >= 0 and var before)
            {
                segments[before] = segments[before] with { Accented = true };
            }
            var vowels = 0;
            foreach (var phoneme in morpheme.Form)
            {
                segments.Add(new Segment(phoneme, phoneme, morpheme, phoneme is Vowel && vowels++ == accent, false));
            }
        }
        foreach (var change in SoundChanges)
        {
            segments = change.Apply(segments, this);
        }

        var syllables = Repaired(word, ref segments);
        foreach (var change in SyllableChanges)
        {
            segments = change.Apply(segments, this, Places(syllables, StressedSyllable(word, hostMorphemes, segments, syllables)));
            syllables = Repaired(word, ref segments);
        }
        var stressed = StressedSyllable(word, hostMorphemes, segments, syllables);
        //marked against the syllables before any consonant is silenced, since it's still spelled
        var marked = stressed is { } s && s != Stress.Rule.StressedSyllable(syllables);
        if (Sandhi.FinalConsonantLoss && Silenced(words, segments) is var silenced && silenced.Any(x => x.Silent))
        {
            segments = silenced;
            syllables = Syllabify(Said(segments), out _)!;
        }
        return new SurfaceForm(word, segments, syllables) { Words = words, StressedSyllable = stressed, StressMarked = marked, LinksToNext = false };
    }

    /*
      the syllable of the first accented vowel if there is one, since every vowel is a nucleus, so
      the leftmost accent among the morphemes wins, as in Russian, and otherwise the rule's. A word
      without a root has no stress, so null. Where clitics keep the stress of their host, as Spanish
      dígamelo keeps díga's, only the host's syllables count, and the rule applies to them alone
     */
    private int? StressedSyllable(Word word, IReadOnlyList<Morpheme> host, List<Segment> segments, List<Syllable> syllables)
    {
        if (word.Root is null)
        {
            return null;
        }
        var nuclei = segments.Where(s => s.Phoneme is Vowel).ToList();
        List<int> own = Sandhi.CliticStress switch
        {
            CliticStress.Group => [],
            CliticStress.Host => [.. Enumerable.Range(0, nuclei.Count).Where(k => host.Contains(nuclei[k].Morpheme))],
            _ => throw new InvalidOperationException($"No stress for clitics like {Sandhi.CliticStress}."),
        };
        //a word alone is all host, and a host left with no vowel of its own is stressed as a whole
        var (start, end) = own.Count > 0 ? (own[0], own[^1] + 1) : (0, nuclei.Count);
        var accented = Enumerable.Range(start, end - start).FirstOrDefault(k => nuclei[k].Accented, -1);
        return accented >= 0 ? accented : start + Stress.Rule.StressedSyllable(syllables[start..end]);
    }

    //the sounds that are said, without those a language's final consonant loss silenced
    private static List<Segment> Said(IEnumerable<Segment> segments) => [.. segments.Where(s => !s.Silent)];

    /*
      final consonant loss: a word's last sound is silenced where it's an obstruent after a vowel of
      the same word and no vowel follows it, as French lost its final consonants but kept them before
      a vowel, so petit is said pəti and petit ami pətitami. A silenced sound is still spelled, as
      French spells it, and a vowel starting the next word says it again (see RealizePhrase). Inside
      a host and its clitics, the vowel after it is already known; at the end, it's taken as a pause.
      It's done only where the word still makes syllables without it, which it always should, since
      a lone consonant leaving a coda only shortens it
     */
    private List<Segment> Silenced(IReadOnlyList<Word> words, List<Segment> segments)
    {
        var result = segments.ToList();
        foreach (var word in words)
        {
            var last = result.FindLastIndex(s => word.Morphemes.Contains(s.Morpheme));
            if (last < 1 || result[last].Phoneme is not Consonant { IsObstruent: true }
                || result[last - 1].Phoneme is not Vowel || !word.Morphemes.Contains(result[last - 1].Morpheme)
                || last + 1 < result.Count && result[last + 1].Phoneme is Vowel)
            {
                continue;
            }
            List<Segment> silenced = [.. result[..last], result[last] with { Silent = true }, .. result[(last + 1)..]];
            if (Syllabify(Said(silenced), out _) is not null)
            {
                result = silenced;
            }
        }
        return result;
    }

    /// <summary>
    /// A sentence's words as they're said together: each word with a root and the clitics that lean
    /// on it said as one word, and then, where those meet, a silenced consonant said again before a
    /// vowel and the sound changes the language makes across words.
    /// </summary>
    public IReadOnlyList<SurfaceForm> RealizePhrase(IReadOnlyList<Word> words)
    {
        foreach (var word in words)
        {
            if ((word.Root is null) != (word.Leans is not null))
            {
                throw new ArgumentException(word.Root is null
                    ? $"The word {word.Gloss} has no root, so it has to lean on a word next to it."
                    : $"The word {word.Gloss} has a root, so it can't lean on another.", nameof(words));
            }
        }
        var forms = Groups(words).Select(g => Realize(g.Words, g.Host)).ToList();
        //liaison: a silenced consonant is said again before a vowel, as the onset it gives it
        for (int i = 0; i + 1 < forms.Count; i++)
        {
            if (forms[i].Segments is [.., { Silent: true }] && forms[i + 1].Segments.First(s => !s.Silent).Phoneme is Vowel)
            {
                List<Segment> segments = [.. forms[i].Segments.SkipLast(1), forms[i].Segments[^1] with { Silent = false }];
                if (Syllabify(Said(segments), out _) is { } syllables)
                {
                    forms[i] = forms[i] with { Segments = segments, Syllables = syllables, LinksToNext = true };
                }
            }
        }
        return AcrossWords(forms);
    }

    /*
      each word with a root and the clitics that lean on it next to it, the host's index among them,
      and each clitic with nothing to lean on, which stands alone, unstressed. A run of clitics leans
      together, as Spanish me lo dice puts both before the verb. Words whose group would have a
      morpheme twice stand alone, since a word never has the same morpheme twice
     */
    private static List<(IReadOnlyList<Word> Words, int Host)> Groups(IReadOnlyList<Word> words)
    {
        var groups = new List<(IReadOnlyList<Word> Words, int Host)>();
        for (int i = 0; i < words.Count;)
        {
            var host = i;
            while (host < words.Count && words[host].Root is null && words[host].Leans == Lean.Forward)
            {
                host++;
            }
            if (host == words.Count || words[host].Root is null)
            {
                groups.Add(([words[i]], 0));
                i++;
                continue;
            }
            var end = host + 1;
            while (end < words.Count && words[end].Root is null && words[end].Leans == Lean.Back)
            {
                end++;
            }
            var group = words.Skip(i).Take(end - i).ToList();
            var morphemes = group.SelectMany(w => w.Morphemes).ToList();
            if (morphemes.Distinct().Count() == morphemes.Count)
            {
                groups.Add((group, host - i));
            }
            else
            {
                groups.AddRange(group.Select(w => ((IReadOnlyList<Word>)[w], 0)));
            }
            i = end;
        }
        return groups;
    }

    /*
      the language's changes across words, each over the whole sentence, kept only where it differs
      from the change applied to each word alone, so only what the meeting of words brings about
      counts. They change only how a sound is said: what it realizes, and so how it's spelled, stays
      as the word has it alone, since spelling keeps a word the same wherever it stands. Silenced
      sounds take no part, so a vowel and the consonant after a silenced one are neighbors
     */
    private List<SurfaceForm> AcrossWords(List<SurfaceForm> forms)
    {
        var said = forms.Select(f => Said(f.Segments)).ToList();
        foreach (var change in Sandhi.AcrossWords)
        {
            var together = change.Apply([.. said.SelectMany(s => s)], this);
            var alone = said.SelectMany(s => change.Apply(s, this)).ToList();
            if (together.Count != alone.Count || together.Count != said.Sum(s => s.Count))
            {
                throw new InvalidOperationException($"{change} changed how many sounds a sentence has, which no change across words may.");
            }
            var at = 0;
            said = [.. said.Select(word => word.Select(segment => together[at] is var t && t.Phoneme != alone[at++].Phoneme ? segment with { Phoneme = t.Phoneme } : segment).ToList())];
        }
        return [.. forms.Select((form, i) =>
        {
            var sounds = new Queue<Segment>(said[i]);
            return form with { Segments = [.. form.Segments.Select(s => s.Silent ? s : sounds.Dequeue())] };
        })];
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

    //where each sound sits, in the order of the word's segments. With no stressed syllable, none is
    private static List<SyllablePlace> Places(IReadOnlyList<Syllable> syllables, int? stressed) =>
    [
        .. syllables.SelectMany((syllable, index) =>
        {
            var closed = syllable.Closed;
            var isStressed = index == stressed;
            return syllable.Onset.Select(_ => new SyllablePlace(SyllablePosition.Onset, closed, isStressed))
                .Append(new SyllablePlace(SyllablePosition.Nucleus, closed, isStressed))
                .Concat(syllable.Coda.Select(_ => new SyllablePlace(SyllablePosition.Coda, closed, isStressed)));
        }),
    ];

    /*
      every vowel is a nucleus. Consonants before the first vowel are its onset and those after the
      last are the final coda; between two vowels, the onset takes as many as it legally can and the
      coda the rest. Returns null, with the offending cluster's [Start, End), when that's impossible.
      Syllables are made of the phonemes the sounds realize, since an allophone fills the slots its
      phoneme does
     */
    private List<Syllable>? Syllabify(List<Segment> segments, out (int Start, int End) badCluster)
    {
        badCluster = default;
        var nuclei = Enumerable.Range(0, segments.Count).Where(i => segments[i].Phoneme is Vowel).ToList();
        if (nuclei.Count == 0)
        {
            throw new InvalidOperationException("A word needs at least one vowel to be syllabified.");
        }

        List<Consonant> ConsonantsBetween(int start, int end) => [.. segments[start..end].Select(s => (Consonant)s.Realizes)];

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

        return [.. nuclei.Select((n, k) => new Syllable([.. onsets[k]], (Vowel)segments[n].Realizes, [.. codas[k]]))];
    }

    /*
      one edit to the bad cluster: the first one that fixes it if any does, and otherwise the first
      one at all. Every edit leaves shorter clusters behind, so repeating this always ends: a lone
      consonant between vowels or at either edge is legal as an onset or a coda, or else it's legal
      nowhere (a coda restriction in a template with no onsets), and then it's deleted
     */
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
                candidates.Add(([.. segments[..at], neighbor with { Phoneme = EpentheticVowel, Realizes = EpentheticVowel, Accented = false }, .. segments[at..]], cluster.End + 1));
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

/// <summary>
/// A word as it's actually pronounced, or a word with a root and the clitics leaning on it, which
/// are said as one word.
/// </summary>
/*
  its syllables are made of the phonemes its sounds realize, so they spell as the word does. They
  leave out the sounds that are silent, which are spelled all the same
 */
public record SurfaceForm(Word Word, IReadOnlyList<Segment> Segments, IReadOnlyList<Syllable> Syllables)
{
    /// <summary>
    /// The words it was said from, in order: the one, or the host and its clitics, whose morphemes
    /// Word runs together.
    /// </summary>
    public required IReadOnlyList<Word> Words { get; init; }

    /// <summary>The sounds as they're said, allophones and all.</summary>
    public IEnumerable<Phoneme> Phonemes => Segments.Where(s => !s.Silent).Select(s => s.Phoneme);

    /// <summary>The phonemes the sounds that are said realize.</summary>
    public IEnumerable<Phoneme> Realized => Segments.Where(s => !s.Silent).Select(s => s.Realizes);

    /// <summary>The index of the stressed syllable, or null for a word with no stress.</summary>
    public required int? StressedSyllable { get; init; }

    /// <summary>
    /// Whether the stress falls where the language's rule, applied to these syllables, wouldn't put
    /// it, as it can only where stress is free. Spelling marks it there, as Spanish marks teléfono.
    /// </summary>
    public required bool StressMarked { get; init; }

    /// <summary>
    /// Whether its last sound, silent on its own, is said before the vowel the next word starts with,
    /// as in French liaison.
    /// </summary>
    public required bool LinksToNext { get; init; }

    public string Spell(Orthography orthography) => string.Concat(Segments.Select((_, i) => Spell(orthography, i)));

    /// <summary>The sounds of one of the words it was said from, spelled.</summary>
    public string Spell(Orthography orthography, Word word) =>
        string.Concat(Enumerable.Range(0, Segments.Count).Where(i => word.Morphemes.Contains(Segments[i].Morpheme)).Select(i => Spell(orthography, i)));

    //spelled by syllable, with a silent sound spelled at the end of the syllable before it
    public string Syllabified(Orthography orthography)
    {
        var syllableOf = new int[Segments.Count];
        var (syllable, left) = (0, Syllables[0].Phonemes.Count());
        for (int i = 0; i < Segments.Count; i++)
        {
            if (!Segments[i].Silent)
            {
                while (left == 0)
                {
                    left = Syllables[++syllable].Phonemes.Count();
                }
                left--;
            }
            syllableOf[i] = syllable;
        }
        return string.Join(".", Enumerable.Range(0, Syllables.Count)
            .Select(k => string.Concat(Enumerable.Range(0, Segments.Count).Where(i => syllableOf[i] == k).Select(i => Spell(orthography, i)))));
    }

    //the index among the segments of the vowel that carries a stress mark, or -1 if none does
    private int MarkedVowel => StressMarked
        ? Segments.Select((s, i) => (s, i)).Where(x => x.s.Phoneme is Vowel).ElementAt(StressedSyllable!.Value).i
        : -1;

    private string Spell(Orthography orthography, int index) =>
        index == MarkedVowel ? orthography.SpellStressed((Vowel)Segments[index].Realizes) : orthography.Spell(Segments[index].Realizes);

    /// <summary>
    /// The IPA of the sounds as they're said, by syllable, with ˈ before the stressed one in place of
    /// the dot, as in [paˈta.ma]. A word of one syllable needs no mark, and one with no stress has none.
    /// </summary>
    public string Transcribed()
    {
        var sounds = Segments.Where(s => !s.Silent).ToList();
        var start = 0;
        var said = new List<string>();
        foreach (var syllable in Syllables)
        {
            said.Add(string.Concat(sounds.Skip(start).Take(syllable.Phonemes.Count()).Select(s => s.Phoneme.IpaSymbol)));
            start += syllable.Phonemes.Count();
        }
        return said.Count == 1 ? said[0] : string.Concat(said.Select((s, i) => (i == StressedSyllable ? "ˈ" : i == 0 ? "" : ".") + s));
    }

    /*
      the surface form split back into its morphemes, lining up with Gloss, with - between a word's
      morphemes and = between a host and its clitics, as the Leipzig rules have it. A morpheme that
      lost all of its sounds to a sound change or cluster repair shows as ∅
     */
    public string Segmented(Orthography orthography) => string.Join("=", Words.Select(word => string.Join("-", word.Morphemes.Select(morpheme =>
        Segments.Any(s => s.Morpheme.Equals(morpheme))
            ? string.Concat(Enumerable.Range(0, Segments.Count).Where(i => Segments[i].Morpheme.Equals(morpheme)).Select(i => Spell(orthography, i)))
            : "∅"))));

    public string Gloss => string.Join("=", Words.Select(w => w.Gloss));
}