namespace LanguageGenerator;

//what a sentence means, independent of any language: words are meanings, like "dog", which each
//language's lexicon turns into its own roots. Realizing one in a language is where all of that
//language's choices (word order, role markers, paradigms, phonology) come together
//TODO definiteness: there are no articles in the lexicon yet, so "the" only shows in translations
public record NounPhrase(string Noun, Number Number);
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
public record Clause(string Verb, Tense Tense, Aspect Aspect, Argument Subject, Argument? Object = null);

public static class Syntax
{
    public static Sentence Realize(Language language, Clause clause, string translation)
    {
        var subjectRole = clause.Object is null ? CaseUse.IntransitiveSubject : CaseUse.TransitiveSubject;
        //a language that drops subject pronouns leaves the verb's agreement, if any, to say who
        var subject = clause.Subject.Value is Pronoun && language.Pronouns.DropsSubjects ? [] : Inflect(language, clause.Subject, subjectRole);
        var @object = clause.Object.HasValue ? Inflect(language, clause.Object.GetValueOrDefault(), CaseUse.DirectObject) : null;
        //the verb agrees with its subject. TODO many ergative languages agree with the absolutive instead
        var (person, number) = clause.Subject.Agreement;
        var verb = language.Paradigms[PartOfSpeech.Verb].Inflect(language.Lexicon.Verb(clause.Verb),
            Features(language.Paradigms[PartOfSpeech.Verb], [clause.Tense, clause.Aspect, person, number]));

        //each constituent keeps its own particles with it
        var words = Order(language.WordOrder.ConstituentWordOrder, subject, verb, @object).SelectMany(c => c);
        return new Sentence([.. words.Select(language.Phonology.Realize)], translation, language.Orthography);
    }

    private static IReadOnlyList<Word> Inflect(Language language, Argument argument, CaseUse role)
    {
        var @case = CaseFor(language, language.RoleMarkers[role]);
        var nouns = language.Paradigms[PartOfSpeech.Noun];
        var pronouns = language.Pronouns.Paradigm;
        return argument switch
        {
            NounPhrase phrase => nouns.Inflect(language.Lexicon.Noun(phrase.Noun), Features(nouns, [phrase.Number, @case])),
            Pronoun pronoun => pronouns.Inflect(language.Pronouns.For(pronoun), Features(pronouns, [@case])),
        };
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

    //the order's own letters say where each constituent goes; an intransitive clause just has no O
    private static IEnumerable<IReadOnlyList<Word>> Order(ConstituentWordOrder order, IReadOnlyList<Word> subject, IReadOnlyList<Word> verb, IReadOnlyList<Word>? @object) =>
        order.ToString()
            .Where(letter => letter != 'O' || @object is not null)
            .Select(letter => letter switch
            {
                'S' => subject,
                'V' => verb,
                _ => @object!,
            });
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
