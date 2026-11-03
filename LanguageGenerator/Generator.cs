using System.Runtime.InteropServices;

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

        var orthography = SelectOrthography(rand, vowels, consonants);
        //the lexicon's seed and root length are drawn here, so that the draws after them stay where
        //they were; the lexicon itself is built last, once there are closed classes for it to avoid
        var (lexiconSeed, extraSyllables) = (rand.Next(), SelectExtraSyllables(rand, syllableRule));

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
        var numerals = GenerateNumerals(rand, phonology, syllableDropoff);
        var possession = GeneratePossession(rand, morphology, wordOrder, phonology, syllableDropoff);
        var lexicon = GenerateLexicon(lexiconSeed, extraSyllables, phonology, syllableDropoff, new Dictionary<PartOfSpeech, IEnumerable<Lexeme>>
        {
            [PartOfSpeech.Noun] = pronouns.Forms.Values,
            [PartOfSpeech.Adjective] = [.. numerals.Words.Values, .. numerals.Many is { } many ? [many] : Array.Empty<Lexeme>()],
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
        };
    }

    //WALS 24A, leaving out its 6 languages that mark possession some other way
    private static readonly (PossessiveMarking Value, int Languages)[] PossessiveMarkingCounts =
    [
        (PossessiveMarking.Dependent, 98),
        (PossessiveMarking.Head, 78),
        (PossessiveMarking.Double, 22),
        (PossessiveMarking.None, 32),
    ];

    //possessors go where adpositions' objects do, before the noun with postpositions and after it
    //with prepositions: Greenberg's Universal 2, which has few exceptions. An isolating language
    //can't mark the possessed noun with an affix, so it marks the possessor, as Mandarin does with
    //de. The affixes agreeing with a possessor are each a short vowel or a syllable, all on one side
    public static Possession GeneratePossession(Random rand, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff)
    {
        var marking = Sampling.Categorical(rand, PossessiveMarkingCounts);
        if (morphology.Fusion == Fusion.Isolating && marking is PossessiveMarking.Head or PossessiveMarking.Double)
        {
            marking = PossessiveMarking.Dependent;
        }
        var possessorFirst = wordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Final;
        if (marking is not (PossessiveMarking.Head or PossessiveMarking.Double))
        {
            return new Possession(marking, possessorFirst, null, ExponentPosition.Suffix);
        }

        var position = rand.NextDouble() < morphology.PrefixChance ? ExponentPosition.Prefix : ExponentPosition.Suffix;
        var affixes = new Dictionary<(Person, Number), Affix>();
        var forms = new DistinctForms();
        foreach (var person in new[] { Person.First, Person.Second, Person.Third })
        {
            foreach (var number in new[] { Number.Singular, Number.Plural })
            {
                var form = forms.Draw(() => VowelOrSyllable(rand, phonology, dropoff));
                affixes[(person, number)] = new Affix(form, [person, number, Possessive.Possessed]);
            }
        }
        return new Possession(marking, possessorFirst, affixes, position);
    }

    //WALS 131A, leaving out its 5 languages with other bases and 4 that count on body parts. Its
    //hybrid vigesimal-decimal languages (22) and pure vigesimal ones (20) are both vigesimal here,
    //since most of the pure ones build 11 to 19 on ten or five too
    private static readonly (NumeralBase Value, int Languages)[] NumeralBaseCounts =
        [(NumeralBase.Decimal, 125), (NumeralBase.Vigesimal, 42), (NumeralBase.Restricted, 20)];
    //WALS 89A, leaving out its 21 languages with no dominant order and 37 with both
    private static readonly (bool Value, int Languages)[] NumeralBeforeNounCounts = [(true, 479), (false, 607)];

    //a short word for each number the base needs, each redrawn to keep it from sounding like the
    //others. A restricted system's limit, and the odds of a singular noun after a numeral and of
    //compound numbers putting units last, are guesses: WALS doesn't count them
    public static NumeralSystem GenerateNumerals(Random rand, Phonology phonology, double dropoff)
    {
        var numeralBase = Sampling.Categorical(rand, NumeralBaseCounts);
        var beforeNoun = Sampling.Categorical(rand, NumeralBeforeNounCounts);
        var nounStaysSingular = rand.NextDouble() < 0.4;
        var largestFirst = rand.NextDouble() < 0.8;
        var limit = numeralBase == NumeralBase.Restricted ? rand.Next(3, 6) : NumeralSystem.Largest;

        var forms = new DistinctForms();
        Lexeme Word(string meaning) =>
            new(new Root(forms.Draw(() => GenerateForm(rand, phonology.SyllableRule, dropoff, 0.15, maxSyllables: 2)), meaning), PartOfSpeech.Numeral);

        var numbers = numeralBase switch
        {
            NumeralBase.Decimal => [.. Enumerable.Range(1, 9), 10, 100],
            NumeralBase.Vigesimal => [.. Enumerable.Range(1, 9), 10, 20, 400],
            _ => Enumerable.Range(1, limit).ToList(),
        };
        var words = numbers.ToDictionary(n => n, n => Word(NumeralNames[n]));
        var many = numeralBase == NumeralBase.Restricted ? Word("many") : null;
        return new NumeralSystem(numeralBase, limit, words, many, beforeNoun, nounStaysSingular, largestFirst);
    }

    //each numeral word's gloss
    private static readonly Dictionary<int, string> NumeralNames = new()
    {
        [1] = "one", [2] = "two", [3] = "three", [4] = "four", [5] = "five", [6] = "six", [7] = "seven", [8] = "eight", [9] = "nine",
        [10] = "ten", [20] = "twenty", [100] = "hundred", [400] = "four.hundred",
    };

    //WALS 30A for how many genders, leaving out its 36 languages with four or more. 31A says some of
    //the two-gender systems aren't sex-based; splitting them about four to one into masculine and
    //feminine and animate and inanimate is a guess
    private static readonly (NounGenders Value, int Languages)[] NounGenderCounts =
    [
        (NounGenders.None, 145),
        (NounGenders.MasculineFeminine, 40),
        (NounGenders.AnimateInanimate, 10),
        (NounGenders.MasculineFeminineNeuter, 26),
    ];
    //WALS 32A
    private static readonly (GenderAssignment Value, int Languages)[] GenderAssignmentCounts =
        [(GenderAssignment.Semantic, 53), (GenderAssignment.SemanticAndFormal, 59)];

    //a marker per gender for words that agree with a noun, all on one side, with the first gender
    //unmarked, as the masculine or the animate usually is. Whether verbs agree too, where they agree
    //with their subject at all, is a guess at even odds
    public static GenderSystem GenerateGenders(Random rand, MorphologicalProfile morphology, Dictionary<PartOfSpeech, Paradigm> paradigms, Phonology phonology, double dropoff)
    {
        var kind = Sampling.Categorical(rand, NounGenderCounts);
        var assignment = Sampling.Categorical(rand, GenderAssignmentCounts);
        var position = rand.NextDouble() < morphology.PrefixChance ? ExponentPosition.Prefix : ExponentPosition.Suffix;
        var verbsAgree = rand.NextDouble() < 0.5 && paradigms[PartOfSpeech.Verb].Categories.Has<Person>();
        var system = new GenderSystem { Genders = kind, Assignment = assignment, Markers = null, MarkerPosition = position, VerbsAgree = false };
        if (kind == NounGenders.None || morphology.Fusion == Fusion.Isolating)
        {
            return system;
        }

        var markers = new Dictionary<Gender, Affix>();
        var forms = new DistinctForms();
        foreach (var (gender, i) in system.Values.Select((g, i) => (g, i)))
        {
            markers[gender] = new Affix(i == 0 ? [] : forms.Draw(() => VowelOrSyllable(rand, phonology, dropoff)), [gender]);
        }
        return new GenderSystem { Genders = kind, Assignment = assignment, Markers = markers, MarkerPosition = position, VerbsAgree = verbsAgree };
    }

    //guesses, with no WALS chapter to go on. Languages whose adjectives are verbs, like Mandarin,
    //Japanese and Korean, don't make them agree with nouns the way Latin or Russian do
    private static readonly (AdjectiveAgreement Value, int Weight)[] AdjectiveAgreementWeights =
        [(AdjectiveAgreement.None, 40), (AdjectiveAgreement.Agreement, 40), (AdjectiveAgreement.Phrasal, 20)];
    private static readonly (AdjectiveAgreement Value, int Weight)[] VerbalAdjectiveAgreementWeights =
        [(AdjectiveAgreement.None, 65), (AdjectiveAgreement.Phrasal, 35)];

    public static AdjectiveAgreement SelectAdjectiveAgreement(Random rand, Predication predication) =>
        Sampling.Categorical(rand, predication.Adjectives == PredicativeAdjectives.Verbal ? VerbalAdjectiveAgreementWeights : AdjectiveAgreementWeights);

    //WALS 118A and 120A, drawn independently. The copula is one syllable, as frequent words are
    private static readonly (PredicativeAdjectives Value, int Languages)[] PredicativeAdjectiveCounts =
        [(PredicativeAdjectives.Verbal, 151), (PredicativeAdjectives.Nonverbal, 132)];
    private static readonly (bool Value, int Languages)[] ZeroCopulaCounts = [(false, 211), (true, 175)];

    public static Predication GeneratePredication(Random rand, Phonology phonology, double dropoff) => new(
        Sampling.Categorical(rand, PredicativeAdjectiveCounts),
        Sampling.Categorical(rand, ZeroCopulaCounts),
        new Lexeme(new Root(GenerateForm(rand, phonology.SyllableRule, dropoff, 0, maxSyllables: 1), "COP"), PartOfSpeech.Verb));

    //WALS 119A
    private static readonly (bool Value, int Languages)[] LocationalCopulaCounts = [(true, 269), (false, 117)];

    //a one-syllable locational copula of its own, redrawn a few times to keep it from sounding like
    //the copula, in WALS's share of languages
    public static Predication GenerateLocationalPredication(Random rand, Predication predication, Phonology phonology, double dropoff)
    {
        if (!Sampling.Categorical(rand, LocationalCopulaCounts))
        {
            return predication;
        }
        var forms = new DistinctForms(draws: 11);
        forms.Take(predication.Copula.Root.Form);
        var form = forms.Draw(() => GenerateForm(rand, phonology.SyllableRule, dropoff, 0, maxSyllables: 1));
        return predication with { LocationalCopula = new Lexeme(new Root(form, "be.at"), PartOfSpeech.Verb) };
    }

    //WALS 143A, leaving out its 87 languages where a second negative is optional and its 64 that use
    //two of these types in different constructions. A negative auxiliary verb counts as a word here
    private static readonly (NegationStrategy Value, int Languages)[] NegationCounts =
    [
        (NegationStrategy.ParticleBefore, 525),
        (NegationStrategy.ParticleAfter, 171),
        (NegationStrategy.Prefix, 162),
        (NegationStrategy.Suffix, 202),
        (NegationStrategy.Double, 114),
    ];

    //an isolating language has no affixes to negate with, so it puts a word where an affix would go.
    //Double negation is two words, as French ne and pas are written; the first is often a clitic in
    //speech, but that's a distinction the generator doesn't make
    public static Negation GenerateNegation(Random rand, MorphologicalProfile morphology, Phonology phonology, double dropoff)
    {
        var strategy = Sampling.Categorical(rand, NegationCounts);
        if (morphology.Fusion == Fusion.Isolating)
        {
            strategy = strategy switch
            {
                NegationStrategy.Prefix => NegationStrategy.ParticleBefore,
                NegationStrategy.Suffix => NegationStrategy.ParticleAfter,
                _ => strategy,
            };
        }
        ExponentPosition[] positions = strategy switch
        {
            NegationStrategy.ParticleBefore => [ExponentPosition.PrecedingParticle],
            NegationStrategy.ParticleAfter => [ExponentPosition.FollowingParticle],
            NegationStrategy.Prefix => [ExponentPosition.Prefix],
            NegationStrategy.Suffix => [ExponentPosition.Suffix],
            NegationStrategy.Double => [ExponentPosition.PrecedingParticle, ExponentPosition.FollowingParticle],
            _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
        };

        //a negative is a frequent, short morpheme: a syllable, or for an affix sometimes just a
        //consonant, the way the generator makes other affixes
        Morpheme Marker(ExponentPosition position)
        {
            if (position is ExponentPosition.Prefix or ExponentPosition.Suffix)
            {
                List<Phoneme> form = rand.NextDouble() < 0.3
                    ? [phonology.Consonants[Sampling.TruncatedGeometric(rand, phonology.Consonants.Count, dropoff)]]
                    : [.. GenerateSyllable(rand, phonology.SyllableRule, dropoff).Phonemes];
                return new Affix(form, [Polarity.Negative]);
            }
            return new Particle([.. GenerateSyllable(rand, phonology.SyllableRule, dropoff).Phonemes], [Polarity.Negative]);
        }
        return new Negation(strategy, [.. positions.Select(p => (p, Marker(p)))]);
    }

    //WALS 39A, leaving out its 2 languages with no 'we' at all and 5 whose only distinction is a
    //separate inclusive
    private static readonly (FirstPersonPlural Value, int Languages)[] FirstPersonPluralCounts =
    [
        (FirstPersonPlural.Plain, 120),
        (FirstPersonPlural.SameAsSingular, 10),
        (FirstPersonPlural.InclusiveExclusive, 63),
    ];

    //WALS 101A, read as whether subject pronouns are usually left out: they are with subject affixes
    //on the verb (437 languages) or optional pronouns (61), and aren't where they're obligatory (82).
    //Optional pronouns are the only kind of dropping open to a language whose verbs don't agree. The
    //rest of its languages put subject markers somewhere this doesn't model (clitics, a different
    //position, mixed)
    private static readonly (bool Value, int Languages)[] AgreeingSubjectDropCounts = [(true, 437 + 61), (false, 82)];
    private static readonly (bool Value, int Languages)[] NonAgreeingSubjectDropCounts = [(true, 61), (false, 82)];

    //one short root per person and number, kept distinct from each other where the phonology allows,
    //since a pronoun is never helped by context the way a noun is. Pronouns are among a language's
    //most frequent words, so they're short, by the law of abbreviation: mostly one syllable, never
    //more than two
    public static PronounSystem GeneratePronouns(Random rand, Dictionary<PartOfSpeech, Paradigm> paradigms, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff)
    {
        var firstPersonPlural = Sampling.Categorical(rand, FirstPersonPluralCounts);
        List<Pronoun> meanings =
        [
            new(Person.First, Number.Singular), new(Person.Second, Number.Singular), new(Person.Third, Number.Singular),
            .. firstPersonPlural switch
            {
                FirstPersonPlural.Plain => (Pronoun[])[new(Person.First, Number.Plural)],
                FirstPersonPlural.InclusiveExclusive => [new(Person.First, Number.Plural, Clusivity.Inclusive), new(Person.First, Number.Plural, Clusivity.Exclusive)],
                FirstPersonPlural.SameAsSingular => [],
                _ => throw new ArgumentOutOfRangeException(nameof(firstPersonPlural), firstPersonPlural, null),
            },
            new(Person.Second, Number.Plural), new(Person.Third, Number.Plural),
        ];

        var forms = new Dictionary<Pronoun, Lexeme>();
        var distinct = new DistinctForms();
        List<Phoneme> NewForm() => distinct.Draw(() => GenerateForm(rand, phonology.SyllableRule, dropoff, 0.15, maxSyllables: 2));
        foreach (var meaning in meanings)
        {
            var form = NewForm();
            //a pronoun's gloss is its person and number, as in 1SG, and one meaning both I and we is just 1
            var gloss = firstPersonPlural == FirstPersonPlural.SameAsSingular && meaning.Person == Person.First ? Person.First.Gloss : meaning.Gloss;
            forms[meaning] = new Lexeme(new Root(form, gloss), PartOfSpeech.Pronoun);
        }
        if (firstPersonPlural == FirstPersonPlural.SameAsSingular)
        {
            forms[new(Person.First, Number.Plural)] = forms[new(Person.First, Number.Singular)];
        }

        //pronouns take the nouns' case markers where nouns mark case in a slot of its own, as in
        //Turkish ben-i 'me', like ev-i 'the house'. Where case is fused with number, pronouns get
        //case markers of their own, since a pronoun's number is already part of it
        var nouns = paradigms[PartOfSpeech.Noun];
        var caseCategory = nouns.Categories.FirstOrDefault(c => c.Is<Case>());
        List<InflectionalCategory> categories = caseCategory is null ? [] : [caseCategory];
        var paradigm = nouns.Slots.FirstOrDefault(s => s.Categories is [var only] && only.Is<Case>()) is { } caseSlot
            ? new Paradigm { PartOfSpeech = PartOfSpeech.Pronoun, Categories = categories, Slots = [caseSlot] }
            : GenerateParadigm(rand, PartOfSpeech.Pronoun, categories, morphology, wordOrder.AdpositionalPhraseDirectionality, phonology, dropoff);

        var agrees = paradigms[PartOfSpeech.Verb].Categories.Has<Person>();
        var dropsSubjects = Sampling.Categorical(rand, agrees ? AgreeingSubjectDropCounts : NonAgreeingSubjectDropCounts);

        //gender comes last, so that adding it left every other pronoun as it was for each seed: an
        //ungendered pronoun's form becomes the masculine, and the other genders get forms of their own
        var gendered = Sampling.Categorical(rand, PronounGenderCounts);
        List<Gender> genders = gendered == PronounGender.None ? []
            : rand.NextDouble() < 0.6 ? [Gender.Masculine, Gender.Feminine]
            : [Gender.Masculine, Gender.Feminine, Gender.Neuter];
        var withGender = new Dictionary<Pronoun, Lexeme>();
        foreach (var (meaning, lexeme) in forms)
        {
            if (!PronounSystem.Distinguishes(gendered, meaning.Person, meaning.Number))
            {
                withGender[meaning] = lexeme;
                continue;
            }
            //a neuter is for things, which are spoken of but not to
            foreach (var gender in genders.Where(g => g != Gender.Neuter || meaning.Person == Person.Third))
            {
                var form = gender == Gender.Masculine ? lexeme.Root.Form : NewForm();
                var genderedMeaning = meaning with { Gender = gender };
                withGender[genderedMeaning] = new Lexeme(new Root(form, genderedMeaning.Gloss), PartOfSpeech.Pronoun);
            }
        }

        return new PronounSystem
        {
            FirstPersonPlural = firstPersonPlural,
            GenderedPronouns = gendered,
            Genders = genders,
            Forms = withGender,
            Paradigm = paradigm,
            DropsSubjects = dropsSubjects,
        };
    }

    //WALS 44A, leaving out its 3 languages with gender only outside the third person singular and
    //plural. Whether the gendered pronouns have a neuter, like English it, is a guess: WALS 30A
    //counts the genders of nouns, not of pronouns
    private static readonly (PronounGender Value, int Languages)[] PronounGenderCounts =
    [
        (PronounGender.None, 254),
        (PronounGender.ThirdSingular, 61),
        (PronounGender.Third, 42),
        (PronounGender.ThirdAndSecond, 18),
    ];

    //basic meanings to fill the lexicon with, glossed the way the Leipzig rules gloss roots
    private static readonly string[] NounMeanings = ["person", "dog", "cat", "fish", "bird", "tree", "stone", "water", "fire", "sun", "house"];
    private static readonly string[] VerbMeanings = ["see", "eat", "drink", "sleep", "walk", "give", "know", "hear"];
    private static readonly string[] AdjectiveMeanings = ["big", "small", "good", "old"];

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

    //a set of conventions, applied to the whole inventory. Conventions that clash for this
    //inventory (acute long vowels beside acute close vowels both give ó, say) are drawn again as
    //a set, so a language never ends up with a convention applied to only some of its sounds.
    //IPA is the fallback, and always unambiguous
    public static Orthography SelectOrthography(Random rand, List<Vowel> vowels, List<Consonant> consonants)
    {
        var inventory = vowels.Concat<Phoneme>(consonants).ToList();
        T Pick<T>(IReadOnlyList<T> options) => options[rand.Next(options.Count)];
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var style = new OrthographicStyle(
                Pick(Enum.GetValues<PostalveolarSpelling>()),
                Pick(Enum.GetValues<LongVowelSpelling>()),
                Pick(Enum.GetValues<MidVowelSpelling>()),
                Pick(OrthographicStyle.SchwaOptions),
                Pick(OrthographicStyle.NearOpenFrontOptions),
                Pick(OrthographicStyle.VelarStopOptions),
                Pick(OrthographicStyle.PalatalGlideOptions));
            var spellings = style.Spell(inventory);
            if (Orthography.IsUnambiguous(spellings.Values))
            {
                return new Orthography(spellings, style);
            }
        }
        return new Orthography(inventory.ToDictionary(p => p, p => p.IpaSymbol));
    }

    //a lexicon that coins each root from its own Random, seeded by the language and the meaning,
    //so that a root never depends on which other words were coined, or in what order. A root is
    //redrawn from that same Random, a few times at most, if it sounds like a closed-class word that
    //fills the same slot, as a pronoun does a noun's, a numeral an adjective's and a copula a verb's:
    //'the person saw' and 'you saw' shouldn't sound alike. Homophones in different slots, like
    //English there and their, are left alone, as languages leave them. The basic meanings are
    //coined up front so that there are words to show
    public static Lexicon GenerateLexicon(int seed, double extraSyllables, Phonology phonology, double dropoff, IReadOnlyDictionary<PartOfSpeech, IEnumerable<Lexeme>> closedClasses)
    {
        string Surface(Root root) => string.Concat(phonology.Realize(new Word([root])).Phonemes.Select(p => p.IpaSymbol));
        var taken = closedClasses.ToDictionary(c => c.Key, c => c.Value.Select(l => Surface(l.Root)).ToHashSet());
        var rule = phonology.SyllableRule;
        var lexicon = new Lexicon((meaning, partOfSpeech) =>
        {
            var rand = Sampling.For(seed, $"{partOfSpeech}:{meaning}");
            var root = new Root(GenerateForm(rand, rule, dropoff, extraSyllables), meaning);
            for (int attempt = 0; attempt < 10 && taken.TryGetValue(partOfSpeech, out var forms) && forms.Contains(Surface(root)); attempt++)
            {
                root = new Root(GenerateForm(rand, rule, dropoff, extraSyllables), meaning);
            }
            return root;
        });
        foreach (var meaning in NounMeanings)
        {
            lexicon.Noun(meaning);
        }
        foreach (var meaning in VerbMeanings)
        {
            lexicon.Verb(meaning);
        }
        foreach (var meaning in AdjectiveMeanings)
        {
            lexicon.Adjective(meaning);
        }
        return lexicon;
    }

    public const int MaxRootSyllables = 5;

    //how many syllables past the first a root has on average. Languages with few possible syllables
    //need longer words to tell them apart, so Hawaiian's roots run longer than English's: about as
    //many syllables as it takes to make RootsToDistinguish distinct forms. Tuned by eye; WALS has
    //nothing on word length
    public static double SelectExtraSyllables(Random rand, SyllableRule rule)
    {
        const double RootsToDistinguish = 100_000;
        var needed = Math.Log(RootsToDistinguish) / Math.Log(Math.Max(rule.DistinctSyllables, 2));
        var noise = 0.75 + 0.5 * rand.NextDouble();
        return Math.Clamp((needed - 1) * noise, 0.2, 2.0);
    }

    public static List<Phoneme> GenerateForm(Random rand, SyllableRule rule, double dropoff, double extraSyllables, int maxSyllables = MaxRootSyllables)
    {
        var form = new List<Phoneme>();
        var numberOfSyllables = Sampling.DisplacedPoisson(rand, extraSyllables, maxSyllables);

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
                Exponents = GenerateExponents(rand, slotCategories, position, morphology.ZeroMarking, phonology, dropoff),
            });
        }
        return new Paradigm { PartOfSpeech = partOfSpeech, Categories = categories, Slots = slots };
    }

    //one exponent per combination of values, with no form at all for combinations the language
    //leaves unmarked. Other forms are kept distinct within a slot where possible; if the phonology
    //keeps producing repeats, the syncretism is allowed
    private static List<(IReadOnlyList<Tagmeme> Values, Morpheme Exponent)> GenerateExponents(Random rand, List<InflectionalCategory> categories, ExponentPosition position, ZeroMarking zeroMarking, Phonology phonology, double dropoff)
    {
        var exponents = new List<(IReadOnlyList<Tagmeme>, Morpheme)>();
        var forms = new DistinctForms();
        foreach (var values in Paradigm.Combinations(categories))
        {
            if (zeroMarking.IsZero(values))
            {
                exponents.Add((values, position is ExponentPosition.Prefix or ExponentPosition.Suffix ? new Affix([], values) : new Particle([], values)));
                continue;
            }
            //affixes are often a bare consonant (English -s, -t), which is what gives sandhi and cluster
            //repair work to do; particles are words of their own, so they're always a whole syllable
            var bare = position is ExponentPosition.Prefix or ExponentPosition.Suffix && rand.NextDouble() < 0.3;
            var form = forms.Draw(() => bare
                ? [phonology.Consonants[Sampling.TruncatedGeometric(rand, phonology.Consonants.Count, dropoff)]]
                : [.. GenerateSyllable(rand, phonology.SyllableRule, dropoff).Phonemes]);
            Morpheme exponent = position is ExponentPosition.Prefix or ExponentPosition.Suffix ? new Affix(form, values) : new Particle(form, values);
            exponents.Add((values, exponent));
        }
        return exponents;
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
        : [.. GenerateSyllable(rand, phonology.SyllableRule, dropoff).Phonemes];

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
        //a restriction only matters for templates with a coda
        var codas = template.Slots.Any(s => s is FinalSlot) ? Sampling.Categorical(rand, CodaRestrictionWeights) : CodaRestriction.Any;
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

    public static List<Vowel> GenerateVowels(Random rand, double chanceOfPhonemePruning, double chanceOfCategoryPruning)
    {
        var vowels = new List<Vowel>(Vowel.All);

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

        //the order is a frequency ranking: Pick draws earlier phonemes more often
        rand.Shuffle(CollectionsMarshal.AsSpan(vowels));

        return vowels;
    }

    public static List<Consonant> GenerateConsonants(Random rand, double chanceofPhonemePruning, double chanceOfCategoryPruning)
    {
        var consonants = new List<Consonant>(Consonant.All);

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

        rand.Shuffle(CollectionsMarshal.AsSpan(consonants));

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

        //the verb phrase's direction follows from the order itself; the others are drawn by it.
        //initializers run in order, so the draws always happen in this order
        var objectFirst = WordOrder.ObjectPrecedesVerb(order);
        return new WordOrder
        {
            ConstituentWordOrder = order,
            AdpositionalPhraseDirectionality = Sampling.Categorical(rand, objectFirst ? ObjectVerbAdpositionCounts : VerbObjectAdpositionCounts),
            NounPhraseDirectionality = Sampling.Categorical(rand, objectFirst ? ObjectVerbNounPhraseCounts : VerbObjectNounPhraseCounts),
            AdjectivePhraseDirectionality = Sampling.Categorical(rand, objectFirst ? ObjectVerbAdjectivePhraseCounts : VerbObjectAdjectivePhraseCounts),
        };
    }

    //each phrase's head-initial and head-final counts, by the order of object and verb. Languages
    //with no dominant order of either kind are left out, since every generated language has one

    // https://wals.info/feature/95A: prepositions are head-initial and postpositions head-final.
    //adposition order follows the verb almost without exception
    private static readonly (HeadDirectionality Value, int Languages)[] ObjectVerbAdpositionCounts =
        [(HeadDirectionality.Initial, 14), (HeadDirectionality.Final, 472)];
    private static readonly (HeadDirectionality Value, int Languages)[] VerbObjectAdpositionCounts =
        [(HeadDirectionality.Initial, 456), (HeadDirectionality.Final, 42)];

    // https://wals.info/feature/97A: noun-adjective is head-initial. Adjective order barely
    //follows the verb; Dryer's chapter is known for showing that
    private static readonly (HeadDirectionality Value, int Languages)[] ObjectVerbNounPhraseCounts =
        [(HeadDirectionality.Initial, 332), (HeadDirectionality.Final, 216)];
    private static readonly (HeadDirectionality Value, int Languages)[] VerbObjectNounPhraseCounts =
        [(HeadDirectionality.Initial, 456), (HeadDirectionality.Final, 114)];

    // https://wals.info/combinations/83A_91A: an adjective before its degree word ("big very") is
    //head-initial
    private static readonly (HeadDirectionality Value, int Languages)[] ObjectVerbAdjectivePhraseCounts =
        [(HeadDirectionality.Initial, 72), (HeadDirectionality.Final, 128)];
    private static readonly (HeadDirectionality Value, int Languages)[] VerbObjectAdjectivePhraseCounts =
        [(HeadDirectionality.Initial, 107), (HeadDirectionality.Final, 88)];

    //WALS won't classify languages as fusional, agglutinative or analytic, but it does measure the
    //properties underneath, each drawn here in proportion to WALS's language counts

    // https://wals.info/combinations/20A_83A: fusion by the order of object and verb. Isolating
    //morphology is far commoner in VO languages; the OV counts are small, but 26A's languages with
    //little affixation lean the same way (35 OV to 100 VO). Neither tone nor ablaut is modeled, so
    //values pairing one with another strategy count as that strategy, and the exclusively tonal
    //languages are left out, as are those with no dominant order
    private static readonly (Fusion Value, int Languages)[] ObjectVerbFusionCounts =
    [
        (Fusion.Concatenative, 70 + 1 + 0),
        (Fusion.Mixed, 5),
        (Fusion.Isolating, 3 + 0),
    ];
    private static readonly (Fusion Value, int Languages)[] VerbObjectFusionCounts =
    [
        (Fusion.Concatenative, 38 + 3 + 2),
        (Fusion.Mixed, 7),
        (Fusion.Isolating, 12 + 1),
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

    public static MorphologicalProfile SelectMorphologicalProfile(Random rand, ConstituentWordOrder order) => new(
        Sampling.Categorical(rand, WordOrder.ObjectPrecedesVerb(order) ? ObjectVerbFusionCounts : VerbObjectFusionCounts),
        new Exponence(
            // https://wals.info/chapter/21: 8 of the 87 languages with case mark it together with number
            CaseWithNumber: rand.NextDouble() < 8 / 87.0,
            //and 24 of the 156 with tense-aspect-mood inflection mark it together with agreement
            TamWithAgreement: rand.NextDouble() < 24 / 156.0,
            //a guess: WALS counts tense and aspect together as TAM, so it has no figure for this
            TenseWithAspect: rand.NextDouble() < 0.3),
        Sampling.Categorical(rand, WordOrder.ObjectPrecedesVerb(order) ? ObjectVerbAffixationCounts : VerbObjectAffixationCounts),
        new ZeroMarking(
            //guesses, but grounded in Greenberg's universals: the singular can be expressed only by
            //zero while the plural never is (35), and the only case that can be zero is the one for
            //the intransitive subject (38). WALS has no counts for either, or for the present tense
            Singular: rand.NextDouble() < 0.9,
            CoreCase: rand.NextDouble() < 0.8,
            Present: rand.NextDouble() < 0.6,
            ThirdPerson: Sampling.Categorical(rand, ThirdPersonZeroCounts)));

    // https://wals.info/feature/103A, among the 284 languages with person marking on verbs. The 21
    //with zero in only some third singular forms are left out, since a paradigm here has one form
    //per cell, and so is the 1 with zero only in the third person nonsingular
    private static readonly (ThirdPersonZero Value, int Languages)[] ThirdPersonZeroCounts =
    [
        (ThirdPersonZero.None, 181),
        (ThirdPersonZero.Singular, 45),
        (ThirdPersonZero.All, 36),
    ];

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

    //WALS 52A, leaving out its 33 languages that mark the two alike only in part: whether 'with a
    //friend' and 'with a knife' share a word, as in English, or each has its own
    private static readonly (bool Value, int Languages)[] ComitativeIsInstrumentalCounts = [(true, 76), (false, 213)];

    //WALS 84A, leaving out its 189 languages with no dominant order
    private static readonly (ObliquePosition Value, int Languages)[] ObjectVerbObliqueCounts =
    [
        (ObliquePosition.BeforeVerbAndObject, 48),
        (ObliquePosition.BetweenObjectAndVerb, 45),
        (ObliquePosition.AfterVerbAndObject, 27),
    ];
    private static readonly (ObliquePosition Value, int Languages)[] VerbObjectObliqueCounts =
        [(ObliquePosition.BeforeVerbAndObject, 3), (ObliquePosition.AfterVerbAndObject, 210)];

    //obliques nearly always follow the verb and its object where the object follows the verb, while
    //languages with the object first split three ways
    public static ObliquePosition SelectObliquePosition(Random rand, ConstituentWordOrder order) =>
        Sampling.Categorical(rand, WordOrder.ObjectPrecedesVerb(order) ? ObjectVerbObliqueCounts : VerbObjectObliqueCounts);

    //a one-syllable word for each role an adposition could mark, glossed in English. 'To' marks
    //both a recipient and a goal, as in English, and the comitative marks instruments too in about
    //a quarter of languages. Each new word is redrawn a few times to keep it from sounding like
    //one before it; a small inventory may still make homophones, as real languages have
    public static Dictionary<CaseUse, Lexeme> GenerateAdpositions(Random rand, Phonology phonology, double dropoff)
    {
        var instrumentalIsComitative = Sampling.Categorical(rand, ComitativeIsInstrumentalCounts);
        var words = new Dictionary<string, Lexeme>();
        var forms = new DistinctForms(draws: 11);
        Lexeme Word(string meaning)
        {
            if (!words.TryGetValue(meaning, out var lexeme))
            {
                var form = forms.Draw(() => GenerateForm(rand, phonology.SyllableRule, dropoff, 0, maxSyllables: 1));
                lexeme = words[meaning] = new Lexeme(new Root(form, meaning), PartOfSpeech.Adposition);
            }
            return lexeme;
        }

        return new()
        {
            [CaseUse.Possessive] = Word("of"),
            [CaseUse.IndirectObject] = Word("to"),
            [CaseUse.Location] = Word("at"),
            [CaseUse.MovementTo] = Word("to"),
            [CaseUse.MovementFrom] = Word("from"),
            [CaseUse.Accompaniment] = Word("with"),
            [CaseUse.Instrument] = Word(instrumentalIsComitative ? "with" : "using"),
        };
    }

    //a role gets its case if the language has one, and otherwise its adposition
    public static Dictionary<CaseUse, RoleMarker> SelectRoleMarkers(MorphosyntacticAlignment alignment, List<Case> cases, WordOrder wordOrder, IReadOnlyDictionary<CaseUse, Lexeme> adpositions)
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
        RoleMarker Adposition(CaseUse use) => new Adposition(adpositions[use], type, governs);
        RoleMarker CaseOrAdposition(Case @case, CaseUse use) => cases.Contains(@case) ? @case : Adposition(use);

        RoleMarker MarkerFor(CaseUse use) => use switch
        {
            CaseUse.IntransitiveSubject => Core(Case.Nominative, Case.Absolutive),
            CaseUse.TransitiveSubject => Core(Case.Nominative, Case.Ergative),
            CaseUse.DirectObject => Core(Case.Accusative, Case.Absolutive),
            CaseUse.Possessive => CaseOrAdposition(Case.Genitive, use),
            CaseUse.IndirectObject => CaseOrAdposition(Case.Dative, use),
            CaseUse.Address => cases.Contains(Case.Vocative) ? Case.Vocative : new Unmarked(),
            CaseUse.Location => CaseOrAdposition(Case.Locative, use),
            CaseUse.MovementTo => Adposition(use),
            CaseUse.MovementFrom => CaseOrAdposition(Case.Ablative, use),
            CaseUse.Accompaniment => Adposition(use),
            CaseUse.Instrument => CaseOrAdposition(Case.Instrumental, use),
            _ => throw new ArgumentOutOfRangeException(nameof(use), use, "Every role needs a marker."),
        };

        return Enum.GetValues<CaseUse>().ToDictionary(use => use, MarkerFor);
    }
}
