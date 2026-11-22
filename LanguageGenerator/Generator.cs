namespace LanguageGenerator;

/*
  chooses everything about a language. The result is a plain Language, which
  knows nothing about how it was chosen. Each feature's choices, and the WALS counts behind them,
  are in the Generator file named for it
 */
public static partial class Generator
{
    /*
      each stage draws from its own Random, so it doesn't matter what order they run in, or how much
      any of them draws. They're in this order only because each needs what's above it. A stage with
      settings is drawn to match the spec, which fixes nothing for LanguageSpec.Free
     */
    public static Language Generate(int seed, LanguageSpec spec)
    {
        //the rules are applied once, for checking the spec and drawing to meet it alike
        var implied = spec.ValueProblems() is [_, ..] values ? throw new SpecException(string.Join(Environment.NewLine, values)) : SpecRules.Close(spec);
        if (implied.Problems is [_, ..] problems)
        {
            throw new SpecException(string.Join(Environment.NewLine, problems));
        }
        var stages = new Stages(seed, implied);
        var syllableDropoff = stages.Draw(Settings.DropoffStage, _ => new Dropoff(0.15));

        var vowels = stages.Draw(Settings.VowelStage, rand => GenerateVowels(rand));
        var consonants = stages.Draw(Settings.ConsonantStage, rand => GenerateConsonants(rand));
        var shape = stages.Draw(Settings.SyllableStage, SelectSyllableShape);
        var syllableRule = new SyllableRule(shape.Template, vowels, consonants, shape.Codas);
        var syllableContact = stages.Draw(Settings.SyllableContactStage, rand => SelectSyllableContact(rand, syllableRule));
        var phonology = new Phonology
        {
            Vowels = vowels,
            Consonants = consonants,
            SyllableRule = syllableRule,
            SoundChanges = stages.Draw(Settings.SoundChangeStage, rand => SelectSoundChanges(rand, syllableRule, vowels)),
            SyllableChanges = stages.Draw(Settings.SyllableChangeStage, rand => SelectSyllableChanges(rand, syllableRule, vowels, syllableContact)),
            ClusterRepair = stages.Draw(Settings.ClusterRepairStage, SelectClusterRepair),
            EpentheticVowel = SelectEpentheticVowel(vowels),
            Stress = stages.Draw(Settings.StressStage, SelectStress),
            SyllableContact = syllableContact,
            Dropoff = syllableDropoff,
        };
        var orthography = SelectOrthography(stages.For("orthography"), vowels, consonants);

        /*
          these stages are drawn together, since what a later one can be depends on the earlier ones:
          a spatial case needs morphology that isn't isolating. Word order comes first because how
          isolating a language is, and how much it prefixes, depend on it
         */
        var (wordOrder, morphology, alignment, roleRegions, caseSystem, plan) = stages.DrawTogether(group =>
        {
            var wordOrder = group.Draw(Settings.WordOrderStage, SelectWordOrder);
            var morphology = group.Draw(Settings.MorphologyStage, rand => SelectMorphologicalProfile(rand, wordOrder.ConstituentWordOrder));
            var alignment = group.Draw(Settings.AlignmentStage, SelectAlignment);
            var roleRegions = group.Draw(Settings.RoleRegionStage, SelectRegions);
            var caseSystem = group.Draw(Settings.CaseStage, rand => new CaseSystem(roleRegions.Regions, SelectCases(rand, alignment, roleRegions.Regions, 0.65)));
            var configurationRegions = group.Draw(Settings.ConfigurationRegionStage, SelectConfigurationRegions);
            var plan = group.Draw(Settings.ConfigurationPlanStage, rand => PlanConfigurations(rand, configurationRegions, morphology, alignment, roleRegions.Regions, caseSystem.Cases));
            return (wordOrder, morphology, alignment, roleRegions, caseSystem, plan);
        });
        var (regions, cases) = (roleRegions.Regions, caseSystem.Cases);
        //the words are coined apart from the plan, so they don't change when the plan is drawn again
        var configurations = CoinConfigurations(stages.For("configuration words"), plan, wordOrder, phonology);
        var paradigms = new Paradigms(
            stages.Draw(Settings.NounParadigmStage, rand => GenerateNounParadigm(rand, morphology, wordOrder, alignment, cases, configurations.Localizations, phonology)),
            stages.Draw(Settings.VerbParadigmStage, rand => GenerateVerbParadigm(rand, morphology, wordOrder, phonology)));
        var pronouns = stages.Draw(Settings.PronounStage, rand => GeneratePronouns(rand, paradigms, morphology, wordOrder, phonology));
        var negation = stages.Draw(Settings.NegationStage, rand => GenerateNegation(rand, morphology, phonology));
        var copula = stages.Draw(Settings.PredicationStage, rand => GeneratePredication(rand, phonology));
        var predication = stages.Draw(Settings.LocationalCopulaStage, rand => GenerateLocationalPredication(rand, copula, phonology));
        var adjectiveAgreement = stages.Draw(Settings.AdjectiveAgreementStage, rand => SelectAdjectiveAgreement(rand, predication));
        var genders = stages.Draw(Settings.GenderStage, rand => GenerateGenders(rand, morphology, paradigms, phonology));
        var adpositions = GenerateAdpositions(stages.For("adpositions"), regions, cases, phonology);
        var duration = GenerateDuration(stages.For("duration"), adpositions, phonology);
        var roleMarkers = SelectRoleMarkers(stages.For("adposition government"), alignment, cases, regions, wordOrder, adpositions, duration);
        var numerals = stages.Draw(Settings.NumeralStage, rand => GenerateNumerals(rand, morphology, phonology));
        var demonstratives = stages.Draw(Settings.DemonstrativeStage, rand => GenerateDemonstratives(rand, morphology, genders, phonology));
        var articles = stages.Draw(Settings.ArticleStage, rand => GenerateArticles(rand, morphology, genders, numerals, demonstratives, phonology));
        var indefinites = stages.Draw(Settings.IndefiniteStage, rand => GenerateIndefinites(rand, morphology, numerals, phonology));
        var possession = stages.Draw(Settings.PossessionStage, rand => GeneratePossession(rand, morphology, wordOrder, phonology));
        var commands = stages.Draw(Settings.CommandStage, rand => GenerateCommands(rand, morphology, wordOrder, phonology));
        var comparison = stages.Draw(Settings.ComparisonStage, rand => GenerateComparison(rand, wordOrder, phonology));
        var degree = stages.Draw(Settings.DegreeStage, rand => GenerateDegree(rand, comparison, morphology, wordOrder, phonology));
        var intensifier = GenerateIntensifier(stages.For("intensifier"), degree, wordOrder, phonology);
        var manner = GenerateManner(stages.For("manner"), morphology, phonology);
        var similative = stages.Draw(Settings.SimilativeStage, rand => GenerateSimilative(rand, comparison, morphology, wordOrder, phonology));
        var passive = stages.Draw(Settings.PassiveStage, rand => GeneratePassive(rand, alignment, morphology, phonology));
        var conjunctions = stages.Draw(Settings.ConjunctionStage, rand => GenerateConjunctions(rand, phonology));
        var subordination = stages.Draw(Settings.SubordinationStage, rand => GenerateSubordination(rand, morphology, wordOrder, phonology));
        var complementation = stages.Draw(Settings.ComplementationStage, rand => GenerateComplementation(rand, morphology, wordOrder, phonology));
        var relativization = stages.Draw(Settings.RelativizationStage, rand => GenerateRelativization(rand, morphology, wordOrder, phonology));
        var modals = stages.Draw(Settings.ModalStage, rand => GenerateModals(rand, morphology, wordOrder, predication, phonology));
        var aspects = stages.Draw(Settings.AspectStage, rand => GenerateAspects(rand, morphology, wordOrder, phonology));
        var questions = stages.Draw(Settings.QuestionStage, rand => GenerateQuestions(rand, morphology, wordOrder, phonology));

        //the lexicon comes last, once there are closed classes for it to avoid
        var lexiconSeed = stages.For("lexicon").Next();
        var extraSyllables = SelectExtraSyllables(SelectRootLengthNoise(stages.For("root length noise")), lexiconSeed, phonology);
        var lexicon = GenerateLexicon(lexiconSeed, extraSyllables, phonology, new Dictionary<PartOfSpeech, IEnumerable<Lexeme>>
        {
            [PartOfSpeech.Noun] = [.. pronouns.Forms.Values, .. questions.Words.Values, .. questions.Adverbs.Values, .. indefinites.Own.Values],
            [PartOfSpeech.Adjective] = [.. numerals.Words.Values, .. indefinites.Quantifiers.Values, .. new[] { numerals.Many, numerals.Ordinals.First, numerals.Ordinals.Second }.OfType<Lexeme>()],
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
        //so are the nouns indefinite pronouns can be made from, as 'some person' is 'someone'
        foreach (var kind in Indefinites.Kinds)
        {
            lexicon.Noun(Indefinites.NounFor(kind));
        }
        //and so are the verbs an aspect's auxiliary is, as English 'has gone' needs 'have'
        foreach (var marker in (AspectMarker[])[aspects.Perfect, aspects.Progressive, aspects.Habitual])
        {
            if (Language.AuxiliaryMeaning(marker.Kind) is { } meaning)
            {
                lexicon.Verb(meaning);
            }
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
            Demonstratives = demonstratives,
            Indefinites = indefinites,
            MorphosyntacticAlignment = alignment,
            RoleRegions = roleRegions,
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
            Complementation = complementation,
            Relativization = relativization,
            Modals = modals,
            Aspects = aspects,
            Questions = questions,
        };
    }

    /// <summary>How many times a stage is drawn, at most, to find a draw that matches a spec.</summary>
    /*
      each draw of a stage with settings takes microseconds, and the rarest combination the odds give
      is about one in 50,000, so this finds nearly any value the odds can give, and gives up on one
      they can't in a fraction of a second
     */
    public const int DrawLimit = 100_000;

    /// <summary>How many times stages drawn together are drawn, at most, to find a draw that matches a spec.</summary>
    //fewer than for a stage alone, since each attempt draws several stages
    public const int GroupLimit = 10_000;

    /*
      a Random for each stage of generating a language, from the language's seed and the stage's name,
      and the stages with settings drawn to match a spec, as its rules imply it. A name can only be
      used once, since two stages sharing one would draw the very same numbers
     */
    private sealed class Stages(int seed, Implications implied)
    {
        private readonly HashSet<string> used = [];
        private readonly Earlier earlier = new();

        public Random For(string stage)
        {
            Claim(stage);
            return Sampling.For(seed, stage);
        }

        public void Claim(string stage)
        {
            if (!used.Add(stage))
            {
                throw new ArgumentException($"The stage '{stage}' was named twice.", nameof(stage));
            }
        }

        public T Draw<T>(Stage<T> stage, Func<Random, T> draw)
        {
            Claim(stage.Name);
            return DrawAlone(stage, draw, DrawLimit);
        }

        public T Record<T>(Stage<T> stage, T result)
        {
            earlier.Record(stage, result);
            return result;
        }

        /*
          a stage drawn to match the spec by itself. The first draw is what the stage would be with no
          spec, so a seed whose draw already matches keeps its language. Otherwise the stage is drawn
          again, each time from a Random of its own, until a draw matches every fixed setting that a
          draw can match and meets every condition, which keeps the odds of the rest conditional on
          them; and if none does within the limit, as for a value the odds never give, the closest
          draw is kept. Any fixed value the kept draw still lacks, as an inventory or a number always
          would, is written into it, and it's mended to meet any condition it still doesn't. A draw
          missing a value that can't be written, one that needs a form it has none for, is passed
          over: otherwise the closest draw could lack a common value of that kind for having chased
          rare ones, which a draw meeting as much of the spec would have
         */
        public T DrawAlone<T>(Stage<T> stage, Func<Random, T> draw, int limit)
        {
            return Record(stage, stage.Blank is not null ? DrawIndependent(stage, draw, limit) : DrawRedrawing(stage, draw, limit));
        }

        private T DrawRedrawing<T>(Stage<T> stage, Func<Random, T> draw, int limit)
        {
            var spec = implied.Spec;
            var conditions = implied.For(stage);
            var settings = Fixed(stage);
            var redrawable = settings.Where(s => s.Redrawable).ToList();
            int Meeting(T drawn) => redrawable.Count(s => s.Matches(drawn, spec.Allowed(s)!)) + conditions.Count(c => c.Holds(drawn, earlier));
            var goal = redrawable.Count + conditions.Count;
            (T Written, int Meeting)? best = null;
            NoFormException? missed = null;
            for (int attempt = 0; (best?.Meeting ?? -1) < goal && attempt < limit; attempt++)
            {
                var drawn = draw(Sampling.For(seed, attempt == 0 ? stage.Name : $"{stage.Name} #{attempt}"));
                if (Meeting(drawn) is var meeting && meeting > (best?.Meeting ?? -1))
                {
                    /*
                      EXCEPTION AS CONTROL FLOW: a write throws NoFormException for a value the draw
                      has no form for, which only trying the write finds out. It's expected, not an
                      error: the draw is just passed over. Only a draw closer than the one kept is
                      tried, and few are
                     */
                    try
                    {
                        best = (Written(drawn), meeting);
                    }
                    catch (NoFormException e)
                    {
                        missed = e;
                    }
                }
            }
            return best is { } kept ? kept.Written : throw missed!;

            //a draw with the fixed values it lacks written in, mended to meet its conditions
            T Written(T drawn)
            {
                var written = settings.Aggregate(drawn, (d, s) => s.Matches(d, spec.Allowed(s)!) ? d : s.Write(d, spec.Allowed(s)!));
                return conditions.Aggregate(written, (d, c) => c.Holds(d, earlier) ? d
                    : c.Repair is { } repair ? repair(d)
                    : throw new InvalidOperationException($"Nothing mends what {stage.Name} draws to meet a condition: {c.Says}"));
            }
        }

        /*
          a stage whose settings make up its whole result and are drawn independently of each other,
          as a semantic map's edges are. Its fixed settings are written straight in, which leaves the
          rest with the odds they'd have anyway and keeps what the seed drew for them. It's drawn
          again until it meets its conditions, and if none does within the limit, its settings are
          searched for the result nearest the draw that does, which the spec's rules make sure there
          is
         */
        private T DrawIndependent<T>(Stage<T> stage, Func<Random, T> draw, int limit)
        {
            var conditions = implied.For(stage);
            var result = WriteFixed(stage, draw(Sampling.For(seed, stage.Name)));
            for (int attempt = 1; attempt < limit && !conditions.All(c => c.Holds(result, earlier)); attempt++)
            {
                result = WriteFixed(stage, draw(Sampling.For(seed, $"{stage.Name} #{attempt}")));
            }
            return conditions.All(c => c.Holds(result, earlier)) ? result
                : stage.Nearest(result, implied.Spec, conditions, earlier)
                ?? throw new InvalidOperationException($"No result of {stage.Name} meets its conditions, which the spec's rules should have refused.");
        }

        //a draw with its fixed values written in
        public T WriteFixed<T>(Stage<T> stage, T drawn) =>
            Fixed(stage).Aggregate(drawn, (d, s) => s.Matches(d, implied.Spec.Allowed(s)!) ? d : s.Write(d, implied.Spec.Allowed(s)!));

        /*
          stages drawn together, since what a later one can be depends on the earlier ones, as a
          spatial case depends on the morphology. The first attempt draws each as it would be with no
          spec, so a seed whose draws already match keeps its language. Otherwise each attempt draws
          every stage up to the last one that might not match again, each from a Random of its own,
          and reruns the rest on what that gives, until an attempt matches every fixed setting and
          meets every condition of every stage in it. That keeps the odds of every free setting
          conditional on all of them, so fixing a spatial case makes concatenative morphology likelier;
          and a stage after the last that might not match can't change whether an attempt does, so
          rerunning it with its first Random keeps more of what the seed gave. An attempt is abandoned
          at the first stage that misses, by an exception (see Group.Missed). If no attempt matches within the limit, the stages are drawn
          one at a time, as stages alone are, each given the ones before it, which the spec's rules
          have made sure allow it, and each up to the same limit, since drawing them together has
          already had its chance
         */
        public T DrawTogether<T>(Func<Group, T> body)
        {
            var first = new Group(this, attempt: 0, redrawThrough: -1, alone: false, claim: true);
            var result = body(first);
            if (first.Met)
            {
                return result;
            }
            for (int attempt = 1; attempt < GroupLimit; attempt++)
            {
                try
                {
                    return body(new Group(this, attempt, first.LastThatMightNotMatch, alone: false, claim: false));
                }
                /*
                  EXCEPTION AS CONTROL FLOW: Group.Draw throws Missed to abandon an attempt at the first
                  stage that misses, since the body is a lambda that can't be stopped partway any
                  other way. It's expected, not an error: catching it just moves on to the next attempt
                 */
                catch (Group.Missed)
                {
                }
            }
            return body(new Group(this, attempt: 0, redrawThrough: -1, alone: true, claim: false));
        }

        //whether a stage's draw might not match: it has a condition to meet, or a fixed setting a draw
        //can miss, unless its settings are written straight in
        public bool MightNotMatch<T>(Stage<T> stage) =>
            implied.For(stage).Count > 0 || (stage.Blank is null && Fixed(stage).Any(s => s.Redrawable));

        public bool Matches<T>(Stage<T> stage, T drawn) =>
            Fixed(stage).Where(s => s.Redrawable).All(s => s.Matches(drawn, implied.Spec.Allowed(s)!)) && implied.For(stage).All(c => c.Holds(drawn, earlier));

        //a draw with the fixed values no draw could give, as an inventory, written in
        public T WriteUnredrawable<T>(Stage<T> stage, T drawn) =>
            Fixed(stage).Where(s => !s.Redrawable).Aggregate(drawn, (d, s) => s.Write(d, implied.Spec.Allowed(s)!));

        public Random Attempt(string key) => Sampling.For(seed, key);

        private List<ISetting<T>> Fixed<T>(Stage<T> stage) => [.. stage.Settings.Where(s => implied.Spec.Allowed(s) is not null)];
    }

    //one attempt at drawing stages together: which stages it draws again, under which names, and
    //whether what it draws matches
    private sealed class Group(Stages stages, int attempt, int redrawThrough, bool alone, bool claim)
    {
        private int next;

        /*
          EXCEPTION AS CONTROL FLOW: thrown to abandon an attempt after the first at the first stage
          that misses, so the stages after it, which can't make the attempt match, aren't drawn for
          nothing. Most attempts at a spec that fixes much miss early, so this makes drawing a group
          several times faster. DrawTogether catches it, and nothing else should see it
         */
        public sealed class Missed : Exception;

        public bool Met { get; private set; } = true;
        public int LastThatMightNotMatch { get; private set; } = -1;

        public T Draw<T>(Stage<T> stage, Func<Random, T> draw)
        {
            var index = next++;
            if (claim)
            {
                stages.Claim(stage.Name);
            }
            if (alone)
            {
                return stages.DrawAlone(stage, draw, GroupLimit);
            }
            /*
              a stage whose settings are written straight in has them all written, and any other just
              the ones no draw could give; an attempt whose stage misses a condition is just rejected,
              as one whose stage misses a setting is
             */
            var key = attempt > 0 && index <= redrawThrough ? $"{stage.Name} #{attempt}" : stage.Name;
            var drawn = draw(stages.Attempt(key));
            drawn = stages.Record(stage, stage.Blank is not null ? stages.WriteFixed(stage, drawn) : stages.WriteUnredrawable(stage, drawn));
            if (stages.MightNotMatch(stage))
            {
                LastThatMightNotMatch = index;
                Met &= stages.Matches(stage, drawn);
                if (!Met && attempt > 0)
                {
                    //EXCEPTION AS CONTROL FLOW: see Missed. The first attempt draws every stage,
                    //since it works out which ones later attempts draw again
                    throw new Missed();
                }
            }
            return drawn;
        }
    }

    /*
      short forms for a closed class of words or affixes, kept distinct from each other where the
      phonology allows: each is drawn up to the given number of times until it's one not yet taken,
      and if every draw repeats one, the last is kept, as real languages keep some syncretism
     */
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
    private static List<Phoneme> VowelOrSyllable(Random rand, Phonology phonology) => rand.NextDouble() < 0.3
        ? [phonology.Vowels[Sampling.TruncatedGeometric(rand, phonology.Vowels.Count, phonology.Dropoff.Value)]]
        : [.. GenerateSyllable(rand, phonology.SyllableRule, phonology.Dropoff, needsOnset: false).Phonemes];
}
