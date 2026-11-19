namespace LanguageGenerator;

/// <summary>
/// How a morphosyntax setting's value is written into what its stage drew, where no draw gave it.
/// These stages coin their markers along with what they decide, so a write reuses the forms the
/// draw already has, as a negative particle's form becomes a prefix's. A value that needs a form
/// where the draw has none, as a passive where it drew none, can't be written; but every such value
/// is drawn at least 4% of the time, so redrawing always gives it long before the limit.
/// </summary>
internal static class SettingWrites
{
    //a language's changes with one added or taken away, kept in the order changes apply
    public static IReadOnlyList<SoundChange> WithChange(IReadOnlyList<SoundChange> changes, IReadOnlyList<SoundChange> order, SoundChange change, bool on) =>
        [.. order.Where(c => c == change ? on : changes.Contains(c))];

    //which of a set of changes, at most one of which a language has, it has
    public static TKind ChangeKindOf<TKind>(IReadOnlyList<SoundChange> changes, IReadOnlyList<(TKind Kind, SoundChange? Change)> kinds) =>
        kinds.FirstOrDefault(k => k.Change is { } change && changes.Contains(change)) is { Change: not null } found ? found.Kind : kinds.Single(k => k.Change is null).Kind;

    public static IReadOnlyList<SoundChange> WithChangeKind<TKind>(IReadOnlyList<SoundChange> changes, IReadOnlyList<SoundChange> order,
        IReadOnlyList<(TKind Kind, SoundChange? Change)> kinds, TKind kind) where TKind : notnull =>
        [.. order.Where(c => kinds.FirstOrDefault(k => k.Change == c) is { Change: not null } k ? k.Kind.Equals(kind) : changes.Contains(c))];

    private static InvalidOperationException NoForm(string value) =>
        new($"{value} can't be written in, since what was drawn has no form to reuse, and no draw gave it. A draw gives it at least 4% of the time, so this shouldn't happen.");

    private static IReadOnlyList<Grammeme> GrammemesOf(Morpheme morpheme) => morpheme.Value switch
    {
        Affix a => a.Grammemes,
        Particle p => p.Grammemes,
        _ => throw new ArgumentException("A marker is an affix or a particle.", nameof(morpheme)),
    };

    //each new marker takes the form of the drawn marker in its place, or of the last one, so a
    //double negation written over a single one has the same form twice
    public static Negation WithNegation(Negation drawn, NegationStrategy strategy)
    {
        var grammemes = GrammemesOf(drawn.Markers[0].Marker);
        var forms = drawn.Markers.Select(m => m.Marker.Form).ToList();
        return new Negation(strategy, [.. Generator.NegationPositions(strategy).Select((position, i) =>
        {
            var form = forms[Math.Min(i, forms.Count - 1)];
            return (position, position is ExponentPosition.Prefix or ExponentPosition.Suffix
                ? (Morpheme)new Affix(form, grammemes)
                : new Particle(form, grammemes));
        })]);
    }

    //marking only the possessor, or nothing, has no affixes, and keeps the suffix position the
    //generator gives it
    public static Possession WithPossession(Possession drawn, PossessiveMarking marking) => marking switch
    {
        PossessiveMarking.Head or PossessiveMarking.Double => drawn.Affixes is null ? throw NoForm($"{marking} possession") : drawn with { Marking = marking },
        _ => drawn with { Marking = marking, Affixes = null, AffixPosition = ExponentPosition.Suffix },
    };

    //a gender system's first gender has no marker, and the rest reuse the drawn markers' forms in
    //turn. Where nothing was drawn, as in an isolating language, the genders have no markers
    public static GenderSystem WithGenders(GenderSystem drawn, NounGenders genders)
    {
        var system = new GenderSystem
        {
            Genders = genders,
            Assignment = drawn.Assignment,
            Markers = null,
            MarkerPosition = drawn.MarkerPosition,
            VerbsAgree = genders != NounGenders.None && drawn.VerbsAgree,
        };
        if (drawn.Markers is null || genders == NounGenders.None)
        {
            return system;
        }
        List<IReadOnlyList<Phoneme>> forms = [.. drawn.Values.Skip(1).Select(g => drawn.Markers[g].Form)];
        return new GenderSystem
        {
            Genders = genders,
            Assignment = system.Assignment,
            Markers = system.Values.Select((gender, i) => (gender, i)).ToDictionary(g => g.gender, g => new Affix(g.i == 0 ? [] : forms[(g.i - 1) % forms.Count], [g.gender])),
            MarkerPosition = system.MarkerPosition,
            VerbsAgree = system.VerbsAgree,
        };
    }

    public static GenderSystem WithAssignment(GenderSystem drawn, GenderAssignment assignment) => new()
    {
        Genders = drawn.Genders,
        Assignment = assignment,
        Markers = drawn.Markers,
        MarkerPosition = drawn.MarkerPosition,
        VerbsAgree = drawn.VerbsAgree,
    };

    public static PolarQuestion PolarQuestionOf(Questions questions) => questions.Polar.Value switch
    {
        QuestionParticle { Position: QuestionParticlePosition.Initial } => PolarQuestion.InitialParticle,
        QuestionParticle { Position: QuestionParticlePosition.Final } => PolarQuestion.FinalParticle,
        QuestionParticle => PolarQuestion.SecondParticle,
        QuestionAffix { Position: ExponentPosition.Prefix } => PolarQuestion.Prefix,
        QuestionAffix => PolarQuestion.Suffix,
        QuestionInversion => PolarQuestion.Inversion,
        _ => PolarQuestion.Intonation,
    };

    public static Questions WithPolarQuestion(Questions drawn, PolarQuestion polar)
    {
        IReadOnlyList<Phoneme>? form = drawn.Polar.Value switch
        {
            QuestionParticle p => p.Marker.Form,
            QuestionAffix a => a.Marker.Form,
            _ => null,
        };
        IReadOnlyList<Phoneme> Form() => form ?? throw NoForm($"{polar} for polar questions");
        PolarQuestionMarking marking = polar switch
        {
            PolarQuestion.InitialParticle => new QuestionParticle(new Particle(Form(), [Mood.Interrogative]), QuestionParticlePosition.Initial),
            PolarQuestion.FinalParticle => new QuestionParticle(new Particle(Form(), [Mood.Interrogative]), QuestionParticlePosition.Final),
            PolarQuestion.SecondParticle => new QuestionParticle(new Particle(Form(), [Mood.Interrogative]), QuestionParticlePosition.Second),
            PolarQuestion.Prefix => new QuestionAffix(new Affix(Form(), [Mood.Interrogative]), ExponentPosition.Prefix),
            PolarQuestion.Suffix => new QuestionAffix(new Affix(Form(), [Mood.Interrogative]), ExponentPosition.Suffix),
            PolarQuestion.Inversion => new QuestionInversion(),
            _ => new QuestionIntonation(),
        };
        return drawn with { Polar = marking };
    }

    public static ComparisonKind ComparisonKindOf(Comparison comparison) => comparison.Value switch
    {
        LocationalComparison => ComparisonKind.Locational,
        ExceedComparison => ComparisonKind.Exceed,
        ConjoinedComparison => ComparisonKind.Conjoined,
        _ => ComparisonKind.Particle,
    };

    //a locational comparison marks the standard as a source, as most do; 'than' and 'exceed' can
    //only take each other's form
    public static Comparison WithComparison(Comparison drawn, ComparisonKind kind)
    {
        var form = drawn.Value switch
        {
            ParticleComparison p => p.Than.Root.Form,
            ExceedComparison e => e.Exceed.Root.Form,
            _ => null,
        };
        return kind switch
        {
            ComparisonKind.Locational => new LocationalComparison(Role.Source),
            ComparisonKind.Conjoined => new ConjoinedComparison(),
            ComparisonKind.Exceed => new ExceedComparison(new Lexeme(new Root(form ?? throw NoForm("Exceed comparison"), "exceed"), PartOfSpeech.Verb)),
            _ => drawn.Value is ParticleComparison ? drawn
                : throw NoForm("Particle comparison"),
        };
    }

    //a word written over an affix would need the side of the adjective it goes on, which only the
    //word order says, so only an affix can be written over a word
    public static DegreeMarking WithComparative(DegreeMarking drawn, MarkerKind kind) => (kind, drawn.Marker.Value) switch
    {
        (MarkerKind.Affix, DegreeWord word) => drawn with { Marker = new DegreeAffix(new Affix(word.Marker.Form, word.Marker.Grammemes), ExponentPosition.Suffix) },
        (MarkerKind.Word, DegreeAffix) => throw NoForm("A comparative word"),
        _ => drawn,
    };

    public static Similative WithSimilative(Similative drawn, MarkerKind kind) => (kind, drawn.Value) switch
    {
        (MarkerKind.Affix, SimilativeWord word) => new SimilativeAffix(new Affix(word.Like.Root.Form, [Case.Similative]), ExponentPosition.Suffix),
        (MarkerKind.Word, SimilativeAffix) => throw NoForm("A similative word"),
        _ => drawn,
    };

    public static Passive? WithPassive(Passive? drawn, bool has) => has ? drawn ?? throw NoForm("A passive") : null;

    public static ModalKind KindOf(ModalMarking marking) => marking.Value switch
    {
        ModalAffix => ModalKind.Affix,
        ModalVerb => ModalKind.Verb,
        _ => ModalKind.Particle,
    };

    private static IReadOnlyList<Phoneme> FormOf(ModalMarking marking) => marking.Value switch
    {
        ModalAffix a => a.Marker.Form,
        ModalVerb v => v.Verb.Root.Form,
        ModalParticle p => p.Marker.Form,
    };

    private static bool? BeforeVerbOf(ModalMarking marking) => marking.Value switch
    {
        ModalVerb v => v.BeforeVerb,
        ModalParticle p => p.BeforeVerb,
        _ => null,
    };

    //each modality of a region has a marker of the region's kind and form, glossed for itself, as
    //the generator gives them
    private static Modals WithRegion(Modals modals, Modality modality, ModalKind kind, IReadOnlyList<Phoneme> form, ExponentPosition affixPosition, bool beforeVerb)
    {
        var markers = new Dictionary<Modality, ModalMarking>(modals.Markers);
        foreach (var m in modals.RegionOf(modality))
        {
            markers[m] = kind switch
            {
                ModalKind.Affix => new ModalAffix(new Affix(form, [m]), affixPosition),
                ModalKind.Verb => new ModalVerb(new Lexeme(new Root(form, ModalityMap.VerbGloss(m)), PartOfSpeech.Verb), beforeVerb),
                _ => new ModalParticle(new Particle(form, [m]), beforeVerb),
            };
        }
        return modals with { Markers = markers };
    }

    //a modality's region keeps its form in its new kind. An affix goes where the language's other
    //modal affixes go, or last; a verb or particle on the side the language's other modal words
    //go, which only the word order says when it has none
    public static Modals WithModalKind(Modals drawn, Modality modality, ModalKind kind)
    {
        var marker = drawn.Markers[modality];
        if (KindOf(marker) == kind)
        {
            return drawn;
        }
        var affixPosition = drawn.Markers.Values.Select(m => m.Value).OfType<ModalAffix>().Select(a => (ExponentPosition?)a.Position).FirstOrDefault() ?? ExponentPosition.Suffix;
        var beforeVerb = drawn.Markers.Values.Select(BeforeVerbOf).FirstOrDefault(b => b is not null)
            ?? (kind == ModalKind.Affix ? false : throw NoForm($"A modal {kind.ToString().ToLowerInvariant()} where every modal is an affix"));
        return WithRegion(drawn, modality, kind, FormOf(marker), affixPosition, beforeVerb);
    }

    //joining two regions gives them the first one's marker; parting them would need a form for
    //the second
    public static Modals WithModalEdge(Modals drawn, (Modality First, Modality Second) edge, bool joined)
    {
        if (drawn.Joined.Contains(edge) == joined)
        {
            return drawn;
        }
        if (!joined)
        {
            throw NoForm($"{edge.First.Name} apart from {edge.Second.Name}");
        }
        var kept = drawn.Markers[edge.First];
        var result = drawn with { Joined = [.. ModalityMap.Edges.Select(e => (e.First, e.Second)).Where(e => e == edge || drawn.Joined.Contains(e))] };
        return WithRegion(result, edge.First, KindOf(kept), FormOf(kept),
            kept.Value is ModalAffix affix ? affix.Position : ExponentPosition.Suffix, BeforeVerbOf(kept) ?? false);
    }
}
