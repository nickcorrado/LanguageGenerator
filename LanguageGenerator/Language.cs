namespace LanguageGenerator;

//I figure this is the top-level object
public class Language
{
    public string? Name { get; set; }
    public List<SyllableRule> SyllableRules { get; set; } = [];
    public List<Category<Vowel>> Vowels { get; set; } = [];
    public List<Category<Consonant>> Consonants { get; set; } = [];
    public List<Word> Words { get; set; } = [];
    public WordOrder WordOrder { get; set; }
    public Morphology Morphology { get; set; }
    public bool HasArticles { get; set; }
    public MorphosyntacticAlignment MorphosyntacticAlignment { get; set; }
    public List<Case> Cases { get; set; }
    public Dictionary<CaseUse, RoleMarker> RoleMarkers { get; set; }

    public Language(Random rand)
    {
        //categories
        //all percentage chances in this program should bubble up somehow, so that they can be easily tweaked in one place
        //var consonantCategories = GenerateCategories(1, 5, PhonemeManager.Consonants, 0.4);
        //var vowelCategories = GenerateCategories(1, 3, PhonemeManager.Vowels, 0.6);

        //var syllableRules = GenerateSyllableRules(1, 3, consonantCategories, vowelCategories, 0.4, 0.15, 0.25);

        //foreach (var rule in syllableRules)
        //{
        //    Console.WriteLine(rule);
        //}

        //now, generate a syllable
        var syllableDropoff = 0.15;

        //var syllables = new List<Syllable>();
        //for (int i = 0; i < 10; i++)
        //{
        //    syllables.Add(GenerateSyllable(syllableRules, syllableDropoff));
        //}

        var wordCount = 25;
        //var words = new List<Word>();
        //for (int i = 0; i < wordCount; i++)
        //{
        //    words.Add(GenerateWord(syllableRules, syllableDropoff));
        //}
        //foreach (var word in words)
        //{
        //    Console.WriteLine(word.Transcription);
        //}

        var vowels = GenerateVowels(rand, 0.15, 0.30);
        var consonants = GenerateConsonants(rand, 0.15, 0.50);
        var syllableRule = SelectSyllableRule(rand, vowels, consonants);

        Console.WriteLine(syllableRule);

        var syllables = new List<Syllable>();
        for (int i = 0; i < 10; i++)
        {
            syllables.Add(GenerateSyllable(rand, syllableRule, syllableDropoff));
        }

        var words = new List<Word>();
        for (int i = 0; i < wordCount; i++)
        {
            words.Add(GenerateWord(rand, syllableRule, syllableDropoff));
        }
        foreach (var word in words)
        {
            Console.WriteLine(word.Transcription);
        }

        //here's a different approach
        //these results are still trash. for some reason, the bias toward certain phonemes is super strong; probably
        //the lists need to be shuffled before anything is picked
        //var consonantCategories2 = GenerateConsonantCategories2(0.7, 0.6);
        //var vowelCategories2 = GenerateVowelCategories2(1, 0.4, 0.2);

        //var syllableRules2 = GenerateSyllableRules(1, 3, consonantCategories2, vowelCategories2, 0.9, 0.15, 0.4, 0.1);

        //foreach (var rule in syllableRules2)
        //{
        //    Console.WriteLine(rule);
        //}

        //var syllables2 = new List<Syllable>();
        //for (int i = 0; i < 10; i++)
        //{
        //    syllables2.Add(GenerateSyllable(syllableRules2, syllableDropoff));
        //}

        //var words2 = new List<Word>();
        //for (int i = 0; i < wordCount; i++)
        //{
        //    words2.Add(GenerateWord(syllableRules2, syllableDropoff));
        //}
        //foreach (var word in words2)
        //{
        //    Console.WriteLine(word.Transcription);
        //}

        Morphology = SelectMorphology(rand);
        WordOrder = WordOrder.Select(rand);

        Console.WriteLine(Morphology);
        Console.WriteLine(WordOrder.ConstituentWordOrder);
        Console.WriteLine($"{WordOrder.HeadDirectionality}, {WordOrder.VerbPhraseDirectionality}, {WordOrder.NounPhraseDirectionality}, {WordOrder.AdjectivePhraseDirectionality}, {WordOrder.AdpositionalPhraseDirectionality}");

        MorphosyntacticAlignment = SelectAlignment(rand, 0.5, 0.25);
        Cases = SelectCases(rand, MorphosyntacticAlignment, 0.75, 0.6, 0.35, 0.65, 0.25, 0.2);
        RoleMarkers = SelectRoleMarkers(MorphosyntacticAlignment, Cases, WordOrder);

        Console.WriteLine(MorphosyntacticAlignment);
        foreach(var @case in Cases) {
            Console.Write(@case);
        }
        Console.WriteLine();
        foreach (var (use, marker) in RoleMarkers)
        {
            Console.WriteLine($"{use}: {marker}");
        }
    }

    public static Word GenerateWord(Random rand, SyllableRule rule, double dropoff)
    {
        var word = new Word();
        int numberOfSyllables = 1;

        //20% chance of monosyllable
        if (rand.NextDouble() > 0.2)
        {
            numberOfSyllables += 1 + Utilities.PowerLaw(rand, 3, 0.20);
        }

        for (int i = 0; i < numberOfSyllables; i++)
        {
            word.Syllables.Add(GenerateSyllable(rand, rule, dropoff));
        }
        return word;
    }

    //public static Word GenerateWord(List<SyllableRule> syllableRules, double syllableDropoff)
    //{
    //    var word = new Word();
    //    int numberOfSyllables = 1;

    //    //20% chance of monosyllable
    //    var rand = new Random();
    //    if (rand.NextDouble() > 0.2)
    //    {
    //        //these appear to be arbitrary values picked by Mark
    //        numberOfSyllables += 1 + Utilities.PowerLaw(4, 0.5);
    //    }
    //    for (int i = 0; i < numberOfSyllables; i++)
    //    {
    //        word.Syllables.Add(GenerateSyllable(syllableRules, syllableDropoff));
    //    }
    //    return word;
    //}

    public static Syllable GenerateSyllable(Random rand, SyllableRule rule, double dropoff)
    {
        var syllable = rule.GenerateSyllable(rand, dropoff);

        syllable = Sandhi.ElideDuplicates(syllable);
        return syllable;
    }

    //public static Syllable GenerateSyllable(List<SyllableRule> syllableRules, double syllableDropoff)
    //{
    //    //pick a syllable rule
    //    var r = Utilities.PowerLaw(syllableRules.Count, 0.5);
    //    var rule = syllableRules[r];

    //    var syllable = new Syllable
    //    {
    //        Onset = new ConsonantCluster(rule.GetConcreteOnset(syllableDropoff)),
    //        Nucleus = rule.GetConcreteNucleus(syllableDropoff),
    //        Coda = rule.GetConcreteCoda(syllableDropoff),
    //    };
    //    syllable = Sandhi.ElideDuplicates(syllable);
    //    return syllable;
    //}

    //only one rule now!
    public static SyllableRule SelectSyllableRule(Random rand, List<Vowel> vowels, List<Consonant> consonants)
    {
        var template = SyllableTemplates.All[rand.Next(SyllableTemplates.All.Count)];
        return new SyllableRule(template, vowels, consonants);
    }

    //public static List<SyllableRule> GenerateSyllableRules(int min, int max,
    //                                                       List<Category<Consonant>> consonantCategories,
    //                                                       List<Category<Vowel>> vowelCategories,
    //                                                       double chanceOfOnset, double chanceOfOnsetCluster,
    //                                                       double chanceOfCoda)
    //{
    //    //rewrite rules
    //    var rand = new Random();
    //    var numberOfRules = rand.Next(min, max);
    //    var rules = new List<SyllableRule>();

    //    for (int i = 0; i < numberOfRules; i++)
    //    {
    //        var onset = new List<Category<Consonant>>();
    //        if (rand.NextDouble() < chanceOfOnset)
    //        {
    //            onset.Add(consonantCategories[rand.Next(consonantCategories.Count)]);
    //            //can add multiple consonants in a row
    //            while (rand.NextDouble() < chanceOfOnsetCluster)
    //            {
    //                //we should just allow approximants and nothing else
    //                //I'm not happy with this solution, but at least the
    //                //results aren't garbage
    //                onset.Add(new Category<Consonant> { Phonemes = PhonemeManager.Liquids });
    //            }
    //        }

    //        var nucleus = vowelCategories[rand.Next(vowelCategories.Count)];

    //        var coda = new Category<Consonant>();
    //        //var coda = new List<Category<Consonant>>();
    //        if (rand.NextDouble() < chanceOfCoda)
    //        {
    //            coda = consonantCategories[rand.Next(consonantCategories.Count)];
    //            //while (rand.NextDouble() < chanceOfCodaCluster)
    //            //{
    //            //    coda.Add(consonantCategories[rand.Next(consonantCategories.Count)]);
    //            //}
    //        }


    //        rules.Add(new SyllableRule()
    //        {
    //            Onset1 = new SyllableCategory<Consonant>() { Phonemes = onset[0] },
    //            Onset2 = new SyllableCategory<Consonant>() { Phonemes = onset[1] },
    //            Onset3 = new SyllableCategory<Consonant>() { Phonemes = onset[2] },
    //            Nucleus = new SyllableCategory<Vowel>() { Phonemes = nucleus },
    //            Coda = new SyllableCategory<Consonant>() { Phonemes = coda },
    //        });
    //    }

    //    return rules;
    //}

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

    //public static List<Category<Consonant>> GenerateConsonantCategories2(double chanceOfPhonemePruning, double chanceOfCategoryPruning)
    //{
    //    var categories = new List<Category<Consonant>>();
    //    categories.AddRange(PhonemeManager.ConsonantCategories);

    //    //shuffle phonemes
    //    for (int i = 0; i < categories.Count; i++)
    //    {
    //        categories[i].Phonemes = Utilities.Shuffle(categories[i].Phonemes).ToList();
    //    }

    //    //prune categories
    //    var rand = new Random();
    //    for (int i = 0; i < categories.Count; i++)
    //    {
    //        if (rand.NextDouble() < chanceOfCategoryPruning)
    //        {
    //            categories.RemoveAt(i);
    //        }
    //    }

    //    //prune phonemes
    //    foreach (var category in categories)
    //    {
    //        for (int i = 0; i < category.Phonemes.Count; i++)
    //        {
    //            if (rand.NextDouble() < chanceOfPhonemePruning)
    //            {
    //                category.Phonemes.RemoveAt(i);
    //            }
    //        }
    //    }

    //    var result = new List<Category<Consonant>>();
    //    foreach (var category in categories)
    //    {
    //        if (category.Phonemes.Count > 0)
    //        {
    //            result.Add(category);
    //        }
    //    }

    //    return result;
    //}

    //public static List<Category<Vowel>> GenerateVowelCategories2(int min, double chanceOfPhonemePruning, double chanceOfCategoryPruning)
    //{
    //    //prune categories
    //    var categories = new List<Category<Vowel>>();
    //    categories.AddRange(PhonemeManager.VowelCategories);

    //    //shuffle phonemes
    //    for (int i = 0; i < categories.Count; i++)
    //    {
    //        categories[i].Phonemes = Utilities.Shuffle(categories[i].Phonemes.ToArray()).ToList();
    //    }

    //    //prune categories
    //    var rand = new Random();
    //    for (int i = 0; i < categories.Count; i++)
    //    {
    //        if (rand.NextDouble() < chanceOfCategoryPruning && categories.Count > 1)
    //        {
    //            categories.RemoveAt(i);
    //        }
    //    }

    //    //prune phonemes
    //    foreach (var category in categories)
    //    {
    //        for (int i = 0; i < category.Phonemes.Count; i++)
    //        {
    //            if (rand.NextDouble() < chanceOfPhonemePruning && category.Phonemes.Count > 1)
    //            {
    //                category.Phonemes.RemoveAt(i);
    //            }
    //        }
    //    }

    //    var result = new List<Category<Vowel>>();
    //    foreach (var category in categories)
    //    {
    //        if (category.Phonemes.Count > 0)
    //        {
    //            result.Add(category);
    //        }
    //    }

    //    return result;
    //}

    //public static List<Category<T>> GenerateCategories<T>(int min, int max, IEnumerable<T> phonemes, double chanceOfSelection) where T : Phoneme
    //{
    //    var rand = new Random();
    //    var numberOfCategories = rand.Next(min, max);

    //    var categories = new Category<T>[numberOfCategories];

    //    int i = 0;
    //    while (i < numberOfCategories)
    //    {
    //        categories[i] = new Category<T>();
    //        var category = categories[i];

    //        foreach (var phoneme in phonemes)
    //        {
    //            if (rand.NextDouble() < chanceOfSelection)
    //            {
    //                category.Phonemes.Add(phoneme);
    //            }
    //        }

    //        if (category.Phonemes.Count > 0)
    //        {
    //            i++;
    //        }
    //    }
    //    return categories.ToList();
    //}

    //these percentages are made up because I CAN'T FIND DATA
    //WALS refuses to categorize languages in this way, hence,
    //it seems, nobody else does, either
    public static Morphology SelectMorphology(Random rand)
    {
        return rand.NextDouble() switch
        {
            < 0.5 => Morphology.Fusional,
            < 0.75 => Morphology.Agglutinative,
            _ => Morphology.Analytic
        };
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

public enum Morphology
{
    Analytic,
    Fusional,
    Agglutinative,
    //planning to leave out polysynthetic or the dubious oligosynthetic
}
