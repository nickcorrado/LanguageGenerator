namespace LanguageGenerator;

//chooses everything about a language. The result is a plain Language, which
//knows nothing about how it was chosen
public static class Generator
{
    public static Language Generate(Random rand)
    {
        //all percentage chances in this program should bubble up somehow, so that they can be easily tweaked in one place
        var syllableDropoff = 0.15;

        var vowels = GenerateVowels(rand, 0.15, 0.30);
        var consonants = GenerateConsonants(rand, 0.15, 0.50);
        var syllableRule = SelectSyllableRule(rand, vowels, consonants);
        var phonology = new Phonology
        {
            Vowels = vowels,
            Consonants = consonants,
            SyllableRule = syllableRule,
            SandhiRules = SelectSandhiRules(rand),
            ClusterRepair = rand.NextDouble() < 0.6 ? ClusterRepair.Epenthesis : ClusterRepair.Deletion,
            EpentheticVowel = SelectEpentheticVowel(vowels),
        };

        var lexicon = GenerateLexicon(rand, syllableRule, syllableDropoff);

        //word order comes first because how much a language prefixes depends on it
        var wordOrder = SelectWordOrder(rand);
        var morphology = SelectMorphologicalProfile(rand, wordOrder.ConstituentWordOrder);

        var alignment = SelectAlignment(rand, 0.5, 0.25);
        var cases = SelectCases(rand, alignment, 0.75, 0.6, 0.35, 0.65, 0.25, 0.2);
        var paradigms = GenerateParadigms(rand, morphology, wordOrder, alignment, cases, phonology, syllableDropoff);

        return new Language
        {
            Phonology = phonology,
            Lexicon = lexicon,
            Paradigms = paradigms,
            MorphologicalProfile = morphology,
            WordOrder = wordOrder,
            HasArticles = rand.NextDouble() < 0.7,
            MorphosyntacticAlignment = alignment,
            Cases = cases,
            RoleMarkers = SelectRoleMarkers(alignment, cases, wordOrder),
        };
    }

    //basic meanings to fill the lexicon with, glossed the way the Leipzig rules gloss roots
    private static readonly string[] NounMeanings = ["person", "dog", "cat", "fish", "bird", "tree", "stone", "water", "fire", "sun", "house"];
    private static readonly string[] VerbMeanings = ["see", "eat", "drink", "sleep", "walk", "give", "know", "hear"];

    //each rule is independent of the others, and they apply in this order
    public static List<SandhiRule> SelectSandhiRules(Random rand)
    {
        var rules = new List<SandhiRule>();
        if (rand.NextDouble() < 0.5)
        {
            rules.Add(new NasalAssimilation());
        }
        if (rand.NextDouble() < 0.5)
        {
            rules.Add(new Degemination());
        }
        if (rand.NextDouble() < 0.3)
        {
            rules.Add(new IntervocalicVoicing());
        }
        return rules;
    }

    //schwa and i are the usual epenthetic vowels; failing those, the language's commonest vowel,
    //which is the first one since picks are biased toward the front
    public static Vowel SelectEpentheticVowel(List<Vowel> vowels) =>
        vowels.Contains(Vowel.ə) ? Vowel.ə : vowels.Contains(Vowel.i) ? Vowel.i : vowels[0];

    public static List<Lexeme> GenerateLexicon(Random rand, SyllableRule rule, double dropoff)
    {
        var lexicon = new List<Lexeme>();
        foreach (var meaning in NounMeanings)
        {
            lexicon.Add(new Lexeme(new Root(GenerateForm(rand, rule, dropoff), meaning), PartOfSpeech.Noun));
        }
        foreach (var meaning in VerbMeanings)
        {
            lexicon.Add(new Lexeme(new Root(GenerateForm(rand, rule, dropoff), meaning), PartOfSpeech.Verb));
        }
        return lexicon;
    }

    public static List<Phoneme> GenerateForm(Random rand, SyllableRule rule, double dropoff)
    {
        var form = new List<Phoneme>();
        int numberOfSyllables = 1;

        //20% chance of monosyllable
        if (rand.NextDouble() > 0.2)
        {
            numberOfSyllables += 1 + Utilities.PowerLaw(rand, 3, 0.20);
        }

        for (int i = 0; i < numberOfSyllables; i++)
        {
            form.AddRange(GenerateSyllable(rand, rule, dropoff).Phonemes);
        }
        return form;
    }

    public static Dictionary<PartOfSpeech, Paradigm> GenerateParadigms(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, MorphosyntacticAlignment alignment, List<Case> cases, Phonology phonology, double dropoff)
    {
        //case and number particles on nouns behave like adpositions, so they follow the same directionality
        var nouns = GenerateParadigm(rand, PartOfSpeech.Noun, SelectNounCategories(rand, alignment, cases), morphology, wordOrder.AdpositionalPhraseDirectionality, phonology, dropoff);
        var verbs = GenerateParadigm(rand, PartOfSpeech.Verb, SelectVerbCategories(rand), morphology, wordOrder.VerbPhraseDirectionality, phonology, dropoff);
        return new() { [PartOfSpeech.Noun] = nouns, [PartOfSpeech.Verb] = verbs };
    }

    //innermost first: number sits closer to the root than case, as it overwhelmingly does.
    //gender isn't here because a noun doesn't inflect for it; it has one, and adjectives and
    //verbs agree with it
    public static List<InflectionalCategory> SelectNounCategories(Random rand, MorphosyntacticAlignment alignment, List<Case> cases)
    {
        var categories = new List<InflectionalCategory>();
        if (rand.NextDouble() < 0.85)
        {
            List<Tagmeme> numbers = rand.NextDouble() < 0.1 ? [Number.Singular, Number.Dual, Number.Plural] : [Number.Singular, Number.Plural];
            categories.Add(new("Number", numbers));
        }
        //with no core cases, a noun still needs a plain form for everything the peripheral cases don't cover
        if (cases.Count > 0)
        {
            categories.Add(alignment == MorphosyntacticAlignment.None ? new("Case", [Case.Direct, .. cases]) : new("Case", [.. cases]));
        }
        return categories;
    }

    //innermost first: aspect, then tense, then subject agreement, the usual order outward from the root
    public static List<InflectionalCategory> SelectVerbCategories(Random rand)
    {
        var categories = new List<InflectionalCategory>();
        if (rand.NextDouble() < 0.3)
        {
            categories.Add(new("Aspect", [Aspect.Imperfective, Aspect.Perfective]));
        }
        if (rand.NextDouble() < 0.8)
        {
            List<Tagmeme> tenses = rand.NextDouble() < 0.4 ? [Tense.Present, Tense.Past, Tense.Future] : [Tense.Present, Tense.Past];
            categories.Add(new("Tense", tenses));
        }
        if (rand.NextDouble() < 0.6)
        {
            categories.Add(new("Person", [Person.First, Person.Second, Person.Third]));
            categories.Add(new("Number", [Number.Singular, Number.Plural]));
        }
        return categories;
    }

    //whether a category shares its marker with the one just inside it
    private static bool FusesWithPrevious(InflectionalCategory inner, InflectionalCategory outer, Exponence exponence) => (inner.Values[0], outer.Values[0]) switch
    {
        //person and number agreement is fused in nearly every language that has it (1SG, 3PL)
        (Person, Number) => true,
        (Number, Case) => exponence.CaseWithNumber,
        (Tense or Aspect, Person) => exponence.TamWithAgreement,
        (Aspect, Tense) => exponence.TenseWithAspect,
        _ => false,
    };

    public static Paradigm GenerateParadigm(Random rand, PartOfSpeech partOfSpeech, List<InflectionalCategory> categories, MorphologicalProfile morphology, HeadDirectionality directionality, Phonology phonology, double dropoff)
    {
        var particlePosition = directionality == HeadDirectionality.Initial ? ExponentPosition.PrecedingParticle : ExponentPosition.FollowingParticle;

        var groups = new List<(List<InflectionalCategory> Categories, ExponentPosition Position)>();
        foreach (var category in categories)
        {
            if (groups.Count > 0 && FusesWithPrevious(groups[^1].Categories[^1], category, morphology.Exponence))
            {
                groups[^1].Categories.Add(category);
            }
            else if (rand.NextDouble() < morphology.ParticleChance)
            {
                groups.Add(([category], particlePosition));
            }
            else
            {
                groups.Add(([category], rand.NextDouble() < morphology.PrefixChance ? ExponentPosition.Prefix : ExponentPosition.Suffix));
            }
        }

        var slots = new List<ExponentSlot>();
        foreach (var (slotCategories, position) in groups)
        {
            slots.Add(new ExponentSlot
            {
                Categories = slotCategories,
                Position = position,
                Exponents = GenerateExponents(rand, slotCategories, position, phonology, dropoff),
            });
        }
        return new Paradigm { PartOfSpeech = partOfSpeech, Categories = categories, Slots = slots };
    }

    //one exponent per combination of values. Forms are kept distinct within a slot where possible;
    //if the phonology keeps producing repeats, the syncretism is allowed
    //TODO real paradigms often leave the commonest cell unmarked (SG, PRS, NOM), with no exponent at all
    private static List<(IReadOnlyList<Tagmeme> Values, Morpheme Exponent)> GenerateExponents(Random rand, List<InflectionalCategory> categories, ExponentPosition position, Phonology phonology, double dropoff)
    {
        var exponents = new List<(IReadOnlyList<Tagmeme>, Morpheme)>();
        var usedForms = new HashSet<string>();
        foreach (var values in Paradigm.Combinations(categories))
        {
            //affixes are often a bare consonant (English -s, -t), which is what gives sandhi and cluster
            //repair work to do; particles are words of their own, so they're always a whole syllable
            var bare = position is ExponentPosition.Prefix or ExponentPosition.Suffix && rand.NextDouble() < 0.3;
            var form = new List<Phoneme>();
            for (int attempt = 0; attempt < 20; attempt++)
            {
                form = bare
                    ? [phonology.Consonants[Utilities.PowerLaw(rand, phonology.Consonants.Count, dropoff)]]
                    : [.. GenerateSyllable(rand, phonology.SyllableRule, dropoff).Phonemes];
                if (usedForms.Add(string.Concat(form.Select(p => p.Transcription))))
                {
                    break;
                }
            }
            Morpheme exponent = position is ExponentPosition.Prefix or ExponentPosition.Suffix ? new Affix(form, values) : new Particle(form, values);
            exponents.Add((values, exponent));
        }
        return exponents;
    }

    public static Syllable GenerateSyllable(Random rand, SyllableRule rule, double dropoff)
    {
        var syllable = rule.GenerateSyllable(rand, dropoff);

        syllable = Sandhi.ElideDuplicates(syllable);
        return syllable;
    }

    //only one rule now!
    public static SyllableRule SelectSyllableRule(Random rand, List<Vowel> vowels, List<Consonant> consonants)
    {
        var template = SyllableTemplates.All[rand.Next(SyllableTemplates.All.Count)];
        return new SyllableRule(template, vowels, consonants);
    }

    public static List<Vowel> GenerateVowels(Random rand, double chanceOfPhonemePruning, double chanceOfCategoryPruning)
    {
        var vowels = new List<Vowel>(PhonemeManager.Vowels);

        for (int i = vowels.Count - 1; i >= 0; i--)
        {
            Vowel vowel = vowels[i];
            if (rand.NextDouble() < chanceOfPhonemePruning)
            {
                vowels.Remove(vowel);
            }
        }

        //chance of removing longs
        //TODO make this a little more sophisticated? meh
        if (rand.NextDouble() < chanceOfCategoryPruning)
        {
            vowels.RemoveAll(v => v.Long);
        }

        vowels = Utilities.Shuffle(rand, vowels).ToList();

        return vowels;
    }

    public static List<Consonant> GenerateConsonants(Random rand, double chanceofPhonemePruning, double chanceOfCategoryPruning)
    {
        var consonants = new List<Consonant>(PhonemeManager.Consonants);

        for (int i = consonants.Count - 1; i >= 0; i--)
        {
            Consonant consonant = consonants[i];
            if (rand.NextDouble() < chanceofPhonemePruning)
            {
                consonants.Remove(consonant);
            }
        }

        //picked some more disposable categories; this could use work
        if (rand.NextDouble() < chanceOfCategoryPruning)
        {
            consonants.RemoveAll(c => c.Place == ConsonantPlace.Labial);
        }
        if (rand.NextDouble() < chanceOfCategoryPruning)
        {
            consonants.RemoveAll(c => c.Place == ConsonantPlace.Palatal);
        }
        if (rand.NextDouble() < chanceOfCategoryPruning)
        {
            consonants.RemoveAll(c => c.Place == ConsonantPlace.Retroflex);
        }

        consonants = Utilities.Shuffle(rand, consonants).ToList();

        return consonants;
    }

    //I've rounded off the real percentages to more even numbers for convenience
    private static readonly (ConstituentWordOrder Order, double Probability)[] ConstituentWordOrders =
    [
        (ConstituentWordOrder.SOV, 0.45),
        (ConstituentWordOrder.SVO, 0.40),
        (ConstituentWordOrder.VSO, 0.10),
        (ConstituentWordOrder.VOS, 0.03),
        (ConstituentWordOrder.OSV, 0.01),
        (ConstituentWordOrder.OVS, 0.01),
    ];

    public static WordOrder SelectWordOrder(Random rand)
    {
        var roll = rand.NextDouble();
        var cumulative = 0.0;
        //rounding can leave the cumulative total a hair under 1; the remainder goes to the last order
        var order = ConstituentWordOrders[^1].Order;
        foreach (var (candidate, probability) in ConstituentWordOrders)
        {
            cumulative += probability;
            if (roll < cumulative)
            {
                order = candidate;
                break;
            }
        }

        //some languages are consistently head-initial, while others are consistently head-final;
        //many are somewhere in the middle. Probability starts at 50%, and every head-initial
        //choice makes the next one likelier to be head-initial too
        var numerator = 1.0;
        var denominator = 2.0;
        HeadDirectionality NextDirectionality()
        {
            var initial = rand.NextDouble() < numerator / denominator;
            if (initial)
            {
                numerator++;
            }
            denominator++;
            return initial ? HeadDirectionality.Initial : HeadDirectionality.Final;
        }

        //initializers run in order, so this is the order the choices are weighted in
        return new WordOrder
        {
            ConstituentWordOrder = order,
            HeadDirectionality = NextDirectionality(),
            VerbPhraseDirectionality = NextDirectionality(),
            NounPhraseDirectionality = NextDirectionality(),
            AdjectivePhraseDirectionality = NextDirectionality(),
            AdpositionalPhraseDirectionality = NextDirectionality(),
        };
    }

    //WALS won't classify languages as fusional, agglutinative or analytic, but it does measure the
    //properties underneath, each drawn here in proportion to WALS's language counts

    // https://wals.info/chapter/20 (165 languages). Neither tone nor ablaut is modeled, so values
    //pairing one with another strategy count as that strategy; the 3 exclusively tonal languages are left out
    private static readonly (Fusion Value, int Languages)[] FusionCounts =
    [
        (Fusion.Concatenative, 125 + 5 + 2),
        (Fusion.Mixed, 13),
        (Fusion.Isolating, 16 + 1),
    ];

    // https://wals.info/combinations/26A_83A: affixation by the order of object and verb, which makes
    //a big difference, since OV languages strongly prefer suffixes. Languages with little or no
    //affixation are left out, since fusion already decides that, and so are the 60 with no
    //dominant order, since every generated language has one
    private static readonly (Affixation Value, int Languages)[] ObjectVerbAffixationCounts =
    [
        (Affixation.StronglySuffixing, 269),
        (Affixation.WeaklySuffixing, 70),
        (Affixation.Equal, 49),
        (Affixation.WeaklyPrefixing, 23),
        (Affixation.StronglyPrefixing, 6),
    ];
    private static readonly (Affixation Value, int Languages)[] VerbObjectAffixationCounts =
    [
        (Affixation.StronglySuffixing, 93),
        (Affixation.WeaklySuffixing, 43),
        (Affixation.Equal, 78),
        (Affixation.WeaklyPrefixing, 61),
        (Affixation.StronglyPrefixing, 51),
    ];

    public static bool ObjectPrecedesVerb(ConstituentWordOrder order) => order is ConstituentWordOrder.SOV or ConstituentWordOrder.OSV or ConstituentWordOrder.OVS;

    public static MorphologicalProfile SelectMorphologicalProfile(Random rand, ConstituentWordOrder order) => new(
        Draw(rand, FusionCounts),
        new Exponence(
            // https://wals.info/chapter/21: 8 of the 87 languages with case mark it together with number
            CaseWithNumber: rand.NextDouble() < 8 / 87.0,
            //and 24 of the 156 with tense-aspect-mood inflection mark it together with agreement
            TamWithAgreement: rand.NextDouble() < 24 / 156.0,
            //a guess: WALS counts tense and aspect together as TAM, so it has no figure for this
            TenseWithAspect: rand.NextDouble() < 0.3),
        Draw(rand, ObjectPrecedesVerb(order) ? ObjectVerbAffixationCounts : VerbObjectAffixationCounts));

    //a value, with probability proportional to how many languages have it
    private static T Draw<T>(Random rand, (T Value, int Languages)[] counts)
    {
        var roll = rand.Next(counts.Sum(c => c.Languages));
        foreach (var (value, languages) in counts)
        {
            if (roll < languages)
            {
                return value;
            }
            roll -= languages;
        }
        throw new System.Diagnostics.UnreachableException();
    }

    public static MorphosyntacticAlignment SelectAlignment(Random rand, double nominativeChance, double ergativeChance)
    {
        if (rand.NextDouble() < nominativeChance)
        {
            return MorphosyntacticAlignment.NominativeAccusative;
        }
        else if (rand.NextDouble() < ergativeChance)
        {
            return MorphosyntacticAlignment.ErgativeAbsolutive;
        }
        //iffy
        else
        {
            return MorphosyntacticAlignment.None;
        }
    }

    public static List<Case> SelectCases(Random rand, MorphosyntacticAlignment alignment, double genitiveChance, double dativeChance, double ablativeChance, double vocativeChance, double locativeChance, double instrumentalChance)
    {
        var cases = new List<Case>();
        switch (alignment)
        {
            case MorphosyntacticAlignment.NominativeAccusative:
                cases.Add(Case.Nominative);
                cases.Add(Case.Accusative);
                break;
            case MorphosyntacticAlignment.ErgativeAbsolutive:
                cases.Add(Case.Ergative);
                cases.Add(Case.Absolutive);
                break;
            default:
                break;
        }

        if (rand.NextDouble() < genitiveChance)
        {
            cases.Add(Case.Genitive);
        }
        if (rand.NextDouble() < dativeChance)
        {
            cases.Add(Case.Dative);
        }
        if (rand.NextDouble() < ablativeChance)
        {
            cases.Add(Case.Ablative);
        }
        if (rand.NextDouble() < vocativeChance)
        {
            cases.Add(Case.Vocative);
        }
        if (rand.NextDouble() < locativeChance)
        {
            cases.Add(Case.Locative);
        }
        if (rand.NextDouble() < instrumentalChance)
        {
            cases.Add(Case.Instrumental);
        }

        return cases;
    }

    //a role gets its case if the language has one, and otherwise an adposition. The English
    //glosses are placeholders until adpositions are real words in the lexicon
    public static Dictionary<CaseUse, RoleMarker> SelectRoleMarkers(MorphosyntacticAlignment alignment, List<Case> cases, WordOrder wordOrder)
    {
        var type = wordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial ? AdpositionType.Preposition : AdpositionType.Postposition;
        //a simplification: real languages often pick the governed case per adposition
        Case? governs = alignment switch
        {
            MorphosyntacticAlignment.NominativeAccusative => Case.Accusative,
            MorphosyntacticAlignment.ErgativeAbsolutive => Case.Absolutive,
            _ => null,
        };

        RoleMarker Core(Case nominativeAccusative, Case ergativeAbsolutive) => alignment switch
        {
            MorphosyntacticAlignment.NominativeAccusative => nominativeAccusative,
            MorphosyntacticAlignment.ErgativeAbsolutive => ergativeAbsolutive,
            _ => new Unmarked(),
        };
        RoleMarker CaseOrAdposition(Case @case, string gloss) =>
            cases.Contains(@case) ? @case : new Adposition(gloss, type, governs);

        RoleMarker MarkerFor(CaseUse use) => use switch
        {
            CaseUse.IntransitiveSubject => Core(Case.Nominative, Case.Absolutive),
            CaseUse.TransitiveSubject => Core(Case.Nominative, Case.Ergative),
            CaseUse.DirectObject => Core(Case.Accusative, Case.Absolutive),
            CaseUse.Possessive => CaseOrAdposition(Case.Genitive, "of"),
            CaseUse.IndirectObject => CaseOrAdposition(Case.Dative, "to"),
            CaseUse.Address => cases.Contains(Case.Vocative) ? Case.Vocative : new Unmarked(),
            CaseUse.Location => CaseOrAdposition(Case.Locative, "at"),
            CaseUse.MovementTo => new Adposition("to", type, governs),
            CaseUse.MovementFrom => CaseOrAdposition(Case.Ablative, "from"),
            CaseUse.Accompaniment => new Adposition("with", type, governs),
            CaseUse.Instrument => CaseOrAdposition(Case.Instrumental, "with"),
            _ => throw new ArgumentOutOfRangeException(nameof(use), use, "Every role needs a marker."),
        };

        return Enum.GetValues<CaseUse>().ToDictionary(use => use, MarkerFor);
    }
}
