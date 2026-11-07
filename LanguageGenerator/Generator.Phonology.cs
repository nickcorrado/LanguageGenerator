namespace LanguageGenerator;

//inventories, syllables, sandhi and spelling
public static partial class Generator
{
    //each rule is independent of the others, and they apply in this order
    public static List<SoundChange> SelectSoundChanges(Random rand)
    {
        var rules = new List<SoundChange>();
        if (rand.NextDouble() < 0.5)
        {
            rules.Add(SoundChange.NasalAssimilation);
        }
        if (rand.NextDouble() < 0.5)
        {
            rules.Add(SoundChange.Degemination);
        }
        if (rand.NextDouble() < 0.3)
        {
            rules.Add(SoundChange.IntervocalicVoicing);
        }
        return rules;
    }

    //more sound changes, applied after the first ones, in this order. What a language does when vowels
    //meet comes last, so that the vowels it removes still separate consonants for the changes before
    //it, and still set a word's harmony. The odds are guesses, final
    //devoicing is for languages whose syllables can end in a consonant, and harmony is for those with
    //vowels to pair. Harmony is drawn last, so that the changes before it stay as they were for each seed
    public static List<SoundChange> SelectLaterSoundChanges(Random rand, SyllableRule rule, IReadOnlyCollection<Vowel> vowels)
    {
        var changes = new List<SoundChange>();
        if (rand.NextDouble() < 0.25)
        {
            changes.Add(SoundChange.Lenition);
        }
        if (rand.NextDouble() < 0.3)
        {
            changes.Add(SoundChange.VoicingAssimilation);
        }
        if (rand.NextDouble() < 0.3 && rule.Template.Slots.Any(s => s is Slot.Final))
        {
            changes.Add(SoundChange.FinalDevoicing);
        }
        var (hiatus, glideFormation) = SelectHiatusResolution(rand, rule.Resolve(Slot.Consonant));
        var harmony = rand.NextDouble() switch
        {
            < 0.15 => SoundChange.FrontBackHarmony,
            < 0.23 => SoundChange.TongueRootHarmony,
            _ => null,
        };
        if (harmony is not null && harmony.CanApplyTo(vowels))
        {
            changes.Add(harmony);
        }
        //every language merges the same vowel twice, after harmony, which can make two vowels alike
        changes.Add(SoundChange.IdenticalVowelFusion);
        if (glideFormation)
        {
            changes.Add(SoundChange.GlideFormation);
        }
        if (hiatus is not null)
        {
            changes.Add(hiatus);
        }
        return changes;
    }

    //how a language deals with vowels that meet at a morpheme boundary, the only place they meet
    //inside a word: one way for most cases, and maybe glide formation before it for close vowels.
    //Casali's surveys find eliding the first vowel commonest, and glide formation common beside it;
    //the rest of the weights are guesses. Coalescence always comes with glide formation, as in
    //Sanskrit. A language without the consonant an insertion needs does the next best thing
    public static (SoundChange? Main, bool GlideFormation) SelectHiatusResolution(Random rand, IReadOnlyCollection<Consonant> consonants)
    {
        var main = Sampling.Categorical(rand, HiatusResolutions);
        var glides = rand.NextDouble() < 0.4;
        var hasGlide = consonants.Contains(Consonant.j) || consonants.Contains(Consonant.w);
        if (main == SoundChange.GlottalInsertion && !consonants.Contains(Consonant.ʔ) && !consonants.Contains(Consonant.h))
        {
            main = SoundChange.GlideInsertion;
        }
        if (main == SoundChange.GlideInsertion && !hasGlide)
        {
            main = null;
        }
        return (main, hasGlide && (glides || main == SoundChange.VowelCoalescence));
    }

    private static readonly (SoundChange? Change, int Weight)[] HiatusResolutions =
    [
        (SoundChange.VowelElision, 30),
        (SoundChange.VowelCoalescence, 15),
        (SoundChange.GreekContraction, 12),
        (SoundChange.GlideInsertion, 12),
        (SoundChange.GlottalInsertion, 8),
        (null, 23),
    ];

    //changes that depend on syllables, for languages whose syllables can end in a consonant. Devoicing
    //and weakening need obstruents in codas, which a coda restriction rules out, and lengthening needs
    //long vowels. It's drawn last, so that the others stay as they were. The odds are guesses
    public static List<SoundChange> SelectSyllableChanges(Random rand, SyllableRule rule, IReadOnlyCollection<Vowel> vowels)
    {
        var changes = new List<SoundChange>();
        var codas = rule.Template.Slots.Any(s => s is Slot.Final);
        if (rand.NextDouble() < 0.2 && codas && rule.Codas == CodaRestriction.Any)
        {
            changes.Add(SoundChange.CodaDevoicing);
        }
        if (rand.NextDouble() < 0.15 && codas && rule.Codas == CodaRestriction.Any)
        {
            changes.Add(SoundChange.CodaWeakening);
        }
        var laxes = rand.NextDouble() < 0.15 && codas;
        if (rand.NextDouble() < 0.12 && codas && vowels.Any(v => v.Long))
        {
            changes.Add(SoundChange.CompensatoryLengthening);
        }
        if (laxes)
        {
            changes.Add(SoundChange.ClosedSyllableLaxing);
        }
        return changes;
    }

    //the central vowels are the usual epenthetic vowels, schwa and then ɨ, as in Korean, and after
    //them the close ones, i more than u, as in Japanese; failing those, the language's commonest
    //vowel, which is the first one since picks are biased toward the front
    public static Vowel SelectEpentheticVowel(IReadOnlyList<Vowel> vowels) =>
        new[] { Vowel.ə, Vowel.ɨ, Vowel.i, Vowel.u }.FirstOrDefault(vowels.Contains) ?? vowels[0];

    //a set of conventions, applied to the whole inventory. Conventions that clash for this
    //inventory (acute long vowels beside acute close vowels both give ó, say) are drawn again as
    //a set, so a language never ends up with a convention applied to only some of its sounds.
    //IPA is the fallback, which never clashes
    public static Orthography SelectOrthography(Random rand, IReadOnlyList<Vowel> vowels, IReadOnlyList<Consonant> consonants)
    {
        var inventory = vowels.Concat<Phoneme>(consonants).ToList();
        T Pick<T>(IReadOnlyList<T> options) => options[rand.Next(options.Count)];
        for (int attempt = 0; attempt < 500; attempt++)
        {
            var style = new OrthographicStyle(
                Pick(Enum.GetValues<PostalveolarSpelling>()),
                Pick(Enum.GetValues<LongVowelSpelling>()),
                Pick(Enum.GetValues<MidVowelSpelling>()),
                Pick(OrthographicStyle.SchwaOptions),
                Pick(OrthographicStyle.NearOpenFrontOptions),
                Pick(OrthographicStyle.VelarStopOptions),
                Pick(OrthographicStyle.PalatalGlideOptions),
                Pick(Enum.GetValues<DentalFricativeSpelling>()),
                Pick(OrthographicStyle.VelarNasalOptions),
                Pick(OrthographicStyle.VelarFricativeOptions),
                Pick(Enum.GetValues<DentalAffricateSpelling>()),
                Pick(Enum.GetValues<AspirationSpelling>()),
                Pick(OrthographicStyle.GlottalStopOptions),
                Pick(OrthographicStyle.PalatalNasalOptions),
                Pick(OrthographicStyle.PalatalStopOptions),
                Pick(OrthographicStyle.CloseCentralOptions));
            var spellings = style.Spell(inventory);
            if (Orthography.AreDistinct(spellings.Values))
            {
                return new Orthography(spellings, style);
            }
        }
        return new Orthography(inventory.ToDictionary(p => p, p => p.IpaSymbol));
    }

    //needsOnset fills the C slot if the onset would otherwise be empty, for a syllable that follows
    //another inside a root
    public static Syllable GenerateSyllable(Random rand, SyllableRule rule, double dropoff, bool needsOnset = false)
    {
        var onset = new List<Consonant>();
        Vowel? nucleus = null;
        Consonant? coda = null;

        foreach (var slot in rule.Template.Slots)
        {
            if (slot is Slot.Vowel)
            {
                if (needsOnset && onset.Count == 0 && rule.Template.Slots.Contains(Slot.Consonant)
                    && Pick(rand, rule.Resolve(Slot.Consonant), 1, dropoff) is { } forced)
                {
                    onset.Add(forced);
                }
                nucleus = Pick(rand, rule.Vowels, 1, dropoff) ?? throw new InvalidOperationException("The inventory has no vowels.");
            }
            //an onset consonant has to be able to follow the one before it, so pl but not nl
            else if (Pick(rand, nucleus is null && onset.Count > 0
                    ? new NaturalClass<Consonant>(rule.Resolve(slot).Where(c => SyllableRule.CanPrecede(onset[^1], c)))
                    : rule.Resolve(slot),
                FillChance(rule.Template, slot), dropoff) is { } consonant)
            {
                if (nucleus is null)
                {
                    onset.Add(consonant);
                }
                else
                {
                    coda = consonant;
                }
            }
        }

        //the template guarantees exactly one vowel slot
        return ElideDuplicates(new Syllable([.. onset], nucleus!, coda));
    }

    //how often a consonantal slot is filled. Every language has syllables with onsets, and most
    //syllables have one: onsets are filled 85% of the time, or about that in templates with two
    //consonants that can start one. A guess, but onsetless syllables are dispreferred everywhere, and
    //at even odds, a quarter of all syllables were a bare vowel, which made homophones of most of the
    //short words
    private static double FillChance(SyllableTemplate template, Slot slot)
    {
        var sibilantOnset = template.Slots.Contains(Slot.Sibilant);
        return slot switch
        {
            Slot.Consonant => sibilantOnset ? 0.7 : 0.85,
            Slot.Sibilant => 0.5,
            Slot.Liquid => sibilantOnset ? 0.2 : 0.3,
            Slot.Final => 0.5,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
        };
    }

    private static T? Pick<T>(Random rand, NaturalClass<T> phonemes, double chance, double dropoff) where T : Phoneme
    {
        //pruning can empty a category (say, both sibilants); then the slot just goes unfilled
        if (!phonemes.Any() || rand.NextDouble() >= chance)
        {
            return null;
        }
        return phonemes[Sampling.TruncatedGeometric(rand, phonemes.Count, dropoff)];
    }

    //if a word has doubled phonemes in the onset, like ppa or nokks,
    //collapse them to one phoneme
    //n.b. this has no effect on geminates, which occur between syllables
    public static Syllable ElideDuplicates(Syllable syllable)
    {
        var onset = syllable.Onset;
        var onsetResult = new List<Consonant>();
        foreach (var c in onset)
        {
            if (onsetResult.Count == 0 || onsetResult.Last() != c)
            {
                onsetResult.Add(c);
            }
        }

        var result = new Syllable([.. onsetResult], syllable.Nucleus, syllable.Coda);
        return result;
    }

    public static SyllableRule SelectSyllableRule(Random rand, IReadOnlyList<Vowel> vowels, IReadOnlyList<Consonant> consonants)
    {
        var template = SyllableTemplates.All[rand.Next(SyllableTemplates.All.Count)];
        //a restriction only matters for templates with a coda
        var codas = template.Slots.Any(s => s is Slot.Final) ? Sampling.Categorical(rand, CodaRestrictionWeights) : CodaRestriction.Any;
        return new SyllableRule(template, vowels, consonants, codas);
    }

    //weights, and a guess: WALS's chapter on syllable structure counts how many consonants may close
    //a syllable but not which ones, and no survey of that turned up
    private static readonly (CodaRestriction Value, int Weight)[] CodaRestrictionWeights =
    [
        (CodaRestriction.Any, 50),
        (CodaRestriction.Sonorants, 25),
        (CodaRestriction.Nasals, 15),
        (CodaRestriction.SingleNasal, 10),
    ];

    //a vowel system is built, not pruned: how many qualities it has, then which, then whether they
    //come long too. Vowels spread out to fill the space (Liljencrants and Lindblom's dispersion), so
    //each size has its usual shapes, and a three-vowel language has a i u rather than any three
    public static List<Vowel> GenerateVowels(Random rand)
    {
        var qualities = Sampling.Categorical(rand, Sampling.Categorical(rand, VowelSystemBands));

        //length is a property of the system, as in Finnish or Japanese, where it doubles every
        //vowel, so a long vowel never comes without its short one. Mid vowels are the likeliest to
        //miss out, as in Classical Arabic's long ā ī ū. Both odds are guesses: WALS has no chapter
        //on length
        var longs = new List<Vowel>();
        if (rand.NextDouble() < 0.35)
        {
            var dropLongMids = rand.NextDouble() < 0.25;
            longs.AddRange(qualities
                .Where(v => !(dropLongMids && v.Height is VowelHeight.CloseMid))
                .Select(v => v.Lengthened)
                .OfType<Vowel>());
        }

        //the order is a frequency ranking: Pick draws earlier phonemes more often. Unmarked vowels
        //are the commonest in words as they are across languages, so the ranking follows
        //VowelMarkedness, jittered so that languages don't all share it, and long vowels, which
        //are rarer in words than short ones, come after them
        return [.. qualities.Concat(longs)
            .Select(v => (Vowel: v, Rank: Array.IndexOf(VowelMarkedness, v) + (v.Long ? VowelMarkedness.Length : 0) + rand.NextDouble() * 2))
            .OrderBy(x => x.Rank)
            .Select(x => x.Vowel)];
    }

    //short vowels from commonest to rarest across languages, roughly as PHOIBLE counts them
    private static readonly Vowel[] VowelMarkedness = [Vowel.a, Vowel.i, Vowel.u, Vowel.e, Vowel.o, Vowel.ə, Vowel.ɛ, Vowel.ɔ, Vowel.ɨ, Vowel.æ];

    //WALS 2A counts languages by how many vowel qualities they have: 93 with 2 to 4, 287 with 5 or
    //6, and 184 with 7 to 14. Within each band, the systems and their weights are guesses at how
    //common each shape is, five vowels being much the commonest size. This inventory has ten short
    //vowels, so the large band tops out at ten
    private static readonly (Vowel[] Qualities, int Weight)[] SmallVowelSystems =
    [
        ([Vowel.a, Vowel.ə], 3),                                 //vertical, as in Kabardian
        ([Vowel.a, Vowel.i, Vowel.u], 50),                       //Arabic, Quechua
        ([Vowel.a, Vowel.i, Vowel.u, Vowel.e], 15),
        ([Vowel.a, Vowel.i, Vowel.u, Vowel.o], 15),
        ([Vowel.a, Vowel.i, Vowel.u, Vowel.ə], 12),
        ([Vowel.a, Vowel.i, Vowel.u, Vowel.ɨ], 5),
    ];

    private static readonly (Vowel[] Qualities, int Weight)[] AverageVowelSystems =
    [
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u], 65),     //Spanish, Japanese, Swahili
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.ə], 15),
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.ɨ], 10),  //Guarani, common in the Americas
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.ɛ], 5),
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.æ], 5),
    ];

    private static readonly (Vowel[] Qualities, int Weight)[] LargeVowelSystems =
    [
        ([Vowel.a, Vowel.e, Vowel.ɛ, Vowel.i, Vowel.o, Vowel.ɔ, Vowel.u], 45),                   //Italian, Yoruba
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.ə, Vowel.æ], 12),
        ([Vowel.a, Vowel.e, Vowel.i, Vowel.o, Vowel.u, Vowel.ə, Vowel.ɨ], 10),                   //Mapudungun
        ([Vowel.a, Vowel.e, Vowel.ɛ, Vowel.i, Vowel.o, Vowel.ɔ, Vowel.u, Vowel.ə], 23),          //Catalan
        ([Vowel.a, Vowel.e, Vowel.ɛ, Vowel.i, Vowel.o, Vowel.ɔ, Vowel.u, Vowel.ə, Vowel.æ], 7),
        ([Vowel.a, Vowel.e, Vowel.ɛ, Vowel.i, Vowel.o, Vowel.ɔ, Vowel.u, Vowel.ə, Vowel.ɨ, Vowel.æ], 3),
    ];

    private static readonly ((Vowel[] Qualities, int Weight)[] Systems, int Weight)[] VowelSystemBands =
        [(SmallVowelSystems, 93), (AverageVowelSystems, 287), (LargeVowelSystems, 184)];

    //a consonant inventory is built, not pruned. Common sounds are near-certain and rare ones
    //unlikely, a voicing contrast is decided for a whole series at once (Clements's feature
    //economy: a language that voices its stops voices them across the board, b d g, not just d),
    //and marked sounds come only with the sounds they imply: z with s, ʒ with ʃ, ŋ with k, and so
    //on. Each sound's odds are roughly the share of PHOIBLE's inventories that have it, counting
    //dental and alveolar as one place, as this inventory does, except where WALS has a closer figure.
    //
    //Real inventories run larger than these, from 6 to over 80 (WALS 1A), partly by stacking series
    //left out here, like ejectives, implosives or clicks, since a player has to be able to say a
    //language to learn it. So sizes here run smaller than WALS's counts, but which sounds a language
    //has should look like a real one
    public static List<Consonant> GenerateConsonants(Random rand)
    {
        //some languages have more of everything optional than others. Without this, every
        //inventory would come out near the same size. Its range is a guess
        var richness = 0.6 + rand.NextDouble() * 0.8;
        bool Likely(double chance) => rand.NextDouble() < chance;
        bool Optional(double chance) => rand.NextDouble() < Math.Min(1, chance * richness);

        //WALS 4A: of 567 languages, 182 contrast voicing in neither stops nor fricatives, 189 in
        //stops alone, 38 in fricatives alone and 158 in both
        var (voicedStops, voicedFricatives) = Sampling.Categorical(rand,
            [((false, false), 182), ((true, false), 189), ((false, true), 38), ((true, true), 158)]);

        var consonants = new HashSet<Consonant>();
        void Add(Consonant consonant, bool present)
        {
            if (present)
            {
                consonants.Add(consonant);
            }
        }

        //stops. WALS 5A: 40 of 567 languages lack p, mostly ones with b, like Arabic, and 41 of the
        //347 with voiced stops lack g. Some languages lack t or k, like Hawaiian
        Add(Consonant.p, Likely(1 - 40 / 567.0));
        Add(Consonant.t, Likely(0.98));
        Add(Consonant.k, Likely(0.98) || !consonants.Contains(Consonant.t));
        if (voicedStops)
        {
            Add(Consonant.b, Likely(0.95));
            Add(Consonant.d, Likely(0.97));
            Add(Consonant.g, consonants.Contains(Consonant.k) && Likely(1 - 41 / 347.0));
        }
        //a series of aspirates, as in Mandarin or Korean, where voicing doesn't matter, or beside
        //voiced stops, as in Hindi or Thai. WALS doesn't count it; roughly a quarter of PHOIBLE's
        //inventories have kʰ
        if (Optional(0.25))
        {
            Add(Consonant.pʰ, consonants.Contains(Consonant.p) && Likely(0.95));
            Add(Consonant.tʰ, consonants.Contains(Consonant.t));
            Add(Consonant.kʰ, consonants.Contains(Consonant.k) && Likely(0.95));
        }
        //labialized velars, as in Hausa or Amharic, in perhaps one inventory in seven
        Add(Consonant.kʷ, consonants.Contains(Consonant.k) && Optional(0.15));
        Add(Consonant.gʷ, consonants.Contains(Consonant.kʷ) && consonants.Contains(Consonant.g) && Likely(0.8));
        Add(Consonant.ʔ, Optional(0.38));
        //a palatal stop, as in Hungarian, and a uvular one, which comes beside k, as in Quechua or
        //Arabic: each in roughly one inventory in seven or eight in PHOIBLE
        Add(Consonant.c, Optional(0.14));
        Add(Consonant.q, consonants.Contains(Consonant.k) && Optional(0.13));

        Add(Consonant.m, Likely(0.97));
        Add(Consonant.n, Likely(0.98));
        Add(Consonant.ŋ, consonants.Contains(Consonant.k) && Optional(0.6));
        Add(Consonant.ɲ, Optional(0.4));

        //fricatives. Some languages have none at all, as is usual in Australia, and s is much
        //the commonest where there are any. Voiced fricatives come with their voiceless partners
        if (Likely(0.9))
        {
            Add(Consonant.s, Likely(0.9));
            Add(Consonant.h, Optional(0.6));
            Add(Consonant.f, Optional(0.45));
            Add(Consonant.ʃ, consonants.Contains(Consonant.s) && Optional(0.45));
            Add(Consonant.x, Optional(0.2));
            //θ hardly ever comes without s, as in English, Greek or Castilian Spanish. Its odds
            //given s keep it in about one language in twenty overall
            Add(Consonant.θ, consonants.Contains(Consonant.s) && Optional(0.06));
            if (voicedFricatives)
            {
                Add(Consonant.z, consonants.Contains(Consonant.s));
                Add(Consonant.v, consonants.Contains(Consonant.f) && Likely(0.85));
                Add(Consonant.ʒ, consonants.Contains(Consonant.ʃ) && Likely(0.7));
                Add(Consonant.ð, consonants.Contains(Consonant.θ) && Likely(0.8));
            }
        }

        //affricates voice along with the stops, as English ʧ ʤ do with p b
        Add(Consonant.ʧ, Optional(0.4));
        Add(Consonant.ʤ, voicedStops && consonants.Contains(Consonant.ʧ) && Likely(0.85));
        Add(Consonant.ʦ, Optional(0.2));
        Add(Consonant.ʣ, voicedStops && consonants.Contains(Consonant.ʦ) && Likely(0.7));

        Add(Consonant.j, Likely(0.9));
        Add(Consonant.w, Likely(0.82));
        //WALS 8A: 95 of 567 languages have no lateral at all
        Add(Consonant.l, Likely(0.75));
        //most languages have one rhotic, a trill or a tap far more often than English's ɹ. Some,
        //like Spanish, have both a trill and a tap, but English speakers struggle to hear them
        //apart, so the weights, which are guesses, keep that rare
        if (Likely(0.75))
        {
            foreach (var rhotic in Sampling.Categorical(rand, Rhotics))
            {
                consonants.Add(rhotic);
            }
        }

        //the order is a frequency ranking: Pick draws earlier phonemes more often. Unmarked
        //consonants are the commonest in words as they are across languages, so the ranking
        //follows ConsonantMarkedness, jittered so that languages don't all share it
        return [.. consonants
            .Select(c => (Consonant: c, Rank: Array.IndexOf(ConsonantMarkedness, c) + rand.NextDouble() * 3))
            .OrderBy(x => x.Rank)
            .Select(x => x.Consonant)];
    }

    private static readonly (Consonant[] Rhotics, int Weight)[] Rhotics =
    [
        ([Consonant.r], 42),
        ([Consonant.ɾ], 38),
        ([Consonant.ɹ], 12),
        ([Consonant.r, Consonant.ɾ], 8),
    ];

    //consonants from commonest to rarest in words: coronals first, as in most lexicons, then roughly
    //by how many languages have them
    private static readonly Consonant[] ConsonantMarkedness =
    [
        Consonant.t, Consonant.n, Consonant.k, Consonant.m, Consonant.s, Consonant.p, Consonant.l, Consonant.j,
        Consonant.w, Consonant.r, Consonant.ɾ, Consonant.h, Consonant.ʔ, Consonant.ɹ, Consonant.b, Consonant.d,
        Consonant.g, Consonant.ŋ, Consonant.ɲ, Consonant.f, Consonant.ʃ, Consonant.ʧ, Consonant.tʰ, Consonant.kʰ,
        Consonant.pʰ, Consonant.ʦ, Consonant.z, Consonant.v, Consonant.ʤ, Consonant.kʷ, Consonant.c, Consonant.x,
        Consonant.q, Consonant.gʷ, Consonant.ʣ, Consonant.ʒ, Consonant.θ, Consonant.ð,
    ];
}
