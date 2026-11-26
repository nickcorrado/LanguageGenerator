namespace LanguageGenerator;

/// <summary>Which harmony, if any, a language's vowels follow.</summary>
public enum VowelHarmony
{
    None,
    FrontBack,
    TongueRoot,
}

/// <summary>What a language mostly does where two vowels meet at a morpheme boundary.</summary>
public enum HiatusResolution
{
    None,
    VowelElision,
    VowelCoalescence,
    GreekContraction,
    GlideInsertion,
    GlottalInsertion,
}

/// <summary>What happens to vowels without the stress.</summary>
public enum UnstressedVowels
{
    None,
    Reduction,
    Raising,
}

/// <summary>How a language repairs the syllable contacts its affixes and sound changes make.</summary>
public enum ContactRepair
{
    None,
    CodaAssimilation,
    OnsetHardening,
    CodaNasalization,
}

/// <summary>Which part of a syllable a sound is.</summary>
public enum SyllablePosition
{
    Onset,
    Nucleus,
    Coda,
}

/// <summary>
/// Where a sound sits in its syllable, whether that syllable ends in a consonant, and whether it's
/// the stressed one.
/// </summary>
public readonly record struct SyllablePlace(SyllablePosition Position, bool Closed, bool Stressed);

/// <summary>
/// One sound as a sound change sees it: the sound before it, already changed, the one after it, not
/// yet, and the whole word as it was before this change. Place is known only to changes that run on
/// syllabified words.
/// </summary>
//Place and Places are null for changes that run before the word has syllables
public readonly record struct Site(Segment Segment, Segment? Previous, Segment? Next, IReadOnlyList<Segment> Word, int Index, SyllablePlace? Place,
    IReadOnlyList<SyllablePlace>? Places)
{
    /// <summary>The sound before this one as it was before this change, unlike Previous.</summary>
    public Phoneme? Before => Index > 0 ? Word[Index - 1].Phoneme : null;

    public Phoneme Sound => Segment.Phoneme;
    public bool BetweenVowels => Previous?.Phoneme is Vowel && Next?.Phoneme is Vowel;

    public IEnumerable<Phoneme> RootSounds => Word.Where(s => s.Morpheme.Value is Root).Select(s => s.Phoneme);
}

/// <summary>
/// A regular change from underlying to surface sounds, like a rule of sandhi, the changes sounds
/// undergo where they meet: which sounds it applies to, in what surroundings, and what they become.
/// </summary>
/*
  each change is applied across a whole word, left to right, so it sees morpheme boundaries, and a
  language applies its changes in order, so one can feed or bleed the next. A change that turns one
  sound into another may make any sound the language can say: a phoneme of its own, which the sound
  then merges into, or else an allophone of the phoneme it realizes (see Segment). Changes that
  merge or insert sounds, or turn a vowel into a consonant or back, make only phonemes, since an
  allophone has to be said for a phoneme of its own kind that's still there to spell. Where no sound
  will do, the sound is left alone. A change is a value, so a language's are a list that can be
  drawn, compared or extended.
  Besides changing or deleting a sound, a change can insert one after it, which belongs to the
  morpheme after it, as an inserted consonant starts the suffix it comes before. Only changes
  that run before syllables can insert, since syllable places are counted by sound. A change can
  also move a stored accent back to the vowel before it, without changing any sound
 */
public sealed class SoundChange(string name, Func<Site, bool> when, Func<Site, Phonology, Phoneme?> becomes, (Vowel First, Vowel Second)[]? pairs = null,
    Func<Site, Phonology, Phoneme?>? insertsAfter = null, Func<Site, bool>? retractsAccent = null)
{
    public override string ToString() => name;

    /*
      an accented vowel that's lost, or becomes a glide, passes its accent to the vowel it met: the
      one before it, which it merged into, if that one's kept, and otherwise the one right after it,
      as an elided vowel leaves its accent to the vowel that took its place. A vowel lost with no
      vowel beside it takes its accent with it, though no change loses one that's accented
     */
    public List<Segment> Apply(List<Segment> segments, Phonology phonology, IReadOnlyList<SyllablePlace>? places = null)
    {
        var result = new List<Segment>();
        var accentForNext = false;
        for (int i = 0; i < segments.Count; i++)
        {
            var site = new Site(segments[i], result.LastOrDefault(), i + 1 < segments.Count ? segments[i + 1] : null, segments, i, places?[i], places);
            var kept = !when(site) ? segments[i]
                : becomes(site, phonology) is { } sound ? (sound == segments[i].Phoneme ? segments[i] : phonology.Changed(segments[i], sound))
                : null;
            if (accentForNext && kept?.Phoneme is Vowel)
            {
                kept = kept with { Accented = true };
            }
            accentForNext = false;
            if (kept is { Accented: true } && retractsAccent?.Invoke(site) == true && result.FindLastIndex(s => s.Phoneme is Vowel) is >= 0 and var before)
            {
                kept = kept with { Accented = false };
                result[before] = result[before] with { Accented = true };
            }
            if (segments[i].Accented && kept?.Phoneme is not Vowel)
            {
                kept = kept is null ? null : kept with { Accented = false };
                if (i > 0 && segments[i - 1].Phoneme is Vowel && result.LastOrDefault()?.Phoneme is Vowel)
                {
                    result[^1] = result[^1] with { Accented = true };
                }
                else
                {
                    accentForNext = true;
                }
            }
            if (kept is not null)
            {
                result.Add(kept);
            }
            if (places is null && site.Next is { } next && insertsAfter?.Invoke(site, phonology) is { } inserted)
            {
                result.Add(new Segment(inserted, inserted, next.Morpheme, false, false));
            }
        }
        return result;
    }

    private static bool NeverChanges(Site site) => false;

    //for changes that delete what they apply to
    private static Phoneme? Nothing(Site site, Phonology phonology) => null;

    /// <summary>Simplifies a doubled consonant to a single one.</summary>
    //the surviving consonant keeps the first one's morpheme, so across a boundary the second morpheme
    //can be left with no sound of its own
    public static SoundChange Degemination { get; } = new("degemination (tt → t)",
        site => site.Sound is Consonant && site.Previous?.Phoneme == site.Sound,
        Nothing);

    /// <summary>A nasal takes the place of articulation of a following stop or nasal.</summary>
    /*
      only where the new nasal can still close its syllable. A language whose codas allow only n, like
      Japanese, says n before k as ŋ where it has no ŋ of its own, as Japanese does, since the
      allophone is still its n. Where it has ŋ, n would merge into a phoneme it can't end a syllable
      with, and cluster repair would then delete it, so there the n stays n
     */
    public static SoundChange NasalAssimilation { get; } = new("nasal assimilation (np → mp)",
        site => site.Sound is Consonant { Manner: ConsonantManner.Nasal } nasal
            && site.Next?.Phoneme is Consonant { Manner: ConsonantManner.Plosive or ConsonantManner.Nasal } next && next.Place != nasal.Place,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, place: ((Consonant)site.Next!.Phoneme).Place) is { } assimilated
            && phonology.SyllableRule.IsLegalCoda([(Consonant)phonology.Changed(site.Segment, assimilated).Realizes])
                ? assimilated
                : site.Sound);

    /// <summary>An unvoiced consonant between vowels takes its voiced counterpart.</summary>
    public static SoundChange IntervocalicVoicing { get; } = new("intervocalic voicing (apa → aba)",
        site => site.Sound is Consonant { Voiced: false } && site.BetweenVowels,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, voiced: true) ?? site.Sound);

    //what each stop weakens to, the likeliest first, of what the language can say
    private static readonly Dictionary<Phoneme, Consonant[]> Lenited = new()
    {
        //p weakens to the ɸ of its own place, as in Tuscan, where the f of other places is no nearer
        //b and g weaken to the fricatives at their own place, as Spanish's do, and failing those to
        //the nearest sounds there are, v and w
        [Consonant.p] = [Consonant.ɸ, Consonant.f],
        [Consonant.b] = [Consonant.β, Consonant.v],
        [Consonant.t] = [Consonant.θ, Consonant.s],
        [Consonant.d] = [Consonant.ð, Consonant.z],
        [Consonant.k] = [Consonant.x],
        [Consonant.g] = [Consonant.ɣ, Consonant.w],
        //and the palatal stops to a fricative and a glide, as Spanish's ɟ is often just j
        [Consonant.c] = [Consonant.ç],
        [Consonant.ɟ] = [Consonant.j],
        [Consonant.ʧ] = [Consonant.ʃ],
        [Consonant.ʤ] = [Consonant.ʒ],
        [Consonant.ʦ] = [Consonant.s],
        [Consonant.ʣ] = [Consonant.z],
        //aspirates weaken to fricatives as they did from Ancient to Modern Greek, pʰ to f, tʰ to θ
        //and kʰ to x, and labialized gʷ to w, as in Latin
        [Consonant.pʰ] = [Consonant.f],
        [Consonant.tʰ] = [Consonant.θ, Consonant.s],
        [Consonant.kʰ] = [Consonant.x],
        [Consonant.gʷ] = [Consonant.w],
    };

    /// <summary>A stop between vowels weakens to a continuant, as in Spanish or Tuscan.</summary>
    //after voicing where a language has both, so that p can weaken all the way to β, or v without it
    public static SoundChange Lenition { get; } = new("lenition (apa → afa)",
        site => site.BetweenVowels && Lenited.ContainsKey(site.Sound),
        (site, phonology) => Lenited[site.Sound].FirstOrDefault(phonology.CanSay) ?? site.Sound);

    /// <summary>An obstruent takes the voicing of an obstruent after it, as in Russian.</summary>
    public static SoundChange VoicingAssimilation { get; } = new("voicing assimilation (apda → abda)",
        site => site.Sound is Consonant { IsObstruent: true } consonant
            && site.Next?.Phoneme is Consonant { IsObstruent: true } next && next.Voiced != consonant.Voiced,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, voiced: ((Consonant)site.Next!.Phoneme).Voiced) ?? site.Sound);

    /// <summary>A voiced obstruent loses its voicing at the end of a word, as in German or Turkish.</summary>
    public static SoundChange FinalDevoicing { get; } = new("final devoicing (kab → kap)",
        site => site.Sound is Consonant { IsObstruent: true, Voiced: true } && site.Next is null,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, voiced: false) ?? site.Sound);

    /// <summary>
    /// Every vowel in a word agrees with the root's first harmonizing vowel in being front or back, as
    /// in Finnish talo-ssa 'in the house' but kylä-ssä 'in the village'.
    /// </summary>
    //a pairs with æ as Finnish a does with ä, being the language's back open vowel, though by its
    //features it's central. ə, ɨ and the diphthongs have no partner, so they're neutral
    public static SoundChange FrontBackHarmony { get; } = Harmony("front-back harmony (kæ-la → kælæ)",
        [(Vowel.æ, Vowel.a), (Vowel.e, Vowel.o), (Vowel.ɛ, Vowel.ɔ), (Vowel.i, Vowel.u), (Vowel.eː, Vowel.oː), (Vowel.iː, Vowel.uː)]);

    /// <summary>
    /// Every vowel in a word agrees with the root's first harmonizing vowel in the position of the
    /// tongue root, heard as close or open mid vowels, as in Akan or Igbo.
    /// </summary>
    public static SoundChange TongueRootHarmony { get; } = Harmony("tongue-root harmony (ke-lɛ → kele)",
        [(Vowel.e, Vowel.ɛ), (Vowel.o, Vowel.ɔ)]);


    /// <summary>Whether a language has both vowels of any pair this change needs, if it needs any.</summary>
    public bool CanApplyTo(IReadOnlyCollection<Vowel> vowels) => pairs?.Any(p => vowels.Contains(p.First) && vowels.Contains(p.Second)) ?? true;

    /*
      harmony spreads from the root both ways, so prefixes agree too, and it reaches the root's own
      vowels, since real languages with harmony have roots that keep it. A vowel with no partner, like
      ə, is neutral: it doesn't change and harmony passes through it. It runs before epenthesis, so an
      inserted vowel doesn't take part. It makes only phonemes, since an affix's vowel alternates
      between two of them, as Finnish -ssa and -ssä are written apart
     */
    private static SoundChange Harmony(string name, (Vowel First, Vowel Second)[] pairs)
    {
        (Vowel First, Vowel Second)? PairOf(Phoneme sound) => pairs.FirstOrDefault(p => p.First == sound || p.Second == sound) is { First: not null } pair ? pair : null;
        //the root's first vowel with a partner: which side of the pairs it's on decides the word
        bool? First(Site site) => site.RootSounds.FirstOrDefault(s => PairOf(s) is not null) is { } trigger ? PairOf(trigger)!.Value.First == trigger : null;

        return new(name,
            site => site.Sound is Vowel && PairOf(site.Sound) is not null && First(site) is not null,
            (site, phonology) =>
            {
                var pair = PairOf(site.Sound)!.Value;
                var agreeing = First(site) == true ? pair.First : pair.Second;
                return phonology.Vowels.Contains(agreeing) ? agreeing : site.Sound;
            },
            pairs);
    }

    /// <summary>
    /// A voiced obstruent loses its voicing at the end of any syllable, as in German, where Tag
    /// 'day' ends in k but Tage 'days' keeps its g, which a vowel makes an onset.
    /// </summary>
    public static SoundChange CodaDevoicing { get; } = new("coda devoicing (kab.da → kap.da)",
        site => site.Place?.Position == SyllablePosition.Coda && site.Sound is Consonant { IsObstruent: true, Voiced: true },
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, voiced: false) ?? site.Sound);

    /// <summary>
    /// An s closing a syllable weakens to h, as in Andalusian or Caribbean Spanish estas, said ehtah.
    /// </summary>
    public static SoundChange CodaWeakening { get; } = new("coda weakening (kas.ta → kah.ta)",
        site => site.Place?.Position == SyllablePosition.Coda && site.Sound == Consonant.s,
        (site, phonology) => phonology.CanSay(Consonant.h) ? Consonant.h : site.Sound);

    private static readonly Dictionary<Phoneme, Vowel> Laxed = new() { [Vowel.e] = Vowel.ɛ, [Vowel.o] = Vowel.ɔ, [Vowel.i] = Vowel.ɪ, [Vowel.u] = Vowel.ʊ };

    /// <summary>
    /// A close or close-mid vowel opens in a syllable that ends in a consonant, as French fête has
    /// the open vowel that féter lacks, and Canadian French says the i of petite as ɪ.
    /// </summary>
    public static SoundChange ClosedSyllableLaxing { get; } = new("closed-syllable laxing (pet → pɛt)",
        site => site is { Place: { Position: SyllablePosition.Nucleus, Closed: true } } && Laxed.ContainsKey(site.Sound),
        (site, phonology) => phonology.CanSay(Laxed[site.Sound]) ? Laxed[site.Sound] : site.Sound);

    private static bool Unstressed(Site site) => site.Place is { Position: SyllablePosition.Nucleus, Stressed: false };

    //each velar and the labialized velar it and a w make
    private static readonly Dictionary<Phoneme, Consonant> Labialized = new() { [Consonant.k] = Consonant.kʷ, [Consonant.g] = Consonant.gʷ };

    /// <summary>
    /// A velar and a w after it in the same onset merge into the labialized velar, as Latin qu
    /// was one sound, kʷ, and not k and w.
    /// </summary>
    /*
      every language with a labialized velar has it, where it has the one that velar makes, so kw
      never sounds like kʷ without being it, whether a root or an affix put the two together. A w
      that starts the next syllable after a k closing one stays, as in ak.wa. It comes first among
      the syllable changes, so the rest see the labialized velar. It keeps the velar's morpheme, so
      a suffix of just the w is left with no sound of its own
     */
    public static SoundChange Labialization { get; } = new("labialization (kwa → kʷa)",
        site => site.Place?.Position == SyllablePosition.Onset,
        (site, phonology) => LabializedAt(site.Word, site.Index, site.Places!, phonology) is { } labialized ? labialized
            : site.Index > 0 && LabializedAt(site.Word, site.Index - 1, site.Places!, phonology) is not null ? null
            : site.Sound);

    //the labialized velar the velar here makes with a w after it in its onset, if the language has it
    private static Consonant? LabializedAt(IReadOnlyList<Segment> word, int index, IReadOnlyList<SyllablePlace> places, Phonology phonology) =>
        index + 1 < word.Count && places[index].Position == SyllablePosition.Onset && places[index + 1].Position == SyllablePosition.Onset
            && word[index + 1].Phoneme == Consonant.w && Labialized.TryGetValue(word[index].Phoneme, out var labialized) && phonology.Consonants.Contains(labialized)
                ? labialized
                : null;

    //the close vowel each glide is
    private static readonly Dictionary<Phoneme, Vowel> Vocalized = new() { [Consonant.j] = Vowel.i, [Consonant.w] = Vowel.u };

    /// <summary>
    /// A glide closing a syllable merges with the vowel before it into the diphthong they make, as
    /// a vowel gliding toward i or u is a diphthong, so the Spanish rey 'king' is said with ei.
    /// </summary>
    /*
      every language with diphthongs has it, where it has the diphthong, so a vowel and a glide after
      it never sound like a diphthong without being one, whether a root or an affix put them
      together: ka-j gives kai. A glide that starts the next syllable stays, as in ka.ja. It comes
      right after labialization, which only touches onsets, so the rest see the diphthong. The
      diphthong keeps the vowel's morpheme, so a suffix of just the glide is left with no sound of its own
     */
    public static SoundChange GlideVocalization { get; } = new("glide vocalization (kaj → kai)",
        site => site.Place?.Position is SyllablePosition.Nucleus or SyllablePosition.Coda,
        (site, phonology) => site.Place!.Value.Position switch
        {
            SyllablePosition.Nucleus => VocalizedAfter(site.Word, site.Index, site.Places!, phonology) ?? site.Sound,
            SyllablePosition.Coda => VocalizedAfter(site.Word, site.Index - 1, site.Places!, phonology) is null ? site.Sound : null,
            SyllablePosition.Onset => site.Sound,
            _ => throw new ArgumentOutOfRangeException(nameof(site), site.Place, null),
        });

    //the diphthong the vowel here makes with a glide closing its syllable, if the language has it
    private static Vowel? VocalizedAfter(IReadOnlyList<Segment> word, int index, IReadOnlyList<SyllablePlace> places, Phonology phonology) =>
        index >= 0 && index + 1 < word.Count && places[index + 1].Position == SyllablePosition.Coda
            && Vocalized.TryGetValue(word[index + 1].Phoneme, out var close)
            && Vowel.DiphthongOf(word[index].Phoneme, close) is { } diphthong && phonology.Vowels.Contains(diphthong)
                ? diphthong
                : null;

    /// <summary>
    /// A stored accent on the last syllable of a word moves to the syllable before it, as
    /// Neo-Štokavian Serbo-Croatian's did (ženà → žèna), so a root accented on its last vowel is
    /// stressed there only where a suffix follows it.
    /// </summary>
    //only a stored accent moves, since stress by rule is already where the rule puts it
    public static SoundChange AccentRetraction { get; } = new("accent retraction (paˈta → ˈpata)",
        NeverChanges, (site, _) => site.Sound,
        retractsAccent: site => site.Sound is Vowel && !site.Word.Skip(site.Index + 1).Any(s => s.Phoneme is Vowel));

    /// <summary>
    /// A voiceless fricative between voiced sounds takes its voiced counterpart unless the stress falls
    /// on the vowel right before it, as Germanic's did by Verner's law, so the s of Old English
    /// wesan 'to be' is the r of wǣron 'were', where the stress once followed it.
    /// </summary>
    /*
      voiced sounds are vowels and sonorants. Where stress is free, roots differ in which of their
      fricatives voice, and so may the forms of one root, as the stress moves; under a fixed rule,
      every word voices alike
     */
    public static SoundChange VernersLaw { get; } = new("Verner's law (aˈsa → aˈza)",
        site => site.Sound is Consonant { Voiced: false, Manner: ConsonantManner.Fricative or ConsonantManner.Sibilant }
            && Voiced(site.Previous?.Phoneme) && Voiced(site.Next?.Phoneme)
            && site.Word.Take(site.Index).ToList().FindLastIndex(s => s.Phoneme is Vowel) is >= 0 and var vowel && site.Places?[vowel].Stressed == false,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, voiced: true) ?? site.Sound);

    private static bool Voiced(Phoneme? phoneme) => phoneme is Vowel or Consonant { IsObstruent: false, Voiced: true };

    /// <summary>Whether an inventory has a voiceless fricative with a voiced counterpart, for Verner's law.</summary>
    public static bool HasFricativePair(IReadOnlyCollection<Consonant> consonants) =>
        consonants.Any(c => c is { Voiced: false, Manner: ConsonantManner.Fricative or ConsonantManner.Sibilant }
            && consonants.Any(v => v.Voiced && v.Place == c.Place && v.Manner == c.Manner && v.Aspirated == c.Aspirated && v.Labialized == c.Labialized));

    /// <summary>
    /// A long vowel loses its length outside the stressed syllable, as length followed stress in
    /// Latin's daughters.
    /// </summary>
    public static SoundChange UnstressedShortening { get; } = new("unstressed shortening (paːˈta → paˈta)",
        site => Unstressed(site) && site.Sound is Vowel { Long: true },
        (site, phonology) => Vowel.All.FirstOrDefault(v => !v.Long && v.Lengthened == site.Sound && phonology.CanSay(v)) ?? site.Sound);

    /// <summary>
    /// A diphthong outside the stressed syllable becomes a single vowel, as Latin's ae and oe became
    /// e, and au o, in Romance: ai, ei and oi give e, au and ou give o, and ui gives i.
    /// </summary>
    //where the language can't say that vowel, the diphthong keeps only the vowel it starts on, and
    //where it can't say that either, it stays a diphthong
    public static SoundChange UnstressedMonophthongization { get; } = new("unstressed monophthongization (kaiˈta → keˈta)",
        site => Unstressed(site) && site.Sound is Vowel { IsDiphthong: true },
        (site, phonology) => new[] { Monophthong((Vowel)site.Sound), ((Vowel)site.Sound).Start! }.FirstOrDefault(phonology.CanSay) ?? site.Sound);

    private static Vowel Monophthong(Vowel diphthong) =>
        diphthong == Vowel.ai || diphthong == Vowel.ei || diphthong == Vowel.oi ? Vowel.e
        : diphthong == Vowel.au || diphthong == Vowel.ou ? Vowel.o
        : diphthong == Vowel.ui ? Vowel.i
        : throw new ArgumentOutOfRangeException(nameof(diphthong), diphthong.IpaSymbol, "Not a diphthong.");

    /// <summary>
    /// A stressed vowel in an open syllable lengthens, as in Italian, where the first vowel of casa is
    /// long but not that of carta.
    /// </summary>
    public static SoundChange OpenSyllableLengthening { get; } = new("open-syllable lengthening (ˈpata → ˈpaːta)",
        site => site.Place is { Position: SyllablePosition.Nucleus, Stressed: true, Closed: false } && site.Sound is Vowel { Lengthened: not null },
        (site, phonology) => phonology.CanSay(((Vowel)site.Sound).Lengthened!) ? ((Vowel)site.Sound).Lengthened : site.Sound);

    /// <summary>
    /// A short vowel right after the stressed syllable is lost between single consonants, as Latin
    /// calidus became Italian caldo, so long as the consonants can then close one syllable and open
    /// the next.
    /// </summary>
    /*
      the vowel needs a vowel two sounds away on each side, the stressed one before it, so it's never
      in the last syllable, and only one vowel goes, leaving a cluster of two. Stress that falls by
      rule is found again afterwards, over the syllables that are left, while a stored accent stays
      on its vowel, as Italian's did
     */
    public static SoundChange Syncope { get; } = new("syncope (ˈkalida → ˈkalda)",
        site => Unstressed(site) && site.Sound is Vowel { Heavy: false } && site.Index >= 2 && site.Index + 2 < site.Word.Count
            && site.Word[site.Index - 1].Phoneme is Consonant && site.Word[site.Index - 2].Phoneme is Vowel
            && site.Places?[site.Index - 2].Stressed == true
            && site.Word[site.Index + 1].Phoneme is Consonant && site.Word[site.Index + 2].Phoneme is Vowel,
        (site, phonology) => phonology.SyllableRule.IsLegalCoda([(Consonant)site.Word[site.Index - 1].Realizes]) ? null : site.Sound);

    /// <summary>
    /// A short vowel right before the stressed syllable is lost between single consonants, as Latin
    /// verecundia became Spanish vergüenza, so long as the consonants can then close one syllable and
    /// open the next.
    /// </summary>
    //the mirror of syncope, with the stressed vowel after it, so it's never in the first syllable
    public static SoundChange PretonicSyncope { get; } = new("pretonic syncope (kaliˈdana → kalˈdana)",
        site => Unstressed(site) && site.Sound is Vowel { Heavy: false } && site.Index >= 2 && site.Index + 2 < site.Word.Count
            && site.Word[site.Index - 1].Phoneme is Consonant && site.Word[site.Index - 2].Phoneme is Vowel
            && site.Word[site.Index + 1].Phoneme is Consonant && site.Word[site.Index + 2].Phoneme is Vowel
            && site.Places?[site.Index + 2].Stressed == true,
        (site, phonology) => phonology.SyllableRule.IsLegalCoda([(Consonant)site.Word[site.Index - 1].Realizes]) ? null : site.Sound);

    /// <summary>
    /// A short unstressed vowel at the end of a word is lost after a consonant, as Latin mare became
    /// Spanish mar, so long as the consonants before it can then end the word.
    /// </summary>
    /*
      a vowel must come earlier in the word, so a word is never left without one. Stress that falls
      by rule is found again over the syllables that are left, as Polish's was when it lost its final
      vowels, while a stored accent stays on its vowel, which is how Spanish and French came to stress
      last syllables their rules wouldn't
     */
    public static SoundChange Apocope { get; } = new("apocope (ˈmare → ˈmar)",
        site => Unstressed(site) && site.Sound is Vowel { Heavy: false } && site.Index == site.Word.Count - 1 && FinalCluster(site.Word) is { Count: > 0 },
        (site, phonology) => phonology.SyllableRule.IsLegalCoda(FinalCluster(site.Word)!) ? null : site.Sound);

    //the consonant phonemes between a word's last two vowels, or null if it has only one
    private static List<Consonant>? FinalCluster(IReadOnlyList<Segment> word)
    {
        var previous = word.Take(word.Count - 1).ToList().FindLastIndex(s => s.Phoneme is Vowel);
        return previous < 0 ? null : [.. word.Skip(previous + 1).Take(word.Count - 2 - previous).Select(s => (Consonant)s.Realizes)];
    }

    /// <summary>
    /// A short unstressed vowel at the start of a word is lost before a consonant, as English esquire
    /// became squire, so long as the consonants after it can then start the word.
    /// </summary>
    /*
      the mirror of apocope: a vowel must come later in the word, and stress that falls by rule is
      found again over the syllables that are left, while a stored accent stays on its vowel
     */
    public static SoundChange Aphaeresis { get; } = new("aphaeresis (aˈmika → ˈmika)",
        site => Unstressed(site) && site.Sound is Vowel { Heavy: false } && site.Index == 0 && InitialCluster(site.Word) is { Count: > 0 },
        (site, phonology) => phonology.SyllableRule.IsLegalOnset(InitialCluster(site.Word)!) ? null : site.Sound);

    //the consonant phonemes between a word's first two vowels, or null if it has only one
    private static List<Consonant>? InitialCluster(IReadOnlyList<Segment> word)
    {
        var next = word.Skip(1).ToList().FindIndex(s => s.Phoneme is Vowel);
        return next < 0 ? null : [.. word.Skip(1).Take(next).Select(s => (Consonant)s.Realizes)];
    }

    //how each unstressed vowel weakens
    private static readonly Dictionary<Phoneme, Vowel> Raised = new()
    {
        [Vowel.e] = Vowel.i, [Vowel.ɛ] = Vowel.i, [Vowel.o] = Vowel.u, [Vowel.ɔ] = Vowel.u,
    };

    /// <summary>
    /// An unstressed short vowel other than a close one weakens to schwa, as in English or Catalan.
    /// </summary>
    //long vowels and diphthongs are left to the changes that shorten them, which come before this
    public static SoundChange UnstressedReduction { get; } = new("unstressed reduction (ˈpata → ˈpatə)",
        site => Unstressed(site) && site.Sound is Vowel { Heavy: false, Height: not VowelHeight.Close } vowel && vowel != Vowel.ə,
        (site, phonology) => phonology.CanSay(Vowel.ə) ? Vowel.ə : site.Sound);

    /// <summary>
    /// An unstressed mid vowel rises to a close one, e to i and o to u, as in Portuguese or Bulgarian.
    /// </summary>
    public static SoundChange UnstressedRaising { get; } = new("unstressed raising (ˈpeto → ˈpetu)",
        site => Unstressed(site) && Raised.ContainsKey(site.Sound),
        (site, phonology) => phonology.CanSay(Raised[site.Sound]) ? Raised[site.Sound] : site.Sound);

    /*
      a coda and the onset consonant right after it, as they met before this change, as phonemes,
      since contact is between the phonemes roots keep to, as syllables are. In a coda of two, only
      the second meets the onset
     */
    private static (Consonant Coda, Consonant Onset)? ContactAfter(Site site) =>
        site is { Place.Position: SyllablePosition.Coda, Segment.Realizes: Consonant coda, Next.Realizes: Consonant onset }
            && site.Places![site.Index + 1].Position == SyllablePosition.Onset
            ? (coda, onset)
            : null;

    //whether the coda ending here would still be legal with its last consonant said as this one
    private static bool IsLegalCodaWith(Site site, Consonant last, Phonology phonology) =>
        phonology.SyllableRule.IsLegalCoda([.. Enumerable.Range(0, site.Index).Reverse().TakeWhile(i => site.Places![i].Position == SyllablePosition.Coda)
            .Reverse().Select(i => (Consonant)site.Word[i].Realizes), (Consonant)phonology.Changed(site.Segment, last).Realizes]);

    private static (Consonant Coda, Consonant Onset)? ContactBefore(Site site) =>
        site is { Place.Position: SyllablePosition.Onset, Segment.Realizes: Consonant onset } && site.Index > 0 && site.Word[site.Index - 1].Realizes is Consonant coda
            && site.Places![site.Index - 1].Position == SyllablePosition.Coda
            ? (coda, onset)
            : null;

    /*
      the contact repairs below change only what the language's syllable contact doesn't allow, so
      where morphemes meet, a coda and an onset that roots would never put together are brought
      into line, as each language does it
     */

    /// <summary>
    /// An obstruent closing a syllable becomes a nasal before a nasal, as Korean kuk-mul 'soup' is
    /// said kuŋmul.
    /// </summary>
    public static SoundChange CodaNasalization { get; } = new("coda nasalization (ak.ma → aŋ.ma)",
        site => ContactAfter(site) is ({ IsObstruent: true }, { Manner: ConsonantManner.Nasal }),
        (site, phonology) => ContactAfter(site) is (var coda, var onset) && !phonology.SyllableContact.Allows(coda, onset)
            && phonology.Counterpart(coda, manner: ConsonantManner.Nasal, voiced: true) is { } nasal && IsLegalCodaWith(site, nasal, phonology)
                ? nasal
                : site.Sound);

    /// <summary>
    /// A sonorant starting a syllable becomes a stop after a less sonorous coda, as the Kazakh plural
    /// -lar is -dar after a nasal and -tar after a voiceless stop.
    /// </summary>
    //the stop takes the coda's voicing where the language has the choice
    public static SoundChange OnsetHardening { get; } = new("onset hardening (at.la → at.ta)",
        site => ContactBefore(site) is (_, { IsObstruent: false }),
        (site, phonology) => ContactBefore(site) is (var coda, var onset) && !phonology.SyllableContact.Allows(coda, onset)
            ? phonology.Counterpart(onset, manner: ConsonantManner.Plosive, voiced: coda.Voiced)
                ?? phonology.Counterpart(onset, manner: ConsonantManner.Plosive, voiced: !coda.Voiced)
                ?? site.Sound
            : site.Sound);

    /// <summary>
    /// A coda becomes the onset after it, doubling it, as Korean sin-la is said silla.
    /// </summary>
    public static SoundChange CodaAssimilation { get; } = new("coda assimilation (an.la → al.la)",
        site => ContactAfter(site) is not null,
        (site, phonology) => ContactAfter(site) is (var coda, var onset) && !phonology.SyllableContact.Allows(coda, onset)
            && IsLegalCodaWith(site, onset, phonology)
                ? onset
                : site.Sound);

    private static readonly Phoneme[] LostCodas = [Consonant.h, Consonant.ɹ, Consonant.r, Consonant.ɾ, Consonant.s];

    /// <summary>
    /// A coda is lost and the vowel before it lengthens to make up for it, as English car lost its r
    /// and Old French teste its s.
    /// </summary>
    /*
      both halves apply only where the language has the long vowel, so a coda is never lost for
      nothing, and the long vowel is always a phoneme, since it's said for two. It follows coda
      weakening, which it can feed, and precedes laxing, whose closed syllables it opens
     */
    public static SoundChange CompensatoryLengthening { get; } = new("compensatory lengthening (kaɹ → kaː)",
        site => site.Place switch
        {
            { Position: SyllablePosition.Nucleus, Closed: true } => site.Sound is Vowel { Lengthened: not null } && site.Next is { } coda && LostCodas.Contains(coda.Phoneme),
            { Position: SyllablePosition.Coda } => LostCodas.Contains(site.Sound) && site.Before is Vowel { Lengthened: not null },
            _ => false,
        },
        (site, phonology) => site.Sound is Vowel vowel
            ? (phonology.Vowels.Contains(vowel.Lengthened!) ? vowel.Lengthened : vowel)
            : (phonology.Vowels.Contains(((Vowel)site.Before!).Lengthened!) ? null : site.Sound));

    /// <summary>
    /// The same vowel twice in a row merges into one, long where the language has long vowels, as
    /// in Greek, where ε + ε gave long ει.
    /// </summary>
    /*
      nearly every language does this, even ones that otherwise leave vowels side by side, so every
      language has it. The merged vowel keeps the first one's morpheme, so a suffix can be left with
      no sound of its own, and it runs before the language's own way with vowels that meet
     */
    public static SoundChange IdenticalVowelFusion { get; } = new("identical vowel fusion (ka-a → kaː)",
        site => site.Sound is Vowel vowel
            && (site.Next?.Phoneme is Vowel next && SameQuality(vowel, next) || site.Previous?.Phoneme is Vowel previous && SameQuality(vowel, previous)),
        (site, phonology) => site.Previous?.Phoneme is Vowel previous && SameQuality((Vowel)site.Sound, previous)
            ? null
            : ((Vowel)site.Sound).Lengthened is { } lengthened && phonology.Vowels.Contains(lengthened) ? lengthened : site.Sound);

    //a diphthong is the same as another only if it glides to the same vowel, so ai and a aren't alike
    private static bool SameQuality(Vowel first, Vowel second) =>
        first.Height == second.Height && first.Backness == second.Backness && first.Rounded == second.Rounded && first.Offglide == second.Offglide;

    private static bool IsHigh(Vowel vowel) => vowel.Height == VowelHeight.Close;

    //whether a vowel takes part in Greek contraction
    private static bool Contracts(Vowel vowel) => !IsHigh(vowel) && !vowel.IsDiphthong;

    //the glide each close vowel becomes, or is echoed by
    private static readonly Dictionary<Phoneme, Consonant> Glides = new()
    {
        [Vowel.i] = Consonant.j, [Vowel.iː] = Consonant.j, [Vowel.u] = Consonant.w, [Vowel.uː] = Consonant.w,
    };

    /// <summary>
    /// A close vowel before another vowel becomes a glide, i to j and u to w, as in Spanish or
    /// Sanskrit, where i + a gives ya.
    /// </summary>
    /*
      only where the syllable the glide joins is legal: the consonants before it have to split into
      a coda for the syllable before, if there is one, and an onset ending in the glide. Elsewhere the
      vowel stays, for whatever else the language does with vowels that meet. ɨ has no glide, so it
      stays too
     */
    public static SoundChange GlideFormation { get; } = new("glide formation (ki-a → kja)",
        site => Glides.ContainsKey(site.Sound) && site.Next?.Phoneme is Vowel,
        (site, phonology) => Glides[site.Sound] is var glide && phonology.Consonants.Contains(glide) && CanStartSyllable(glide, site, phonology.SyllableRule)
            ? glide
            : site.Sound);

    private static bool CanStartSyllable(Consonant glide, Site site, SyllableRule rule)
    {
        var cluster = site.Word.Take(site.Index).Reverse().TakeWhile(s => s.Realizes is Consonant).Select(s => (Consonant)s.Realizes).Reverse().ToList();
        //a coda needs a vowel before it; at the start of a word, the whole cluster is the onset
        var codaLengths = site.Index > cluster.Count ? Enumerable.Range(0, cluster.Count + 1) : [0];
        return codaLengths.Any(c => rule.IsLegalCoda(cluster[..c]) && rule.IsLegalOnset([.. cluster[c..], glide]));
    }

    /// <summary>
    /// A vowel and a close vowel after it merge into the diphthong that glides from one to the
    /// other, as Greek's did, where ε + ι gave ει, and Latin's, where de + inde gave deinde.
    /// </summary>
    /*
      only where the language has the diphthong. A vowel joins at most one diphthong, so in o-u-i the
      o takes the u and the i is left after them. It follows glide formation, so a close vowel before
      a vowel has already become a glide, and the vowels it leaves meet the rest of what the language
      does where vowels meet
     */
    public static SoundChange DiphthongFormation { get; } = new("diphthong formation (ka-i → kai)",
        site => site.Sound is Vowel,
        (site, phonology) => DiphthongStartingAt(site.Word, site.Index, phonology) is { } diphthong ? diphthong
            : site.Index > 0 && DiphthongStartingAt(site.Word, site.Index - 1, phonology) is not null ? null
            : site.Sound);

    //the diphthong the vowel here and the one after it make in this language, if any, which they don't
    //if this one already makes one with the vowel before it
    private static Vowel? DiphthongStartingAt(IReadOnlyList<Segment> word, int index, Phonology phonology) =>
        index + 1 < word.Count && Vowel.DiphthongOf(word[index].Phoneme, word[index + 1].Phoneme) is { } diphthong && phonology.Vowels.Contains(diphthong)
            && !(index > 0 && DiphthongStartingAt(word, index - 1, phonology) is not null)
                ? diphthong
                : null;

    /// <summary>
    /// Two vowels that meet, neither of them close, merge into one long vowel, as in Attic Greek:
    /// a rounded vowel wins, as e + o gave ō, and otherwise the first one does, as a + e gave ā.
    /// </summary>
    /*
      close vowels don't contract, as Greek ι and υ didn't, so they're left for glide formation, or
      left alone, and nor do diphthongs, which end in one. Schwa gives way to anything. The vowel is
      long where the language has it long
     */
    public static SoundChange GreekContraction { get; } = new("Greek contraction (ka-e → kaː, ke-o → koː)",
        site => site.Sound is Vowel vowel && Contracts(vowel)
            && (site.Before is Vowel before && Contracts(before) || site.Next?.Phoneme is Vowel next && Contracts(next)),
        (site, phonology) =>
        {
            if (site.Before is Vowel before && Contracts(before))
            {
                return null;
            }
            var run = site.Word.Skip(site.Index).Select(s => s.Phoneme).TakeWhile(p => p is Vowel v && Contracts(v)).Cast<Vowel>().ToList();
            var full = run.Where(v => v != Vowel.ə).DefaultIfEmpty(Vowel.ə).ToList();
            var winner = full.FirstOrDefault(v => v.Rounded) ?? full[0];
            return winner.Lengthened is { } lengthened && phonology.Vowels.Contains(lengthened) ? lengthened : winner;
        });

    /// <summary>
    /// A glide comes between vowels that meet, the one that goes with the first vowel, as in many
    /// languages of West Africa and the Pacific: j after a front vowel, w after a rounded one.
    /// </summary>
    /*
      a diphthong ends on a close vowel, so the glide is that one's, as ai-a gives aija. After a, ə or
      ɨ, which go with neither, the glide goes with the second vowel instead. A language without the
      glide leaves the vowels as they are
     */
    public static SoundChange GlideInsertion { get; } = new("glide insertion (ki-a → kija, ku-a → kuwa)",
        NeverChanges, Nothing,
        insertsAfter: (site, phonology) =>
        {
            if (site.Sound is not Vowel vowel || site.Next?.Phoneme is not Vowel second)
            {
                return null;
            }
            var first = vowel.Offglide ?? vowel;
            var glide = first.Rounded ? Consonant.w
                : first.Backness == VowelFrontness.Front ? Consonant.j
                : second.Rounded ? Consonant.w : Consonant.j;
            return phonology.Consonants.Contains(glide) ? glide : null;
        });

    /// <summary>
    /// A glottal stop comes between vowels that meet, or h where a language has no glottal stop, as
    /// in Malay or Hawaiian.
    /// </summary>
    public static SoundChange GlottalInsertion { get; } = new("glottal insertion (ka-i → kaʔi)",
        NeverChanges, Nothing,
        insertsAfter: (site, phonology) => site.Sound is Vowel && site.Next?.Phoneme is Vowel
            ? new[] { Consonant.ʔ, Consonant.h }.FirstOrDefault(phonology.Consonants.Contains)
            : null);

    /// <summary>The first of two vowels that meet at a morpheme boundary is lost.</summary>
    public static SoundChange VowelElision { get; } = new("vowel elision (ka-a → ka, ka-o → ko)",
        site => site.Sound is Vowel && site.Next is { Phoneme: Vowel } next && !next.Morpheme.Equals(site.Segment.Morpheme),
        Nothing);

    /// <summary>
    /// Vowels that meet merge into one halfway between them, wherever they meet, as in Sanskrit, where
    /// a + i gives long e and a + u long o.
    /// </summary>
    /*
      the merged vowel keeps the first one's morpheme, so a suffix can be left with no sound of its own.
      Languages that do this turn close vowels into glides first, as Sanskrit does, so it mostly
      merges vowels that glide formation couldn't
     */
    public static SoundChange VowelCoalescence { get; } = new("vowel coalescence (ka-i → keː, ka-u → koː)",
        site => site.Sound is Vowel && (site.Before is Vowel || site.Next?.Phoneme is Vowel),
        (site, phonology) => site.Before is Vowel
            ? null
            : site.Word.Skip(site.Index).Select(s => s.Phoneme).TakeWhile(p => p is Vowel).Cast<Vowel>().Aggregate((first, second) => Merged(first, second, phonology)));

    /*
      schwa gives way to anything. Otherwise the result is rounded if either vowel was, as Greek o wins
      over e, and halfway between their heights, and long if it can be, since it fills the time of two.
      Going by features means it works for any inventory. A diphthong meets the vowel after it with
      the close vowel it ends on, and the vowel before it with the one it starts on, and the result is
      always a single vowel, or the first one, in an inventory without any
     */
    private static Vowel Merged(Vowel first, Vowel second, Phonology phonology)
    {
        (first, second) = (first.Offglide ?? first, second.Start ?? second);
        if (first == Vowel.ə || second == Vowel.ə)
        {
            return first == Vowel.ə ? second : first;
        }
        var rounded = first.Rounded || second.Rounded;
        var height = ((int)first.Height + (int)second.Height) / 2.0;
        return phonology.Vowels.Where(v => v != Vowel.ə && !v.IsDiphthong)
            .MinBy(v => (v.Rounded != rounded, Math.Abs((int)v.Height - height), !v.Long, v == Vowel.ɨ)) ?? first;
    }

    //these come after the changes they list, since static properties are set in the order they're written

    /// <summary>Every change a language's sound changes can have, in the order they apply.</summary>
    public static IReadOnlyList<SoundChange> SoundChangeOrder { get; } =
    [
        NasalAssimilation, Degemination, IntervocalicVoicing, Lenition, VoicingAssimilation, FinalDevoicing, FrontBackHarmony, TongueRootHarmony,
        IdenticalVowelFusion, GlideFormation, DiphthongFormation, VowelElision, VowelCoalescence, GreekContraction, GlideInsertion, GlottalInsertion,
    ];

    /// <summary>Every change a language's syllable changes can have, in the order they apply.</summary>
    public static IReadOnlyList<SoundChange> SyllableChangeOrder { get; } =
    [
        Labialization, GlideVocalization, AccentRetraction, CodaDevoicing, CodaWeakening, CompensatoryLengthening, ClosedSyllableLaxing, VernersLaw, OpenSyllableLengthening, UnstressedShortening,
        UnstressedMonophthongization, Syncope, PretonicSyncope, Apocope, Aphaeresis, UnstressedReduction, UnstressedRaising, CodaAssimilation, OnsetHardening, CodaNasalization,
    ];

    /// <summary>
    /// The sound changes that can also apply across words, in the order they apply: those that only
    /// change a sound by its neighbors, without syllables, deleting or inserting anything.
    /// </summary>
    public static IReadOnlyList<SoundChange> AcrossWordOrder { get; } = [NasalAssimilation, IntervocalicVoicing, Lenition, VoicingAssimilation];

    /// <summary>The change for each kind of harmony, of which a language has one at most.</summary>
    public static IReadOnlyList<(VowelHarmony Kind, SoundChange? Change)> Harmonies { get; } =
        [(VowelHarmony.None, null), (VowelHarmony.FrontBack, FrontBackHarmony), (VowelHarmony.TongueRoot, TongueRootHarmony)];

    public static IReadOnlyList<(HiatusResolution Kind, SoundChange? Change)> HiatusResolutions { get; } =
    [
        (HiatusResolution.None, null), (HiatusResolution.VowelElision, VowelElision), (HiatusResolution.VowelCoalescence, VowelCoalescence),
        (HiatusResolution.GreekContraction, GreekContraction), (HiatusResolution.GlideInsertion, GlideInsertion), (HiatusResolution.GlottalInsertion, GlottalInsertion),
    ];

    public static IReadOnlyList<(UnstressedVowels Kind, SoundChange? Change)> UnstressedChanges { get; } =
        [(UnstressedVowels.None, null), (UnstressedVowels.Reduction, UnstressedReduction), (UnstressedVowels.Raising, UnstressedRaising)];

    public static IReadOnlyList<(ContactRepair Kind, SoundChange? Change)> ContactRepairs { get; } =
    [
        (ContactRepair.None, null), (ContactRepair.CodaAssimilation, CodaAssimilation), (ContactRepair.OnsetHardening, OnsetHardening),
        (ContactRepair.CodaNasalization, CodaNasalization),
    ];
}
