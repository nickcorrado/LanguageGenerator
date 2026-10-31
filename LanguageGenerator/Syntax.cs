namespace LanguageGenerator;

//what a sentence means, independent of any language. Realizing one in a language is where all
//of that language's choices (word order, role markers, paradigms, phonology) come together
//TODO definiteness: there are no articles in the lexicon yet, so "the" only shows in translations
public record NounPhrase(Lexeme Noun, Number Number);
public record Clause(Lexeme Verb, Tense Tense, Aspect Aspect, NounPhrase Subject, NounPhrase? Object = null);

public static class Syntax
{
    public static Sentence Realize(Language language, Clause clause, string translation)
    {
        var subjectRole = clause.Object is null ? CaseUse.IntransitiveSubject : CaseUse.TransitiveSubject;
        var subject = Inflect(language, clause.Subject, subjectRole);
        var @object = clause.Object is { } o ? Inflect(language, o, CaseUse.DirectObject) : null;
        //the verb agrees with its subject. TODO many ergative languages agree with the absolutive instead
        var verb = language.Paradigms[PartOfSpeech.Verb].Inflect(clause.Verb,
            Features(language.Paradigms[PartOfSpeech.Verb], [clause.Tense, clause.Aspect, Person.Third, clause.Subject.Number]));

        //each constituent keeps its own particles with it
        var words = Order(language.WordOrder.ConstituentWordOrder, subject, verb, @object).SelectMany(c => c);
        return new Sentence([.. words.Select(language.Phonology.Realize)], translation);
    }

    private static IReadOnlyList<Word> Inflect(Language language, NounPhrase phrase, CaseUse role)
    {
        var paradigm = language.Paradigms[PartOfSpeech.Noun];
        return paradigm.Inflect(phrase.Noun, Features(paradigm, [phrase.Number, CaseFor(language, language.RoleMarkers[role])]));
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
    //the language doesn't have (a future in a language with only present and past, say) falls back
    //on the category's first value
    private static List<Tagmeme> Features(Paradigm paradigm, IReadOnlyList<Tagmeme> wanted) =>
        [.. paradigm.Categories.Select(category => wanted.FirstOrDefault(category.Values.Contains, category.Values[0]))];

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

public record Sentence(IReadOnlyList<SurfaceForm> Words, string Translation)
{
    public string Text => string.Join(" ", Words.Select(w => w.Transcription));

    // https://www.eva.mpg.de/lingua/resources/glossing-rules.php
    //the sentence, then its morphemes and their glosses aligned word by word, then a free translation
    public string Interlinear()
    {
        var widths = Words.Select(w => Math.Max(w.Segmented.Length, w.Gloss.Length)).ToList();
        string Aligned(Func<SurfaceForm, string> line) =>
            string.Join("  ", Words.Select((w, i) => line(w).PadRight(widths[i]))).TrimEnd();
        return string.Join(Environment.NewLine, Text, Aligned(w => w.Segmented), Aligned(w => w.Gloss), $"'{Translation}'");
    }
}
