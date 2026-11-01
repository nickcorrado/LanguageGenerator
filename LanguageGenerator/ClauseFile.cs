using System.Reflection;

namespace LanguageGenerator;

//a file of clauses to translate, each the English it means followed by an indented line saying
//what it means in glosses:
//
//  # a comment
//  the dog saw the cat
//    see.PST.PFV  subject:dog  object:cat
//
//The indented line is the verb, then its arguments in any order as role:noun, each word an English
//meaning with Leipzig glosses joined by periods, as in a gloss line. A pronoun is its person and
//number instead, as in subject:1SG or object:1PL.INCL. Anything not given takes its unmarked value:
//present, imperfective, singular and, for 'we', exclusive. Whatever the generator can't express
//yet is an error, never dropped
public static class ClauseFile
{
    public record Entry(string Translation, Clause Clause, int Line);

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

    //the roles a clause can have now, and the rest of CaseUse's roles, which it can't yet
    private static readonly string[] Roles = ["subject", "object"];
    private static readonly string[] FutureRoles = ["recipient", "possessor", "location", "goal", "source", "companion", "instrument", "addressee"];

    private static Clause ParseClause(string line, int number)
    {
        var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens[0].Contains(':'))
        {
            throw new ClauseFormatException($"A clause starts with its verb, not with '{tokens[0]}'.", number);
        }
        var (verb, verbFeatures) = ParseWord(tokens[0], number);

        Tense? tense = null;
        Aspect? aspect = null;
        foreach (var feature in verbFeatures)
        {
            switch (feature)
            {
                case Tense t when tense is null:
                    tense = t;
                    break;
                case Aspect a when aspect is null:
                    aspect = a;
                    break;
                case Tense or Aspect:
                    throw new ClauseFormatException($"'{verb}' has more than one {feature.GetType().Name.ToLowerInvariant()}.", number);
                case Person or Number:
                    throw new ClauseFormatException($"'{verb}' can't take {feature.Gloss}: a verb agrees with its subject.", number);
                default:
                    throw new ClauseFormatException($"'{verb}' can't take {feature.Gloss} ({feature.Name}) yet: verbs have only tense and aspect.", number);
            }
        }

        var arguments = new Dictionary<string, Argument>();
        foreach (var token in tokens.Skip(1))
        {
            var colon = token.IndexOf(':');
            if (colon < 0)
            {
                throw new ClauseFormatException($"'{token}' needs a role, as in subject:{token}.", number);
            }
            var role = token[..colon];
            if (FutureRoles.Contains(role))
            {
                throw new ClauseFormatException($"Clauses can't have a {role} yet, only {string.Join(" and ", Roles)}.", number);
            }
            if (!Roles.Contains(role))
            {
                throw new ClauseFormatException($"'{role}' isn't a role. The roles are {string.Join(" and ", Roles)}.", number);
            }
            if (arguments.ContainsKey(role))
            {
                throw new ClauseFormatException($"The clause has more than one {role}.", number);
            }
            arguments[role] = ParseArgument(token[(colon + 1)..], number);
        }

        if (!arguments.TryGetValue("subject", out var subject))
        {
            throw new ClauseFormatException("The clause needs a subject, as in subject:dog.", number);
        }
        return new Clause(verb, tense ?? Tense.Present, aspect ?? Aspect.Imperfective, subject, arguments.TryGetValue("object", out var @object) ? @object : (Argument?)null);
    }

    private static Argument ParseArgument(string token, int number)
    {
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
        foreach (var gloss in parts.Skip(1))
        {
            var feature = Glosses.TryGetValue(gloss, out var tagmeme) ? tagmeme
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
                case Case:
                    throw new ClauseFormatException($"{parts[0]} can't take {feature.Gloss}: a pronoun's case comes from its role.", number);
                default:
                    throw new ClauseFormatException($"{parts[0]} can't take {feature.Gloss} ({feature.Name}) yet: pronouns have only person, number and clusivity.", number);
            }
        }
        return new Pronoun(person, count, clusivity);
    }

    //a meaning and the glosses after it, as in dog.PL
    private static (string Meaning, List<Tagmeme> Features) ParseWord(string token, int number)
    {
        var parts = token.Split('.');
        var meaning = parts[0];
        if (meaning.Length == 0)
        {
            throw new ClauseFormatException($"'{token}' needs a word before its glosses.", number);
        }
        return (meaning, [.. parts.Skip(1).Select(gloss => Glosses.TryGetValue(gloss, out var tagmeme) ? tagmeme
            : throw new ClauseFormatException($"'{gloss}' isn't a gloss.{Suggestion(gloss)}", number))]);
    }

    //every value of every category, by its gloss
    private static readonly Dictionary<string, Tagmeme> Glosses = typeof(Tagmeme).Assembly.GetTypes()
        .Where(t => t.BaseType == typeof(Tagmeme))
        .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == t))
        .Select(p => (Tagmeme)p.GetValue(null)!)
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
