using System.Reflection;

namespace LanguageGenerator;

//a file of clauses to translate, each the English it means followed by an indented line saying
//what it means in glosses:
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
//is in parentheses, noun first, as in object:(dog.PL big old). The verb be says what its
//subject is, with noun:fool or adjective:locked in place of an object. Obliques say where, to where,
//from where, to whom, with whom and with what, as location:house, goal:river, source:(village old),
//recipient:dog, companion:1PL and instrument:knife, in the order they're said. Anything not given takes its
//unmarked value: present, imperfective, singular, exclusive for 'we' and masculine for a gendered
//pronoun. Whatever the generator can't express yet is an error, never dropped
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

    //the roles a clause can have now, and the rest of CaseUse's roles, which it can't yet. A noun or
    //adjective is what be says its subject is
    private static readonly Dictionary<string, CaseUse> ObliqueRoles = new()
    {
        ["recipient"] = CaseUse.IndirectObject,
        ["location"] = CaseUse.Location,
        ["goal"] = CaseUse.MovementTo,
        ["source"] = CaseUse.MovementFrom,
        ["companion"] = CaseUse.Accompaniment,
        ["instrument"] = CaseUse.Instrument,
    };
    private static readonly string[] Roles = ["subject", "object", "noun", "adjective", .. ObliqueRoles.Keys];
    private static readonly string[] FutureRoles = ["possessor", "addressee"];

    private static Statement ParseClause(string line, int number)
    {
        var tokens = Tokenize(line, number);
        if (tokens[0].Contains(':'))
        {
            throw new ClauseFormatException($"A clause starts with its verb, not with '{tokens[0]}'.", number);
        }
        var (verb, verbFeatures) = ParseWord(tokens[0], number);

        Tense? tense = null;
        Aspect? aspect = null;
        Polarity? polarity = null;
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
                case Polarity p when polarity is null:
                    polarity = p;
                    break;
                case Tense or Aspect or Polarity:
                    throw new ClauseFormatException($"'{verb}' has more than one {feature.GetType().Name.ToLowerInvariant()}.", number);
                case Person or Number:
                    throw new ClauseFormatException($"'{verb}' can't take {feature.Gloss}: a verb agrees with its subject.", number);
                default:
                    throw new ClauseFormatException($"'{verb}' can't take {feature.Gloss} ({feature.Name}) yet: verbs have only tense, aspect and polarity.", number);
            }
        }

        var arguments = new Dictionary<string, Argument>();
        var obliques = new List<(string Role, Oblique Oblique)>();
        Complement? complement = null;
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
                case var oblique when ObliqueRoles.TryGetValue(oblique, out var use):
                    obliques.Add((role, new Oblique(use, ParseArgument(value, number))));
                    break;
                default:
                    arguments[role] = ParseArgument(value, number);
                    break;
            }
        }

        if (!arguments.TryGetValue("subject", out var subject))
        {
            throw new ClauseFormatException("The clause needs a subject, as in subject:dog.", number);
        }
        var negative = polarity == Polarity.Negative;
        if (verb == "be")
        {
            if (arguments.ContainsKey("object"))
            {
                throw new ClauseFormatException("be has no object. Say what its subject is with noun: or adjective:, as in noun:fool.", number);
            }
            if (obliques.Count > 0)
            {
                throw new ClauseFormatException($"be can't take {A(obliques[0].Role)} yet, only a noun: or adjective: saying what its subject is.", number);
            }
            if (!complement.HasValue)
            {
                throw new ClauseFormatException("be needs a noun or adjective saying what its subject is, as in noun:fool or adjective:big.", number);
            }
            return new CopularClause(complement.GetValueOrDefault(), tense ?? Tense.Present, aspect ?? Aspect.Imperfective, subject, negative);
        }
        if (complement.HasValue)
        {
            throw new ClauseFormatException($"Only be takes a noun: or adjective:. '{verb}' takes an object:.", number);
        }
        return new Clause(verb, tense ?? Tense.Present, aspect ?? Aspect.Imperfective, subject, arguments.TryGetValue("object", out var @object) ? @object : (Argument?)null,
            Negative: negative, Obliques: obliques.Count > 0 ? [.. obliques.Select(o => o.Oblique)] : null);
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
        foreach (var word in words.Skip(1).Where(w => w.Contains(':')))
        {
            throw new ClauseFormatException($"Noun phrases can't have {A(word[..word.IndexOf(':')])} yet, only adjectives.", number);
        }
        var noun = (NounPhrase)ParseArgument(words[0], number).Value!;
        return noun with { Modifiers = [.. words.Skip(1).Select(w => ParseAdjective(w, number))] };
    }

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
