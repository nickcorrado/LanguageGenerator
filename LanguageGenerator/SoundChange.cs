namespace LanguageGenerator;

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
//each change is applied across a whole word, left to right, so it sees morpheme boundaries, and a
//language applies its changes in order, so one can feed or bleed the next. Changes only ever use
//phonemes from the language's own inventory; where it has no suitable phoneme, the sound is left
//alone. A change is a value, so a language's are a list that can be drawn, compared or extended.
//Besides changing or deleting a sound, a change can insert one after it, which belongs to the
//morpheme after it, as an inserted consonant starts the suffix it comes before. Only changes
//that run before syllables can insert, since syllable places are counted by sound
public sealed class SoundChange(string name, Func<Site, bool> when, Func<Site, Phonology, Phoneme?> becomes, (Vowel First, Vowel Second)[]? pairs = null,
    Func<Site, Phonology, Phoneme?>? insertsAfter = null)
{
    public override string ToString() => name;

    public List<Segment> Apply(List<Segment> segments, Phonology phonology, IReadOnlyList<SyllablePlace>? places = null)
    {
        var result = new List<Segment>();
        for (int i = 0; i < segments.Count; i++)
        {
            var site = new Site(segments[i], result.LastOrDefault(), i + 1 < segments.Count ? segments[i + 1] : null, segments, i, places?[i], places);
            if (!when(site))
            {
                result.Add(segments[i]);
            }
            else if (becomes(site, phonology) is { } sound)
            {
                result.Add(segments[i] with { Phoneme = sound });
            }
            if (places is null && site.Next is { } next && insertsAfter?.Invoke(site, phonology) is { } inserted)
            {
                result.Add(new Segment(inserted, next.Morpheme));
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
    //only where the new nasal can still close its syllable. A language whose codas allow only n, like
    //Japanese, would otherwise turn n before k into a ŋ it can't end a syllable with, and cluster
    //repair would then delete it. Japanese's n does take the next consonant's place, but as one
    //phoneme, which there's no way to say here, so the n stays n
    public static SoundChange NasalAssimilation { get; } = new("nasal assimilation (np → mp)",
        site => site.Sound is Consonant { Manner: ConsonantManner.Nasal } nasal
            && site.Next?.Phoneme is Consonant { Manner: ConsonantManner.Plosive or ConsonantManner.Nasal } next && next.Place != nasal.Place,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, place: ((Consonant)site.Next!.Phoneme).Place) is { } assimilated
            && phonology.SyllableRule.IsLegalCoda([assimilated])
                ? assimilated
                : site.Sound);

    /// <summary>An unvoiced consonant between vowels takes its voiced counterpart.</summary>
    public static SoundChange IntervocalicVoicing { get; } = new("intervocalic voicing (apa → aba)",
        site => site.Sound is Consonant { Voiced: false } && site.BetweenVowels,
        (site, phonology) => phonology.Counterpart((Consonant)site.Sound, voiced: true) ?? site.Sound);

    //what each stop weakens to, the likeliest first, of what the language has
    private static readonly Dictionary<Phoneme, Consonant[]> Lenited = new()
    {
        [Consonant.p] = [Consonant.f],
        [Consonant.b] = [Consonant.v],
        [Consonant.t] = [Consonant.θ, Consonant.s],
        [Consonant.d] = [Consonant.ð, Consonant.z],
        [Consonant.k] = [Consonant.x],
        [Consonant.g] = [Consonant.w],
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
    //after voicing where a language has both, so that p can weaken all the way to v
    public static SoundChange Lenition { get; } = new("lenition (apa → afa)",
        site => site.BetweenVowels && Lenited.ContainsKey(site.Sound),
        (site, phonology) => Lenited[site.Sound].FirstOrDefault(phonology.Consonants.Contains) ?? site.Sound);

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
    //features it's central. ə and ɨ have no partner, so they're neutral
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

    //harmony spreads from the root both ways, so prefixes agree too, and it reaches the root's own
    //vowels, since real languages with harmony have roots that keep it. A vowel with no partner, like
    //ə, is neutral: it doesn't change and harmony passes through it. It runs before epenthesis, so an
    //inserted vowel doesn't take part
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
        (site, phonology) => phonology.Consonants.Contains(Consonant.h) ? Consonant.h : site.Sound);

    private static readonly Dictionary<Phoneme, Vowel> Laxed = new() { [Vowel.e] = Vowel.ɛ, [Vowel.o] = Vowel.ɔ };

    /// <summary>
    /// A close-mid vowel opens in a syllable that ends in a consonant, as French fête has the open
    /// vowel that féter lacks.
    /// </summary>
    public static SoundChange ClosedSyllableLaxing { get; } = new("closed-syllable laxing (pet → pɛt)",
        site => site is { Place: { Position: SyllablePosition.Nucleus, Closed: true } } && Laxed.ContainsKey(site.Sound),
        (site, phonology) => phonology.Vowels.Contains(Laxed[site.Sound]) ? Laxed[site.Sound] : site.Sound);

    private static bool Unstressed(Site site) => site.Place is { Position: SyllablePosition.Nucleus, Stressed: false };

    /// <summary>
    /// A long vowel loses its length outside the stressed syllable, as length followed stress in
    /// Latin's daughters.
    /// </summary>
    public static SoundChange UnstressedShortening { get; } = new("unstressed shortening (paːˈta → paˈta)",
        site => Unstressed(site) && site.Sound is Vowel { Long: true },
        (site, phonology) => phonology.Vowels.FirstOrDefault(v => !v.Long && v.Lengthened == site.Sound) ?? site.Sound);

    /// <summary>
    /// A stressed vowel in an open syllable lengthens, as in Italian, where the first vowel of casa is
    /// long but not that of carta.
    /// </summary>
    public static SoundChange OpenSyllableLengthening { get; } = new("open-syllable lengthening (ˈpata → ˈpaːta)",
        site => site.Place is { Position: SyllablePosition.Nucleus, Stressed: true, Closed: false } && site.Sound is Vowel { Lengthened: not null },
        (site, phonology) => phonology.Vowels.Contains(((Vowel)site.Sound).Lengthened!) ? ((Vowel)site.Sound).Lengthened : site.Sound);

    /// <summary>
    /// A short vowel right after the stressed syllable is lost between single consonants, as Latin
    /// calidus became Italian caldo, so long as the consonants can then close one syllable and open
    /// the next.
    /// </summary>
    //the vowel needs a vowel two sounds away on each side, the stressed one before it, so it's never
    //in the last syllable, and only one vowel goes, leaving a cluster of two. The stress is found
    //again afterwards, over the syllables that are left
    public static SoundChange Syncope { get; } = new("syncope (ˈkalida → ˈkalda)",
        site => Unstressed(site) && site.Sound is Vowel { Long: false } && site.Index >= 2 && site.Index + 2 < site.Word.Count
            && site.Word[site.Index - 1].Phoneme is Consonant && site.Word[site.Index - 2].Phoneme is Vowel
            && site.Places?[site.Index - 2].Stressed == true
            && site.Word[site.Index + 1].Phoneme is Consonant && site.Word[site.Index + 2].Phoneme is Vowel,
        (site, phonology) => phonology.SyllableRule.IsLegalCoda([(Consonant)site.Word[site.Index - 1].Phoneme]) ? null : site.Sound);

    //how each unstressed vowel weakens
    private static readonly Dictionary<Phoneme, Vowel> Raised = new()
    {
        [Vowel.e] = Vowel.i, [Vowel.ɛ] = Vowel.i, [Vowel.o] = Vowel.u, [Vowel.ɔ] = Vowel.u,
    };

    /// <summary>
    /// An unstressed vowel other than a close one weakens to schwa, as in English or Catalan.
    /// </summary>
    public static SoundChange UnstressedReduction { get; } = new("unstressed reduction (ˈpata → ˈpatə)",
        site => Unstressed(site) && site.Sound is Vowel { Long: false, Height: not VowelHeight.Close } vowel && vowel != Vowel.ə,
        (site, phonology) => phonology.Vowels.Contains(Vowel.ə) ? Vowel.ə : site.Sound);

    /// <summary>
    /// An unstressed mid vowel rises to a close one, e to i and o to u, as in Portuguese or Bulgarian.
    /// </summary>
    public static SoundChange UnstressedRaising { get; } = new("unstressed raising (ˈpeto → ˈpetu)",
        site => Unstressed(site) && Raised.ContainsKey(site.Sound),
        (site, phonology) => phonology.Vowels.Contains(Raised[site.Sound]) ? Raised[site.Sound] : site.Sound);

    //a coda and the onset consonant right after it, as they met before this change
    private static (Consonant Coda, Consonant Onset)? ContactAfter(Site site) =>
        site is { Place.Position: SyllablePosition.Coda, Sound: Consonant coda, Next.Phoneme: Consonant onset } ? (coda, onset) : null;

    private static (Consonant Coda, Consonant Onset)? ContactBefore(Site site) =>
        site is { Place.Position: SyllablePosition.Onset, Sound: Consonant onset, Before: Consonant coda }
            && site.Places![site.Index - 1].Position == SyllablePosition.Coda
            ? (coda, onset)
            : null;

    //the contact repairs below change only what the language's syllable contact doesn't allow, so
    //where morphemes meet, a coda and an onset that roots would never put together are brought
    //into line, as each language does it

    /// <summary>
    /// An obstruent closing a syllable becomes a nasal before a nasal, as Korean kuk-mul 'soup' is
    /// said kuŋmul.
    /// </summary>
    public static SoundChange CodaNasalization { get; } = new("coda nasalization (ak.ma → aŋ.ma)",
        site => ContactAfter(site) is ({ IsObstruent: true }, { Manner: ConsonantManner.Nasal }),
        (site, phonology) => ContactAfter(site) is (var coda, var onset) && !phonology.SyllableContact.Allows(coda, onset)
            && phonology.Counterpart(coda, manner: ConsonantManner.Nasal, voiced: true) is { } nasal && phonology.SyllableRule.IsLegalCoda([nasal])
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
            && phonology.SyllableRule.IsLegalCoda([onset])
                ? onset
                : site.Sound);

    private static readonly Phoneme[] LostCodas = [Consonant.h, Consonant.ɹ, Consonant.r, Consonant.ɾ, Consonant.s];

    /// <summary>
    /// A coda is lost and the vowel before it lengthens to make up for it, as English car lost its r
    /// and Old French teste its s.
    /// </summary>
    //both halves apply only where the language has the long vowel, so a coda is never lost for nothing.
    //It follows coda weakening, which it can feed, and precedes laxing, whose closed syllables it opens
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
    //nearly every language does this, even ones that otherwise leave vowels side by side, so every
    //language has it. The merged vowel keeps the first one's morpheme, so a suffix can be left with
    //no sound of its own, and it runs before the language's own way with vowels that meet
    public static SoundChange IdenticalVowelFusion { get; } = new("identical vowel fusion (ka-a → kaː)",
        site => site.Sound is Vowel vowel
            && (site.Next?.Phoneme is Vowel next && SameQuality(vowel, next) || site.Previous?.Phoneme is Vowel previous && SameQuality(vowel, previous)),
        (site, phonology) => site.Previous?.Phoneme is Vowel previous && SameQuality((Vowel)site.Sound, previous)
            ? null
            : ((Vowel)site.Sound).Lengthened is { } lengthened && phonology.Vowels.Contains(lengthened) ? lengthened : site.Sound);

    private static bool SameQuality(Vowel first, Vowel second) =>
        first.Height == second.Height && first.Backness == second.Backness && first.Rounded == second.Rounded;

    private static bool IsHigh(Vowel vowel) => vowel.Height == VowelHeight.Close;

    //the glide each close vowel becomes, or is echoed by
    private static readonly Dictionary<Phoneme, Consonant> Glides = new()
    {
        [Vowel.i] = Consonant.j, [Vowel.iː] = Consonant.j, [Vowel.u] = Consonant.w, [Vowel.uː] = Consonant.w,
    };

    /// <summary>
    /// A close vowel before another vowel becomes a glide, i to j and u to w, as in Spanish or
    /// Sanskrit, where i + a gives ya.
    /// </summary>
    //only where the syllable the glide joins is legal: the consonants before it have to split into
    //a coda for the syllable before, if there is one, and an onset ending in the glide. Elsewhere the
    //vowel stays, for whatever else the language does with vowels that meet. ɨ has no glide, so it
    //stays too
    public static SoundChange GlideFormation { get; } = new("glide formation (ki-a → kja)",
        site => Glides.ContainsKey(site.Sound) && site.Next?.Phoneme is Vowel,
        (site, phonology) => Glides[site.Sound] is var glide && phonology.Consonants.Contains(glide) && CanStartSyllable(glide, site, phonology.SyllableRule)
            ? glide
            : site.Sound);

    private static bool CanStartSyllable(Consonant glide, Site site, SyllableRule rule)
    {
        var cluster = site.Word.Take(site.Index).Reverse().TakeWhile(s => s.Phoneme is Consonant).Select(s => (Consonant)s.Phoneme).Reverse().ToList();
        //a coda needs a vowel before it; at the start of a word, the whole cluster is the onset
        var codaLengths = site.Index > cluster.Count ? Enumerable.Range(0, cluster.Count + 1) : [0];
        return codaLengths.Any(c => rule.IsLegalCoda(cluster[..c]) && rule.IsLegalOnset([.. cluster[c..], glide]));
    }

    /// <summary>
    /// Two vowels that meet, neither of them close, merge into one long vowel, as in Attic Greek:
    /// a rounded vowel wins, as e + o gave ō, and otherwise the first one does, as a + e gave ā.
    /// </summary>
    //close vowels don't contract, as Greek ι and υ didn't, so they're left for glide formation, or
    //left alone. Schwa gives way to anything. The vowel is long where the language has it long
    public static SoundChange GreekContraction { get; } = new("Greek contraction (ka-e → kaː, ke-o → koː)",
        site => site.Sound is Vowel vowel && !IsHigh(vowel)
            && (site.Before is Vowel before && !IsHigh(before) || site.Next?.Phoneme is Vowel next && !IsHigh(next)),
        (site, phonology) =>
        {
            if (site.Before is Vowel before && !IsHigh(before))
            {
                return null;
            }
            var run = site.Word.Skip(site.Index).Select(s => s.Phoneme).TakeWhile(p => p is Vowel v && !IsHigh(v)).Cast<Vowel>().ToList();
            var full = run.Where(v => v != Vowel.ə).DefaultIfEmpty(Vowel.ə).ToList();
            var winner = full.FirstOrDefault(v => v.Rounded) ?? full[0];
            return winner.Lengthened is { } lengthened && phonology.Vowels.Contains(lengthened) ? lengthened : winner;
        });

    /// <summary>
    /// A glide comes between vowels that meet, the one that goes with the first vowel, as in many
    /// languages of West Africa and the Pacific: j after a front vowel, w after a rounded one.
    /// </summary>
    //after a, ə or ɨ, which go with neither, the glide goes with the second vowel instead. A language
    //without the glide leaves the vowels as they are
    public static SoundChange GlideInsertion { get; } = new("glide insertion (ki-a → kija, ku-a → kuwa)",
        NeverChanges, Nothing,
        insertsAfter: (site, phonology) =>
        {
            if (site.Sound is not Vowel first || site.Next?.Phoneme is not Vowel second)
            {
                return null;
            }
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
    //the merged vowel keeps the first one's morpheme, so a suffix can be left with no sound of its own.
    //Languages that do this turn close vowels into glides first, as Sanskrit does, so it mostly
    //merges vowels that glide formation couldn't
    public static SoundChange VowelCoalescence { get; } = new("vowel coalescence (ka-i → keː, ka-u → koː)",
        site => site.Sound is Vowel && (site.Before is Vowel || site.Next?.Phoneme is Vowel),
        (site, phonology) => site.Before is Vowel
            ? null
            : site.Word.Skip(site.Index).Select(s => s.Phoneme).TakeWhile(p => p is Vowel).Cast<Vowel>().Aggregate((first, second) => Merged(first, second, phonology)));

    //schwa gives way to anything. Otherwise the result is rounded if either vowel was, as Greek o wins
    //over e, and halfway between their heights, and long if it can be, since it fills the time of two.
    //Going by features means it works for any inventory
    private static Vowel Merged(Vowel first, Vowel second, Phonology phonology)
    {
        if (first == Vowel.ə || second == Vowel.ə)
        {
            return first == Vowel.ə ? second : first;
        }
        var rounded = first.Rounded || second.Rounded;
        var height = ((int)first.Height + (int)second.Height) / 2.0;
        return phonology.Vowels.Where(v => v != Vowel.ə)
            .MinBy(v => (v.Rounded != rounded, Math.Abs((int)v.Height - height), !v.Long, v == Vowel.ɨ))!;
    }
}
