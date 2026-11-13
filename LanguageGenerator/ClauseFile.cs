using System.Reflection;

namespace LanguageGenerator;

/// <summary>
/// A file of clauses to translate, each the English it means followed by an indented line saying
/// what it means in glosses.
/// </summary>
//for example:
//
//  # a comment
//  the dog didn't see the cat
//    see.PST.PFV.NEG  subject:dog  object:cat
//  the gate is locked
//    be  subject:gate  adjective:locked
//
//The indented line is the verb, then its arguments in any order as role:noun, each word an English
//meaning with Leipzig glosses joined by periods, as in a gloss line. A pronoun is its person and
//number instead, as in subject:1SG, object:1PL.INCL or subject:3SG.F, and a noun with adjectives
//or a number is in parentheses, noun first, as in object:(dog.PL big old) or subject:(wolf 3 gray).
//A number is in digits, and gives the noun its number when the noun doesn't: two is dual and a few
//paucal. An ordinal is in digits too, with its English ending, as in subject:(day 3rd), and leaves
//the noun's number alone. A possessor goes in the parentheses too, as possessor:king or possessor:3SG.F, for 'the
//king's' or 'her', or a phrase of its own, as possessor:(sister possessor:1SG) for 'my sister's'. A noun is definite with DEF, as 'the dog' is dog.DEF, indefinite with NDF, as 'a
//dog' is dog.NDF, and bare with neither, as 'dogs', 'water' and 'my dog' are. The verb be says what its subject is, with noun:fool or adjective:locked in place of an
//object, or where it is, with location:house. Obliques say where, to where, from where, to whom,
//with whom and with what, as location:house, goal:river, source:(village old), recipient:dog,
//companion:1PL and instrument:knife, in the order they're said. A verb with IMP is a command, as in
//drink.IMP.NEG object:wine, with no tense or aspect and no subject but subject:2PL to tell several
//people, and be.IMP says what or where to be, as in be.IMP adjective:careful. be with an adjective
//can compare its subject with a standard: be subject:marta adjective:tall standard:(man.PL) is
//'Marta is taller than the men'. A verb with PASS is passive: its subject is what's done to, and
//agent: is who does it, if anyone says, as in carry.PASS subject:light.DEF agent:(figure.NDF small). A verb
//with ABIL, PERM, OBLIG or ADVS says its subject can, may, must or should, as in find.OBLIG subject:2SG, and NEG
//after one negates the modal, as swim.ABIL.NEG is 'can't swim', while NEG before one negates the
//verb, as sing.NEG.OBLIG is 'must not sing'. time: says when, as in time:night, and duration: for how
//long, as in duration:(year.PL 200). when: is a word saying when, as in when:now, often: one saying how
//often or whether still, as in often:again, and manner: an adjective saying how, as manner:careful is
//'carefully'; a clause can have several of
//each, in the order they're said. INTS on an adjective says 'very', as in adjective:tired.INTS. Any clause can have an addressee, who it's said to, a bare noun as in addressee:marta, and
//a command to a plural addressee is to all of them. Anything not given takes its unmarked value: present, imperfective, singular, exclusive for
//'we' and masculine for a gendered pronoun. Whatever the generator can't express yet is an error,
//never dropped
public static class ClauseFile
{
    public record Entry(string Translation, Statement Statement, int Line);

    public static IReadOnlyList<Entry> Parse(string text)
    {
        var entries = new List<Entry>();
        (string Text, int Line)? translation = null;
        //whether the last clause read can be joined by a line starting with a conjunction or a subordinator
        var continuing = false;
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var number = i + 1;
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            if (!char.IsWhiteSpace(line[0]))
            {
                if (translation is { } pending)
                {
                    throw new ClauseFormatException($"'{pending.Text}' needs an indented clause under it saying what it means.", pending.Line);
                }
                translation = (line.Trim(), number);
                continuing = false;
            }
            else if (ConjunctionOf(line) is not null || SubordinatorOf(line) is not null)
            {
                //a line starting with a conjunction joins its clause to the ones above it, and one
                //starting with a subordinator puts its clause under them
                if (!continuing)
                {
                    throw new ClauseFormatException($"A line starting with '{line.Trim().Split(' ')[0]}' joins a clause to the one above it, so it can't come first.", number);
                }
                var previous = entries[^1];
                var rest = line.Trim()[(line.Trim().IndexOf(' ') + 1)..];
                entries[^1] = previous with
                {
                    Statement = ConjunctionOf(line) is { } conjunction
                        ? ToCoordinated(conjunction, previous.Statement, ParseClause(rest, number, null), number)
                        : ToSubordinated(previous.Statement, SubordinatorOf(line).GetValueOrDefault(), rest, number),
                };
            }
            else if (translation is { } english)
            {
                entries.Add(new Entry(english.Text, ParseClause(line, number, null), number));
                translation = null;
                continuing = true;
            }
            else
            {
                throw new ClauseFormatException("A clause needs the English it translates on an unindented line above it.", number);
            }
        }
        if (translation is { } last)
        {
            throw new ClauseFormatException($"'{last.Text}' needs an indented clause under it saying what it means.", last.Line);
        }
        return entries;
    }

    //'and', 'or' and 'but', written as words of their own
    private static readonly Dictionary<string, Conjunction> Conjunctions = new()
    {
        ["and"] = Conjunction.And,
        ["or"] = Conjunction.Or,
        ["but"] = Conjunction.But,
    };

    //the conjunction an indented line starts with, if it starts with one
    private static Conjunction? ConjunctionOf(string line) =>
        line.Trim().Split(' ', 2) is [var first, _] && Conjunctions.TryGetValue(first, out var conjunction) ? conjunction : null;

    //the words that start a subordinate clause's line
    private static readonly Dictionary<string, Subordinator> Subordinators = Enum.GetValues<Subordinator>().ToDictionary(s => s.ToString().ToLowerInvariant());

    private static Subordinator? SubordinatorOf(string line) =>
        line.Trim().Split(' ', 2) is [var first, _] && Subordinators.TryGetValue(first, out var subordinator) ? subordinator : null;

    //a clause under the ones above it. Only a purpose clause can leave out its subject, which is then
    //the main clause's, as in 'to cross the river, you need a rope'. It can't be a command, or be said
    //to someone, since the main clause is
    private static Subordinated ToSubordinated(Statement main, Subordinator subordinator, string line, int number)
    {
        var shares = subordinator == Subordinator.Purpose && !Tokenize(line, number).Any(t => t.StartsWith("subject:"));
        var clause = ParseClause(line, number, shares ? Syntax.SubjectOf(main) : (Argument?)null);
        return clause.Value switch
        {
            Command or CopularCommand => throw new ClauseFormatException($"A clause under another can't be a command: {subordinator.ToString().ToLowerInvariant()} takes a clause with a subject.", number),
            Addressed => throw new ClauseFormatException("Say who's addressed in the main clause, not in one under it.", number),
            Question => throw new ClauseFormatException("A clause under another can't be a question: ask it in the main clause.", number),
            _ => new Subordinated(main, subordinator, clause, shares),
        };
    }

    //clauses joined by a conjunction, neither of them a question yet
    private static Coordinated ToCoordinated(Conjunction conjunction, Statement first, Statement second, int number) =>
        IsQuestion(first) || IsQuestion(second)
            ? throw new ClauseFormatException("A question can't be joined to another clause yet.", number)
            : new Coordinated(conjunction, first, second);

    private static bool IsQuestion(Statement statement) => statement.Value switch
    {
        Question => true,
        Addressed a => IsQuestion(a.Statement),
        Coordinated c => IsQuestion(c.First) || IsQuestion(c.Second),
        Subordinated s => IsQuestion(s.Main),
        _ => false,
    };

    //each noun these clauses use, with the English of the first clause to use it
    public static IReadOnlyList<(string Noun, string Translation)> Nouns(IEnumerable<Entry> entries) =>
        [.. entries.SelectMany(e => ArgumentsOf(e.Statement).SelectMany(NounsIn).Select(n => (Noun: n, e.Translation))).DistinctBy(n => n.Noun)];

    private static IEnumerable<Argument> ArgumentsOf(Statement statement) => statement switch
    {
        Clause c => [c.Subject, .. Given(c.Object), .. c.Obliques.Select(o => o.Argument)],
        PassiveClause c => [c.Patient, .. Given(c.Agent), .. c.Obliques.Select(o => o.Argument)],
        CopularClause { Complement: NounPhrase noun } c => [c.Subject, noun, .. c.Obliques.Select(o => o.Argument)],
        CopularClause { Complement: Oblique location } c => [c.Subject, location.Argument, .. c.Obliques.Select(o => o.Argument)],
        CopularClause c => [c.Subject, .. c.Obliques.Select(o => o.Argument)],
        Command c => [.. Given(c.Object), .. c.Obliques.Select(o => o.Argument)],
        CopularCommand { Complement: NounPhrase noun } c => [noun, .. c.Obliques.Select(o => o.Argument)],
        CopularCommand { Complement: Oblique location } c => [location.Argument, .. c.Obliques.Select(o => o.Argument)],
        CopularCommand c => [.. c.Obliques.Select(o => o.Argument)],
        Comparative c => [.. ArgumentsOf(c.Clause), .. Given(c.Standard)],
        Addressed a => [.. ArgumentsOf(a.Statement), a.Addressee],
        Coordinated c => [.. ArgumentsOf(c.First), .. ArgumentsOf(c.Second)],
        Subordinated s => [.. ArgumentsOf(s.Main), .. ArgumentsOf(s.Clause)],
        Modalized m => ArgumentsOf(m.Clause),
        Question q => ArgumentsOf(q.Statement),
    };

    private static IEnumerable<Argument> Given(Argument? argument) => argument.HasValue ? [argument.GetValueOrDefault()] : [];

    private static IEnumerable<string> NounsIn(Argument argument) => argument switch
    {
        NounPhrase n => [n.Noun, .. Given(n.Possessor).SelectMany(NounsIn)],
        Pronoun => [],
        QuestionWord => [],
        Coordination c => c.Items.SelectMany(NounsIn),
    };

    //the roles a clause can have. An oblique role is written as its name in lowercase, but for the
    //similative, which is like:. A noun or adjective is what be says its subject is, a standard what
    //an adjective compares it with, and an addressee who the clause is said to. An adverb fills no
    //role, but is written as one named for what it says, and a clause can have any number of them
    private static readonly Dictionary<string, Role> ObliqueRoles = Oblique.Roles.ToDictionary(r => r == Role.Similative ? "like" : r.ToString().ToLowerInvariant());
    private static readonly Dictionary<string, AdverbKind> AdverbRoles = Enum.GetValues<AdverbKind>().ToDictionary(k => k.ToString().ToLowerInvariant());
    private static readonly string[] Roles = ["subject", "object", "noun", "adjective", "standard", .. ObliqueRoles.Keys, "agent", "addressee", .. AdverbRoles.Keys, "manner"];

    //what the format can say, for describing it elsewhere, as the glosser's prompt does
    public static IReadOnlyList<string> RoleNames => Roles;
    public static IReadOnlyList<T> ValuesOf<T>() where T : Grammeme => [.. Glosses.Values.OfType<T>()];

    //a subject that isn't said is the given one, if there is one
    private static Statement ParseClause(string line, int number, Argument? impliedSubject)
    {
        var tokens = Tokenize(line, number);
        if (tokens[0].Contains(':'))
        {
            throw new ClauseFormatException($"A clause starts with its verb, not with '{tokens[0]}'.", number);
        }
        var (verb, glosses) = ParseWord(tokens[0], number);
        var features = ParseVerbFeatures(verb, glosses, number);
        //why fills no role, so it's written on its own
        var why = tokens.Skip(1).Count(t => t == "why") switch
        {
            0 => false,
            1 => true,
            _ => throw new ClauseFormatException("The clause has more than one why.", number),
        };
        var roles = ParseRoles(verb, tokens.Skip(1).Where(t => t != "why"), number);
        var addressee = roles.Arguments.TryGetValue("addressee", out var called) ? ToAddressee(called, number) : null;
        Argument? standard = roles.Arguments.TryGetValue("standard", out var than) ? than : (Argument?)null;
        if (roles.Arguments.ContainsKey("agent") && !features.Passive)
        {
            throw new ClauseFormatException($"Only a passive verb takes an agent:, as in {verb}.PASS subject:light agent:figure for 'the light is {verb}ed by a figure'.", number);
        }
        if (features.Passive && (features.Imperative || verb == "be"))
        {
            throw new ClauseFormatException(verb == "be" ? "be can't be passive." : "A command can't be passive yet.", number);
        }
        if (standard.HasValue && (verb != "be" || features.Imperative))
        {
            throw new ClauseFormatException(features.Imperative
                ? "A command can't compare yet."
                : $"Only be takes a standard:, as in be subject:marta adjective:tall.CMPR standard:(man.PL) for 'Marta is taller than the men'.", number);
        }
        if (roles.Compared && features.Imperative)
        {
            throw new ClauseFormatException("A command can't compare yet.", number);
        }
        if (standard.HasValue && !roles.Compared && roles.Complement is { Value: Adjective adjective })
        {
            throw new ClauseFormatException($"A standard: compares a comparative adjective:, as in adjective:{adjective.Meaning}.CMPR for 'more {adjective.Meaning} than'.", number);
        }
        if (features.Modality is { } modality && (features.Imperative || roles.Compared || standard.HasValue))
        {
            throw new ClauseFormatException(features.Imperative
                ? $"A command can't take {modality.Gloss}: it already says what to do."
                : $"A comparison can't take {modality.Gloss} yet.", number);
        }
        var asked = CheckQuestion(features, roles, why, number);

        Statement statement;
        if (features.Imperative)
        {
            statement = ToCommand(verb, features, roles, addressee, number);
        }
        else if ((roles.Arguments.TryGetValue("subject", out var said) ? said : impliedSubject) is not { } subject)
        {
            throw new ClauseFormatException("The clause needs a subject, as in subject:dog.", number);
        }
        else if (verb == "be")
        {
            var clause = ToCopularClause(features, roles, subject, number);
            statement = roles.Compared || standard.HasValue ? ToComparative(clause, standard, number) : clause;
        }
        else if (features.Passive)
        {
            statement = ToPassiveClause(verb, features, roles, subject, number);
        }
        else
        {
            statement = ToClause(verb, features, roles, subject, number);
        }
        if (features.Modality is { } modal)
        {
            statement = new Modalized(statement, modal, features.ModalNegative);
        }
        if (asked)
        {
            statement = new Question(statement, why);
        }
        return addressee is null ? statement : new Addressed(statement, addressee);
    }

    //the question words, in the roles they ask about
    private static readonly Dictionary<string, Interrogative> QuestionWords = Enum.GetValues<Interrogative>().ToDictionary(w => w.ToString().ToLowerInvariant());

    //whether the clause is a question: a yes-no question with Q, or a content question with one
    //question word in a role or why. Where asks about a place, so it's only where, to where or from
    //where, and a question word can't be a standard yet
    private static bool CheckQuestion(VerbFeatures features, ClauseRoles roles, bool why, int number)
    {
        var questioned = roles.Arguments.Select(a => (a.Key, a.Value)).Concat(roles.Obliques.Select(o => (Key: o.Role, o.Oblique.Argument)))
            .Where(a => a.Item2.Value is QuestionWord).ToList();
        if (!features.Question && !why && questioned.Count == 0)
        {
            return false;
        }
        if (features.Imperative)
        {
            throw new ClauseFormatException("A command can't be a question.", number);
        }
        if (questioned.Count + (why ? 1 : 0) > 1)
        {
            throw new ClauseFormatException("A question can only ask one thing yet: one who, what, where or why.", number);
        }
        if (features.Question && (why || questioned.Count > 0))
        {
            throw new ClauseFormatException($"A question with {(why ? "why" : WordOf(questioned[0].Item2))} needs no Q: Q is only for a yes-no question.", number);
        }
        if (questioned is [var (role, argument)])
        {
            var word = ((QuestionWord)argument.Value!).Word;
            if (role == "standard")
            {
                throw new ClauseFormatException("A comparison can't ask about its standard yet.", number);
            }
            if (role == "like")
            {
                throw new ClauseFormatException($"A question can't ask what something is like yet, as in like:{WordOf(argument)}.", number);
            }
            if (role is "time" or "duration")
            {
                throw new ClauseFormatException($"A question can't ask when or for how long yet, as in {role}:{WordOf(argument)}.", number);
            }
            if (word == Interrogative.Where && role is not ("location" or "goal" or "source"))
            {
                throw new ClauseFormatException($"where asks about a place, so it's location:where, goal:where for 'where to' or source:where for 'where from', not {role}:where.", number);
            }
            if (roles.Obliques.Any(o => o.Oblique.Argument.Value is QuestionWord && o.Oblique.Configuration != Configuration.At))
            {
                throw new ClauseFormatException($"A question word can't take a configuration yet, as in 'under what?'.", number);
            }
        }
        return true;

        static string WordOf(Argument argument) => ((QuestionWord)argument.Value!).Word.ToString().ToLowerInvariant();
    }

    //'Marta is taller than the men': an adjective, said of the subject more than of the standard, or
    //with no standard, 'Marta is taller', more than of something already known
    private static Comparative ToComparative(CopularClause clause, Argument? standard, int number)
    {
        if (clause.Complement.Value is not Adjective)
        {
            throw new ClauseFormatException("A standard: compares an adjective:, as in adjective:tall.CMPR standard:(man.PL) for 'taller than the men'.", number);
        }
        if (clause.Negative)
        {
            throw new ClauseFormatException("A comparison can't be negated yet.", number);
        }
        return new Comparative(clause, standard);
    }

    //who a clause is said to: a noun phrase, bare, since vocatives take no article
    private static NounPhrase ToAddressee(Argument argument, int number) => argument.Value switch
    {
        NounPhrase { Definiteness: { } } noun => throw new ClauseFormatException($"An addressee takes no article: write addressee:{noun.Noun}, not {noun.Definiteness.Gloss}.", number),
        NounPhrase noun => noun,
        _ => throw new ClauseFormatException("An addressee is a noun, as in addressee:marta. A clause said to 'you' needs none.", number),
    };

    //Negative negates the verb, and ModalNegative the modal
    private record VerbFeatures(Tense? Tense, Aspect? Aspect, bool Negative, bool Imperative, bool Question, bool Passive, Modality? Modality, bool ModalNegative);

    //the obliques keep the role they were written with, which is how be finds its location:.
    //Compared is whether the adjective: is comparative, as tall.CMPR is
    private record ClauseRoles(Dictionary<string, Argument> Arguments, List<(string Role, Oblique Oblique)> Obliques, Complement? Complement, bool Compared, List<Adverbial> Adverbs)
    {
        public Argument? Object => Arguments.TryGetValue("object", out var argument) ? argument : (Argument?)null;
        public ValueList<Oblique> AllObliques => [.. Obliques.Select(o => o.Oblique)];
    }

    private static VerbFeatures ParseVerbFeatures(string verb, IReadOnlyList<Grammeme> glosses, int number)
    {
        if (glosses.FirstOrDefault(g => g is not (Tense or Aspect or Polarity or Modality) && g != Mood.Imperative && g != Mood.Interrogative && g != Voice.Passive) is { } unsupported)
        {
            throw Unsupported(verb, unsupported, number);
        }
        if (glosses.GroupBy(g => g.GetType()).FirstOrDefault(g => g.Count() > 1) is { } repeated)
        {
            throw new ClauseFormatException($"'{verb}' has more than one {repeated.Key.Name.ToLowerInvariant()}.", number);
        }
        //a negation after the modal negates the modal, as glosses put a morpheme outside what it
        //scopes over: swim.ABIL.NEG is 'can't swim', and sing.NEG.OBLIG 'must not sing'
        var modality = glosses.OfType<Modality>().SingleOrDefault();
        var negative = glosses.Contains(Polarity.Negative);
        var order = glosses.ToList();
        var modalNegative = negative && modality is not null && order.IndexOf(Polarity.Negative) > order.IndexOf(modality);
        return new VerbFeatures(
            glosses.OfType<Tense>().SingleOrDefault(),
            glosses.OfType<Aspect>().SingleOrDefault(),
            negative && !modalNegative,
            glosses.Contains(Mood.Imperative),
            glosses.Contains(Mood.Interrogative),
            glosses.Contains(Voice.Passive),
            modality,
            modalNegative);
    }

    private static ClauseFormatException Unsupported(string verb, Grammeme feature, int number) => feature switch
    {
        Mood m when m == Mood.Prohibitive => new($"Write a command not to do something as {verb}.IMP.NEG, not {verb}.{m.Gloss}.", number),
        Person or Number => new($"'{verb}' can't take {feature.Gloss}: a verb agrees with its subject.", number),
        _ => new($"'{verb}' can't take {feature.Gloss} ({feature.Name}) yet: verbs have only tense, aspect, polarity, the imperative, Q for a question, the passive and the modals {string.Join(", ", ValuesOf<Modality>().Select(m => m.Gloss))}.", number),
    };

    private static ClauseRoles ParseRoles(string verb, IEnumerable<string> tokens, int number)
    {
        var arguments = new Dictionary<string, Argument>();
        var obliques = new List<(string Role, Oblique Oblique)>();
        var adverbs = new List<Adverbial>();
        Complement? complement = null;
        var compared = false;
        foreach (var token in tokens)
        {
            var colon = token.IndexOf(':');
            if (colon < 0)
            {
                throw new ClauseFormatException($"'{token}' needs a role, as in subject:{token}.", number);
            }
            var (role, configuration) = ParseConfiguration(token[..colon], number);
            if (role == "adverb")
            {
                throw new ClauseFormatException($"Say what an adverb says: when:{token[(colon + 1)..]} for when, often:{token[(colon + 1)..]} for how often or whether still, or manner: with an adjective for how, as manner:careful is 'carefully'.", number);
            }
            if (role == "possessor")
            {
                throw new ClauseFormatException($"A possessor goes inside its noun's parentheses, as in object:(house possessor:{token[(colon + 1)..]}).", number);
            }
            if (!Roles.Contains(role))
            {
                throw new ClauseFormatException($"'{role}' isn't a role. The roles are {string.Join(", ", Roles)}.", number);
            }
            if (arguments.ContainsKey(role) || obliques.Any(o => o.Role == role) || role is "noun" or "adjective" && complement.HasValue)
            {
                throw new ClauseFormatException(role is "noun" or "adjective" ? "The clause has more than one noun or adjective." : $"The clause has more than one {role}.", number);
            }
            if (verb == "be" && role is "noun" or "adjective" or "location" && (complement.HasValue || obliques.Any(o => o.Role == "location")))
            {
                throw new ClauseFormatException("be says one thing of its subject: a noun:, an adjective: or a location:.", number);
            }
            var value = token[(colon + 1)..];
            switch (role)
            {
                case var name when AdverbRoles.TryGetValue(name, out var kind):
                    adverbs.Add(ParseAdverb(value, kind, number));
                    break;
                case "manner":
                    adverbs.Add(ParseManner(value, number));
                    break;
                case "noun":
                    complement = ParseArgument(value, number).Value switch
                    {
                        NounPhrase noun => noun,
                        QuestionWord => throw new ClauseFormatException($"be can't ask what its subject is yet, as in noun:{value} for '{value} are you?'.", number),
                        _ => throw new ClauseFormatException($"be can't say its subject is {value} yet: the noun has to be a noun, not a pronoun.", number),
                    };
                    break;
                case "adjective" when value.StartsWith('('):
                    complement = ParseAdjectiveCoordination(value, number);
                    break;
                case "adjective":
                    //only what be says of its subject can be comparative, so CMPR is taken off here
                    //before the adjective is read
                    compared = value.Split('.').Skip(1).Contains(Degree.Comparative.Gloss);
                    complement = ParseAdjective(compared ? string.Join('.', value.Split('.').Where(p => p != Degree.Comparative.Gloss)) : value, number);
                    break;
                case var name when ObliqueRoles.TryGetValue(name, out var oblique):
                    if (configuration != Configuration.At && !Oblique.Spatial.Contains(oblique))
                    {
                        throw new ClauseFormatException($"Only a role saying where can say in, on or under, as location., goal., source. and route. do, not {role}.", number);
                    }
                    obliques.Add((role, new Oblique(oblique, ParseArgument(value, number), configuration)));
                    break;
                case var _ when configuration != Configuration.At:
                    throw new ClauseFormatException($"Only a role saying where can say in, on or under, as location., goal., source. and route. do, not {role}.", number);
                default:
                    arguments[role] = ParseArgument(value, number);
                    break;
            }
        }
        return new ClauseRoles(arguments, obliques, complement, compared, adverbs);
    }

    //where relative to a place a spatial role says, written after the role with a period, as in
    //goal.in:river for 'into the river'. A role with none is At, which is never written
    private static readonly Dictionary<string, Configuration> Configurations = Enum.GetValues<Configuration>()
        .Where(c => c != Configuration.At)
        .ToDictionary(c => c == Configuration.InFront ? "front" : c.ToString().ToLowerInvariant());

    public static IReadOnlyList<string> ConfigurationNames => [.. Configurations.Keys];

    private static (string Role, Configuration Configuration) ParseConfiguration(string written, int number)
    {
        var parts = written.Split('.');
        return parts switch
        {
            [var role] => (role, Configuration.At),
            [var role, "at"] => throw new ClauseFormatException($"{role}:, with nothing after it, already means at, so {role}.at: is just {role}:.", number),
            [var role, var name] when Configurations.TryGetValue(name, out var configuration) => (role, configuration),
            [var role, var name] => throw new ClauseFormatException($"'{name}' isn't a configuration. After {role}. can come {string.Join(", ", Configurations.Keys)}.", number),
            _ => throw new ClauseFormatException($"A role takes one configuration, as in goal.in: for 'into', not {written}.", number),
        };
    }

    //a command is said to one person or several: as many as its subject says, or as its addressee
    //is, as 'children, come' tells several. A command with be says what or where to be
    private static Statement ToCommand(string verb, VerbFeatures features, ClauseRoles roles, NounPhrase? addressee, int number)
    {
        if (features.Tense is not null || features.Aspect is not null)
        {
            throw new ClauseFormatException($"'{verb}' can't take {(features.Tense as Grammeme ?? features.Aspect)!.Gloss}: a command has no tense or aspect.", number);
        }
        var told = addressee is { Number: var called } && called != Number.Singular ? Number.Plural : Number.Singular;
        if (roles.Arguments.TryGetValue("subject", out var subject))
        {
            told = subject.Value is Pronoun { Person: var person, Number: var count } && person == Person.Second ? count
                : throw new ClauseFormatException("A command is said to 'you', so its subject can only be 2SG or 2PL, as in subject:2PL.", number);
        }
        if (verb == "be")
        {
            return new CopularCommand(CopularComplement(roles, number), told, features.Negative) { Obliques = CopularObliques(roles), Adverbs = [.. roles.Adverbs] };
        }
        RejectComplement(verb, roles, number);
        return new Command(verb, told, roles.Object, features.Negative, roles.AllObliques) { Adverbs = [.. roles.Adverbs] };
    }

    private static CopularClause ToCopularClause(VerbFeatures features, ClauseRoles roles, Argument subject, int number) =>
        new(CopularComplement(roles, number), features.Tense ?? Tense.Present, features.Aspect ?? Aspect.Imperfective, subject, features.Negative)
        {
            Obliques = CopularObliques(roles),
            Adverbs = [.. roles.Adverbs],
        };

    //the only obliques be takes besides a location: when, and for how long
    private static readonly string[] CopularRoles = ["time", "duration"];

    private static ValueList<Oblique> CopularObliques(ClauseRoles roles) => [.. roles.Obliques.Where(o => CopularRoles.Contains(o.Role)).Select(o => o.Oblique)];

    //what be says its subject is, or where it is
    private static Complement CopularComplement(ClauseRoles roles, int number)
    {
        if (roles.Arguments.ContainsKey("object"))
        {
            throw new ClauseFormatException("be has no object. Say what its subject is with noun: or adjective:, as in noun:fool.", number);
        }
        //where its subject is, as in location:house
        var complement = roles.Complement;
        if (roles.Obliques.Find(o => o.Role == "location") is { Oblique: { } location })
        {
            complement = location;
        }
        if (roles.Obliques.Any(o => o.Role == "like"))
        {
            throw new ClauseFormatException("be can't say what its subject is like yet: like: goes with other verbs, as in shine subject:3SG.N like:water.", number);
        }
        if (roles.Obliques.Find(o => o.Role != "location" && !CopularRoles.Contains(o.Role)) is { Role: { } other })
        {
            throw new ClauseFormatException($"be can't take {A(other)} yet, only a noun:, adjective: or location: saying what or where its subject is, and time: or duration:.", number);
        }
        if (!complement.HasValue)
        {
            throw new ClauseFormatException("be needs a noun, adjective or location saying what or where its subject is, as in noun:fool, adjective:big or location:house.", number);
        }
        return complement.GetValueOrDefault();
    }

    //a passive's subject is its patient, so it has no object
    private static PassiveClause ToPassiveClause(string verb, VerbFeatures features, ClauseRoles roles, Argument subject, int number)
    {
        RejectComplement(verb, roles, number);
        if (roles.Object.HasValue)
        {
            throw new ClauseFormatException($"A passive has no object: what's done to is its subject, as in {verb}.PASS subject:light.", number);
        }
        var agent = roles.Arguments.TryGetValue("agent", out var by) ? by : (Argument?)null;
        return new PassiveClause(verb, features.Tense ?? Tense.Present, features.Aspect ?? Aspect.Imperfective, subject, agent, features.Negative, roles.AllObliques)
        {
            Adverbs = [.. roles.Adverbs],
        };
    }

    private static Clause ToClause(string verb, VerbFeatures features, ClauseRoles roles, Argument subject, int number)
    {
        RejectComplement(verb, roles, number);
        return new Clause(verb, features.Tense ?? Tense.Present, features.Aspect ?? Aspect.Imperfective, subject, roles.Object,
            Negative: features.Negative, Obliques: roles.AllObliques)
        {
            Adverbs = [.. roles.Adverbs],
        };
    }

    private static void RejectComplement(string verb, ClauseRoles roles, int number)
    {
        if (roles.Complement.HasValue)
        {
            throw new ClauseFormatException($"Only be takes a noun: or adjective:. '{verb}' takes an object:.", number);
        }
    }

    //a noun and its adjectives, in parentheses, as in (dog.PL big old). A possessor can be a noun
    //phrase in parentheses of its own, as in (son possessor:(sister possessor:1SG)) for 'my sister's
    //son', and nothing else can
    private static NounPhrase ParseModifiedNounPhrase(string token, int number)
    {
        if (!token.EndsWith(')'))
        {
            throw new ClauseFormatException($"'{token}' has to end at its ), as in (dog big).", number);
        }
        var words = Tokenize(token[1..^1], number);
        if (words.Find(w => w.Contains('(') && !w.StartsWith("possessor:(")) is { } nested)
        {
            throw new ClauseFormatException($"Only a possessor can be a noun phrase of its own inside parentheses, as in (son possessor:(sister possessor:1SG)), not '{nested}'.", number);
        }
        if (words.Count == 0)
        {
            throw new ClauseFormatException("A noun phrase in parentheses needs a noun.", number);
        }
        if (ParsePronoun(words[0], number) is not null)
        {
            throw new ClauseFormatException($"{words[0]} can't take adjectives: pronouns don't, here.", number);
        }
        foreach (var word in words.Skip(1).Where(w => w.Contains(':') && !w.StartsWith("possessor:")))
        {
            throw new ClauseFormatException($"Noun phrases can't have {A(word[..word.IndexOf(':')])} yet, only adjectives, a number, an ordinal and a possessor.", number);
        }
        var noun = ParseArgument(words[0], number).Value as NounPhrase
            ?? throw new ClauseFormatException($"{words[0]} can't take adjectives, a number or a possessor yet.", number);

        int? count = null;
        int? ordinal = null;
        Argument? possessor = null;
        var adjectives = new List<Adjective>();
        foreach (var word in words.Skip(1))
        {
            if (word.StartsWith("possessor:"))
            {
                if (possessor.HasValue)
                {
                    throw new ClauseFormatException($"'{words[0]}' has more than one possessor.", number);
                }
                possessor = ParseArgument(word["possessor:".Length..], number);
                if (possessor.GetValueOrDefault().Value is QuestionWord)
                {
                    throw new ClauseFormatException("A possessor can't be a question word yet, as in 'whose house'.", number);
                }
            }
            else if (word.All(char.IsAsciiDigit))
            {
                if (count is not null)
                {
                    throw new ClauseFormatException($"'{words[0]}' has more than one number.", number);
                }
                count = int.Parse(word);
                if (count is < 1 or > NumeralSystem.Largest)
                {
                    throw new ClauseFormatException($"Numbers go from 1 to {NumeralSystem.Largest} for now, not {word}.", number);
                }
            }
            else if (word.Length > 2 && word[..^2].All(char.IsAsciiDigit) && word[^2..] is "st" or "nd" or "rd" or "th")
            {
                if (ordinal is not null)
                {
                    throw new ClauseFormatException($"'{words[0]}' has more than one ordinal.", number);
                }
                ordinal = int.Parse(word[..^2]);
                if (ordinal is < 1 or > NumeralSystem.Largest)
                {
                    throw new ClauseFormatException($"Ordinals go from 1st to {NumeralSystem.Largest}th for now, not {word}.", number);
                }
            }
            else if (Conjunctions.ContainsKey(word))
            {
                throw new ClauseFormatException($"Write {word} first, before what it joins, as in ({word} dog.DEF cat.DEF), not '({string.Join(' ', words)})'.", number);
            }
            else if (NumberWords.Contains(word.Split('.')[0]))
            {
                throw new ClauseFormatException($"Write numbers in digits, as in ({words[0]} 3), not '{word}'.", number);
            }
            else if (OrdinalWords.Contains(word.Split('.')[0]))
            {
                throw new ClauseFormatException($"Write ordinals in digits, as in ({words[0]} 3rd), not '{word}'.", number);
            }
            else
            {
                adjectives.Add(ParseAdjective(word, number));
            }
        }
        //a number gives the noun its number unless the noun has one already
        var given = ParseWord(words[0], number).Features.OfType<Number>().Any();
        return noun with
        {
            Adjectives = [.. adjectives],
            Count = count,
            Ordinal = ordinal,
            Possessor = possessor,
            Number = given || count is not { } n ? noun.Number : NumberFor(n),
        };
    }

    //one is singular, two dual, a few paucal and more plural, each falling back as the language needs
    public static Number NumberFor(int count) => count switch
    {
        1 => Number.Singular,
        2 => Number.Dual,
        <= 5 => Number.Paucal,
        _ => Number.Plural,
    };

    //English number words, which a numeral should be written in digits instead of
    private static readonly HashSet<string> NumberWords =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve",
        "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty", "thirty",
        "forty", "fifty", "sixty", "seventy", "eighty", "ninety", "hundred", "thousand", "dozen",
    ];

    //English ordinals, which should be written in digits too. Second isn't here, since it's also a
    //noun, and last isn't, since it's no ordinal
    private static readonly HashSet<string> OrdinalWords =
    [
        "first", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth", "eleventh", "twelfth",
        "thirteenth", "fourteenth", "fifteenth", "sixteenth", "seventeenth", "eighteenth", "nineteenth", "twentieth",
        "thirtieth", "fortieth", "fiftieth", "sixtieth", "seventieth", "eightieth", "ninetieth", "hundredth",
    ];

    //an adjective, which has no features of its own yet
    //a coordination in parentheses, conjunction first, as in (and dog.DEF cat.DEF): the conjunction and
    //the two or more things it joins, or null if the parentheses hold something else
    private static (Conjunction Conjunction, List<string> Items)? Coordinating(string token, int number)
    {
        if (!token.EndsWith(')'))
        {
            return null;
        }
        var words = Tokenize(token[1..^1], number);
        if (words.Count == 0 || !Conjunctions.TryGetValue(words[0], out var conjunction))
        {
            return null;
        }
        if (words.Count < 3)
        {
            throw new ClauseFormatException($"'{token}' joins too few things: {words[0]} needs two or more, as in ({words[0]} dog.DEF cat.DEF).", number);
        }
        return (conjunction, words[1..]);
    }

    //adjectives said together, as in adjective:(and warm crowded)
    private static AdjectiveCoordination ParseAdjectiveCoordination(string token, int number)
    {
        if (Coordinating(token, number) is not var (conjunction, items) || conjunction == Conjunction.But)
        {
            throw new ClauseFormatException($"Adjectives in parentheses are joined with and or or first, as in adjective:(and warm crowded), not '{token}'.", number);
        }
        return new AdjectiveCoordination(conjunction, [.. items.Select(item => ParseAdjective(item, number))]);
    }

    //an adjective, maybe with INTS for 'very'
    private static Adjective ParseAdjective(string token, int number)
    {
        var (meaning, features) = ParseWord(token, number);
        if (QuestionWords.ContainsKey(meaning) || meaning == "why")
        {
            throw new ClauseFormatException($"{meaning} isn't an adjective: a question word goes in the role it asks about, as in subject:who.", number);
        }
        if (features.Contains(Degree.Comparative))
        {
            throw new ClauseFormatException($"Only the adjective: be says of its subject can take CMPR yet, as in be subject:3SG adjective:{meaning}.CMPR, not one in a noun phrase or joined to another.", number);
        }
        if (features.Contains(Derivation.Adverbial))
        {
            throw new ClauseFormatException($"'{meaning}' can't take ADV: an adverb made from an adjective is written manner:{meaning}, as in climb subject:2SG manner:careful for 'you climb carefully'.", number);
        }
        if (features.Count(f => f == Degree.Intensive) > 1)
        {
            throw new ClauseFormatException($"'{meaning}' has more than one INTS.", number);
        }
        if (features.Find(f => f != Degree.Intensive) is { } other)
        {
            throw new ClauseFormatException($"'{meaning}' can't take {other.Gloss}: adjectives have no features but CMPR and INTS yet.", number);
        }
        return new Adjective(meaning, features.Contains(Degree.Intensive));
    }

    //how something is done, as an adjective: manner:careful is 'carefully', and manner:slow.INTS
    //'very slowly'
    private static Manner ParseManner(string token, int number)
    {
        if (token.StartsWith('('))
        {
            throw new ClauseFormatException($"manner: takes one adjective, as in manner:careful, not '{token}'. Write manner: again for another.", number);
        }
        if (token.Split('.').Skip(1).Contains(Degree.Comparative.Gloss))
        {
            throw new ClauseFormatException("A manner can't compare yet, as in 'more slowly'.", number);
        }
        return new Manner(ParseAdjective(token, number));
    }

    //words for where wait on demonstratives and spatial adpositions, which most languages make them from
    private static readonly HashSet<string> PlaceAdverbs =
        ["here", "there", "inside", "outside", "above", "below", "upstairs", "downstairs", "everywhere", "somewhere", "nowhere", "away", "upstream", "downstream"];

    //a word of its own saying when or how often, with no glosses
    private static Adverb ParseAdverb(string token, AdverbKind kind, int number)
    {
        var role = kind.ToString().ToLowerInvariant();
        if (token.StartsWith('('))
        {
            throw new ClauseFormatException($"An adverb is one word, as in {role}:{(kind == AdverbKind.When ? "now" : "again")}, not '{token}'. Write {role}: again for another.", number);
        }
        var (meaning, features) = ParseWord(token, number);
        if (meaning == "why" || QuestionWords.ContainsKey(meaning))
        {
            throw new ClauseFormatException(meaning == "why"
                ? "why fills no role, so it's written on its own, as in leave.PST subject:3SG why."
                : $"{meaning} isn't an adverb: a question word goes in the role it asks about, as in subject:who.", number);
        }
        if (meaning is "when" or "how")
        {
            throw new ClauseFormatException($"A question can't ask {meaning} yet.", number);
        }
        if (meaning is "very" or "much")
        {
            throw new ClauseFormatException($"{meaning} goes on an adjective as INTS, as in adjective:tired.INTS or manner:slow.INTS.", number);
        }
        if (meaning == "not")
        {
            throw new ClauseFormatException("not is NEG on the verb, as in see.NEG.", number);
        }
        if (PlaceAdverbs.Contains(meaning))
        {
            throw new ClauseFormatException($"'{meaning}' can't be said yet: words for where, like here and there, wait on demonstratives and spatial adpositions.", number);
        }
        if (features.Count > 0)
        {
            throw new ClauseFormatException($"'{meaning}' can't take {features[0].Gloss}: an adverb takes no glosses. For 'very slowly', write manner:slow.INTS.", number);
        }
        return new Adverb(meaning, kind);
    }

    //the words of a clause, split on spaces outside parentheses, so that object:(dog big) is one
    private static List<string> Tokenize(string line, int number)
    {
        var tokens = new List<string>();
        var token = new System.Text.StringBuilder();
        var depth = 0;
        foreach (var c in line)
        {
            if (char.IsWhiteSpace(c) && depth == 0)
            {
                if (token.Length > 0)
                {
                    tokens.Add(token.ToString());
                    token.Clear();
                }
                continue;
            }
            depth += c switch { '(' => 1, ')' => -1, _ => 0 };
            if (depth < 0)
            {
                throw new ClauseFormatException("There's a ) without a ( before it.", number);
            }
            token.Append(c);
        }
        if (depth > 0)
        {
            throw new ClauseFormatException("There's a ( that isn't closed.", number);
        }
        if (token.Length > 0)
        {
            tokens.Add(token.ToString());
        }
        return tokens;
    }

    private static Argument ParseArgument(string token, int number)
    {
        if (token.StartsWith('(') && Coordinating(token, number) is var (conjunction, items))
        {
            if (conjunction == Conjunction.But)
            {
                throw new ClauseFormatException("but joins only clauses, on a line of its own starting with but.", number);
            }
            var joined = items.Select(item => ParseArgument(item, number)).ToList();
            if (joined.Any(a => a.Value is QuestionWord))
            {
                throw new ClauseFormatException($"A question word can't be joined to anything with {conjunction.ToString().ToLowerInvariant()} yet.", number);
            }
            return new Coordination(conjunction, [.. joined]);
        }
        if (token.StartsWith('('))
        {
            return ParseModifiedNounPhrase(token, number);
        }
        if (ParsePronoun(token, number) is { } pronoun)
        {
            return pronoun;
        }
        var (noun, features) = ParseWord(token, number);
        if (noun == "why")
        {
            throw new ClauseFormatException("why fills no role, so it's written on its own, as in leave.PST subject:3SG why.", number);
        }
        if (QuestionWords.TryGetValue(noun, out var asked))
        {
            return features.Count == 0 ? new QuestionWord(asked)
                : throw new ClauseFormatException($"{noun} can't take {features[0].Gloss}: a question word takes no glosses.", number);
        }
        Number? count = null;
        Definiteness? definiteness = null;
        foreach (var feature in features)
        {
            switch (feature)
            {
                case Number n when count is null:
                    count = n;
                    break;
                case Number:
                    throw new ClauseFormatException($"'{noun}' has more than one number.", number);
                case Definiteness d when definiteness is null:
                    definiteness = d;
                    break;
                case Definiteness:
                    throw new ClauseFormatException($"'{noun}' is either DEF or NDF, not both.", number);
                case Case:
                    throw new ClauseFormatException($"'{noun}' can't take {feature.Gloss}: a noun's case comes from its role.", number);
                case Polarity:
                    throw new ClauseFormatException($"'{noun}' can't take {feature.Gloss}: it's the verb that's negated.", number);
                default:
                    throw new ClauseFormatException($"'{noun}' can't take {feature.Gloss} ({feature.Name}) yet: nouns have only number and definiteness.", number);
            }
        }
        return new NounPhrase(noun, count ?? Number.Singular, Definiteness: definiteness);
    }

    //a person and number, as in 3PL, and maybe a clusivity after them. Null if it isn't a pronoun
    private static Pronoun? ParsePronoun(string token, int number)
    {
        var parts = token.Split('.');
        if (parts[0].Length < 2 || !Glosses.TryGetValue(parts[0][..1], out var p) || p is not Person person
            || !Glosses.TryGetValue(parts[0][1..], out var n) || n is not Number count)
        {
            return null;
        }

        Clusivity? clusivity = null;
        Gender? gender = null;
        foreach (var gloss in parts.Skip(1))
        {
            var feature = Glosses.TryGetValue(gloss, out var grammeme) ? grammeme
                : throw new ClauseFormatException($"'{gloss}' isn't a gloss.{Suggestion(gloss)}", number);
            switch (feature)
            {
                case Clusivity when person != Person.First || count == Number.Singular:
                    throw new ClauseFormatException($"{parts[0]} can't take {feature.Gloss}: only 'we' includes or excludes the hearer.", number);
                case Clusivity c when clusivity is null:
                    clusivity = c;
                    break;
                case Clusivity:
                    throw new ClauseFormatException($"{parts[0]} has more than one clusivity.", number);
                //whether the language's pronouns have a gender for this person is up to the language
                case Gender g when g != Gender.Masculine && g != Gender.Feminine && g != Gender.Neuter:
                    throw new ClauseFormatException($"{parts[0]} can't take {g.Gloss}: a pronoun's gender is masculine, feminine or neuter.", number);
                case Gender g when gender is null:
                    gender = g;
                    break;
                case Gender:
                    throw new ClauseFormatException($"{parts[0]} has more than one gender.", number);
                case Case:
                    throw new ClauseFormatException($"{parts[0]} can't take {feature.Gloss}: a pronoun's case comes from its role.", number);
                default:
                    throw new ClauseFormatException($"{parts[0]} can't take {feature.Gloss} ({feature.Name}) yet: pronouns have only person, number, clusivity and gender.", number);
            }
        }
        return new Pronoun(person, count, clusivity, gender);
    }

    //a role with its article, as in 'an instrument'
    private static string A(string role) => ("aeiou".Contains(role[0]) ? "an " : "a ") + role;

    //a meaning and the glosses after it, as in dog.PL. Case doesn't matter in a meaning, so Marta and
    //marta are one word
    private static (string Meaning, List<Grammeme> Features) ParseWord(string token, int number)
    {
        var parts = token.Split('.');
        var meaning = parts[0].ToLowerInvariant();
        if (meaning.Length == 0)
        {
            throw new ClauseFormatException($"'{token}' needs a word before its glosses.", number);
        }
        return (meaning, [.. parts.Skip(1).Select(gloss => Glosses.TryGetValue(gloss, out var grammeme) ? grammeme
            : throw new ClauseFormatException($"'{gloss}' isn't a gloss.{Suggestion(gloss)}", number))]);
    }

    //every value of every category, by its gloss
    private static readonly Dictionary<string, Grammeme> Glosses = typeof(Grammeme).Assembly.GetTypes()
        .Where(t => t.BaseType == typeof(Grammeme))
        .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == t))
        .Select(p => (Grammeme)p.GetValue(null)!)
        .ToDictionary(t => t.Gloss);

    //a guess at the gloss meant, from its name: PAST or past for PST, PERF for PFV
    private static string Suggestion(string gloss)
    {
        var meant = Glosses.Values.FirstOrDefault(t => t.Name.StartsWith(gloss, StringComparison.OrdinalIgnoreCase))
            ?? Glosses.Values.FirstOrDefault(t => t.Gloss.Equals(gloss, StringComparison.OrdinalIgnoreCase));
        return meant is null ? "" : $" Did you mean {meant.Gloss}, for {meant.Name.ToLowerInvariant()}?";
    }
}

public class ClauseFormatException(string message, int line) : FormatException($"Line {line}: {message}")
{
    public int Line { get; } = line;
}
