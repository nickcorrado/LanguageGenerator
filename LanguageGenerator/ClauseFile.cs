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
//king's' or 'her'. The verb be says what its subject is, with noun:fool or adjective:locked in place of an
//object, or where it is, with location:house. Obliques say where, to where, from where, to whom,
//with whom and with what, as location:house, goal:river, source:(village old), recipient:dog,
//companion:1PL and instrument:knife, in the order they're said. A verb with IMP is a command, as in
//drink.IMP.NEG object:wine, with no tense or aspect and no subject but subject:2PL to tell several
//people. Anything not given takes its unmarked value: present, imperfective, singular, exclusive for
//'we' and masculine for a gendered pronoun. Whatever the generator can't express yet is an error,
//never dropped
public static class ClauseFile
{
    public record Entry(string Translation, Statement Statement, int Line);

    public static IReadOnlyList<Entry> Parse(string text)
    {
        var entries = new List<Entry>();
        (string Text, int Line)? translation = null;
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
            }
            else if (translation is { } english)
            {
                entries.Add(new Entry(english.Text, ParseClause(line, number), number));
                translation = null;
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

    //each noun these clauses use, with the English of the first clause to use it
    public static IReadOnlyList<(string Noun, string Translation)> Nouns(IEnumerable<Entry> entries) =>
        [.. entries.SelectMany(e => ArgumentsOf(e.Statement).SelectMany(NounsIn).Select(n => (Noun: n, e.Translation))).DistinctBy(n => n.Noun)];

    private static IEnumerable<Argument> ArgumentsOf(Statement statement) => statement switch
    {
        Clause c => [c.Subject, .. Given(c.Object), .. c.Obliques.Select(o => o.Argument)],
        CopularClause { Complement: NounPhrase noun } c => [c.Subject, noun],
        CopularClause { Complement: Oblique location } c => [c.Subject, location.Argument],
        CopularClause c => [c.Subject],
        Command c => [.. Given(c.Object), .. c.Obliques.Select(o => o.Argument)],
    };

    private static IEnumerable<Argument> Given(Argument? argument) => argument.HasValue ? [argument.GetValueOrDefault()] : [];

    private static IEnumerable<string> NounsIn(Argument argument) => argument switch
    {
        NounPhrase n => [n.Noun, .. Given(n.Possessor).SelectMany(NounsIn)],
        Pronoun => [],
    };

    //the roles a clause can have now, and the rest of the Role enum's, which it can't yet. An oblique
    //role is written as its name in lowercase. A noun or adjective is what be says its subject is
    private static readonly Dictionary<string, Role> ObliqueRoles = Oblique.Roles.ToDictionary(r => r.ToString().ToLowerInvariant());
    private static readonly string[] Roles = ["subject", "object", "noun", "adjective", .. ObliqueRoles.Keys];
    private static readonly string[] FutureRoles = ["addressee"];

    //what the format can say, for describing it elsewhere, as the glosser's prompt does
    public static IReadOnlyList<string> RoleNames => Roles;
    public static IReadOnlyList<string> UnsupportedRoleNames => FutureRoles;
    public static IReadOnlyList<T> ValuesOf<T>() where T : Grammeme => [.. Glosses.Values.OfType<T>()];

    private static Statement ParseClause(string line, int number)
    {
        var tokens = Tokenize(line, number);
        if (tokens[0].Contains(':'))
        {
            throw new ClauseFormatException($"A clause starts with its verb, not with '{tokens[0]}'.", number);
        }
        var (verb, glosses) = ParseWord(tokens[0], number);
        var features = ParseVerbFeatures(verb, glosses, number);
        var roles = ParseRoles(verb, tokens.Skip(1), number);

        if (features.Imperative)
        {
            return ToCommand(verb, features, roles, number);
        }
        if (!roles.Arguments.TryGetValue("subject", out var subject))
        {
            throw new ClauseFormatException("The clause needs a subject, as in subject:dog.", number);
        }
        if (verb == "be")
        {
            return ToCopularClause(features, roles, subject, number);
        }
        return ToClause(verb, features, roles, subject, number);
    }

    private record VerbFeatures(Tense? Tense, Aspect? Aspect, bool Negative, bool Imperative);

    //the obliques keep the role they were written with, which is how be finds its location:
    private record ClauseRoles(Dictionary<string, Argument> Arguments, List<(string Role, Oblique Oblique)> Obliques, Complement? Complement)
    {
        public Argument? Object => Arguments.TryGetValue("object", out var argument) ? argument : (Argument?)null;
    }

    private static VerbFeatures ParseVerbFeatures(string verb, IReadOnlyList<Grammeme> glosses, int number)
    {
        if (glosses.FirstOrDefault(g => g is not (Tense or Aspect or Polarity) && g != Mood.Imperative) is { } unsupported)
        {
            throw Unsupported(verb, unsupported, number);
        }
        if (glosses.GroupBy(g => g.GetType()).FirstOrDefault(g => g.Count() > 1) is { } repeated)
        {
            throw new ClauseFormatException($"'{verb}' has more than one {repeated.Key.Name.ToLowerInvariant()}.", number);
        }
        return new VerbFeatures(
            glosses.OfType<Tense>().SingleOrDefault(),
            glosses.OfType<Aspect>().SingleOrDefault(),
            glosses.Contains(Polarity.Negative),
            glosses.Contains(Mood.Imperative));
    }

    private static ClauseFormatException Unsupported(string verb, Grammeme feature, int number) => feature switch
    {
        Mood m when m == Mood.Prohibitive => new($"Write a command not to do something as {verb}.IMP.NEG, not {verb}.{m.Gloss}.", number),
        Person or Number => new($"'{verb}' can't take {feature.Gloss}: a verb agrees with its subject.", number),
        _ => new($"'{verb}' can't take {feature.Gloss} ({feature.Name}) yet: verbs have only tense, aspect, polarity and the imperative.", number),
    };

    private static ClauseRoles ParseRoles(string verb, IEnumerable<string> tokens, int number)
    {
        var arguments = new Dictionary<string, Argument>();
        var obliques = new List<(string Role, Oblique Oblique)>();
        Complement? complement = null;
        foreach (var token in tokens)
        {
            var colon = token.IndexOf(':');
            if (colon < 0)
            {
                throw new ClauseFormatException($"'{token}' needs a role, as in subject:{token}.", number);
            }
            var role = token[..colon];
            if (role == "possessor")
            {
                throw new ClauseFormatException($"A possessor goes inside its noun's parentheses, as in object:(house possessor:{token[(colon + 1)..]}).", number);
            }
            if (FutureRoles.Contains(role))
            {
                throw new ClauseFormatException($"Clauses can't have {A(role)} yet, only {string.Join(", ", Roles)}.", number);
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
                case "noun":
                    complement = ParseArgument(value, number).Value as NounPhrase
                        ?? throw new ClauseFormatException($"be can't say its subject is {value} yet: the noun has to be a noun, not a pronoun.", number);
                    break;
                case "adjective":
                    complement = ParseAdjective(value, number);
                    break;
                case var name when ObliqueRoles.TryGetValue(name, out var oblique):
                    obliques.Add((role, new Oblique(oblique, ParseArgument(value, number))));
                    break;
                default:
                    arguments[role] = ParseArgument(value, number);
                    break;
            }
        }
        return new ClauseRoles(arguments, obliques, complement);
    }

    private static Command ToCommand(string verb, VerbFeatures features, ClauseRoles roles, int number)
    {
        if (verb == "be")
        {
            throw new ClauseFormatException("be can't be a command yet.", number);
        }
        if (features.Tense is not null || features.Aspect is not null)
        {
            throw new ClauseFormatException($"'{verb}' can't take {(features.Tense as Grammeme ?? features.Aspect)!.Gloss}: a command has no tense or aspect.", number);
        }
        RejectComplement(verb, roles, number);
        var told = Number.Singular;
        if (roles.Arguments.TryGetValue("subject", out var addressee))
        {
            told = addressee.Value is Pronoun { Person: var person, Number: var count } && person == Person.Second ? count
                : throw new ClauseFormatException("A command is said to 'you', so its subject can only be 2SG or 2PL, as in subject:2PL.", number);
        }
        return new Command(verb, told, roles.Object, features.Negative, [.. roles.Obliques.Select(o => o.Oblique)]);
    }

    private static CopularClause ToCopularClause(VerbFeatures features, ClauseRoles roles, Argument subject, int number)
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
        if (roles.Obliques.Find(o => o.Role != "location") is { Role: { } other })
        {
            throw new ClauseFormatException($"be can't take {A(other)} yet, only a noun:, adjective: or location: saying what or where its subject is.", number);
        }
        if (!complement.HasValue)
        {
            throw new ClauseFormatException("be needs a noun, adjective or location saying what or where its subject is, as in noun:fool, adjective:big or location:house.", number);
        }
        return new CopularClause(complement.GetValueOrDefault(), features.Tense ?? Tense.Present, features.Aspect ?? Aspect.Imperfective, subject, features.Negative);
    }

    private static Clause ToClause(string verb, VerbFeatures features, ClauseRoles roles, Argument subject, int number)
    {
        RejectComplement(verb, roles, number);
        return new Clause(verb, features.Tense ?? Tense.Present, features.Aspect ?? Aspect.Imperfective, subject, roles.Object,
            Negative: features.Negative, Obliques: [.. roles.Obliques.Select(o => o.Oblique)]);
    }

    private static void RejectComplement(string verb, ClauseRoles roles, int number)
    {
        if (roles.Complement.HasValue)
        {
            throw new ClauseFormatException($"Only be takes a noun: or adjective:. '{verb}' takes an object:.", number);
        }
    }

    //a noun and its adjectives, in parentheses, as in (dog.PL big old)
    private static NounPhrase ParseModifiedNounPhrase(string token, int number)
    {
        if (!token.EndsWith(')'))
        {
            throw new ClauseFormatException($"'{token}' has to end at its ), as in (dog big).", number);
        }
        var words = token[1..^1].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
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
        var noun = (NounPhrase)ParseArgument(words[0], number).Value!;

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
    private static Adjective ParseAdjective(string token, int number)
    {
        var (meaning, features) = ParseWord(token, number);
        if (features.Count > 0)
        {
            throw new ClauseFormatException($"'{meaning}' can't take {features[0].Gloss}: adjectives have no features yet.", number);
        }
        return new Adjective(meaning);
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
            if (depth > 1)
            {
                throw new ClauseFormatException("Noun phrases can't be nested yet.", number);
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
        if (token.StartsWith('('))
        {
            return ParseModifiedNounPhrase(token, number);
        }
        if (ParsePronoun(token, number) is { } pronoun)
        {
            return pronoun;
        }
        var (noun, features) = ParseWord(token, number);
        Number? count = null;
        foreach (var feature in features)
        {
            switch (feature)
            {
                case Number n when count is null:
                    count = n;
                    break;
                case Number:
                    throw new ClauseFormatException($"'{noun}' has more than one number.", number);
                case Case:
                    throw new ClauseFormatException($"'{noun}' can't take {feature.Gloss}: a noun's case comes from its role.", number);
                case Polarity:
                    throw new ClauseFormatException($"'{noun}' can't take {feature.Gloss}: it's the verb that's negated.", number);
                default:
                    throw new ClauseFormatException($"'{noun}' can't take {feature.Gloss} ({feature.Name}) yet: nouns have only number.", number);
            }
        }
        return new NounPhrase(noun, count ?? Number.Singular);
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
