namespace LanguageGenerator;

//alignment, cases, adpositions, and how each role is marked
public static partial class Generator
{
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

    //a one-syllable word for each role an adposition could mark, glossed in English. 'To' marks
    //both a recipient and a goal, as in English, and the comitative marks instruments too in about
    //a quarter of languages. Each new word is redrawn a few times to keep it from sounding like
    //one before it; a small inventory may still make homophones, as real languages have
    public static Dictionary<Role, Lexeme> GenerateAdpositions(Random rand, Phonology phonology, double dropoff)
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
            [Role.Possessor] = Word("of"),
            [Role.Recipient] = Word("to"),
            [Role.Location] = Word("at"),
            [Role.Goal] = Word("to"),
            [Role.Source] = Word("from"),
            [Role.Companion] = Word("with"),
            [Role.Instrument] = Word(instrumentalIsComitative ? "with" : "using"),
        };
    }

    //a role gets its case if the language has one, and otherwise its adposition
    public static Dictionary<Role, RoleMarker> SelectRoleMarkers(MorphosyntacticAlignment alignment, IReadOnlyList<Case> cases, WordOrder wordOrder, IReadOnlyDictionary<Role, Lexeme> adpositions)
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
        RoleMarker Adposition(Role role) => new Adposition(adpositions[role], type, governs);
        RoleMarker CaseOrAdposition(Case @case, Role role) => cases.Contains(@case) ? @case : Adposition(role);

        RoleMarker MarkerFor(Role role) => role switch
        {
            Role.IntransitiveSubject => Core(Case.Nominative, Case.Absolutive),
            Role.TransitiveSubject => Core(Case.Nominative, Case.Ergative),
            Role.DirectObject => Core(Case.Accusative, Case.Absolutive),
            Role.Possessor => CaseOrAdposition(Case.Genitive, role),
            Role.Recipient => CaseOrAdposition(Case.Dative, role),
            Role.Addressee => cases.Contains(Case.Vocative) ? Case.Vocative : new Unmarked(),
            Role.Location => CaseOrAdposition(Case.Locative, role),
            Role.Goal => Adposition(role),
            Role.Source => CaseOrAdposition(Case.Ablative, role),
            Role.Companion => Adposition(role),
            Role.Instrument => CaseOrAdposition(Case.Instrumental, role),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Every role needs a marker."),
        };

        return Enum.GetValues<Role>().ToDictionary(role => role, MarkerFor);
    }
}
