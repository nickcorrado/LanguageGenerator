namespace LanguageGenerator;

//chooses everything about a language. The result is a plain Language, which
//knows nothing about how it was chosen. Each feature's choices, and the WALS counts behind them,
//are in the Generator file named for it
public static partial class Generator
{
    public static Language Generate(Random rand)
    {
        var syllableDropoff = 0.15;

        var vowels = GenerateVowels(rand);
        var consonants = GenerateConsonants(rand);
        var syllableRule = SelectSyllableRule(rand, vowels, consonants);
        var phonology = new Phonology
        {
            Vowels = vowels,
            Consonants = consonants,
            SyllableRule = syllableRule,
            SoundChanges = SelectSoundChanges(rand),
            ClusterRepair = rand.NextDouble() < 0.6 ? ClusterRepair.Epenthesis : ClusterRepair.Deletion,
            EpentheticVowel = SelectEpentheticVowel(vowels),
            Stress = SelectStress(rand),
            SyllableContact = SelectSyllableContact(rand, syllableRule),
        };

        var orthography = SelectOrthography(rand, vowels, consonants);
        //the lexicon's seed and root length noise are drawn here, so that the draws after them stay
        //where they were; the lexicon itself is built last, once the sound changes that decide how
        //long its roots must be are known, and there are closed classes for it to avoid
        var (lexiconSeed, rootLengthNoise) = (rand.Next(), SelectRootLengthNoise(rand));

        //word order comes first because how isolating a language is, and how much it prefixes, depend on it
        var wordOrder = SelectWordOrder(rand);
        var morphology = SelectMorphologicalProfile(rand, wordOrder.ConstituentWordOrder);

        var alignment = SelectAlignment(rand, 0.5, 0.25);
        var cases = SelectCases(rand, alignment, 0.75, 0.6, 0.35, 0.65, 0.25, 0.2);
        var paradigms = GenerateParadigms(rand, morphology, wordOrder, alignment, cases, phonology, syllableDropoff);
        var hasArticles = rand.NextDouble() < 0.7;
        //last, so that adding these left everything before them as it was for each seed
        var pronouns = GeneratePronouns(rand, paradigms, morphology, wordOrder, phonology, syllableDropoff);
        var negation = GenerateNegation(rand, morphology, phonology, syllableDropoff);
        var predication = GeneratePredication(rand, phonology, syllableDropoff);
        var adjectiveAgreement = SelectAdjectiveAgreement(rand, predication);
        var genders = GenerateGenders(rand, morphology, paradigms, phonology, syllableDropoff);
        var adpositions = GenerateAdpositions(rand, phonology, syllableDropoff);
        wordOrder = wordOrder with { ObliquePosition = SelectObliquePosition(rand, wordOrder.ConstituentWordOrder) };
        var roleMarkers = SelectRoleMarkers(alignment, cases, wordOrder, adpositions);
        predication = GenerateLocationalPredication(rand, predication, phonology, syllableDropoff);
        var numerals = GenerateNumerals(rand, morphology, phonology, syllableDropoff);
        var possession = GeneratePossession(rand, morphology, wordOrder, phonology, syllableDropoff);
        phonology = phonology with
        {
            SoundChanges = [.. phonology.SoundChanges, .. SelectLaterSoundChanges(rand, syllableRule, vowels)],
            SyllableChanges = SelectSyllableChanges(rand, syllableRule, vowels, phonology.SyllableContact),
        };
        var commands = GenerateCommands(rand, morphology, wordOrder, phonology, syllableDropoff);
        var extraSyllables = SelectExtraSyllables(rootLengthNoise, lexiconSeed, phonology, syllableDropoff);
        var lexicon = GenerateLexicon(lexiconSeed, extraSyllables, phonology, syllableDropoff, new Dictionary<PartOfSpeech, IEnumerable<Lexeme>>
        {
            [PartOfSpeech.Noun] = pronouns.Forms.Values,
            [PartOfSpeech.Adjective] = [.. numerals.Words.Values, .. new[] { numerals.Many, numerals.Ordinals.First, numerals.Ordinals.Second }.OfType<Lexeme>()],
            [PartOfSpeech.Verb] = [predication.Copula, .. predication.LocationalCopula is { } locational ? [locational] : Array.Empty<Lexeme>()],
        });

        return new Language
        {
            Phonology = phonology,
            Orthography = orthography,
            Lexicon = lexicon,
            Paradigms = paradigms,
            MorphologicalProfile = morphology,
            WordOrder = wordOrder,
            HasArticles = hasArticles,
            MorphosyntacticAlignment = alignment,
            Cases = cases,
            RoleMarkers = roleMarkers,
            Pronouns = pronouns,
            Negation = negation,
            Predication = predication,
            AdjectiveAgreement = adjectiveAgreement,
            Genders = genders,
            Numerals = numerals,
            Possession = possession,
            Commands = commands,
        };
    }

    //short forms for a closed class of words or affixes, kept distinct from each other where the
    //phonology allows: each is drawn up to the given number of times until it's one not yet taken,
    //and if every draw repeats one, the last is kept, as real languages keep some syncretism
    private sealed class DistinctForms(int draws = 20)
    {
        private readonly HashSet<string> taken = [];

        public void Take(IEnumerable<Phoneme> form) => taken.Add(Key(form));

        public List<Phoneme> Draw(Func<List<Phoneme>> draw)
        {
            var form = new List<Phoneme>();
            for (int attempt = 0; attempt < draws; attempt++)
            {
                form = draw();
                if (taken.Add(Key(form)))
                {
                    break;
                }
            }
            return form;
        }

        private static string Key(IEnumerable<Phoneme> form) => string.Join(" ", form.Select(p => p.IpaSymbol));
    }

    //a short affix: a lone vowel 30% of the time, and otherwise a syllable
    private static List<Phoneme> VowelOrSyllable(Random rand, Phonology phonology, double dropoff) => rand.NextDouble() < 0.3
        ? [phonology.Vowels[Sampling.TruncatedGeometric(rand, phonology.Vowels.Count, dropoff)]]
        : [.. GenerateSyllable(rand, phonology.SyllableRule, dropoff, needsOnset: false).Phonemes];
}
