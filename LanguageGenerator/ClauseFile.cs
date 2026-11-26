using System.Reflection;

namespace LanguageGenerator;

/// <summary>
/// A file of clauses to translate, each the English it means followed by an indented line saying
/// what it means in glosses.
/// </summary>
/*
  for example:

    # a comment
    the dog didn't see the cat
      see.PST.PFV.NEG  subject:dog  object:cat

  docs/clause-format.md describes the format. A gloss line is read in two steps: ClauseSyntax reads
  what's nested in what, words, groups in parentheses and clauses in brackets, each with the role
  it's written with, and this builds what that means into a Statement, refusing whatever the
  generator can't express yet, never dropping it
 */
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
                        ? ToCoordinated(conjunction, previous.Statement, Ungapped(ParseClause(ClauseSyntax.Read(rest, number), number, null), number), number)
                        : ToSubordinated(previous.Statement, SubordinatorOf(line).GetValueOrDefault(), ClauseSyntax.Read(rest, number), number),
                };
            }
            else if (translation is { } english)
            {
                entries.Add(new Entry(english.Text, Ungapped(ParseClause(ClauseSyntax.Read(line, number), number, null), number), number));
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

    /*
      a clause under the ones above it. Only a purpose clause can leave out its subject, which is then
      the main clause's, as in 'to cross the river, you need a rope'. It can't be a command, or be said
      to someone, since the main clause is
     */
    private static Subordinated ToSubordinated(Statement main, Subordinator subordinator, SyntaxClause line, int number)
    {
        var shares = subordinator == Subordinator.Purpose && !line.Items.Any(i => i.Role == "subject");
        var clause = Ungapped(ParseClause(line, number, shares ? Syntax.SubjectOf(main) : (Argument?)null), number);
        return clause.Value switch
        {
            Clause { Force.Value: Directive } => throw new ClauseFormatException($"A clause under another can't be a command: {subordinator.ToString().ToLowerInvariant()} takes a clause with a subject.", number),
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
        Clause c => c.Arguments,
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
        //an indefinite pronoun's noun is the language's to say, and no noun of the text's
        NounPhrase { Indefinite: not null } n => n.Relative is { } relative ? ArgumentsOf(relative).SelectMany(NounsIn) : [],
        NounPhrase n => [n.Noun, .. Given(n.Possessor).SelectMany(NounsIn), .. n.Relative is { } relative ? ArgumentsOf(relative).SelectMany(NounsIn) : []],
        Pronoun => [],
        QuestionWord or DemonstrativePronoun or DemonstrativePlace => [],
        Coordination c => c.Items.SelectMany(NounsIn),
        ComplementClause c => ArgumentsOf(c.Clause).SelectMany(NounsIn),
        Quotation q => ArgumentsOf(q.Statement).SelectMany(NounsIn),
        Gap or RelativeWord => [],
    };

    /*
      the gaps in a statement outside its relative clauses, wherever they are: in its roles, in noun
      phrases, joined to others, or in clauses inside it. A complement's subject that's the main
      clause's is no gap of its own, even where the main clause's is one
     */
    private static int Gaps(Statement statement) => ArgumentsOf(statement).Sum(Gaps);

    private static int Gaps(Argument argument) => argument switch
    {
        Gap => 1,
        NounPhrase n => n.Possessor is { } possessor ? Gaps(possessor) : 0,
        Coordination c => c.Items.Sum(Gaps),
        ComplementClause { SharesSubject: true } c => Gaps(c.Clause) - (Syntax.SubjectOf(c.Clause).Value is Gap ? 1 : 0),
        ComplementClause c => Gaps(c.Clause),
        Quotation q => Gaps(q.Statement),
        Pronoun or DemonstrativePronoun or QuestionWord or DemonstrativePlace or RelativeWord => 0,
    };

    //a clause that isn't relative has no gap, which marks only where a relative clause's noun goes
    private static Statement Ungapped(Statement statement, int number) => Gaps(statement) == 0 ? statement
        : throw new ClauseFormatException("_ marks the role a relative clause's noun fills in it, so it goes only inside relative:[...], as in object:(net.DEF relative:[mend.PST  subject:3SG.F  object:_]).", number);

    /*
      the roles of a relative clause's own that its gap is in. It can be its subject, its object, the
      agent of a passive or an oblique, but not what it compares something with
     */
    private static IEnumerable<string> GapRoles(Statement statement)
    {
        return statement.Value switch
        {
            Clause c => c.Fillers.Where(f => f.Argument.Value is Gap).Select(NameOf),
            Comparative c => [.. GapRoles(c.Clause), .. c.Standard is { Value: Gap } ? ["standard"] : Array.Empty<string>()],
            Modalized m => GapRoles(m.Clause),
            _ => [],
        };

        //an oblique's role is written as its name, as a location be says its subject is in is
        static string NameOf(Filler filler) => filler switch
        {
            { Oblique: { } oblique } => oblique.Role.ToString().ToLowerInvariant(),
            { Role: Role.IntransitiveSubject or Role.TransitiveSubject } => "subject",
            { Role: Role.DirectObject } => "object",
            { Role: Role.Agent } => "agent",
            { Role: var role } => throw new ArgumentOutOfRangeException(nameof(filler), role, null),
        };
    }

    /*
      a relative clause: a clause of its own with one gap, in one of its own roles rather than in a
      noun phrase or a clause inside it, where its noun goes. It says something of its noun, so it
      can't be a command, a question or said to someone
     */
    private static Statement ParseRelative(SyntaxValue value, int number)
    {
        const string Example = "relative:[mend.PST  subject:3SG.F  object:_]";
        if (value.Value is not SyntaxClause written || written.Items.Count == 0)
        {
            throw new ClauseFormatException($"relative: is a clause in brackets with _ where its noun goes, as in (net.DEF {Example}), not '{value.Text}'.", number);
        }
        var clause = ParseClause(written, number, null);
        var roles = GapRoles(clause).ToList();
        return clause.Value switch
        {
            Clause { Force.Value: Directive } => throw new ClauseFormatException("A relative clause can't be a command: it says something of its noun.", number),
            Question => throw new ClauseFormatException("A relative clause can't be a question.", number),
            Addressed => throw new ClauseFormatException("A relative clause can't be said to someone: say who's addressed in the main clause.", number),
            _ when roles.Count == 0 && Gaps(clause) == 0 => throw new ClauseFormatException($"A relative clause needs _ in the role its noun fills in it, as in {Example}.", number),
            _ when roles.Count != 1 || Gaps(clause) != 1 => throw new ClauseFormatException("A relative clause has one _, in a role of its own, not inside a noun phrase or another clause in it.", number),
            _ when roles[0] == "standard" => throw new ClauseFormatException("A relative clause's noun can't be what it compares something with yet.", number),
            _ => clause,
        };
    }

    /*
      the roles a clause can have. An oblique role is written as its name in lowercase, but for the
      similative, which is like:. A noun or adjective is what be says its subject is, a standard what
      an adjective compares it with, and an addressee who the clause is said to. An adverb fills no
      role, but is written as one named for what it says, and a clause can have any number of them
     */
    private static readonly Dictionary<string, Role> ObliqueRoles = Oblique.Roles.ToDictionary(r => r == Role.Similative ? "like" : r.ToString().ToLowerInvariant());
    private static readonly Dictionary<string, AdverbKind> AdverbRoles = Enum.GetValues<AdverbKind>().ToDictionary(k => k.ToString().ToLowerInvariant());
    private static readonly string[] Roles = ["subject", "object", "quote", "noun", "adjective", "standard", .. ObliqueRoles.Keys, "agent", "addressee", .. AdverbRoles.Keys, "manner"];

    //what the format can say, for describing it elsewhere, as the glosser's prompt does
    public static IReadOnlyList<string> RoleNames => Roles;
    public static IReadOnlyList<T> ValuesOf<T>() where T : Grammeme => [.. Glosses.Values.OfType<T>()];

    //a subject that isn't said is the given one, if there is one
    private static Statement ParseClause(SyntaxClause written, int number, Argument? impliedSubject)
    {
        if (written.Items is not [{ Role: null, Value.Value: SyntaxWord head }, ..])
        {
            throw written.Items.Count == 0
                ? new ClauseFormatException("A clause needs a verb.", number)
                : new ClauseFormatException($"A clause starts with its verb, not with '{written.Items[0].Text}'.", number);
        }
        var (verb, glosses) = ParseWord(head.Text, number);
        var features = ParseVerbFeatures(verb, glosses, number);
        //why and how fill no role, so they're written on their own
        static bool IsAdverb(SyntaxItem item) => item is { Role: null, Value.Value: SyntaxWord { Text: var text } } && QuestionAdverbs.ContainsKey(text);
        var adverb = written.Items.Skip(1).Where(IsAdverb).ToList() switch
        {
            [] => (QuestionAdverb?)null,
            [var one] => QuestionAdverbs[one.Text],
            _ => throw new ClauseFormatException("A question can only ask one thing yet: one why or how.", number),
        };
        var roles = ParseRoles(verb, written.Items.Skip(1).Where(i => !IsAdverb(i)), features.Imperative, impliedSubject, number);
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
        /*
          an epistemic modal judges a perfect or progressive whole, as 'he must have left' is sure
          now of a past; a situational one with either is mostly a regret, as 'you should have gone'
          is, which can't be said yet
         */
        if (features.Modality is { Epistemic: false } situational && features.Aspect is { } aspect && (aspect == Aspect.Perfect || aspect == Aspect.Progressive))
        {
            throw new ClauseFormatException($"{situational.Gloss} can't take {aspect.Gloss} yet, as in 'should have gone' or 'should be waiting'. " +
                $"A modal that's sure or unsure of what happened, as in 'must have gone' or 'might be waiting', is {Modality.Presumptive.Gloss} or {Modality.Dubitative.Gloss}.", number);
        }
        var asked = CheckQuestion(features, roles, adverb, number);

        Force force = features.Imperative ? ToDirective(verb, features, roles, addressee, number) : ToDeclarative(features, roles, impliedSubject, number);
        var clause = new Clause(ToPredicate(verb, features, roles, number), force, features.Negative, verb == "be" ? CopularObliques(roles) : roles.AllObliques, [.. roles.Adverbs]);
        //only be compares, and not in a command, which the checks above make sure of
        Statement statement = roles.Compared || standard.HasValue ? ToComparative(clause, standard, number) : clause;
        //a negative indefinite says the clause is negated, and a language whose verb takes negation
        //beside one adds it itself
        if ((features.Negative || features.ModalNegative) && ArgumentsOf(statement).Any(Indefinites.IsNegative))
        {
            throw new ClauseFormatException("A clause with nobody, nothing, nowhere or no already says 'not', so its verb takes no NEG, as in come.PST subject:nobody for 'nobody came'.", number);
        }
        if (features.Modality is { } modal)
        {
            statement = new Modalized(statement, modal, features.ModalNegative);
        }
        if (asked)
        {
            statement = new Question(statement, adverb);
        }
        return addressee is null ? statement : new Addressed(statement, addressee);
    }

    //the question words, in the roles they ask about
    private static readonly Dictionary<string, Interrogative> QuestionWords = Enum.GetValues<Interrogative>().ToDictionary(w => w.ToString().ToLowerInvariant());

    //the question words that fill no role, written on their own
    private static readonly Dictionary<string, QuestionAdverb> QuestionAdverbs = Enum.GetValues<QuestionAdverb>().ToDictionary(w => w.ToString().ToLowerInvariant());

    //what to write for a question word that fills no role, written where it can't go
    private static string OnItsOwn(string word) => $"{word} fills no role, so it's written on its own, as in leave.PST subject:3SG {word}.";

    /*
      whether the clause is a question: a yes-no question with Q, or a content question with one
      question word in a role, or why or how. Where asks about a place, so it's only where, to where
      or from where, when asks about a time, so it's only in time:, and a question word can't be a
      standard yet
     */
    private static bool CheckQuestion(VerbFeatures features, ClauseRoles roles, QuestionAdverb? adverb, int number)
    {
        var questioned = roles.Arguments.Select(a => (a.Key, a.Value)).Concat(roles.Obliques.Select(o => (Key: o.Role, o.Oblique.Argument)))
            .Where(a => a.Item2.Value is QuestionWord).ToList();
        if (!features.Question && adverb is null && questioned.Count == 0)
        {
            return false;
        }
        if (features.Imperative)
        {
            throw new ClauseFormatException("A command can't be a question.", number);
        }
        if (questioned.Count + (adverb is null ? 0 : 1) > 1)
        {
            throw new ClauseFormatException("A question can only ask one thing yet: one who, what, where, when, why or how.", number);
        }
        if (features.Question && (adverb is not null || questioned.Count > 0))
        {
            throw new ClauseFormatException($"A question with {(adverb is { } a ? a.ToString().ToLowerInvariant() : WordOf(questioned[0].Item2))} needs no Q: Q is only for a yes-no question.", number);
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
            if (role == "duration")
            {
                throw new ClauseFormatException($"A question can't ask for how long yet, as in duration:{WordOf(argument)}.", number);
            }
            if (word == Interrogative.Where && role is not ("location" or "goal" or "source"))
            {
                throw new ClauseFormatException($"where asks about a place, so it's location:where, goal:where for 'where to' or source:where for 'where from', not {role}:where.", number);
            }
            if ((word == Interrogative.When) != (role == "time"))
            {
                throw new ClauseFormatException(word == Interrogative.When
                    ? $"when asks about a time, so it's time:when, not {role}:when."
                    : $"A question asking when is time:when, not time:{WordOf(argument)}.", number);
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
    private static Comparative ToComparative(Clause clause, Argument? standard, int number)
    {
        if (clause.Predicate.Value is not Being { Complement.Value: Adjective })
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
        NounPhrase { Demonstrative: { } } noun => throw new ClauseFormatException($"An addressee takes no demonstrative yet: write addressee:{noun.Noun}, not {noun.Demonstrative.Gloss}.", number),
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
        Phase p => new($"Write {verb}.{p.Gloss} as {(p == Phase.Inceptive ? "begin" : "stop")} object:[to  {verb}], which a language with {(p == Phase.Inceptive ? "an" : "a")} {p.Name.ToLowerInvariant()} says with {p.Gloss} on its verb.", number),
        _ => new($"'{verb}' can't take {feature.Gloss} ({feature.Name}) yet: verbs have only tense, aspect, polarity, the imperative, Q for a question, the passive and the modals {string.Join(", ", ValuesOf<Modality>().Select(m => m.Gloss))}.", number),
    };

    /*
      the subject and addressee are read first, whatever order they're written in, so that a clause
      in brackets can share the subject, which a command's is 'you'. The obliques and adverbs keep
      the order they're written in
     */
    private static ClauseRoles ParseRoles(string verb, IEnumerable<SyntaxItem> items, bool imperative, Argument? impliedSubject, int number)
    {
        var arguments = new Dictionary<string, Argument>();
        var obliques = new List<(string Role, Oblique Oblique)>();
        var adverbs = new List<Adverbial>();
        Complement? complement = null;
        var compared = false;
        var quoted = false;
        Argument Sharer()
        {
            var subject = arguments.TryGetValue("subject", out var said) ? said : impliedSubject;
            if (imperative)
            {
                var addressee = arguments.TryGetValue("addressee", out var called) ? ToAddressee(called, number) : null;
                return new Pronoun(Person.Second, Told(subject, addressee, number));
            }
            return subject ?? throw new ClauseFormatException("The clause needs a subject, as in subject:dog.", number);
        }
        foreach (var item in items.OrderBy(i => i.Role is "subject" or "addressee" ? 0 : 1))
        {
            if (item.Role is not { } written)
            {
                throw new ClauseFormatException($"'{item.Text}' needs a role, as in subject:{item.Text}.", number);
            }
            var (role, configuration) = ParseConfiguration(written, number);
            var value = item.Value;
            if (role == "adverb")
            {
                throw new ClauseFormatException($"Say what an adverb says: when:{value.Text} for when, often:{value.Text} for how often or whether still, or manner: with an adjective for how, as manner:careful is 'carefully'.", number);
            }
            if (role == "possessor")
            {
                throw new ClauseFormatException($"A possessor goes inside its noun's parentheses, as in object:(house possessor:{value.Text}).", number);
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
            if (role is "object" or "quote" && (quoted || role == "quote" && arguments.ContainsKey("object")))
            {
                throw new ClauseFormatException("The clause has more than one object: a quote: is what's said, in place of an object:.", number);
            }
            if (value.Value is SyntaxClause embedded)
            {
                arguments["object"] = role is "object" or "quote" ? ParseEmbedded(role, embedded, Sharer(), number)
                    : throw Misplaced($"{role}:", number);
                quoted |= role == "quote";
                continue;
            }
            if (role == "quote")
            {
                throw new ClauseFormatException($"quote: is what someone said, word for word, as a clause in brackets, as in quote:[be  subject:2SG  adjective:late], not '{value.Text}'.", number);
            }
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
                        QuestionWord => throw new ClauseFormatException($"be can't ask what its subject is yet, as in noun:{value.Text} for '{value.Text} are you?'.", number),
                        _ => throw new ClauseFormatException($"be can't say its subject is {value.Text} yet: the noun has to be a noun, not a pronoun.", number),
                    };
                    break;
                case "adjective" when value.Value is SyntaxGroup together:
                    complement = ParseAdjectiveCoordination(together, number);
                    break;
                case "adjective":
                    //only what be says of its subject can be comparative, so CMPR is taken off here
                    //before the adjective is read
                    var glossed = value.Text.Split('.');
                    compared = glossed.Skip(1).Contains(Degree.Comparative.Gloss);
                    complement = ParseAdjective(compared ? string.Join('.', glossed.Where(p => p != Degree.Comparative.Gloss)) : value.Text, number);
                    break;
                case var name when ObliqueRoles.TryGetValue(name, out var oblique):
                    if (configuration != Configuration.At && !Oblique.Spatial.Contains(oblique))
                    {
                        throw new ClauseFormatException($"Only a role saying where can say in, on or under, as location., goal., source. and route. do, not {role}.", number);
                    }
                    obliques.Add((role, new Oblique(oblique, ParseObliqueArgument(value, oblique, configuration, number), configuration)));
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
    //is, as 'children, come' tells several
    private static Directive ToDirective(string verb, VerbFeatures features, ClauseRoles roles, NounPhrase? addressee, int number)
    {
        if (features.Tense is not null || features.Aspect is not null)
        {
            throw new ClauseFormatException($"'{verb}' can't take {(features.Tense as Grammeme ?? features.Aspect)!.Gloss}: a command has no tense or aspect.", number);
        }
        return new Directive(Told(roles.Arguments.TryGetValue("subject", out var subject) ? subject : (Argument?)null, addressee, number));
    }

    //a clause that isn't a command says when, present and imperfective unless it says otherwise, and needs a subject
    private static Declarative ToDeclarative(VerbFeatures features, ClauseRoles roles, Argument? impliedSubject, int number) =>
        (roles.Arguments.TryGetValue("subject", out var said) ? said : impliedSubject) is { } subject
            ? new Declarative(features.Tense ?? Tense.Present, features.Aspect ?? Aspect.Imperfective, subject)
            : throw new ClauseFormatException("The clause needs a subject, as in subject:dog.", number);

    //how many a command is said to
    private static Number Told(Argument? subject, NounPhrase? addressee, int number)
    {
        if (subject is { } said)
        {
            return said.Value is Pronoun { Person: var person, Number: var count } && person == Person.Second ? count
                : throw new ClauseFormatException("A command is said to 'you', so its subject can only be 2SG or 2PL, as in subject:2PL.", number);
        }
        return addressee is { Number: var called } && called != Number.Singular ? Number.Plural : Number.Singular;
    }

    //a clause in brackets anywhere but an object: or quote:, which can come up wherever a value is
    //read: in a role, among a noun phrase's parts, or joined to others
    private static ClauseFormatException Misplaced(string where, int number) => new($"Only object: and quote: take a clause in brackets, not {where}.", number);

    //the words that start a complement clause, and what each says
    private static readonly Dictionary<string, ComplementKind> Complementizers = new()
    {
        ["that"] = ComplementKind.Proposition,
        ["to"] = ComplementKind.Event,
    };

    /*
      an object: or quote: in brackets. A quotation is said as it was said, so it can be anything a
      clause can, and takes nothing from the clause around it. A complement starts with that, for
      what's said, known or seen, which has a time of its own, or to, for what's wanted, tried or
      begun, whose time the main verb sets, so it has no tense or aspect; and only an event can leave
      out its subject, which is then the main clause's, as in 'they want to go home'
     */
    private static Argument ParseEmbedded(string role, SyntaxClause embedded, Argument subject, int number)
    {
        if (role == "quote")
        {
            return embedded.Items.Count > 0 ? new Quotation(ParseClause(embedded, number, null))
                : throw new ClauseFormatException("A quote: needs the clause that was said inside its brackets.", number);
        }
        if (embedded.Items is not [{ Role: null, Value.Value: SyntaxWord { Text: var first } }, _, ..] || !Complementizers.TryGetValue(first, out var kind))
        {
            throw new ClauseFormatException("A clause in object: starts with that, for what's said, known or seen, or to, for what's wanted, tried or begun, " +
                "as in object:[that  be.PST  subject:2SG  adjective:late] or object:[to  go  goal:home]. What's said word for word is a quote:[...].", number);
        }
        var rest = embedded with { Items = [.. embedded.Items.Skip(1)] };
        if (kind == ComplementKind.Event && rest.Items[0].Value.Value is SyntaxWord verb
            && ParseWord(verb.Text, number).Features.FirstOrDefault(f => f is Tense or Aspect) is { } time)
        {
            throw new ClauseFormatException($"A clause starting with to can't take {time.Gloss}: the verb it's the object of says when.", number);
        }
        var shares = kind == ComplementKind.Event && !rest.Items.Any(i => i.Role == "subject");
        var clause = ParseClause(rest, number, shares ? subject : (Argument?)null);
        return clause.Value switch
        {
            Clause { Force.Value: Directive } => throw new ClauseFormatException($"A clause in object: can't be a command: write {(kind == ComplementKind.Event ? "to" : "that")} with a subject, or quote the command with quote:[...].", number),
            Addressed => throw new ClauseFormatException("Say who's addressed in the main clause, not in one inside it, unless it's quoted with quote:[...].", number),
            Question => throw new ClauseFormatException("A question inside another clause can't be said yet, unless it's quoted with quote:[...].", number),
            _ => new ComplementClause(kind, clause, shares),
        };
    }

    /*
      what a clause says of its subject: with be, what or where it is, or told, what or where to be;
      a passive's subject is its patient, so it has no object
     */
    private static Predicate ToPredicate(string verb, VerbFeatures features, ClauseRoles roles, int number)
    {
        if (verb == "be")
        {
            return new Being(CopularComplement(roles, number));
        }
        RejectComplement(verb, roles, number);
        if (!features.Passive)
        {
            return new Doing(verb, roles.Object);
        }
        if (roles.Object.HasValue)
        {
            throw new ClauseFormatException($"A passive has no object: what's done to is its subject, as in {verb}.PASS subject:light.", number);
        }
        return new Done(verb, roles.Arguments.TryGetValue("agent", out var by) ? by : (Argument?)null);
    }

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

    private static void RejectComplement(string verb, ClauseRoles roles, int number)
    {
        if (roles.Complement.HasValue)
        {
            throw new ClauseFormatException($"Only be takes a noun: or adjective:. '{verb}' takes an object:.", number);
        }
    }

    /*
      a noun and its adjectives, in parentheses, as in (dog.PL big old). A possessor can be a noun
      phrase in parentheses of its own, as in (son possessor:(sister possessor:1SG)) for 'my sister's
      son', and nothing else can
     */
    private static NounPhrase ParseModifiedNounPhrase(SyntaxGroup group, int number)
    {
        var items = group.Items;
        if (items.FirstOrDefault(i => i.Value.Value is SyntaxClause && i.Role != "relative") is { } clause)
        {
            throw Misplaced($"'{clause.Text}' in a noun phrase", number);
        }
        if (items.FirstOrDefault(i => i.Value.Value is SyntaxGroup && i.Role != "possessor") is { } nested)
        {
            throw new ClauseFormatException($"Only a possessor can be a noun phrase of its own inside parentheses, as in (son possessor:(sister possessor:1SG)), not '{nested.Text}'.", number);
        }
        if (items.Count == 0)
        {
            throw new ClauseFormatException("A noun phrase in parentheses needs a noun.", number);
        }
        if (items[0].Role is not null)
        {
            throw new ClauseFormatException($"A noun phrase in parentheses starts with its noun, not '{items[0].Text}'.", number);
        }
        var head = items[0].Text;
        if (ParsePronoun(head, number) is not null)
        {
            throw new ClauseFormatException($"{head} can't take adjectives: pronouns don't, here.", number);
        }
        if (ParseDemonstrativePronoun(head, number) is { } demonstrative)
        {
            throw new ClauseFormatException($"{head} can't take adjectives: a demonstrative goes on its noun, as in (key.{demonstrative.Distance.Gloss} small).", number);
        }
        foreach (var item in items.Skip(1).Where(i => i.Role is not (null or "possessor" or "relative")))
        {
            throw new ClauseFormatException($"Noun phrases can't have {A(item.Role!)} yet, only adjectives, a number, an ordinal, a quantifier, a possessor and a relative clause.", number);
        }
        var noun = ParseArgument(items[0].Value, number).Value as NounPhrase
            ?? throw new ClauseFormatException($"{head} can't take adjectives, a number or a possessor yet.", number);
        if (noun.Indefinite is not null && items.Skip(1).FirstOrDefault(i => i.Role != "relative") is { } extra)
        {
            throw new ClauseFormatException($"{head} takes only a relative clause, not '{extra.Text}'.", number);
        }

        int? count = null;
        int? ordinal = null;
        Quantifier? quantifier = null;
        Argument? possessor = null;
        Statement? relative = null;
        var adjectives = new List<Adjective>();
        foreach (var item in items.Skip(1))
        {
            var word = item.Value.Text;
            if (item.Role == "relative")
            {
                relative = relative is null ? ParseRelative(item.Value, number)
                    : throw new ClauseFormatException($"'{head}' has more than one relative clause.", number);
            }
            else if (item.Role == "possessor")
            {
                if (possessor.HasValue)
                {
                    throw new ClauseFormatException($"'{head}' has more than one possessor.", number);
                }
                possessor = ParseArgument(item.Value, number);
                if (possessor.GetValueOrDefault().Value is QuestionWord)
                {
                    throw new ClauseFormatException("A possessor can't be a question word yet, as in 'whose house'.", number);
                }
            }
            else if (word.All(char.IsAsciiDigit))
            {
                if (count is not null)
                {
                    throw new ClauseFormatException($"'{head}' has more than one number.", number);
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
                    throw new ClauseFormatException($"'{head}' has more than one ordinal.", number);
                }
                ordinal = int.Parse(word[..^2]);
                if (ordinal is < 1 or > NumeralSystem.Largest)
                {
                    throw new ClauseFormatException($"Ordinals go from 1st to {NumeralSystem.Largest}th for now, not {word}.", number);
                }
            }
            else if (QuantifierWords.TryGetValue(word.Split('.')[0], out var q))
            {
                quantifier = word.Contains('.') ? throw new ClauseFormatException($"{q.ToString().ToLowerInvariant()} takes no glosses: its noun has them, as in ({head} {word.Split('.')[0]}).", number)
                    : quantifier is null ? q
                    : throw new ClauseFormatException($"'{head}' has more than one quantifier.", number);
            }
            else if (word.Split('.')[0] == "each")
            {
                throw new ClauseFormatException($"Write every for each, as in ({head} every).", number);
            }
            else if (Conjunctions.ContainsKey(word))
            {
                throw new ClauseFormatException($"Write {word} first, before what it joins, as in ({word} dog.DEF cat.DEF), not '{group.Text}'.", number);
            }
            else if (NumberWords.Contains(word.Split('.')[0]))
            {
                throw new ClauseFormatException($"Write numbers in digits, as in ({head} 3), not '{word}'.", number);
            }
            else if (OrdinalWords.Contains(word.Split('.')[0]))
            {
                throw new ClauseFormatException($"Write ordinals in digits, as in ({head} 3rd), not '{word}'.", number);
            }
            else
            {
                adjectives.Add(ParseAdjective(word, number));
            }
        }
        /*
          a quantifier says how much there is, as an indefinite article would, so it can't take one,
          and every and no pick out no particular one, so they can't be definite either, though 'all
          the dogs' and 'most of the men' are. Only all goes with a number, as in 'all three dogs'
         */
        if (quantifier is { } said)
        {
            var name = said.ToString().ToLowerInvariant();
            if (noun.Definiteness == Definiteness.Indefinite)
            {
                throw new ClauseFormatException($"'{head}' takes {name} or NDF, not both: write ({head.Split('.')[0]} {name}).", number);
            }
            if (said is Quantifier.Every or Quantifier.No && (noun.Definiteness is not null || noun.Demonstrative is not null))
            {
                throw new ClauseFormatException($"{name} can't go with {(noun.Definiteness?.Gloss ?? noun.Demonstrative!.Gloss)}: it picks out no particular one.", number);
            }
            if (count is not null && said != Quantifier.All)
            {
                throw new ClauseFormatException($"Only all goes with a number, as in ({head} all 3), not {name}.", number);
            }
        }
        //a number gives the noun its number unless the noun has one already
        var given = ParseWord(head, number).Features.OfType<Number>().Any();
        return noun with
        {
            Adjectives = [.. adjectives],
            Count = count,
            Ordinal = ordinal,
            Possessor = possessor,
            Relative = relative,
            Quantifier = quantifier ?? noun.Quantifier,
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

    /*
      a coordination in parentheses, conjunction first, as in (and dog.DEF cat.DEF): the conjunction and
      the two or more things it joins, which fill no roles of their own, or null if the parentheses
      hold something else
     */
    private static (Conjunction Conjunction, List<SyntaxValue> Items)? Coordinating(SyntaxGroup group, int number)
    {
        if (group.Items is not [{ Role: null, Value.Value: SyntaxWord { Text: var first } }, ..] || !Conjunctions.TryGetValue(first, out var conjunction))
        {
            return null;
        }
        if (group.Items.Count < 3)
        {
            throw new ClauseFormatException($"'{group.Text}' joins too few things: {first} needs two or more, as in ({first} dog.DEF cat.DEF).", number);
        }
        if (group.Items.Skip(1).FirstOrDefault(i => i.Role is not null) is { } filling)
        {
            throw new ClauseFormatException($"What {first} joins fills the role the parentheses are written with, so it has none of its own, as '{filling.Text}' does.", number);
        }
        return (conjunction, [.. group.Items.Skip(1).Select(i => i.Value)]);
    }

    //adjectives said together, as in adjective:(and warm crowded)
    private static AdjectiveCoordination ParseAdjectiveCoordination(SyntaxGroup group, int number)
    {
        if (Coordinating(group, number) is not var (conjunction, items) || conjunction == Conjunction.But || items.Any(i => i.Value is not SyntaxWord))
        {
            throw new ClauseFormatException($"Adjectives in parentheses are joined with and or or first, as in adjective:(and warm crowded), not '{group.Text}'.", number);
        }
        return new AdjectiveCoordination(conjunction, [.. items.Select(item => ParseAdjective(item.Text, number))]);
    }

    //an adjective, maybe with INTS for 'very'
    private static Adjective ParseAdjective(string token, int number)
    {
        var (meaning, features) = ParseWord(token, number);
        if (QuestionWords.ContainsKey(meaning) || QuestionAdverbs.ContainsKey(meaning))
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
    private static Manner ParseManner(SyntaxValue value, int number)
    {
        if (value.Value is not SyntaxWord { Text: var token })
        {
            throw new ClauseFormatException($"manner: takes one adjective, as in manner:careful, not '{value.Text}'. Write manner: again for another.", number);
        }
        if (token.Split('.').Skip(1).Contains(Degree.Comparative.Gloss))
        {
            throw new ClauseFormatException("A manner can't compare yet, as in 'more slowly'.", number);
        }
        return new Manner(ParseAdjective(token, number));
    }

    //words for where besides here, there and the indefinites wait on spatial adpositions, which most
    //languages make them from
    private static readonly HashSet<string> PlaceAdverbs =
        ["inside", "outside", "above", "below", "upstairs", "downstairs", "away", "upstream", "downstream"];

    //'here' and 'there', which say a place by how far it is, as demonstratives do
    private static readonly Dictionary<string, Distance> PlaceDemonstratives = new()
    {
        ["here"] = Distance.Proximal,
        ["there"] = Distance.Distal,
    };

    private static readonly string PlaceRoles = "location:here, goal:here for 'to here' or source:here for 'from here'";

    //the indefinite pronouns, each its series and kind
    private static readonly Dictionary<string, (Quantifier Series, Ontology Kind)> IndefiniteWords = new()
    {
        ["someone"] = (Quantifier.Some, Ontology.Person),
        ["something"] = (Quantifier.Some, Ontology.Thing),
        ["somewhere"] = (Quantifier.Some, Ontology.Place),
        ["nobody"] = (Quantifier.No, Ontology.Person),
        ["nothing"] = (Quantifier.No, Ontology.Thing),
        ["nowhere"] = (Quantifier.No, Ontology.Place),
        ["everyone"] = (Quantifier.Every, Ontology.Person),
        ["everything"] = (Quantifier.Every, Ontology.Thing),
        ["everywhere"] = (Quantifier.Every, Ontology.Place),
        ["sometime"] = (Quantifier.Some, Ontology.Time),
        ["never"] = (Quantifier.No, Ontology.Time),
        ["always"] = (Quantifier.Every, Ontology.Time),
    };

    //what an indefinite that only some roles take says, and the roles it goes in, for an error to suggest
    private static string Placed(string word, Ontology kind) =>
        $"{word} says {(kind == Ontology.Place ? "a place" : "a time")}, so it's {Joined(Indefinites.RolesOf(kind)!.Select(r => $"{r.ToString().ToLowerInvariant()}:{word}"), "or")}";

    private static string Joined(IEnumerable<string> items, string conjunction) => items.ToList() switch
    {
        [var one] => one,
        var many => $"{string.Join(", ", many[..^1])} {conjunction} {many[^1]}",
    };

    //other English words for them, and what to write instead
    private static readonly Dictionary<string, string> IndefiniteSpellings = new()
    {
        ["somebody"] = "someone",
        ["everybody"] = "everyone",
        ["noone"] = "nobody",
        ["anyone"] = "nobody in a negated clause, or someone in a question",
        ["anybody"] = "nobody in a negated clause, or someone in a question",
        ["anything"] = "nothing in a negated clause, or something in a question",
        ["anywhere"] = "nowhere in a negated clause, or somewhere in a question",
        ["ever"] = "never in a negated clause, or sometime in a question",
    };

    private static readonly Dictionary<string, Quantifier> QuantifierWords = Enum.GetValues<Quantifier>().ToDictionary(q => q.ToString().ToLowerInvariant());
    public static IReadOnlyList<string> QuantifierNames => [.. QuantifierWords.Keys];

    //an indefinite pronoun, which takes no glosses, or null if the word isn't one
    private static NounPhrase? ParseIndefinite(string meaning, IReadOnlyList<Grammeme> features, int number)
    {
        if (IndefiniteSpellings.TryGetValue(meaning, out var instead))
        {
            throw new ClauseFormatException($"Write {instead}, not {meaning}.", number);
        }
        if (!IndefiniteWords.TryGetValue(meaning, out var indefinite))
        {
            return null;
        }
        return features.Count == 0 ? Indefinites.Pronoun(indefinite.Series, indefinite.Kind)
            : throw new ClauseFormatException($"{meaning} can't take {features[0].Gloss}: an indefinite pronoun takes no glosses.", number);
    }

    /*
      what a role saying where, to where or from where holds: 'here' or 'there', which can't take a
      configuration, as a question's where can't, with MED on 'there' for one near the hearer or
      neither near nor far, or anything else a role holds
     */
    private static Argument ParseObliqueArgument(SyntaxValue value, Role role, Configuration configuration, int number)
    {
        if (value.Value is not SyntaxWord { Text: var token })
        {
            return ParseArgument(value, number);
        }
        var (meaning, features) = ParseWord(token, number);
        var restricted = IndefiniteWords.TryGetValue(meaning, out var indefinite) && Indefinites.RolesOf(indefinite.Kind) is not null;
        if (!PlaceDemonstratives.TryGetValue(meaning, out var distance) && !restricted)
        {
            return ParseArgument(value, number);
        }
        if (restricted ? !Indefinites.RolesOf(indefinite.Kind)!.Contains(role) : role is not (Role.Location or Role.Goal or Role.Source))
        {
            var says = restricted ? Placed(meaning, indefinite.Kind) : $"{meaning} says a place, so it's {PlaceRoles.Replace("here", meaning)}";
            throw new ClauseFormatException($"{says}, not {role.ToString().ToLowerInvariant()}:{meaning}.", number);
        }
        if (configuration != Configuration.At)
        {
            throw new ClauseFormatException($"{meaning} can't take a configuration, as in 'under {meaning}'.", number);
        }
        if (restricted)
        {
            return ParseIndefinite(meaning, features, number)!;
        }
        return features switch
        {
            [] => new DemonstrativePlace(distance),
            [var medial] when medial == Distance.Medial && meaning == "there" => new DemonstrativePlace(Distance.Medial),
            [var other, ..] => throw new ClauseFormatException($"{meaning} can't take {other.Gloss}: only there takes a gloss, MED, for a place near the hearer or neither near nor far.", number),
        };
    }

    /*
      a demonstrative standing alone, as in PROX for 'this' or DIST.PL for 'those', with a number and
      nothing else, or null if the token doesn't start with a distance
     */
    private static DemonstrativePronoun? ParseDemonstrativePronoun(string token, int number)
    {
        var parts = token.Split('.');
        if (!Glosses.TryGetValue(parts[0], out var first) || first is not Distance distance)
        {
            return null;
        }
        Number? count = null;
        foreach (var gloss in parts.Skip(1))
        {
            var feature = Glosses.TryGetValue(gloss, out var grammeme) ? grammeme
                : throw new ClauseFormatException($"'{gloss}' isn't a gloss.{Suggestion(gloss)}", number);
            count = feature switch
            {
                Number n when count is null => n,
                Number => throw new ClauseFormatException($"{parts[0]} has more than one number.", number),
                Case => throw new ClauseFormatException($"{parts[0]} can't take {feature.Gloss}: a demonstrative's case comes from its role.", number),
                _ => throw new ClauseFormatException($"{parts[0]} can't take {feature.Gloss} ({feature.Name}): a demonstrative standing alone has only a distance and a number.", number),
            };
        }
        return new DemonstrativePronoun(distance, count ?? Number.Singular);
    }

    //a word of its own saying when or how often, with no glosses
    private static Adverb ParseAdverb(SyntaxValue value, AdverbKind kind, int number)
    {
        var role = kind.ToString().ToLowerInvariant();
        if (value.Value is not SyntaxWord { Text: var token })
        {
            throw new ClauseFormatException($"An adverb is one word, as in {role}:{(kind == AdverbKind.When ? "now" : "again")}, not '{value.Text}'. Write {role}: again for another.", number);
        }
        var (meaning, features) = ParseWord(token, number);
        if (QuestionAdverbs.ContainsKey(meaning))
        {
            throw new ClauseFormatException(OnItsOwn(meaning), number);
        }
        if (QuestionWords.TryGetValue(meaning, out var asked))
        {
            throw new ClauseFormatException(asked == Interrogative.When
                ? $"when asks about a time, so it's time:when, not {role}:when."
                : $"{meaning} isn't an adverb: a question word goes in the role it asks about, as in subject:who.", number);
        }
        if (meaning is "very" or "much")
        {
            throw new ClauseFormatException($"{meaning} goes on an adjective as INTS, as in adjective:tired.INTS or manner:slow.INTS.", number);
        }
        if (meaning == "not")
        {
            throw new ClauseFormatException("not is NEG on the verb, as in see.NEG.", number);
        }
        if (PlaceDemonstratives.ContainsKey(meaning))
        {
            throw new ClauseFormatException($"{meaning} says a place, so it's {PlaceRoles}, not {role}:{meaning}.", number);
        }
        if (IndefiniteWords.TryGetValue(meaning, out var indefinite))
        {
            throw new ClauseFormatException(Indefinites.RolesOf(indefinite.Kind) is not null
                ? $"{Placed(meaning, indefinite.Kind)}, not {role}:{meaning}."
                : $"{meaning} isn't an adverb: it fills a role, as in subject:{meaning}.", number);
        }
        if (PlaceAdverbs.Contains(meaning))
        {
            throw new ClauseFormatException($"'{meaning}' can't be said yet: words for where besides here, there and somewhere, like inside and upstream, wait on spatial adpositions.", number);
        }
        if (features.Count > 0)
        {
            throw new ClauseFormatException($"'{meaning}' can't take {features[0].Gloss}: an adverb takes no glosses. For 'very slowly', write manner:slow.INTS.", number);
        }
        return new Adverb(meaning, kind);
    }

    //a word, a noun phrase or a coordination; a clause in brackets is only ever an object, which
    //ParseRoles reads before it gets here
    private static Argument ParseArgument(SyntaxValue value, int number)
    {
        if (value.Value is SyntaxClause clause)
        {
            throw Misplaced($"'{clause.Text}'", number);
        }
        if (value.Value is SyntaxGroup group && Coordinating(group, number) is var (conjunction, items))
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
        if (value.Value is SyntaxGroup phrase)
        {
            return ParseModifiedNounPhrase(phrase, number);
        }
        var token = value.Text;
        if (token == "_")
        {
            return new Gap(null);
        }
        if (ParsePronoun(token, number) is { } pronoun)
        {
            return pronoun;
        }
        if (ParseDemonstrativePronoun(token, number) is { } demonstrative)
        {
            return demonstrative;
        }
        var (noun, features) = ParseWord(token, number);
        if (QuestionAdverbs.ContainsKey(noun))
        {
            throw new ClauseFormatException(OnItsOwn(noun), number);
        }
        if (PlaceDemonstratives.ContainsKey(noun))
        {
            throw new ClauseFormatException($"{noun} says a place, so it's {PlaceRoles}, on its own.", number);
        }
        if (ParseIndefinite(noun, features, number) is { } indefinite)
        {
            return indefinite.Indefinite is not { } kind || Indefinites.RolesOf(kind) is null ? indefinite
                : throw new ClauseFormatException($"{Placed(noun, kind)}, on its own.", number);
        }
        if (QuestionWords.TryGetValue(noun, out var asked))
        {
            return features.Count == 0 ? new QuestionWord(asked)
                : throw new ClauseFormatException($"{noun} can't take {features[0].Gloss}: a question word takes no glosses.", number);
        }
        Number? count = null;
        Definiteness? definiteness = null;
        Distance? distance = null;
        foreach (var feature in features)
        {
            switch (feature)
            {
                case Distance when distance is not null:
                    throw new ClauseFormatException($"'{noun}' has more than one demonstrative.", number);
                case Distance when definiteness is not null:
                case Definiteness when distance is not null:
                    throw new ClauseFormatException($"'{noun}' takes a demonstrative or {(definiteness ?? feature).Gloss}, not both: a demonstrative already makes it definite.", number);
                case Distance d:
                    distance = d;
                    break;
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
                    throw new ClauseFormatException($"'{noun}' can't take {feature.Gloss}: it's the verb that's negated, or for 'no {noun}', write ({noun} no).", number);
                case Quantification:
                    throw new ClauseFormatException($"'{noun}' can't take {feature.Gloss}: write NDF for 'a {noun}', and a quantifier as a word, as in ({noun} some) or ({noun} every).", number);
                default:
                    throw new ClauseFormatException($"'{noun}' can't take {feature.Gloss} ({feature.Name}) yet: nouns have only number, definiteness and a demonstrative.", number);
            }
        }
        return new NounPhrase(noun, count ?? Number.Singular, Definiteness: definiteness, Demonstrative: distance);
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

    //a guess at the gloss meant, from its name: PAST or past for PST, PERFECTIVE for PFV
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
