namespace LanguageGenerator;

//chooses everything about a language. The result is a plain Language, which
//knows nothing about how it was chosen. Each feature's choices, and the WALS counts behind them,
//are in the Generator file named for it
public static partial class Generator
{
    //each stage draws from its own Random, so it doesn't matter what order they run in, or how much
    //any of them draws. They're in this order only because each needs what's above it. A stage with
    //settings is drawn to match the spec, which fixes nothing for LanguageSpec.Free
    public static Language Generate(int seed, LanguageSpec spec)
    {
        if (spec.Problems() is [_, ..] problems)
        {
            throw new SpecException(string.Join(Environment.NewLine, problems));
        }
        var stages = new Stages(seed);
        var syllableDropoff = stages.Draw(spec, Settings.DropoffStage, _ => 0.15);

        var vowels = stages.Draw(spec, Settings.VowelStage, rand => GenerateVowels(rand));
        var consonants = stages.Draw(spec, Settings.ConsonantStage, rand => GenerateConsonants(rand));
        var shape = stages.Draw(spec, Settings.SyllableStage, SelectSyllableShape);
        var syllableRule = new SyllableRule(shape.Template, vowels, consonants, shape.Codas);
        var syllableContact = SelectSyllableContact(stages.For("syllable contact"), syllableRule);
        var phonology = new Phonology
        {
            Vowels = vowels,
            Consonants = consonants,
            SyllableRule = syllableRule,
            SoundChanges = SelectSoundChanges(stages.For("sound changes"), syllableRule, vowels),
            SyllableChanges = SelectSyllableChanges(stages.For("syllable changes"), syllableRule, vowels, syllableContact),
            ClusterRepair = stages.Draw(spec, Settings.ClusterRepairStage, SelectClusterRepair),
            EpentheticVowel = SelectEpentheticVowel(vowels),
            Stress = stages.Draw(spec, Settings.StressStage, SelectStress),
            SyllableContact = syllableContact,
        };
        var orthography = SelectOrthography(stages.For("orthography"), vowels, consonants);

        //word order comes first because how isolating a language is, and how much it prefixes, depend on it
        var wordOrder = stages.Draw(spec, Settings.WordOrderStage, SelectWordOrder);
        var morphology = stages.Draw(spec, Settings.MorphologyStage, rand => SelectMorphologicalProfile(rand, wordOrder.ConstituentWordOrder));

        var alignment = stages.Draw(spec, Settings.AlignmentStage, SelectAlignment);
        var regions = SelectRegions(stages.For("role regions"));
        var cases = SelectCases(stages.For("cases"), alignment, regions, 0.65);
        var configurations = GenerateConfigurations(stages.For("configurations"), morphology, wordOrder, alignment, regions, cases, phonology, syllableDropoff);
        var paradigms = new Paradigms(
            GenerateNounParadigm(stages.For("noun paradigm"), morphology, wordOrder, alignment, cases, configurations.Localizations, phonology, syllableDropoff),
            GenerateVerbParadigm(stages.For("verb paradigm"), morphology, wordOrder, phonology, syllableDropoff));
        var pronouns = GeneratePronouns(stages.For("pronouns"), paradigms, morphology, wordOrder, phonology, syllableDropoff);
        var negation = GenerateNegation(stages.For("negation"), morphology, phonology, syllableDropoff);
        var predication = GenerateLocationalPredication(stages.For("locational copula"),
            GeneratePredication(stages.For("predication"), phonology, syllableDropoff), phonology, syllableDropoff);
        var adjectiveAgreement = SelectAdjectiveAgreement(stages.For("adjective agreement"), predication);
        var genders = GenerateGenders(stages.For("genders"), morphology, paradigms, phonology, syllableDropoff);
        var adpositions = GenerateAdpositions(stages.For("adpositions"), regions, cases, phonology, syllableDropoff);
        var duration = GenerateDuration(stages.For("duration"), adpositions, phonology, syllableDropoff);
        var roleMarkers = SelectRoleMarkers(stages.For("adposition government"), alignment, cases, regions, wordOrder, adpositions, duration);
        var numerals = GenerateNumerals(stages.For("numerals"), morphology, phonology, syllableDropoff);
        var articles = GenerateArticles(stages.For("articles"), morphology, genders, numerals, phonology, syllableDropoff);
        var possession = GeneratePossession(stages.For("possession"), morphology, wordOrder, phonology, syllableDropoff);
        var commands = GenerateCommands(stages.For("commands"), morphology, wordOrder, phonology, syllableDropoff);
        var comparison = GenerateComparison(stages.For("comparison"), wordOrder, phonology, syllableDropoff);
        var degree = GenerateDegree(stages.For("degree"), comparison, morphology, wordOrder, phonology, syllableDropoff);
        var intensifier = GenerateIntensifier(stages.For("intensifier"), degree, wordOrder, phonology, syllableDropoff);
        var manner = GenerateManner(stages.For("manner"), morphology, phonology, syllableDropoff);
        var similative = GenerateSimilative(stages.For("similative"), comparison, morphology, wordOrder, phonology, syllableDropoff);
        var passive = GeneratePassive(stages.For("passive"), alignment, morphology, phonology, syllableDropoff);
        var conjunctions = GenerateConjunctions(stages.For("conjunctions"), phonology, syllableDropoff);
        var subordination = GenerateSubordination(stages.For("subordination"), morphology, wordOrder, phonology, syllableDropoff);
        var modals = GenerateModals(stages.For("modals"), morphology, wordOrder, predication, phonology, syllableDropoff);
        var questions = GenerateQuestions(stages.For("questions"), morphology, wordOrder, phonology, syllableDropoff);

        //the lexicon comes last, once there are closed classes for it to avoid
        var lexiconSeed = stages.For("lexicon").Next();
        var extraSyllables = SelectExtraSyllables(SelectRootLengthNoise(stages.For("root length noise")), lexiconSeed, phonology, syllableDropoff);
        var lexicon = GenerateLexicon(lexiconSeed, extraSyllables, phonology, syllableDropoff, new Dictionary<PartOfSpeech, IEnumerable<Lexeme>>
        {
            [PartOfSpeech.Noun] = [.. pronouns.Forms.Values, .. questions.Words.Values, questions.Why],
            [PartOfSpeech.Adjective] = [.. numerals.Words.Values, .. new[] { numerals.Many, numerals.Ordinals.First, numerals.Ordinals.Second }.OfType<Lexeme>()],
            [PartOfSpeech.Verb] = [predication.Copula, .. predication.LocationalCopula is { } locational ? [locational] : Array.Empty<Lexeme>(),
                .. modals.Markers.Values.Select(m => m.Value).OfType<ModalVerb>().Select(m => m.Verb),
                .. comparison.Value is ExceedComparison exceed ? [exceed.Exceed] : Array.Empty<Lexeme>()],
        });
        //a relational noun is an ordinary noun, so 'the top of the hill' and 'on the hill' can share
        //a word, but it's needed whether or not a text has that noun
        foreach (var noun in configurations.Markers.Values.Select(m => m.Value).OfType<RelationalNoun>())
        {
            lexicon.Noun(noun.Meaning);
        }

        return new Language
        {
            Phonology = phonology,
            Orthography = orthography,
            Lexicon = lexicon,
            Paradigms = paradigms,
            MorphologicalProfile = morphology,
            WordOrder = wordOrder,
            Articles = articles,
            MorphosyntacticAlignment = alignment,
            Cases = cases,
            RoleMarkers = roleMarkers,
            Configurations = configurations,
            Pronouns = pronouns,
            Negation = negation,
            Predication = predication,
            AdjectiveAgreement = adjectiveAgreement,
            Genders = genders,
            Numerals = numerals,
            Possession = possession,
            Commands = commands,
            Comparison = comparison,
            Degree = degree,
            Similative = similative,
            Intensifier = intensifier,
            Manner = manner,
            Passive = passive,
            Conjunctions = conjunctions,
            Subordination = subordination,
            Modals = modals,
            Questions = questions,
            Dropoff = syllableDropoff,
        };
    }

    /// <summary>How many times a stage is drawn, at most, to find a draw that matches a spec.</summary>
    //each draw of a stage with settings takes microseconds, and the rarest combination the odds give
    //is about one in 50,000, so this finds nearly any value the odds can give, and gives up on one
    //they can't in a fraction of a second
    public const int DrawLimit = 100_000;

    //a Random for each stage of generating a language, from the language's seed and the stage's name.
    //A name can only be used once, since two stages sharing one would draw the very same numbers
    private sealed class Stages(int seed)
    {
        private readonly HashSet<string> used = [];

        public Random For(string stage) => used.Add(stage)
            ? Sampling.For(seed, stage)
            : throw new ArgumentException($"The stage '{stage}' was named twice.", nameof(stage));

        //a stage drawn to match the spec. The first draw is what the stage would be with no spec,
        //so a seed whose draw already matches keeps its language. Otherwise the stage is drawn again,
        //each time from a Random of its own, until a draw matches every fixed setting that a draw
        //can match, which keeps the odds of the rest conditional on them; and if none does within
        //the limit, as for a value the odds never give, the closest draw is kept. Any fixed value
        //the kept draw still lacks, as an inventory or a number always would, is written into it
        public T Draw<T>(LanguageSpec spec, Stage<T> stage, Func<Random, T> draw)
        {
            var result = draw(For(stage.Name));
            var settings = stage.Settings.Where(s => spec.Allowed(s) is not null).ToList();
            var redrawable = settings.Where(s => s.Redrawable).ToList();
            int Matching(T drawn) => redrawable.Count(s => s.Matches(drawn, spec.Allowed(s)!));
            var best = Matching(result);
            for (int attempt = 1; best < redrawable.Count && attempt < DrawLimit; attempt++)
            {
                var drawn = draw(Sampling.For(seed, $"{stage.Name} #{attempt}"));
                if (Matching(drawn) is var matching && matching > best)
                {
                    (result, best) = (drawn, matching);
                }
            }
            return settings.Aggregate(result, (drawn, s) => s.Matches(drawn, spec.Allowed(s)!) ? drawn : s.Write(drawn, spec.Allowed(s)!));
        }
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
