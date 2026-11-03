namespace LanguageGenerator;

//what a sentence means, independent of any language: words are meanings, like "dog", which each
//language's lexicon turns into its own roots. Realizing one in a language is where all of that
//language's choices (word order, role markers, paradigms, phonology) come together
//TODO definiteness: there are no articles in the lexicon yet, so "the" only shows in translations
//a noun, and any adjectives modifying it, in the order they're said in English, and how many of it
//there are, if a numeral says, as in 'three wolves'
public record NounPhrase(string Noun, Number Number, IReadOnlyList<Adjective>? Modifiers = null, int? Count = null)
{
    public IReadOnlyList<Adjective> Adjectives => Modifiers ?? [];

    //by value, adjectives and all, so that two phrases saying the same thing are equal
    public virtual bool Equals(NounPhrase? other) =>
        other is not null && Noun == other.Noun && Number == other.Number && Count == other.Count && Adjectives.SequenceEqual(other.Adjectives);

    public override int GetHashCode() => Adjectives.Aggregate(HashCode.Combine(Noun, Number, Count), HashCode.Combine);
}
//what fills a role: a noun phrase, or a pronoun
public union Argument(NounPhrase, Pronoun)
{
    //what the verb agrees with: a noun is third person
    public (Person Person, Number Number) Agreement => this switch
    {
        NounPhrase n => (Person.Third, n.Number),
        Pronoun p => (p.Person, p.Number),
    };
}
//a phrase saying where, to where, from where, to whom, with whom or with what something happened
public record Oblique(CaseUse Role, Argument Argument)
{
    public static IReadOnlyList<CaseUse> Roles { get; } =
        [CaseUse.IndirectObject, CaseUse.Location, CaseUse.MovementTo, CaseUse.MovementFrom, CaseUse.Accompaniment, CaseUse.Instrument];
}

//obliques are in the order they're said in English
public record Clause(string Verb, Tense Tense, Aspect Aspect, Argument Subject, Argument? Object = null, bool Negative = false, IReadOnlyList<Oblique>? Obliques = null)
{
    public IReadOnlyList<Oblique> Adjuncts => Obliques ?? [];

    //by value, obliques and all, like NounPhrase
    public virtual bool Equals(Clause? other) =>
        other is not null && Verb == other.Verb && Tense == other.Tense && Aspect == other.Aspect && Subject.Equals(other.Subject)
        && Object.Equals(other.Object) && Negative == other.Negative && Adjuncts.SequenceEqual(other.Adjuncts);

    public override int GetHashCode() => Adjuncts.Aggregate(HashCode.Combine(Verb, Tense, Aspect, Subject, Object, Negative), HashCode.Combine);
}

//a property, like "big", said of something
public record Adjective(string Meaning);
//what a copular clause says its subject is: a noun phrase, as in 'you are a fool', or an adjective,
//as in 'the gate is locked', or where it is, as in 'the dog is in the house', with a Location
public union Complement(NounPhrase, Adjective, Oblique);
public record CopularClause(Complement Complement, Tense Tense, Aspect Aspect, Argument Subject, bool Negative = false);

//anything a sentence can say
public union Statement(Clause, CopularClause);

public static class Syntax
{
    public static Sentence Realize(Language language, Statement statement, string translation) => statement switch
    {
        Clause c => Realize(language, c, translation),
        CopularClause c => Realize(language, c, translation),
    };

    public static Sentence Realize(Language language, Clause clause, string translation)
    {
        var subjectRole = clause.Object is null ? CaseUse.IntransitiveSubject : CaseUse.TransitiveSubject;
        var subject = Subject(language, clause.Subject, subjectRole);
        var @object = clause.Object.HasValue ? Inflect(language, clause.Object.GetValueOrDefault(), CaseUse.DirectObject) : null;
        var verb = Verb(language, language.Lexicon.Verb(clause.Verb), clause.Tense, clause.Aspect, clause.Subject, clause.Negative);
        var obliques = clause.Adjuncts.SelectMany(o => Oblique(language, o)).ToList();
        return Sentence(language, Order(language.WordOrder, subject, verb, @object, obliques), translation);
    }

    //an oblique takes its role's case, and its adposition, if the role has one, before or after it
    private static IReadOnlyList<Word> Oblique(Language language, Oblique oblique)
    {
        if (!LanguageGenerator.Oblique.Roles.Contains(oblique.Role))
        {
            throw new ArgumentException($"{oblique.Role} isn't an oblique role.", nameof(oblique));
        }
        var phrase = Inflect(language, oblique.Argument, oblique.Role);
        return language.RoleMarkers[oblique.Role] switch
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
        var subject = Subject(language, clause.Subject, CaseUse.IntransitiveSubject);
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
            if (location.Role != CaseUse.Location)
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
            NounPhrase noun => Inflect(language, noun, CaseUse.IntransitiveSubject),
            Adjective adjective when language.AdjectiveAgreement == AdjectiveAgreement.Agreement =>
                [AgreeingAdjective(language, language.Lexicon.Adjective(adjective.Meaning),
                    Features(language.Paradigms[PartOfSpeech.Noun], [AgreementOf(language, clause.Subject).Number, CaseFor(language, language.RoleMarkers[CaseUse.IntransitiveSubject])]),
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
    private static IReadOnlyList<Word> Subject(Language language, Argument subject, CaseUse role) =>
        subject.Value is Pronoun && language.Pronouns.DropsSubjects ? [] : Inflect(language, subject, role);

    //the verb agrees with its subject. TODO many ergative languages agree with the absolutive instead
    private static IReadOnlyList<Word> Verb(Language language, Lexeme verb, Tense tense, Aspect aspect, Argument subject, bool negative)
    {
        var (person, number) = AgreementOf(language, subject);
        var paradigm = language.Paradigms[PartOfSpeech.Verb];
        var words = paradigm.Inflect(verb, Features(paradigm, [tense, aspect, person, number])).ToList();
        var genders = language.Genders;
        var gender = genders.VerbsAgree ? GenderOf(language, subject) : null;
        if (gender is not null)
        {
            var main = words.FindIndex(w => w.Morphemes.Any(m => m.Value is Root));
            words[main] = genders.Agree(words[main], gender, nextToRoot: false);
        }
        return negative ? language.Negation.Apply(words) : words;
    }

    //the number a noun takes: its own, unless a numeral leaves it singular, as in Turkish üç kurt
    //'three wolf'
    private static Number NumberOf(Language language, NounPhrase phrase) =>
        phrase.Count is not null && language.Numerals.NounStaysSingular ? Number.Singular : phrase.Number;

    //what a verb or adjective agrees with: the person and number of the subject's word, so that a
    //verb is singular with 'three wolf' too, as in Hungarian
    private static (Person Person, Number Number) AgreementOf(Language language, Argument argument) => argument.Value switch
    {
        NounPhrase n => (Person.Third, NumberOf(language, n)),
        _ => argument.Agreement,
    };

    //the gender something agrees with: a noun's own, or a pronoun's, if the language has that gender.
    //A pronoun with no gender given has none to agree with, since what it refers to isn't known
    private static Gender? GenderOf(Language language, Argument argument) => argument.Value switch
    {
        NounPhrase n => language.Genders.Of(language.Lexicon.Noun(n.Noun)),
        Pronoun { Gender: { } g } when language.Genders.Values.Contains(g) => g,
        _ => null,
    };

    //an adjective agreeing with a noun: it takes the noun's markers for these features, but only its
    //affixes, since a particle belongs to the phrase and is said once, and a marker for its gender.
    //Its zero markers go unglossed, since the noun's gloss already shows them
    private static Word AgreeingAdjective(Language language, Lexeme adjective, IReadOnlyList<Tagmeme> features, Gender? gender)
    {
        var inflected = language.Paradigms[PartOfSpeech.Noun].Inflect(adjective with { PartOfSpeech = PartOfSpeech.Noun }, features);
        var word = inflected.First(w => w.Morphemes.Any(m => m.Value is Root)) with { Covert = null };
        return gender is null ? word : language.Genders.Agree(word, gender, nextToRoot: true);
    }

    //each constituent keeps its own particles with it
    private static Sentence Sentence(Language language, IEnumerable<IReadOnlyList<Word>> constituents, string translation) =>
        new([.. constituents.SelectMany(c => c).Select(language.Phonology.Realize)], translation, language.Orthography);

    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, CaseUse role)
    {
        var @case = CaseFor(language, language.RoleMarkers[role]);
        var nouns = language.Paradigms[PartOfSpeech.Noun];
        var pronouns = language.Pronouns.Paradigm;
        return argument switch
        {
            NounPhrase phrase => Modified(language, phrase, Features(nouns, [NumberOf(language, phrase), @case])),
            Pronoun pronoun => pronouns.Inflect(language.Pronouns.For(pronoun), Features(pronouns, [@case])),
        };
    }

    //the noun's words with its adjectives placed around it: after the noun in a language whose noun
    //phrases are head-initial, before it in one whose are head-final, in the order given either way.
    //A numeral goes outside them, before or after, as the language puts numerals, so that 'three big
    //wolves' is either three big wolf or wolf big three. The noun's particles stay outside it all
    private static IReadOnlyList<Word> Modified(Language language, NounPhrase phrase, IReadOnlyList<Tagmeme> features)
    {
        var nounLexeme = language.Lexicon.Noun(phrase.Noun);
        var noun = language.Paradigms[PartOfSpeech.Noun].Inflect(nounLexeme, features);
        if (phrase.Adjectives.Count == 0 && phrase.Count is null)
        {
            return noun;
        }
        var main = noun.ToList().FindIndex(w => w.Morphemes.Any(m => m.Value is Root));
        var head = noun[main];
        var agreement = language.AdjectiveAgreement;

        var adjectives = phrase.Adjectives.Select(a =>
        {
            var lexeme = language.Lexicon.Adjective(a.Meaning);
            return agreement == AdjectiveAgreement.Agreement
                ? AgreeingAdjective(language, lexeme, features, language.Genders.Of(nounLexeme))
                : new Word([lexeme.Root]);
        }).ToList();

        var headFirst = language.WordOrder.NounPhraseDirectionality == HeadDirectionality.Initial;
        List<Word> words = headFirst ? [head, .. adjectives] : [.. adjectives, head];
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
    private static Case CaseFor(Language language, RoleMarker marker)
    {
        var citation = language.MorphosyntacticAlignment switch
        {
            MorphosyntacticAlignment.NominativeAccusative => Case.Nominative,
            MorphosyntacticAlignment.ErgativeAbsolutive => Case.Absolutive,
            _ => Case.Direct,
        };
        return marker switch
        {
            Case c => c,
            Adposition a => a.Governs ?? citation,
            Unmarked => citation,
        };
    }

    //each category the paradigm inflects for takes whichever wanted value it distinguishes. A value
    //the language doesn't have falls back on the nearest one it does, and failing that on the
    //category's first value
    private static List<Tagmeme> Features(Paradigm paradigm, IReadOnlyList<Tagmeme> wanted) =>
        [.. paradigm.Categories.Select(category =>
            wanted.Where(w => w.GetType() == category.Values[0].GetType())
                .Select(w => Nearest(w, category.Values))
                .FirstOrDefault(v => v is not null) ?? category.Values[0])];

    //the value itself if it's available, or else the nearest available one it falls back on
    public static Tagmeme? Nearest(Tagmeme wanted, IReadOnlyCollection<Tagmeme> available) =>
        ((Tagmeme[])[wanted, .. Fallbacks.GetValueOrDefault(wanted, [])]).FirstOrDefault(available.Contains);

    //what a value turns into in a language that doesn't distinguish it, nearest first: a dual or a
    //paucal is still more than one, a language without a future speaks of it in the present, as
    //English often does, and an aorist is a perfective
    private static readonly Dictionary<Tagmeme, Tagmeme[]> Fallbacks = new()
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
