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
  it's written with, and this builds what that means into a Sentence, refusing whatever the
  generator can't express yet, never dropping it
 */
public static class ClauseFile
{
    public record Entry(string Translation, Utterance Utterance, int Line);

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
            //an entry's first gloss line can be headed by a conjunction, joining events, but never by a subordinator
            else if (SubordinatorOf(line) is not null || translation is null && ConjunctionOf(line) is not null)
            {
                //a line starting with a conjunction joins its clause to the ones above it, and one
                //starting with a subordinator puts its clause under them
                if (!continuing)
                {
                    throw new ClauseFormatException($"A line starting with '{line.Trim().Split(' ')[0]}' joins a clause to the one above it, so it can't come first.", number);
                }
                var previous = entries[^1];
                var message = previous.Utterance.Value is Message { Content.Value: Sentence } said ? said
                    : throw new ClauseFormatException($"A line starting with '{line.Trim().Split(' ')[0]}' joins a clause to the one above it, which a fragment with no verb isn't.", number);
                var main = (Sentence)message.Content.Value!;
                var rest = line.Trim()[(line.Trim().IndexOf(' ') + 1)..];
                Sentence joined = ConjunctionOf(line) is { } conjunction
                    ? ToCoordinated(conjunction, main, Ungapped(ParseClause(ClauseSyntax.Read(rest, number), number, null,
                        "Say who's addressed on the first line, not on one joined to it: the whole sentence is said to them."), number), number)
                    : ToSubordinated(main, SubordinatorOf(line).GetValueOrDefault(), ClauseSyntax.Read(rest, number), number);
                entries[^1] = previous with { Utterance = message with { Content = joined } };
            }
            else if (translation is { } english)
            {
                entries.Add(new Entry(english.Text, ParseUtterance(ClauseSyntax.Read(line, number), number), number));
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
    private static Subordinated ToSubordinated(Sentence main, Subordinator subordinator, SyntaxClause line, int number)
    {
        var purpose = subordinator == Subordinator.Purpose;
        var shares = purpose && !line.Items.Any(i => i.Role == "subject");
        //a purpose clause's time is the main clause's
        if (purpose && line.Items is [{ Role: null, Value.Value: SyntaxWord verb }, ..]
            && ParseWord(verb.Text, number).Features.FirstOrDefault(f => f is Tense or EpistemicModality) is { } time)
        {
            throw new ClauseFormatException($"A purpose clause can't take {time.Gloss}: the clause it's under says when.", number);
        }
        var clause = Ungapped(ParseClause(line, number, shares ? new Shared() : (Argument?)null, "Say who's addressed in the main clause, not in one under it."), number);
        return clause.Value switch
        {
            Clause { Value: Directive } => throw new ClauseFormatException($"A clause under another can't be a command: {subordinator.ToString().ToLowerInvariant()} takes a clause with a subject.", number),
            Clause { Value: Interrogative } => throw new ClauseFormatException("A clause under another can't be a question: ask it in the main clause.", number),
            Clause { Value: Declarative { Propositional: var said } } => new Subordinated(main, purpose
                ? new Purpose(said.Proposition.StateOfAffairs)
                : new Circumstance(Relations[subordinator], said)),
            _ => throw new ClauseFormatException("A clause under another is one clause.", number),
        };
    }

    //how a circumstance bears on the clause it's under, by the subordinator that says it
    private static readonly Dictionary<Subordinator, Relation> Relations = new()
    {
        [Subordinator.If] = Relation.If,
        [Subordinator.When] = Relation.When,
        [Subordinator.Before] = Relation.Before,
        [Subordinator.After] = Relation.After,
        [Subordinator.Until] = Relation.Until,
        [Subordinator.Because] = Relation.Because,
    };

    //clauses joined by a conjunction, neither of them a question yet
    private static Coordinated<Sentence> ToCoordinated(Conjunction conjunction, Sentence first, Sentence second, int number) =>
        IsQuestion(first) || IsQuestion(second)
            ? throw new ClauseFormatException("A question can't be joined to another clause yet.", number)
            : new Coordinated<Sentence>(conjunction, first, second);

    private static bool IsQuestion(Sentence statement) => statement.Value switch
    {
        Clause { Value: Interrogative } => true,
        Coordinated<Sentence> c => IsQuestion(c.First) || IsQuestion(c.Second),
        Subordinated s => IsQuestion(s.Main),
        _ => false,
    };

    //each noun these clauses use, with the English of the first clause to use it
    public static IReadOnlyList<(string Noun, string Translation)> Nouns(IEnumerable<Entry> entries) =>
        [.. entries.SelectMany(e => ArgumentsOf(e.Utterance).SelectMany(NounsIn).Select(n => (Noun: n, e.Translation))).DistinctBy(n => n.Noun)];

    //what an utterance says, then who it's said to
    private static IEnumerable<Argument> ArgumentsOf(Utterance utterance) => utterance switch
    {
        Message message => [.. ArgumentsOf(message.Content), .. message.Addressee is { } addressee ? [addressee] : Array.Empty<Argument>()],
        Call call => [call.Addressee],
    };

    private static IEnumerable<Argument> ArgumentsOf(Content content) => content switch
    {
        Sentence statement => ArgumentsOf(statement),
        Fragment fragment => ArgumentsOf(fragment),
    };

    private static IEnumerable<Argument> ArgumentsOf(Fragment fragment) => fragment switch
    {
        Standalone standalone => [standalone.Argument],
        Oblique oblique => [oblique.Argument],
        Heading heading => [heading.Label, heading.Title],
        Interjection or Answer => [],
    };

    private static IEnumerable<Argument> ArgumentsOf(Sentence statement) => statement switch
    {
        Clause c => c.StateOfAffairs.Events.SelectMany(ArgumentsOf),
        Coordinated<Sentence> c => [.. ArgumentsOf(c.First), .. ArgumentsOf(c.Second)],
        Subordinated s => [.. ArgumentsOf(s.Main), .. ArgumentsOf(s.Clause)],
    };

    private static IEnumerable<Argument> ArgumentsOf(AdverbialClause clause) => clause switch
    {
        Purpose purpose => ArgumentsOf(purpose.StateOfAffairs),
        Circumstance circumstance => ArgumentsOf(circumstance.Propositional),
    };

    private static IEnumerable<Argument> ArgumentsOf(Complement complement) => complement switch
    {
        Said said => ArgumentsOf(said.Propositional),
        Wanted wanted => ArgumentsOf(wanted.StateOfAffairs),
    };

    private static IEnumerable<Argument> ArgumentsOf(Propositional propositional) => ArgumentsOf(propositional.Proposition.StateOfAffairs);

    private static IEnumerable<Argument> ArgumentsOf(StateOfAffairs layers) => layers.Events.SelectMany(ArgumentsOf);

    //an event's arguments, with those of the clauses its adjectives and manners are compared with
    private static IEnumerable<Argument> ArgumentsOf(Event @event) => [.. @event.Arguments, .. @event.Adjectives.SelectMany(a => a.Clauses).SelectMany(ArgumentsOf)];

    private static IEnumerable<Argument> Given(Argument? argument) => argument.HasValue ? [argument.GetValueOrDefault()] : [];

    private static IEnumerable<string> NounsIn(Argument argument) => argument switch
    {
        //an indefinite pronoun's noun is the language's to say, and no noun of the text's
        NounPhrase { Head.Value: IndefinitePronoun } n => n.Relative is { } relative ? ArgumentsOf(relative).SelectMany(NounsIn) : [],
        NounPhrase { Head.Value: Noun noun } n => [noun.Meaning, .. Given(noun.Possessor).SelectMany(NounsIn), .. noun.Adjectives.SelectMany(a => a.Arguments).SelectMany(NounsIn),
            .. noun.Adjectives.SelectMany(a => a.Clauses).SelectMany(ArgumentsOf).SelectMany(NounsIn),
            .. n.Relative is { } relative ? ArgumentsOf(relative).SelectMany(NounsIn) : []],
        Pronoun => [],
        QuestionWord or DemonstrativePronoun or DemonstrativePlace => [],
        Coordination c => c.Items.SelectMany(NounsIn),
        Complement c => ArgumentsOf(c).SelectMany(NounsIn),
        Quotation q => ArgumentsOf(q.Utterance).SelectMany(NounsIn),
        Gap or RelativeWord or Shared => [],
    };

    /*
      the gaps in a statement outside its relative clauses, wherever they are: in its roles, in noun
      phrases, joined to others, or in clauses inside it. A complement's shared subject is no gap of
      its own, even where the main clause's is one
     */
    private static int Gaps(Sentence statement) => ArgumentsOf(statement).Sum(Gaps);

    private static int Gaps(Argument argument) => argument switch
    {
        Gap => 1,
        NounPhrase { Head.Value: Noun noun } => Given(noun.Possessor).Sum(Gaps) + noun.Adjectives.SelectMany(a => a.Arguments).Sum(Gaps),
        NounPhrase => 0,
        Coordination c => c.Items.Sum(Gaps),
        Complement c => ArgumentsOf(c).Sum(Gaps),
        Quotation q => ArgumentsOf(q.Utterance).Sum(Gaps),
        Pronoun or DemonstrativePronoun or QuestionWord or DemonstrativePlace or RelativeWord or Shared => 0,
    };

    //a clause that isn't relative has no gap, which marks only where a relative clause's noun goes,
    //and no degree gap, which goes only in a standard
    private static Sentence Ungapped(Sentence statement, int number) => Gaps(statement) == 0 ? WithoutDegreeGap(statement, number)
        : throw new ClauseFormatException("_ marks the role a relative clause's noun fills in it, so it goes only inside relative:[...], as in object:(net.DEF relative:[mend.PST  subject:3SG.F  object:_]).", number);

    //the degree gaps in a statement's own events, outside the clauses inside it, which are read on their own
    private static int DegreeGaps(Sentence statement) => statement switch
    {
        Clause c => c.StateOfAffairs.Events.Sum(e => e.Adjectives.Count(a => a.Grade is { Value: DegreeGap })),
        Coordinated<Sentence> c => DegreeGaps(c.First) + DegreeGaps(c.Second),
        Subordinated s => DegreeGaps(s.Main),
    };

    private static Sentence WithoutDegreeGap(Sentence statement, int number) => DegreeGaps(statement) == 0 ? statement : throw DegreeGapOutsideStandard(number);

    private static ClauseFormatException DegreeGapOutsideStandard(int number) =>
        new($"_ as an adjective: or manner: goes only in a clause in standard:[...], where it stands for what's compared, as in {StandardExample}.", number);

    private const string StandardExample = "adjective:warm.CMPR  standard:[be.ADVS  subject:3SG.N  adjective:_] for 'warmer than it should be'";

    /*
      a clause a comparison is with, as in 'warmer than it should be': a clause of its own that says
      something, with _ as its one adjective: or manner:, where its degree goes. The clause it's in
      gives _ its word
     */
    private static Propositional ParseStandard(SyntaxClause written, int number)
    {
        if (written.Items.Count == 0)
        {
            throw new ClauseFormatException($"A standard:[...] needs a clause inside its brackets, as in {StandardExample}.", number);
        }
        var clause = ParseClause(written, number, null, "A standard can't be said to someone: say who's addressed in the main clause.");
        return clause.Value switch
        {
            Clause { Value: Directive } => throw new ClauseFormatException("A standard can't be a command.", number),
            Clause { Value: Interrogative } => throw new ClauseFormatException("A standard can't be a question.", number),
            Clause c when c.StateOfAffairs.Events.Skip(1).Any() => throw new ClauseFormatException("A standard can't join events yet.", number),
            _ when Gaps(clause) > 0 => throw new ClauseFormatException($"A standard's _ is its adjective: or manner:, not one of its roles, as in {StandardExample}.", number),
            _ when DegreeGaps(clause) != 1 => throw new ClauseFormatException($"A standard:[...] has one _, as its adjective: or manner:, as in {StandardExample}.", number),
            Clause { Value: Declarative stated } => stated.Propositional,
            _ => throw new ClauseFormatException("A standard is one clause.", number),
        };
    }

    //a standard's clause with the word its degree gap stands for
    private static Propositional Gapped(Propositional clause, string meaning) => clause.Rebuilt(p => p with
    {
        StateOfAffairs = p.StateOfAffairs.Rebuilt(e => e with
        {
            Predicate = e.Predicate.Value is Being { Attribute.Value: Adjective { Grade.Value: DegreeGap } gap } being ? being with { Attribute = gap with { Meaning = meaning } } : e.Predicate,
            Adverbs = [.. e.Adverbs.Select(a => a.Value is Manner { Adjective: { Grade.Value: DegreeGap } gap } ? new Manner(gap with { Meaning = meaning }) : a)],
        }),
    });

    //a standard that's an argument: anything but a question word, yet
    private static Argument StandardArgument(SyntaxValue value, int number) => ParseArgument(value, number) is var standard && standard.Value is QuestionWord
        ? throw new ClauseFormatException("A comparison can't ask about its standard yet.", number)
        : standard;

    /*
      the roles of a relative clause's own that its gap is in. It can be its subject, its object, the
      agent of a passive or an oblique, but not what it compares something with
     */
    private static IEnumerable<string> GapRoles(Sentence statement)
    {
        return statement.Value switch
        {
            Clause c => [.. c.StateOfAffairs.Events.SelectMany(e => e.Fillers).Where(f => f.Argument.Value is Gap).Select(NameOf),
                .. c.StateOfAffairs.Events.Any(e => e.Predicate.Value is Being { Attribute.Value: Adjective { Grade.Value: Comparative { Standard.Value: Argument { Value: Gap } } } }) ? ["standard"] : Array.Empty<string>()],
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

    //a line with a verb first is a clause, and one that starts with a role has none, so it's a fragment
    private static Utterance ParseUtterance(SyntaxClause written, int number)
    {
        if (written.Items is [{ Role: not null }, ..])
        {
            return ParseFragment(written, number);
        }
        var statement = Ungapped(ParseClause(written, number, null, out var addressee), number);
        return new Message(statement, addressee);
    }

    /*
      a line with no verb: a phrase on its own, as noun:, or an oblique, as goal: or beneficiary:,
      either of them said to someone or not, a heading, which is its label: and its title as noun:,
      or just addressee:, calling someone. It asks nothing, and has no gap, which only a relative
      clause has
     */
    private static Utterance ParseFragment(SyntaxClause written, int number)
    {
        const string Says = "A line with no verb is a fragment, which says a noun:, an oblique such as goal: or beneficiary:, a heading's label: and noun:, an interjection:, an answer:, or who's called, as addressee:";
        NounPhrase? addressee = null;
        //an interjection or an answer, which are said alike
        Fragment? exclamation = null;
        Argument? label = null;
        Argument? noun = null;
        Oblique? oblique = null;
        foreach (var item in written.Items)
        {
            if (item.Role is not { } name)
            {
                throw new ClauseFormatException($"'{item.Text}' needs a role. {Says}. A clause starts with its verb.", number);
            }
            var (role, configuration) = ParseConfiguration(name, number);
            var isOblique = ObliqueRoles.TryGetValue(role, out var obliqueRole);
            if (configuration != Configuration.At && !(isOblique && Oblique.Spatial.Contains(obliqueRole)))
            {
                throw new ClauseFormatException($"Only a role saying where can say in, on or under, as location., goal., source. and route. do, not {role}.", number);
            }
            if (role is "addressee" && addressee is not null || role is "interjection" or "answer" && exclamation.HasValue || role is "label" && label.HasValue || role is "noun" && noun.HasValue || isOblique && oblique is not null)
            {
                throw new ClauseFormatException(isOblique ? "A fragment says one oblique."
                    : role is "interjection" or "answer" ? "A fragment says one interjection: or answer:." : $"The fragment has more than one {role}.", number);
            }
            switch (role)
            {
                case "addressee":
                    addressee = ToAddressee(ParseArgument(item.Value, number), number);
                    break;
                case "interjection":
                    exclamation = ParseInterjection(item.Value, number);
                    break;
                case "answer":
                    exclamation = ParseAnswer(item.Value, number);
                    break;
                case "label":
                    label = Standing(ParseArgument(item.Value, number), number);
                    break;
                case "noun":
                    noun = Standing(ParseArgument(item.Value, number), number);
                    break;
                case var _ when isOblique:
                    oblique = new Oblique(obliqueRole, Standing(ParseObliqueArgument(item.Value, obliqueRole, configuration, number), number), configuration);
                    break;
                default:
                    throw new ClauseFormatException($"{Says}, not {role}:. A clause starts with its verb.", number);
            }
        }
        if (oblique is not null && (noun.HasValue || label.HasValue))
        {
            throw new ClauseFormatException("A fragment says a noun: or an oblique, not both.", number);
        }
        if (exclamation is { } exclaimed)
        {
            return noun.HasValue || label.HasValue || oblique is not null
                ? throw new ClauseFormatException("An interjection: or answer: is said on its own, or to someone with addressee:, not with a noun: or an oblique yet.", number)
                : new Message(exclaimed, addressee);
        }
        if (label is { } heading)
        {
            return noun is { } title
                ? addressee is null ? new Message((Fragment)new Heading(heading, title), null)
                    : throw new ClauseFormatException("A heading is said to no one, so it takes no addressee:.", number)
                : throw new ClauseFormatException("A heading's label: needs its title as noun:, as in label:item  noun:(key rusty).", number);
        }
        if (noun is { } standing)
        {
            return new Message((Fragment)new Standalone(standing), addressee);
        }
        if (oblique is { } placed)
        {
            return new Message((Fragment)placed, addressee);
        }
        //every item says something, so a line with none of the others calls someone
        return new Call(addressee ?? throw new InvalidOperationException("A fragment's line has at least one item."));
    }

    //an interjection is its meaning, a word with no glosses, as in interjection:hello, but for yes
    //and no, which answer
    private static Interjection ParseInterjection(SyntaxValue value, int number) => value.Value switch
    {
        SyntaxWord { Text: var text } when Answers.ContainsKey(text.ToLowerInvariant()) =>
            throw new ClauseFormatException($"{text} answers, so it's answer:{text.ToLowerInvariant()}, not interjection:{text}.", number),
        SyntaxWord { Text: var text } when !text.Contains('.') && text != "_" => new Interjection(text.ToLowerInvariant()),
        _ => throw new ClauseFormatException($"An interjection is a word with no glosses, as in interjection:hello, not '{value.Text}'.", number),
    };

    private static readonly Dictionary<string, Polarity> Answers = new()
    {
        ["yes"] = Polarity.Affirmative,
        ["no"] = Polarity.Negative,
    };

    //an answer is yes or no
    private static Answer ParseAnswer(SyntaxValue value, int number) =>
        value.Value is SyntaxWord { Text: var text } && Answers.TryGetValue(text, out var polarity)
            ? new Answer(polarity)
            : throw new ClauseFormatException($"An answer is answer:yes or answer:no, not answer:{value.Text}.", number);

    //what a fragment says, which asks nothing yet and has no gap
    private static Argument Standing(Argument argument, int number) => argument.Value switch
    {
        QuestionWord => throw new ClauseFormatException("A fragment can't ask anything yet, as 'where?' does.", number),
        _ when Gaps(argument) > 0 => throw new ClauseFormatException("_ marks the role a relative clause's noun fills in it, so it goes only inside relative:[...].", number),
        _ => argument,
    };

    /*
      a relative clause: a clause of its own with one gap, in one of its own roles rather than in a
      noun phrase or a clause inside it, where its noun goes. It says something of its noun, so it
      can't be a command, a question or said to someone
     */
    private static Propositional ParseRelative(SyntaxValue value, int number)
    {
        const string Example = "relative:[mend.PST  subject:3SG.F  object:_]";
        if (value.Value is not SyntaxClause written || written.Items.Count == 0)
        {
            throw new ClauseFormatException($"relative: is a clause in brackets with _ where its noun goes, as in (net.DEF {Example}), not '{value.Text}'.", number);
        }
        var clause = ParseClause(written, number, null, "A relative clause can't be said to someone: say who's addressed in the main clause.");
        var roles = GapRoles(clause).ToList();
        return clause.Value switch
        {
            Clause { Value: Directive } => throw new ClauseFormatException("A relative clause can't be a command: it says something of its noun.", number),
            Clause { Value: Interrogative } => throw new ClauseFormatException("A relative clause can't be a question.", number),
            Clause c when c.StateOfAffairs.Events.Skip(1).Any() => throw new ClauseFormatException("A relative clause can't join events yet.", number),
            _ when roles.Count == 0 && Gaps(clause) == 0 => throw new ClauseFormatException($"A relative clause needs _ in the role its noun fills in it, as in {Example}.", number),
            _ when roles.Count != 1 || Gaps(clause) != 1 => throw new ClauseFormatException("A relative clause has one _, in a role of its own, not inside a noun phrase or another clause in it.", number),
            _ when roles[0] == "standard" => throw new ClauseFormatException("A relative clause's noun can't be what it compares something with yet.", number),
            _ when DegreeGaps(clause) > 0 => throw DegreeGapOutsideStandard(number),
            Clause { Value: Declarative stated } => stated.Propositional,
            _ => throw new ClauseFormatException("A relative clause is one clause.", number),
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

    //a clause that can't be said to anyone, as one inside another can't, with what to say where it is
    private static Sentence ParseClause(SyntaxClause written, int number, Argument? impliedSubject, string unaddressable)
    {
        var statement = ParseClause(written, number, impliedSubject, out var addressee);
        return addressee is null ? statement : throw new ClauseFormatException(unaddressable, number);
    }

    //a clause, and who it's said to, if anyone. A subject that isn't said is the given one, if there is one
    private static Sentence ParseClause(SyntaxClause written, int number, Argument? impliedSubject, out NounPhrase? addressee)
    {
        if (written.Items is not [{ Role: null, Value.Value: SyntaxWord head }, ..])
        {
            throw written.Items.Count == 0
                ? new ClauseFormatException("A clause needs a verb.", number)
                : new ClauseFormatException($"A clause starts with its verb, not with '{written.Items[0].Text}'.", number);
        }
        var (verb, glosses) = ParseWord(head.Text, number);
        if (Conjunctions.TryGetValue(verb, out var conjunction))
        {
            return ParseJoined(conjunction, verb, glosses, written, number, impliedSubject, out addressee);
        }
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
        addressee = roles.Arguments.TryGetValue("addressee", out var called) ? ToAddressee(called, number) : null;
        var standard = roles.Standard;
        if (roles.Arguments.ContainsKey("agent") && !features.Passive)
        {
            throw new ClauseFormatException($"Only a passive verb takes an agent:, as in {verb}.PASS subject:light agent:figure for 'the light is {verb}ed by a figure'.", number);
        }
        if (features.Passive && (features.Imperative || verb == "be"))
        {
            throw new ClauseFormatException(verb == "be" ? "be can't be passive." : "A command can't be passive yet.", number);
        }
        /*
          a standard: goes with the clause's one comparative, be's adjective: or a manner:. A comparison
          of the clause, be's or a manner's with a standard, can't be a command, take a modal or be
          negated yet, since a language that compares with two clauses would have to say each
         */
        var manners = roles.ComparedManners;
        var beCompared = roles.Attribute is { Value: Adjective { Grade.Value: Comparative } };
        if (features.Imperative && (roles.Compared || standard.HasValue))
        {
            throw new ClauseFormatException("A command can't compare yet.", number);
        }
        if (standard.HasValue && !beCompared && manners.Count == 0)
        {
            throw new ClauseFormatException(roles.Attribute switch
            {
                { Value: Adjective adjective } => $"A standard: compares a comparative adjective:, as in adjective:{adjective.Meaning}.CMPR for 'more {adjective.Meaning} than'.",
                { Value: AdjectiveCoordination } when roles.Compared => "Adjectives joined can't share a standard: yet, as in 'older and wiser than his brother'.",
                _ when verb == "be" => "A standard: compares an adjective:, as in adjective:tall.CMPR standard:(man.PL) for 'taller than the men'.",
                _ => "A standard: compares a comparative adjective: or manner:, as in manner:slow.CMPR standard:cat for 'more slowly than the cat'.",
            }, number);
        }
        if (standard.HasValue && (beCompared ? 1 : 0) + manners.Count > 1)
        {
            throw new ClauseFormatException("A standard: goes with one comparative, and this clause has more than one.", number);
        }
        if (standard.HasValue && !beCompared && features.Negated)
        {
            throw new ClauseFormatException("A comparison can't be negated yet.", number);
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
          is, or the perfect over a modal, as in 'she has had to leave', which can't be said yet
         */
        if (features.Modality is SituationalModality situational && features.Aspect is { } aspect && (aspect == Aspect.Perfect || aspect == Aspect.Progressive))
        {
            throw new ClauseFormatException($"{situational.Gloss} can't take {aspect.Gloss} yet, as in 'should have gone' or 'should be waiting'. " +
                $"A modal that's sure or unsure of what happened, as in 'must have gone' or 'might be waiting', is {EpistemicModality.Presumptive.Gloss} or {EpistemicModality.Dubitative.Gloss}.", number);
        }
        var asked = CheckQuestion(features, roles, adverb, number);

        var subject = features.Imperative ? ToDirectiveSubject(verb, features, roles, addressee, number) : ToSubject(roles, impliedSubject, number);
        var predicate = ToPredicate(verb, features, roles, number);
        IReadOnlyList<Adverbial> adverbs = roles.Adverbs;
        if (beCompared)
        {
            //not in a command, which the checks above make sure of
            predicate = ToCompared((Being)predicate.Value!, standard, features, number);
        }
        else if (standard.HasValue)
        {
            //the one comparative manner, which the checks above make sure of
            adverbs = [.. adverbs.Select((a, i) => i == manners[0] ? new Manner(Compared(((Manner)a.Value!).Adjective, standard.GetValueOrDefault())) : a)];
        }
        var @event = new Event(predicate, subject, verb == "be" ? CopularObliques(roles) : roles.AllObliques,
            [.. adverbs, .. adverb is { } why ? [new QuestionAdverbial(why)] : Array.Empty<Adverbial>()]);
        //a negative indefinite says the clause is negated, and a language whose verb takes negation
        //beside one adds it itself
        if (features.Negated && @event.Arguments.Any(Indefinites.IsNegative))
        {
            throw new ClauseFormatException("A clause with nobody, nothing, nowhere or no already says 'not', so its verb takes no NEG, as in come.PST subject:nobody for 'nobody came'.", number);
        }
        //each operator wraps what's written before it, as glosses put a morpheme outside what it scopes over
        StateOfAffairs layers = @event;
        foreach (var @operator in features.Operators)
        {
            layers = @operator switch
            {
                Polarity => new Negated(layers),
                SituationalModality modal => new Modalized(modal, layers),
                Aspect aspected => new Aspected(aspected, layers),
                _ => throw new InvalidOperationException($"{@operator.Gloss} scopes over nothing here."),
            };
        }
        if (features.Imperative)
        {
            return (Clause)new Directive(layers);
        }
        //a judgment wraps the proposition, and a negation written after it denies the judgment
        Propositional proposition = new Proposition(layers, features.Tense ?? Tense.Present);
        if (features.Modality is EpistemicModality epistemic)
        {
            proposition = new Judged(epistemic, proposition);
            proposition = features.Denied ? new Denied(proposition) : proposition;
        }
        return asked ? (Clause)new Interrogative(proposition) : new Declarative(proposition);
    }

    /*
      states of affairs joined under one tense and sentence type, as in 'she turns and walks away':
      the conjunction heads them, glossed with what they share, the tense, the mood and any aspect
      over them all, with the subject they share, if they do, and who's addressed, and each event
      goes in brackets with its own roles and its own negation, modal, aspect or passive. An event
      with no subject has the shared one, or a command's 'you'. A negation, a modal or a question
      over them all can't be said yet, since saying it on each changes what it says, and but joins
      only clauses
     */
    private static Sentence ParseJoined(Conjunction conjunction, string word, IReadOnlyList<Grammeme> glosses, SyntaxClause written, int number, Argument? impliedSubject, out NounPhrase? addressee)
    {
        const string Example = "and.PST  subject:3SG.F  [turn]  [walk  goal:away]";
        //a verb after it is a clause joined to the one above, which the line can't follow when it's first
        if (written.Items.Skip(1).FirstOrDefault() is { Role: null, Value.Value: SyntaxWord })
        {
            throw new ClauseFormatException($"A line starting with '{word}' joins a clause to the one above it, so it can't come first.", number);
        }
        if (conjunction == Conjunction.But)
        {
            throw new ClauseFormatException("but joins only clauses, on a line of its own starting with but.", number);
        }
        var features = ParseVerbFeatures(word, glosses, number);
        if (features.Question || features.Modality is not null || features.Negated)
        {
            throw new ClauseFormatException("A negation, a modal or Q over joined events can't be said yet, since saying it on each changes what it says: "
                + "write it on each event, or join whole clauses on lines of their own.", number);
        }
        if (features.Imperative && features.Aspect is not null || features.Passive)
        {
            throw new ClauseFormatException(features.Passive ? "PASS goes on the event that's passive, not on the conjunction." : "A command has no aspect.", number);
        }
        List<SyntaxClause> events = [];
        Argument? subject = impliedSubject;
        addressee = null;
        foreach (var item in written.Items.Skip(1))
        {
            switch (item)
            {
                case { Role: null, Value.Value: SyntaxClause @event }:
                    events.Add(@event);
                    break;
                case { Role: "subject" }:
                    subject = ParseArgument(item.Value, number);
                    break;
                case { Role: "addressee" }:
                    addressee = ToAddressee(ParseArgument(item.Value, number), number);
                    break;
                default:
                    throw new ClauseFormatException($"Joined events share only their subject: and who's addressed, on the conjunction; '{item.Text}' goes in the brackets of the event it's part of.", number);
            }
        }
        if (events.Count < 2)
        {
            throw new ClauseFormatException($"A conjunction heading a clause joins two events or more, each in brackets, as in {Example}.", number);
        }
        var shared = features.Imperative ? new Pronoun(Person.Second, Told(subject, addressee, number)) : subject;
        StateOfAffairs? layers = null;
        foreach (var @event in events)
        {
            if (@event.Items is [{ Role: null, Value.Value: SyntaxWord verb }, ..]
                && ParseWord(verb.Text, number).Features.FirstOrDefault(f => f is Tense or EpistemicModality || f == Mood.Imperative || f == Mood.Interrogative) is { } whole)
            {
                throw new ClauseFormatException($"{whole.Gloss} is what joined events share, so it goes on the conjunction, as in and.{whole.Gloss}, not on one of them.", number);
            }
            var clause = ParseClause(@event, number, shared, "Who's addressed goes on the conjunction, not on one of the events it joins.");
            var joined = clause.Value is Clause { Value: Declarative { Propositional.Value: Proposition proposition } }
                ? proposition.StateOfAffairs
                : throw new ClauseFormatException("An event in brackets says something, and can't ask yet.", number);
            layers = layers is { } before ? new Coordinated<StateOfAffairs>(conjunction, before, joined) : joined;
        }
        //an aspect on the conjunction is over them all
        foreach (var aspect in features.Operators.OfType<Aspect>())
        {
            layers = new Aspected(aspect, layers!.Value);
        }
        return features.Imperative
            ? (Clause)new Directive(layers!.Value)
            : new Declarative(new Proposition(layers!.Value, features.Tense ?? Tense.Present));
    }

    //the question words, in the roles they ask about
    private static readonly Dictionary<string, InterrogativeWord> QuestionWords = Enum.GetValues<InterrogativeWord>().ToDictionary(w => w.ToString().ToLowerInvariant());

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
            if (role == "like")
            {
                throw new ClauseFormatException($"A question can't ask what something is like yet, as in like:{WordOf(argument)}.", number);
            }
            if (role == "duration")
            {
                throw new ClauseFormatException($"A question can't ask for how long yet, as in duration:{WordOf(argument)}.", number);
            }
            if (word == InterrogativeWord.Where && role is not ("location" or "goal" or "source"))
            {
                throw new ClauseFormatException($"where asks about a place, so it's location:where, goal:where for 'where to' or source:where for 'where from', not {role}:where.", number);
            }
            if ((word == InterrogativeWord.When) != (role == "time"))
            {
                throw new ClauseFormatException(word == InterrogativeWord.When
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

    /*
      'Marta is taller than the men': be's comparative adjective, said of the subject more than of the
      standard, or with no standard, 'Marta is taller', more than of something already known
     */
    private static Being ToCompared(Being being, Standard? standard, VerbFeatures features, int number)
    {
        if (features.Negated)
        {
            throw new ClauseFormatException("A comparison can't be negated yet.", number);
        }
        return standard is { } than ? being with { Attribute = Compared((Adjective)being.Attribute.Value!, than) } : being;
    }

    //a comparative adjective compared with this standard, whose degree gap, if it's a clause, stands for the adjective
    private static Adjective Compared(Adjective adjective, Standard standard) => adjective.Grade is { Value: Comparative compared }
        ? adjective with { Grade = compared with { Standard = standard.Value is Propositional clause ? Gapped(clause, adjective.Meaning) : standard } }
        : throw new ArgumentException("Only a comparative adjective takes a standard.", nameof(adjective));

    //the word a standard's clause compares, written _, until the clause it's in gives it its own
    private static Adjective DegreeGapped => new("_", new DegreeGap());

    //who a clause is said to: a noun phrase, bare, since vocatives take no article
    private static NounPhrase ToAddressee(Argument argument, int number) => argument.Value switch
    {
        NounPhrase { Head.Value: Noun { Determiner: { Value: Definiteness article } } noun } =>
            throw new ClauseFormatException($"An addressee takes no article: write addressee:{noun.Meaning}, not {article.Gloss}.", number),
        NounPhrase { Head.Value: Noun { Determiner: { Value: Distance demonstrative } } noun } =>
            throw new ClauseFormatException($"An addressee takes no demonstrative yet: write addressee:{noun.Meaning}, not {demonstrative.Gloss}.", number),
        NounPhrase phrase => phrase,
        _ => throw new ClauseFormatException("An addressee is a noun, as in addressee:marta. A clause said to 'you' needs none.", number),
    };

    /*
      a verb's glosses: its tense, its mood, the passive and its modal, if it has one, and the
      operators over its event, negation, a situational modal and aspect, innermost first, as glosses
      put a morpheme outside what it scopes over: swim.ABIL.NEG is 'can't swim', and sing.NEG.OBLIG
      'must not sing'. Aspect is the one written, if any, and the imperfective is no operator, since
      a state of affairs with no aspect has it
     */
    //a negation after an epistemic modal denies the judgment, as leave.PRF.DUB.NEG is 'can't have left'
    private record VerbFeatures(Tense? Tense, Aspect? Aspect, bool Imperative, bool Question, bool Passive, Modality? Modality, IReadOnlyList<Grammeme> Operators, bool Denied)
    {
        public bool Negated => Operators.Contains(Polarity.Negative) || Denied;
    }

    //the obliques keep the role they were written with, which is how be finds its location:
    private record ClauseRoles(Dictionary<string, Argument> Arguments, List<(string Role, Oblique Oblique)> Obliques, Attribute? Attribute, List<Adverbial> Adverbs, Standard? Standard)
    {
        public Argument? Object => Arguments.TryGetValue("object", out var argument) ? argument : (Argument?)null;
        public ValueList<Oblique> AllObliques => [.. Obliques.Select(o => o.Oblique)];

        //whether be compares its subject, with a comparative adjective: alone, as tall.CMPR is, or joined to others
        public bool Compared => Attribute is { Value: Adjective { Grade.Value: Comparative } }
            || Attribute is { Value: AdjectiveCoordination together } && together.Adjectives.Any(a => a.Grade is { Value: Comparative });

        //the indexes of the manners that are comparative, as manner:slow.CMPR is
        public List<int> ComparedManners => [.. Adverbs.Select((a, i) => (a, i)).Where(m => m.a.Value is Manner { Adjective.Grade.Value: Comparative }).Select(m => m.i)];
    }

    private static VerbFeatures ParseVerbFeatures(string verb, IReadOnlyList<Grammeme> glosses, int number)
    {
        if (glosses.FirstOrDefault(g => g is not (Tense or Aspect or Polarity or Modality) && g != Mood.Imperative && g != Mood.Interrogative && g != Voice.Passive) is { } unsupported)
        {
            throw Unsupported(verb, unsupported, number);
        }
        //a modal of either kind is one category here, as a verb takes one of them
        if (glosses.GroupBy(g => g is Modality ? typeof(Modality) : g.GetType()).FirstOrDefault(g => g.Count() > 1) is { } repeated)
        {
            throw new ClauseFormatException($"'{verb}' has more than one {repeated.Key.Name.ToLowerInvariant()}.", number);
        }
        var modality = glosses.OfType<Modality>().SingleOrDefault();
        var order = glosses.ToList();
        var denied = modality is EpistemicModality && order.IndexOf(Polarity.Negative) > order.IndexOf(modality);
        return new VerbFeatures(
            glosses.OfType<Tense>().SingleOrDefault(),
            glosses.OfType<Aspect>().SingleOrDefault(),
            glosses.Contains(Mood.Imperative),
            glosses.Contains(Mood.Interrogative),
            glosses.Contains(Voice.Passive),
            modality,
            [.. glosses.Where(g => g == Polarity.Negative && !denied || g is SituationalModality || g is Aspect aspect && aspect != Aspect.Imperfective)],
            denied);
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
        Attribute? attribute = null;
        Standard? standard = null;
        var quoted = false;
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
            if (arguments.ContainsKey(role) || obliques.Any(o => o.Role == role) || role is "noun" or "adjective" && attribute.HasValue)
            {
                throw new ClauseFormatException(role is "noun" or "adjective" ? "The clause has more than one noun or adjective." : $"The clause has more than one {role}.", number);
            }
            //a standard is an argument or a clause, which the clause's comparative takes
            if (role == "standard")
            {
                if (standard.HasValue)
                {
                    throw new ClauseFormatException("The clause has more than one standard.", number);
                }
                standard = value.Value is SyntaxClause than ? ParseStandard(than, number) : StandardArgument(value, number);
                continue;
            }
            if (verb == "be" && role is "noun" or "adjective" or "location" && (attribute.HasValue || obliques.Any(o => o.Role == "location")))
            {
                throw new ClauseFormatException("be says one thing of its subject: a noun:, an adjective: or a location:.", number);
            }
            if (role is "object" or "quote" && (quoted || role == "quote" && arguments.ContainsKey("object")))
            {
                throw new ClauseFormatException("The clause has more than one object: a quote: is what's said, in place of an object:.", number);
            }
            if (value.Value is SyntaxClause embedded)
            {
                arguments["object"] = role is "object" or "quote" ? ParseEmbedded(role, embedded, number)
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
                    adverbs.Add(value.Text == "_" ? new Manner(DegreeGapped) : ParseManner(value, number));
                    break;
                case "noun":
                    attribute = ParseArgument(value, number).Value switch
                    {
                        NounPhrase noun => noun,
                        QuestionWord => throw new ClauseFormatException($"be can't ask what its subject is yet, as in noun:{value.Text} for '{value.Text} are you?'.", number),
                        _ => throw new ClauseFormatException($"be can't say its subject is {value.Text} yet: the noun has to be a noun, not a pronoun.", number),
                    };
                    break;
                case "adjective" when value.Value is SyntaxGroup together:
                    attribute = ParseAdjectiveCoordination(together, number);
                    break;
                case "adjective":
                    attribute = value.Text == "_" ? DegreeGapped : ParseAdjective(value.Text, number);
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
        return new ClauseRoles(arguments, obliques, attribute, adverbs, standard);
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
    private static Pronoun ToDirectiveSubject(string verb, VerbFeatures features, ClauseRoles roles, NounPhrase? addressee, int number)
    {
        if (features.Tense is not null || features.Aspect is not null)
        {
            throw new ClauseFormatException($"'{verb}' can't take {(features.Tense as Grammeme ?? features.Aspect)!.Gloss}: a command has no tense or aspect.", number);
        }
        return new Pronoun(Person.Second, Told(roles.Arguments.TryGetValue("subject", out var subject) ? subject : (Argument?)null, addressee, number));
    }

    //a clause that isn't a command needs a subject
    private static Argument ToSubject(ClauseRoles roles, Argument? impliedSubject, int number) =>
        (roles.Arguments.TryGetValue("subject", out var said) ? said : impliedSubject) is { } subject
            ? subject
            : throw new ClauseFormatException("The clause needs a subject, as in subject:dog.", number);

    //how many a command is said to
    private static Number Told(Argument? subject, NounPhrase? addressee, int number)
    {
        if (subject is { } said)
        {
            return said.Value is Pronoun { Person: var person, Number: var count } && person == Person.Second ? count
                : throw new ClauseFormatException("A command is said to 'you', so its subject can only be 2SG or 2PL, as in subject:2PL.", number);
        }
        return addressee?.Head.Value is Noun { Number: var called } && called != Number.Singular ? Number.Plural : Number.Singular;
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
    private static Argument ParseEmbedded(string role, SyntaxClause embedded, int number)
    {
        if (role == "quote")
        {
            if (embedded.Items.Count == 0)
            {
                throw new ClauseFormatException("A quote: needs what was said inside its brackets.", number);
            }
            var said = ParseUtterance(embedded, number);
            return said.Value is Message { Content.Value: Fragment { Value: Heading } }
                ? throw new ClauseFormatException("A heading isn't said, so it can't be quoted: quote: takes a clause or a fragment.", number)
                : new Quotation(said);
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
        var clause = WithoutDegreeGap(ParseClause(rest, number, shares ? new Shared() : (Argument?)null, "Say who's addressed in the main clause, not in one inside it, unless it's quoted with quote:[...]."), number);
        return clause.Value switch
        {
            Clause { Value: Directive } => throw new ClauseFormatException($"A clause in object: can't be a command: write {(kind == ComplementKind.Event ? "to" : "that")} with a subject, or quote the command with quote:[...].", number),
            Clause { Value: Interrogative } => throw new ClauseFormatException("A question inside another clause can't be said yet, unless it's quoted with quote:[...].", number),
            Clause { Value: Declarative { Propositional: var said } } when kind == ComplementKind.Proposition => (Complement)new Said(said),
            Clause { Value: Declarative { Propositional.Value: Proposition wanted } } => (Complement)new Wanted(wanted.StateOfAffairs),
            Clause { Value: Declarative } => throw new ClauseFormatException("A clause starting with to can't take an epistemic modal: what's wanted isn't judged so or not.", number),
            _ => throw new ClauseFormatException("A clause in object: is one clause.", number),
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
            return new Being(CopularAttribute(roles, number));
        }
        RejectAttribute(verb, roles, number);
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
    private static Attribute CopularAttribute(ClauseRoles roles, int number)
    {
        if (roles.Arguments.ContainsKey("object"))
        {
            throw new ClauseFormatException("be has no object. Say what its subject is with noun: or adjective:, as in noun:fool.", number);
        }
        //where its subject is, as in location:house
        var attribute = roles.Attribute;
        if (roles.Obliques.Find(o => o.Role == "location") is { Oblique: { } location })
        {
            attribute = location;
        }
        if (roles.Obliques.Any(o => o.Role == "like"))
        {
            throw new ClauseFormatException("be can't say what its subject is like yet: like: goes with other verbs, as in shine subject:3SG.N like:water.", number);
        }
        if (roles.Obliques.Find(o => o.Role != "location" && !CopularRoles.Contains(o.Role)) is { Role: { } other })
        {
            throw new ClauseFormatException($"be can't take {A(other)} yet, only a noun:, adjective: or location: saying what or where its subject is, and time: or duration:.", number);
        }
        if (!attribute.HasValue)
        {
            throw new ClauseFormatException("be needs a noun, adjective or location saying what or where its subject is, as in noun:fool, adjective:big or location:house.", number);
        }
        return attribute.GetValueOrDefault();
    }

    private static void RejectAttribute(string verb, ClauseRoles roles, int number)
    {
        if (roles.Attribute.HasValue)
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
        if (items.FirstOrDefault(i => i.Value.Value is SyntaxClause && i.Role is not ("relative" or "standard")) is { } clause)
        {
            throw Misplaced($"'{clause.Text}' in a noun phrase", number);
        }
        if (items.FirstOrDefault(i => i.Value.Value is SyntaxGroup && i.Role is not ("possessor" or "standard")) is { } nested)
        {
            throw new ClauseFormatException($"Only a possessor or a standard can be a noun phrase of its own inside parentheses, as in (son possessor:(sister possessor:1SG)), not '{nested.Text}'.", number);
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
        foreach (var item in items.Skip(1).Where(i => i.Role is not (null or "possessor" or "relative" or "standard")))
        {
            throw new ClauseFormatException($"Noun phrases can't have {A(item.Role!)} yet, only adjectives, a number, an ordinal, a quantifier, a possessor, a standard and a relative clause.", number);
        }
        var parsed = ParseArgument(items[0].Value, number).Value as NounPhrase
            ?? throw new ClauseFormatException($"{head} can't take adjectives, a number or a possessor yet.", number);
        if (parsed.Head.Value is IndefinitePronoun && items.Skip(1).FirstOrDefault(i => i.Role != "relative") is { } extra)
        {
            throw new ClauseFormatException($"{head} takes only a relative clause, not '{extra.Text}'.", number);
        }

        int? count = null;
        int? ordinal = null;
        Quantifier? quantifier = null;
        Argument? possessor = null;
        Standard? standard = null;
        Propositional? relative = null;
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
            else if (item.Role == "standard")
            {
                if (standard.HasValue)
                {
                    throw new ClauseFormatException($"'{head}' has more than one standard.", number);
                }
                standard = item.Value.Value is SyntaxClause compared ? ParseStandard(compared, number) : StandardArgument(item.Value, number);
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
        //an indefinite pronoun takes nothing but its relative clause
        if (parsed.Head.Value is not Noun noun)
        {
            return parsed with { Relative = relative };
        }
        if (quantifier is { } said)
        {
            var name = said.ToString().ToLowerInvariant();
            if (noun.Determiner is { Value: Definiteness article } && article == Definiteness.Indefinite)
            {
                throw new ClauseFormatException($"'{head}' takes {name} or NDF, not both: write ({head.Split('.')[0]} {name}).", number);
            }
            if (said is Quantifier.Every or Quantifier.No && noun.Determiner is { Value: Grammeme determiner })
            {
                throw new ClauseFormatException($"{name} can't go with {determiner.Gloss}: it picks out no particular one.", number);
            }
            if (count is not null && said != Quantifier.All)
            {
                throw new ClauseFormatException($"Only all goes with a number, as in ({head} all 3), not {name}.", number);
            }
        }
        //a standard goes with the noun's one comparative adjective, as in (arm.PL strong.CMPR standard:...)
        if (standard is { } than)
        {
            var compared = adjectives.FindAll(a => a.Grade is { Value: Comparative });
            if (compared.Count != 1)
            {
                throw new ClauseFormatException(compared.Count == 0
                    ? $"A standard: in a noun phrase compares its comparative adjective, as in ({head} big.CMPR standard:cat) for 'a bigger {head.Split('.')[0]} than the cat'."
                    : $"A standard: goes with one comparative, and '{head}' has more than one.", number);
            }
            adjectives = [.. adjectives.Select(a => a == compared[0] ? Compared(a, than) : a)];
        }
        //a number gives the noun its number unless the noun has one already
        var given = ParseWord(head, number).Features.OfType<Number>().Any();
        return new NounPhrase(noun with
        {
            Adjectives = [.. adjectives],
            Count = count,
            Ordinal = ordinal,
            Possessor = possessor,
            Quantifier = quantifier,
            Number = given || count is not { } n ? noun.Number : NumberFor(n),
        }, relative);
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

    /*
      an adjective, maybe with INTS for 'very' or CMPR for 'more', or both for 'much more', as
      small.CMPR.INTS is 'much smaller'. A standard is the phrase's or the clause's to give it
     */
    private static Adjective ParseAdjective(string token, int number)
    {
        var (meaning, features) = ParseWord(token, number);
        if (QuestionWords.ContainsKey(meaning) || QuestionAdverbs.ContainsKey(meaning))
        {
            throw new ClauseFormatException($"{meaning} isn't an adjective: a question word goes in the role it asks about, as in subject:who.", number);
        }
        if (meaning == "_")
        {
            throw new ClauseFormatException($"_ stands for what's compared only as a standard's own adjective: or manner:, bare, as in {StandardExample}.", number);
        }
        if (features.Contains(Derivation.Adverbial))
        {
            throw new ClauseFormatException($"'{meaning}' can't take ADV: an adverb made from an adjective is written manner:{meaning}, as in climb subject:2SG manner:careful for 'you climb carefully'.", number);
        }
        if (features.Find(f => f != Degree.Intensive && f != Degree.Comparative) is { } other)
        {
            throw new ClauseFormatException($"'{meaning}' can't take {other.Gloss}: adjectives have no features but CMPR and INTS yet.", number);
        }
        if (features.GroupBy(f => f).FirstOrDefault(g => g.Count() > 1) is { } repeated)
        {
            throw new ClauseFormatException($"'{meaning}' has more than one {repeated.Key.Gloss}.", number);
        }
        var intensive = features.Contains(Degree.Intensive);
        Grade? grade = features.Contains(Degree.Comparative) ? new Comparative(null, intensive ? new Much() : (Differential?)null)
            : intensive ? new Intensified()
            : (Grade?)null;
        return new Adjective(meaning, grade);
    }

    //how something is done, as an adjective: manner:careful is 'carefully', and manner:slow.INTS
    //'very slowly'
    private static Manner ParseManner(SyntaxValue value, int number)
    {
        if (value.Value is not SyntaxWord { Text: var token })
        {
            throw new ClauseFormatException($"manner: takes one adjective, as in manner:careful, not '{value.Text}'. Write manner: again for another.", number);
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
    private static IndefinitePronoun? ParseIndefinite(string meaning, IReadOnlyList<Grammeme> features, int number)
    {
        if (IndefiniteSpellings.TryGetValue(meaning, out var instead))
        {
            throw new ClauseFormatException($"Write {instead}, not {meaning}.", number);
        }
        if (!IndefiniteWords.TryGetValue(meaning, out var indefinite))
        {
            return null;
        }
        return features.Count == 0 ? new IndefinitePronoun(indefinite.Series, indefinite.Kind)
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
            return new NounPhrase(ParseIndefinite(meaning, features, number)!, null);
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
            throw new ClauseFormatException(asked == InterrogativeWord.When
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
            return Indefinites.RolesOf(indefinite.Kind) is null ? new NounPhrase(indefinite, null)
                : throw new ClauseFormatException($"{Placed(noun, indefinite.Kind)}, on its own.", number);
        }
        if (QuestionWords.TryGetValue(noun, out var asked))
        {
            return features.Count == 0 ? new QuestionWord(asked)
                : throw new ClauseFormatException($"{noun} can't take {features[0].Gloss}: a question word takes no glosses.", number);
        }
        Number? count = null;
        Determiner? determiner = null;
        foreach (var feature in features)
        {
            switch (feature)
            {
                case Distance when determiner?.Value is Distance:
                    throw new ClauseFormatException($"'{noun}' has more than one demonstrative.", number);
                case Distance when determiner?.Value is Definiteness article:
                    throw BothDeterminers(article);
                case Definiteness article when determiner?.Value is Distance:
                    throw BothDeterminers(article);
                case Distance d:
                    determiner = d;
                    break;
                case Number n when count is null:
                    count = n;
                    break;
                case Number:
                    throw new ClauseFormatException($"'{noun}' has more than one number.", number);
                case Definiteness d when determiner is null:
                    determiner = d;
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
        return new NounPhrase(new Noun(noun, count ?? Number.Singular, [], null, null, null, determiner, null), null);

        ClauseFormatException BothDeterminers(Definiteness article) =>
            new($"'{noun}' takes a demonstrative or {article.Gloss}, not both: a demonstrative already makes it definite.", number);
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

    //every value of every category, by its gloss, a category being a kind of grammeme, or a kind of one, as a situational modality is
    private static readonly Dictionary<string, Grammeme> Glosses = typeof(Grammeme).Assembly.GetTypes()
        .Where(t => t.IsSubclassOf(typeof(Grammeme)))
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
