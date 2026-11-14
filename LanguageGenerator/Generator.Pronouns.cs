namespace LanguageGenerator;

//personal pronouns
public static partial class Generator
{
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
    public static PronounSystem GeneratePronouns(Random rand, Paradigms paradigms, MorphologicalProfile morphology, WordOrder wordOrder, Phonology phonology, double dropoff)
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
        List<Phoneme> NewForm() => distinct.Draw(() => GenerateForm(rand, phonology, dropoff, 0.15, maxSyllables: 2));
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
        //case markers of their own, since a pronoun's number is already part of it. A localization
        //goes with case, as 'under it' is as spatial as 'under the table'
        var nouns = paradigms.Nouns;
        static bool Takes(InflectionalCategory category) => category.Is<Case>() || category.Is<Localization>();
        List<InflectionalCategory> categories = [.. nouns.Categories.Where(Takes)];
        List<ExponentSlot> slots = [.. nouns.Slots.Where(s => s.Categories.All(Takes))];
        var paradigm = slots.Sum(s => s.Categories.Count) == categories.Count
            ? new Paradigm { PartOfSpeech = PartOfSpeech.Pronoun, Categories = categories, Slots = slots }
            : GenerateParadigm(rand, PartOfSpeech.Pronoun, categories, morphology, wordOrder.AdpositionalPhraseDirectionality, phonology, dropoff);

        var agrees = paradigms.Verbs.Categories.Has<Person>();
        var dropsSubjects = Sampling.Categorical(rand, agrees ? AgreeingSubjectDropCounts : NonAgreeingSubjectDropCounts);

        //an ungendered pronoun's form becomes the masculine, and the other genders get forms of their own
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
}
