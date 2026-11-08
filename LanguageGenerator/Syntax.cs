namespace LanguageGenerator;

//what a sentence means, independent of any language: words are meanings, like "dog", which each
//language's lexicon turns into its own roots. Realizing one in a language is where all of that
//language's choices (word order, role markers, paradigms, phonology) come together
/// <summary>
/// A noun with any adjectives modifying it, in English order, how many of it there are if a numeral
/// says, as in 'three wolves', whose it is, as in 'the king's house', which one it is, as in 'the
/// third', and whether it's definite, as 'the dog' is and 'a dog' isn't.
/// </summary>
//no definiteness is a bare noun, as English has for 'dogs', 'water' or 'my dog'
public record NounPhrase(string Noun, Number Number, ValueList<Adjective> Adjectives = default, int? Count = null, Argument? Possessor = null, int? Ordinal = null,
    Definiteness? Definiteness = null);
/// <summary>What fills a role: a noun phrase or a pronoun.</summary>
//what agrees with one depends on the language, so that's Syntax.AgreementOf's to say
public union Argument(NounPhrase, Pronoun);
/// <summary>
/// A phrase saying where, to where, from where, to whom, with whom or with what something happened.
/// </summary>
public record Oblique(Role Role, Argument Argument)
{
    public static IReadOnlyList<Role> Roles { get; } =
        [Role.Recipient, Role.Location, Role.Goal, Role.Source, Role.Companion, Role.Instrument];
}

//obliques are in the order they're said in English
public record Clause(string Verb, Tense Tense, Aspect Aspect, Argument Subject, Argument? Object = null, bool Negative = false, ValueList<Oblique> Obliques = default);

/// <summary>A property, like 'big', said of something.</summary>
public record Adjective(string Meaning);
/// <summary>
/// What a copular clause says of its subject: a noun phrase, as in 'you are a fool', an adjective,
/// as in 'the gate is locked', or a Location, as in 'the dog is in the house'.
/// </summary>
public union Complement(NounPhrase, Adjective, Oblique);
/// <summary>A clause saying what or where its subject is, as English does with be.</summary>
public record CopularClause(Complement Complement, Tense Tense, Aspect Aspect, Argument Subject, bool Negative = false);

/// <summary>
/// Telling one person or more to do something, or with Negative, not to. Who's told is 'you', so it
/// isn't said, and a command has no tense or aspect of its own.
/// </summary>
public record Command(string Verb, Number Number, Argument? Object = null, bool Negative = false, ValueList<Oblique> Obliques = default);

/// <summary>
/// A statement said to someone called by name or by what they are, as in 'Marta, come here' or 'old
/// man, the wolves are in the village'.
/// </summary>
//the addressee is a noun phrase, never a pronoun, and bare, since a vocative takes no article in
//nearly every language
public record Addressed(Statement Statement, NounPhrase Addressee);

/// <summary>Anything a sentence can say.</summary>
public union Statement(Clause, CopularClause, Command, Addressed);

public static class Syntax
{
    public static Sentence Realize(Language language, Statement statement, string translation) => statement switch
    {
        Clause c => Realize(language, c, translation),
        CopularClause c => Realize(language, c, translation),
        Command c => Realize(language, c, translation),
        Addressed a => Realize(language, a, translation),
    };

    //the addressee comes first, in the addressee's case: the vocative, where the language has one,
    //and otherwise the case a noun is cited in. Where a vocative goes is a matter of discourse more
    //than grammar, which WALS doesn't count, and first is the commonest place
    public static Sentence Realize(Language language, Addressed addressed, string translation)
    {
        var sentence = Realize(language, addressed.Statement, translation);
        var addressee = Marked(language, addressed.Addressee, Role.Addressee).Select(language.Phonology.Realize);
        return sentence with { Words = [.. addressee, .. sentence.Words] };
    }

    //the verb takes the language's imperative, or its ordinary second person where it has none for
    //this number or doesn't use it to forbid, and only its prohibitive negator says 'don't', if it
    //has one. The subject goes unsaid even in a language that says its pronouns, as in English
    public static Sentence Realize(Language language, Command command, string translation)
    {
        var commands = language.Commands;
        var verb = language.Lexicon.Verb(command.Verb);
        var imperative = command.Negative && !commands.ProhibitiveUsesImperative ? null : commands.MarkerFor(command.Number);
        var words = imperative.HasValue
            ? commands.Imperative(verb, imperative.GetValueOrDefault())
            : Verb(language, verb, Tense.Present, Aspect.Imperfective, new Pronoun(Person.Second, command.Number), negative: false);
        if (command.Negative)
        {
            words = (commands.Prohibitive ?? language.Negation).Apply(words);
        }
        var @object = command.Object.HasValue ? Inflect(language, command.Object.GetValueOrDefault(), Role.DirectObject) : null;
        var obliques = command.Obliques.SelectMany(o => Oblique(language, o)).ToList();
        return Sentence(language, Order(language.WordOrder, [], words, @object, obliques), translation);
    }

    public static Sentence Realize(Language language, Clause clause, string translation)
    {
        var subjectRole = clause.Object is null ? Role.IntransitiveSubject : Role.TransitiveSubject;
        var subject = Subject(language, clause.Subject, subjectRole);
        var @object = clause.Object.HasValue ? Inflect(language, clause.Object.GetValueOrDefault(), Role.DirectObject) : null;
        var verb = Verb(language, language.Lexicon.Verb(clause.Verb), clause.Tense, clause.Aspect, clause.Subject, clause.Negative);
        var obliques = clause.Obliques.SelectMany(o => Oblique(language, o)).ToList();
        return Sentence(language, Order(language.WordOrder, subject, verb, @object, obliques), translation);
    }

    //an oblique takes its role's case, and its adposition, if the role has one, before or after it
    private static IReadOnlyList<Word> Oblique(Language language, Oblique oblique)
    {
        if (!LanguageGenerator.Oblique.Roles.Contains(oblique.Role))
        {
            throw new ArgumentException($"{oblique.Role} isn't an oblique role.", nameof(oblique));
        }
        return Marked(language, oblique.Argument, oblique.Role);
    }

    //a phrase marked for a role: its case, and its adposition, if the role has one, before or after it
    private static IReadOnlyList<Word> Marked(Language language, Argument argument, Role role)
    {
        var phrase = Inflect(language, argument, role);
        return language.RoleMarkers[role] switch
        {
            Adposition { Type: AdpositionType.Preposition } a => [new Word([a.Lexeme.Root]), .. phrase],
            Adposition a => [.. phrase, new Word([a.Lexeme.Root])],
            _ => phrase,
        };
    }

    //a copular clause's complement goes where an object would, so it precedes the copula in an OV
    //language, as in Turkish and Japanese, and follows it in a VO one. Where adjectives are verbal,
    //the adjective takes the verb's place instead, with no copula
    public static Sentence Realize(Language language, CopularClause clause, string translation)
    {
        var predication = language.Predication;
        var subject = Subject(language, clause.Subject, Role.IntransitiveSubject);
        if (clause.Complement.Value is Adjective verbal && predication.Adjectives == PredicativeAdjectives.Verbal)
        {
            //a property word inflects as a verb in a language whose adjectives are verbs
            var adjective = language.Lexicon.Adjective(verbal.Meaning) with { PartOfSpeech = PartOfSpeech.Verb };
            var predicate = Verb(language, adjective, clause.Tense, clause.Aspect, clause.Subject, clause.Negative);
            return Sentence(language, Order(language.WordOrder, subject, predicate, null, []), translation);
        }

        //a location is an oblique, and goes where obliques go, with the language's locational copula
        if (clause.Complement.Value is Oblique location)
        {
            if (location.Role != Role.Location)
            {
                throw new ArgumentException($"A copular clause can say where its subject is, but not its {location.Role}.", nameof(clause));
            }
            var be = predication.Drops(clause.Tense, clause.Negative, locational: true)
                ? []
                : Verb(language, predication.CopulaFor(locational: true), clause.Tense, clause.Aspect, clause.Subject, clause.Negative);
            return Sentence(language, Order(language.WordOrder, subject, be, null, Oblique(language, location)), translation);
        }

        //a predicate noun takes the same case as an intransitive subject. A predicate adjective, in a
        //language whose adjectives agree, agrees with the subject in number, gender and that case,
        //as in Latin canis magnus est 'the dog is big'; otherwise it's just its root
        IReadOnlyList<Word> complement = clause.Complement.Value switch
        {
            NounPhrase noun => Inflect(language, noun, Role.IntransitiveSubject),
            Adjective adjective when language.AdjectiveAgreement == AdjectiveAgreement.Agreement =>
                [AgreeingAdjective(language, language.Lexicon.Adjective(adjective.Meaning),
                    Features(language.Paradigms.Nouns, [AgreementOf(language, clause.Subject).Number, CaseFor(language, language.RoleMarkers[Role.IntransitiveSubject])]),
                    GenderOf(language, clause.Subject))],
            Adjective adjective => [new Word([language.Lexicon.Adjective(adjective.Meaning).Root])],
            _ => throw new ArgumentException("A copular clause needs a complement.", nameof(clause)),
        };
        var copula = predication.Drops(clause.Tense, clause.Negative)
            ? []
            : Verb(language, predication.Copula, clause.Tense, clause.Aspect, clause.Subject, clause.Negative);
        return Sentence(language, Order(language.WordOrder, subject, copula, complement, []), translation);
    }

    //a language that drops subject pronouns leaves the verb's agreement, if any, to say who
    private static IReadOnlyList<Word> Subject(Language language, Argument subject, Role role) =>
        subject.Value is Pronoun && language.Pronouns.DropsSubjects ? [] : Inflect(language, subject, role);

    //the verb agrees with its subject. TODO many ergative languages agree with the absolutive instead
    private static IReadOnlyList<Word> Verb(Language language, Lexeme verb, Tense tense, Aspect aspect, Argument subject, bool negative)
    {
        var (person, number) = AgreementOf(language, subject);
        var paradigm = language.Paradigms.Verbs;
        var words = paradigm.Inflect(verb, Features(paradigm, [tense, aspect, person, number])).ToList();
        var genders = language.Genders;
        var gender = genders.VerbsAgree ? GenderOf(language, subject) : null;
        if (gender is not null)
        {
            var main = words.FindIndex(w => w.Root is not null);
            words[main] = genders.Agree(words[main], gender, nextToRoot: false);
        }
        return negative ? language.Negation.Apply(words) : words;
    }

    //the number a noun takes: its own, unless a numeral leaves it singular, as in Turkish üç kurt
    //'three wolf'
    private static Number NumberOf(Language language, NounPhrase phrase) =>
        phrase.Count is not null && language.Numerals.NounStaysSingular ? Number.Singular : phrase.Number;

    //what a verb or adjective agrees with: a noun is third person, in the number of its word, so
    //that a verb is singular with 'three wolf' too, as in Hungarian
    private static (Person Person, Number Number) AgreementOf(Language language, Argument argument) => argument switch
    {
        NounPhrase n => (Person.Third, NumberOf(language, n)),
        Pronoun p => (p.Person, p.Number),
    };

    //the gender something agrees with: a noun's own, or a pronoun's, if the language has that gender.
    //A pronoun with no gender given has none to agree with, since what it refers to isn't known
    private static Gender? GenderOf(Language language, Argument argument) => argument.Value switch
    {
        NounPhrase n => language.GenderOf(language.Lexicon.Noun(n.Noun)),
        Pronoun { Gender: { } g } when language.Genders.Values.Contains(g) => g,
        _ => null,
    };

    //an adjective agreeing with a noun: it takes the noun's markers for these features, but only its
    //affixes, since a particle belongs to the phrase and is said once, and a marker for its gender.
    //Its zero markers go unglossed, since the noun's gloss already shows them
    private static Word AgreeingAdjective(Language language, Lexeme adjective, IReadOnlyList<Grammeme> features, Gender? gender)
    {
        var inflected = language.Paradigms.Nouns.Inflect(adjective with { PartOfSpeech = PartOfSpeech.Noun }, features);
        var word = inflected.First(w => w.Root is not null) with { Covert = null };
        return gender is null ? word : language.Genders.Agree(word, gender, nextToRoot: true);
    }

    //each constituent keeps its own particles with it
    private static Sentence Sentence(Language language, IEnumerable<IReadOnlyList<Word>> constituents, string translation) =>
        new([.. constituents.SelectMany(c => c).Select(language.Phonology.Realize)], translation, language.Orthography);

    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, Role role) =>
        Inflect(language, argument, CaseFor(language, language.RoleMarkers[role]));

    //a noun phrase on its own, in the case a noun is cited in, as in a dictionary or a name
    public static IReadOnlyList<Word> Cited(Language language, NounPhrase phrase) => Inflect(language, phrase, Citation(language));

    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, Case @case)
    {
        var nouns = language.Paradigms.Nouns;
        var pronouns = language.Pronouns.Paradigm;
        return argument switch
        {
            NounPhrase phrase => Possessed(language, phrase, language.Articles.Apply(Modified(language, phrase, Features(nouns, [NumberOf(language, phrase), @case])),
                language.Lexicon.Noun(phrase.Noun).Root, phrase.Definiteness, phrase.Number, language.GenderOf(language.Lexicon.Noun(phrase.Noun)))),
            Pronoun pronoun => pronouns.Inflect(language.Pronouns.For(pronoun), Features(pronouns, [@case])),
        };
    }

    //a phrase with its possessor, if it has one, before or after it all, as the language puts them.
    //The possessor takes the possessive role's case or adposition where the language marks it, and
    //is plain otherwise. Where the language marks the possessed noun, the noun takes an affix
    //agreeing with the possessor, and where it marks only that, the affix is all a pronoun possessor
    //needs, as in Hungarian ház-am 'my house'
    private static IReadOnlyList<Word> Possessed(Language language, NounPhrase phrase, IReadOnlyList<Word> words)
    {
        if (!phrase.Possessor.HasValue)
        {
            return words;
        }
        var possessor = phrase.Possessor.GetValueOrDefault();
        var possession = language.Possession;
        List<Word> possessed = [.. words];
        if (possession.MarksPossessed)
        {
            var root = language.Lexicon.Noun(phrase.Noun).Root;
            var at = possessed.FindIndex(w => w.Root == root);
            var (person, number) = AgreementOf(language, possessor);
            possessed[at] = possession.Agree(possessed[at], person, number);
        }
        if (possession.Marking == PossessiveMarking.Head && possessor.Value is Pronoun)
        {
            return possessed;
        }
        var owner = possession.MarksPossessor ? Marked(language, possessor, Role.Possessor) : Inflect(language, possessor, Citation(language));
        return possession.PossessorFirst ? [.. owner, .. possessed] : [.. possessed, .. owner];
    }

    //the noun's words with its adjectives placed around it: after the noun in a language whose noun
    //phrases are head-initial, before it in one whose are head-final, in the order given either way.
    //A numeral goes outside them, before or after, as the language puts numerals, so that 'three big
    //wolves' is either three big wolf or wolf big three. An ordinal goes like the outermost
    //adjective, inside the numeral, as in 'the first three big wolves', but never agrees, even where
    //adjectives do. The noun's particles stay outside it all
    private static IReadOnlyList<Word> Modified(Language language, NounPhrase phrase, IReadOnlyList<Grammeme> features)
    {
        var nounLexeme = language.Lexicon.Noun(phrase.Noun);
        var noun = language.Paradigms.Nouns.Inflect(nounLexeme, features);
        if (phrase.Adjectives.Count == 0 && phrase.Count is null && phrase.Ordinal is null)
        {
            return noun;
        }
        var main = noun.ToList().FindIndex(w => w.Root is not null);
        var head = noun[main];
        var agreement = language.AdjectiveAgreement;

        var adjectives = phrase.Adjectives.Select(a =>
        {
            var lexeme = language.Lexicon.Adjective(a.Meaning);
            return agreement == AdjectiveAgreement.Agreement
                ? AgreeingAdjective(language, lexeme, features, language.GenderOf(nounLexeme))
                : new Word([lexeme.Root]);
        }).ToList();

        var headFirst = language.WordOrder.NounPhraseDirectionality == HeadDirectionality.Initial;
        IReadOnlyList<Word> ordinal = phrase.Ordinal is { } nth ? language.Numerals.SayOrdinal(nth) : [];
        List<Word> words = headFirst ? [head, .. adjectives, .. ordinal] : [.. ordinal, .. adjectives, head];
        var at = headFirst ? 0 : words.Count - 1;
        if (phrase.Count is { } count)
        {
            var numerals = language.Numerals;
            List<Word> numeral = [.. numerals.Say(count).Select(n => new Word([n.Root]))];
            words = numerals.BeforeNoun ? [.. numeral, .. words] : [.. words, .. numeral];
            at += numerals.BeforeNoun ? numeral.Count : 0;
        }
        if (agreement == AdjectiveAgreement.Phrasal)
        {
            //the noun's affixes move to the edges of the phrase
            var root = head.Morphemes.ToList().FindIndex(m => m.Value is Root);
            var prefixes = head.Morphemes.Take(root).ToList();
            var suffixes = head.Morphemes.Skip(root + 1).ToList();
            words[at] = head with { Morphemes = [head.Morphemes[root]] };
            words[0] = words[0] with { Morphemes = [.. prefixes, .. words[0].Morphemes] };
            words[^1] = words[^1] with { Morphemes = [.. words[^1].Morphemes, .. suffixes] };
        }
        return [.. noun.Take(main), .. words, .. noun.Skip(main + 1)];
    }

    //the case a noun takes for a role. Roles marked some other way still need a value for the
    //Case category: an adposition's governed case, or the plain citation case
    private static Case CaseFor(Language language, RoleMarker marker) => marker switch
    {
        Case c => c,
        Adposition a => a.Governs ?? Citation(language),
        Unmarked => Citation(language),
    };

    //the plain case a noun is cited in
    private static Case Citation(Language language) => language.MorphosyntacticAlignment switch
    {
        MorphosyntacticAlignment.NominativeAccusative => Case.Nominative,
        MorphosyntacticAlignment.ErgativeAbsolutive => Case.Absolutive,
        _ => Case.Direct,
    };

    //each category the paradigm inflects for takes whichever wanted value it distinguishes. A value
    //the language doesn't have falls back on the nearest one it does, and failing that on the
    //category's first value
    private static List<Grammeme> Features(Paradigm paradigm, IReadOnlyList<Grammeme> wanted) =>
        [.. paradigm.Categories.Select(category =>
            wanted.Where(w => w.GetType() == category.Values[0].GetType())
                .Select(w => Nearest(w, category.Values))
                .FirstOrDefault(v => v is not null) ?? category.Values[0])];

    //the value itself if it's available, or else the nearest available one it falls back on
    public static Grammeme? Nearest(Grammeme wanted, IReadOnlyCollection<Grammeme> available) =>
        ((Grammeme[])[wanted, .. Fallbacks.GetValueOrDefault(wanted, [])]).FirstOrDefault(available.Contains);

    //what a value turns into in a language that doesn't distinguish it, nearest first: a dual or a
    //paucal is still more than one, a language without a future speaks of it in the present, as
    //English often does, and an aorist is a perfective
    private static readonly Dictionary<Grammeme, Grammeme[]> Fallbacks = new()
    {
        [Number.Dual] = [Number.Paucal, Number.Plural],
        [Number.Paucal] = [Number.Plural],
        [Number.Collective] = [Number.Plural],
        [Tense.Future] = [Tense.Present],
        [Aspect.Aorist] = [Aspect.Perfective],
        [Mood.Subjunctive] = [Mood.Indicative],
        [Mood.Imperative] = [Mood.Subjunctive, Mood.Indicative],
    };

    //the order's own letters say where each constituent goes; an intransitive clause just has no O.
    //Obliques go together, as X, before or after the verb and object, or just before the verb
    private static IEnumerable<IReadOnlyList<Word>> Order(WordOrder order, IReadOnlyList<Word> subject, IReadOnlyList<Word> verb, IReadOnlyList<Word>? @object, IReadOnlyList<Word> obliques)
    {
        var letters = order.ConstituentWordOrder.ToString().Where(letter => letter != 'O' || @object is not null).ToList();
        if (obliques.Count > 0)
        {
            var verbAndObject = letters.Select((letter, i) => (letter, i)).Where(p => p.letter != 'S').Select(p => p.i).ToList();
            letters.Insert(order.ObliquePosition switch
            {
                ObliquePosition.BeforeVerbAndObject => verbAndObject.Min(),
                ObliquePosition.BetweenObjectAndVerb => letters.IndexOf('V'),
                _ => verbAndObject.Max() + 1,
            }, 'X');
        }
        return letters.Select(letter => letter switch
        {
            'S' => subject,
            'V' => verb,
            'X' => obliques,
            _ => @object!,
        });
    }
}

public record Sentence(IReadOnlyList<SurfaceForm> Words, string Translation, Orthography Orthography)
{
    public string Text => string.Join(" ", Words.Select(w => w.Spell(Orthography)));

    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //the sentence, then its morphemes and their glosses aligned word by word, then a free translation
    public string Interlinear()
    {
        var widths = Words.Select(w => Math.Max(w.Segmented(Orthography).Length, w.Gloss.Length)).ToList();
        string Aligned(Func<SurfaceForm, string> line) =>
            string.Join("  ", Words.Select((w, i) => line(w).PadRight(widths[i]))).TrimEnd();
        return string.Join(Environment.NewLine, Text, Aligned(w => w.Segmented(Orthography)), Aligned(w => w.Gloss), $"'{Translation}'");
    }
}
