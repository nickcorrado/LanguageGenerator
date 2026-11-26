namespace LanguageGenerator;

/// <summary>
/// How a morphosyntax setting's value is written into what its stage drew, where no draw gave it.
/// These stages coin their markers along with what they decide, so a write reuses the forms the
/// draw already has, as a negative particle's form becomes a prefix's. A value that needs a form
/// where the draw has none, as a passive where it drew none, can't be written; but every such value
/// is drawn at least 2% of the time, so redrawing gives it long before the limit, and where no draw
/// meets the whole spec, the draw that's kept is one whose missing values can all be written.
/// </summary>
internal static class SettingWrites
{
    //an inventory with a sound added where its rank puts it, or taken away
    public static IReadOnlyList<T> WithSound<T>(IReadOnlyList<T> inventory, T sound, bool on) where T : Phoneme =>
        !on ? [.. inventory.Where(p => p != sound)]
        : inventory.Contains(sound) ? inventory
        : [.. inventory.TakeWhile(p => Generator.Rank(p) <= Generator.Rank(sound)), sound, .. inventory.SkipWhile(p => Generator.Rank(p) <= Generator.Rank(sound))];

    //an inventory cut to its commonest sounds, or with the commonest of the rest added, as they rank
    public static IReadOnlyList<T> WithCount<T>(IReadOnlyList<T> inventory, IReadOnlyList<T> all, int count) where T : Phoneme =>
        inventory.Count >= count ? [.. inventory.Take(count)]
        : all.Except(inventory).OrderBy(Generator.Rank).Take(count - inventory.Count).Aggregate(inventory, (added, sound) => WithSound(added, sound, true));

    //a language's changes with one added or taken away, kept in the order changes apply
    public static IReadOnlyList<SoundChange> WithChange(IReadOnlyList<SoundChange> changes, IReadOnlyList<SoundChange> order, SoundChange change, bool on) =>
        [.. order.Where(c => c == change ? on : changes.Contains(c))];

    //which of a set of changes, at most one of which a language has, it has
    public static TKind ChangeKindOf<TKind>(IReadOnlyList<SoundChange> changes, IReadOnlyList<(TKind Kind, SoundChange? Change)> kinds) =>
        kinds.FirstOrDefault(k => k.Change is { } change && changes.Contains(change)) is { Change: not null } found ? found.Kind : kinds.Single(k => k.Change is null).Kind;

    public static IReadOnlyList<SoundChange> WithChangeKind<TKind>(IReadOnlyList<SoundChange> changes, IReadOnlyList<SoundChange> order,
        IReadOnlyList<(TKind Kind, SoundChange? Change)> kinds, TKind kind) where TKind : notnull =>
        [.. order.Where(c => kinds.FirstOrDefault(k => k.Change == c) is { Change: not null } k ? k.Kind.Equals(kind) : changes.Contains(c))];

    //a setting whose values each need forms a draw may not have, so it's only ever drawn
    public static Func<T, TValue, T> Unwritable<T, TValue>(string value) => (_, _) => throw NoForm(value);

    public static NounNumber NounNumberOf(Paradigm nouns) => nouns.Categories.FirstOrDefault(c => c.Is<Number>())?.Values.Count switch
    {
        null => NounNumber.None,
        2 => NounNumber.SingularPlural,
        _ => NounNumber.SingularDualPlural,
    };

    public static Tenses TensesOf(Paradigm verbs) => verbs.Categories.FirstOrDefault(c => c.Is<Tense>())?.Values.Count switch
    {
        null => Tenses.None,
        2 => Tenses.PresentPast,
        _ => Tenses.PresentPastFuture,
    };

    public static PronounSystem WithDropsSubjects(PronounSystem drawn, bool drops) => new()
    {
        FirstPersonPlural = drawn.FirstPersonPlural,
        GenderedPronouns = drawn.GenderedPronouns,
        Genders = drawn.Genders,
        Forms = drawn.Forms,
        Paradigm = drawn.Paradigm,
        DropsSubjects = drops,
    };

    public static Predication WithLocationalCopula(Predication drawn, bool has) =>
        has ? (drawn.LocationalCopula is not null ? drawn : throw NoForm("A locational copula")) : drawn with { LocationalCopula = null };

    private static bool IsAffix(Article article) => article.Position is ExponentPosition.Prefix or ExponentPosition.Suffix;

    public static DefiniteArticle DefiniteOf(Articles articles) => articles.Definite switch
    {
        null => DefiniteArticle.None,
        _ when articles.DefiniteFromDemonstrative => DefiniteArticle.Demonstrative,
        var article when IsAffix(article) => DefiniteArticle.Affix,
        _ => DefiniteArticle.Word,
    };

    public static IndefiniteArticle IndefiniteOf(Articles articles) => articles.Indefinite switch
    {
        null => IndefiniteArticle.None,
        var article when IsAffix(article) => IndefiniteArticle.Affix,
        var article when article.Forms.Values.Any(m => m.Value is Root) => IndefiniteArticle.One,
        _ => IndefiniteArticle.Word,
    };

    //an article can be taken away, but one of another kind needs forms where it goes
    public static Articles WithDefinite(Articles drawn, DefiniteArticle kind) =>
        kind == DefiniteOf(drawn) ? drawn : kind == DefiniteArticle.None ? drawn with { Definite = null, DefiniteFromDemonstrative = false } : throw NoForm($"A definite article that's a {kind}");

    public static Articles WithIndefinite(Articles drawn, IndefiniteArticle kind) =>
        kind == IndefiniteOf(drawn) ? drawn : kind == IndefiniteArticle.None ? drawn with { Indefinite = null } : throw NoForm($"An indefinite article that's {kind}");

    //an article stops agreeing in gender by keeping its first gender's forms for every noun
    public static Articles WithArticleGenderAgreement(Articles drawn, bool agrees)
    {
        static Article? Ungendered(Article? article)
        {
            if (article is not { AgreesInGender: true })
            {
                return article;
            }
            var first = article.Forms.Keys.First().Gender;
            return article with
            {
                Forms = article.Forms.Where(f => f.Key.Gender == first).ToDictionary(f => ((Gender?)null, f.Key.Number), f => f.Value.Value switch
                {
                    Affix a => (Morpheme)(a with { Grammemes = [.. a.Grammemes.Where(g => g is not Gender)] }),
                    Particle p => new Particle(p.Form, [.. p.Grammemes.Where(g => g is not Gender)]),
                    _ => f.Value,
                }),
            };
        }
        var agreesNow = drawn.Definite?.AgreesInGender == true || drawn.Indefinite?.AgreesInGender == true;
        return agrees == agreesNow ? drawn
            : agrees ? throw NoForm("An article agreeing in gender")
            : drawn with { Definite = Ungendered(drawn.Definite), Indefinite = Ungendered(drawn.Indefinite) };
    }

    /*
      a demonstrative moves between a word and an affix keeping its forms, which are a syllable
      either way, and an affix can't stand alone, so its pronouns need stems of their own, and a
      draw without them is passed over
     */
    public static Demonstratives WithDemonstrativePosition(Demonstratives drawn, ExponentPosition position)
    {
        var affix = position is ExponentPosition.Prefix or ExponentPosition.Suffix;
        return drawn with
        {
            Position = position,
            Forms = drawn.Forms.ToDictionary(f => f.Key, f => affix ? (Morpheme)new Affix(f.Value.Form, Glosses(f.Value), AccentOf(f.Value)) : new Particle(f.Value.Form, Glosses(f.Value))),
            Pronouns = affix && drawn.Pronouns.Value is SharedStems ? throw NoForm("A demonstrative affix, whose pronouns need stems of their own") : drawn.Pronouns,
        };

        static IReadOnlyList<Grammeme> Glosses(Morpheme m) => m.Value switch
        {
            Affix a => a.Grammemes,
            Particle p => p.Grammemes,
            _ => throw new ArgumentException("A demonstrative is an affix or a word.", nameof(m)),
        };
    }

    //a demonstrative's pronouns and words for places are what was drawn, as each has its own words or none
    public static Demonstratives WithOwnStems(Demonstratives drawn, bool own) => own == drawn.Pronouns.Value is OwnStems ? drawn
        : throw NoForm(own ? "Demonstrative pronouns with stems of their own" : "Demonstrative pronouns that are the words beside a noun");

    public static Demonstratives WithPlacesFromDemonstratives(Demonstratives drawn, bool from) => from ? drawn with { Places = new SharedPlaces() }
        : drawn.Places.Value is OwnPlaces ? drawn : throw NoForm("Words of their own for 'here' and 'there'");

    //a demonstrative stops agreeing in gender by keeping its first gender's forms for every noun,
    //which are the forms its pronouns' stems were taken from
    public static Demonstratives WithDemonstrativeGenderAgreement(Demonstratives drawn, bool agrees)
    {
        if (agrees == drawn.AgreesInGender)
        {
            return drawn;
        }
        if (agrees)
        {
            throw NoForm("A demonstrative agreeing in gender");
        }
        var first = drawn.Forms.Keys.First().Gender;
        return drawn with
        {
            Forms = drawn.Forms.Where(f => f.Key.Gender == first).ToDictionary(f => (f.Key.Distance, (Gender?)null, f.Key.Number), f => f.Value.Value switch
            {
                Affix a => (Morpheme)(a with { Grammemes = [.. a.Grammemes.Where(g => g is not Gender)] }),
                Particle p => new Particle(p.Form, [.. p.Grammemes.Where(g => g is not Gender)]),
                _ => f.Value,
            }),
        };
    }

    public static ImperativeForms ImperativesOf(Commands commands) => (commands.Singular, commands.Plural) switch
    {
        (null, null) => ImperativeForms.None,
        (not null, null) => ImperativeForms.SingularOnly,
        (null, not null) => ImperativeForms.PluralOnly,
        var (singular, plural) when singular.Equals(plural) => ImperativeForms.OneForBoth,
        _ => ImperativeForms.SingularAndPlural,
    };

    //an imperative written in reuses the form of one the draw has, glossed for its new numbers, though
    //a plural of its own always has a form, and two imperatives need two
    public static Commands WithImperatives(Commands drawn, ImperativeForms forms)
    {
        if (ImperativesOf(drawn) == forms)
        {
            return drawn;
        }
        static Morpheme Glossed(Morpheme marker, params Grammeme[] values) => marker switch
        {
            Affix a => a with { Grammemes = values },
            Particle p => new Particle(p.Form, values),
            _ => marker,
        };
        var any = drawn.Singular ?? drawn.Plural;
        var sounded = drawn.Plural is { Form.Count: > 0 } ? drawn.Plural : drawn.Singular is { Form.Count: > 0 } ? drawn.Singular : null;
        switch (forms)
        {
            case ImperativeForms.None:
                return drawn with { Singular = null, Plural = null };
            case ImperativeForms.SingularOnly when any is { } marker:
                return drawn with { Singular = Glossed(marker, Mood.Imperative, Number.Singular), Plural = null };
            case ImperativeForms.PluralOnly when sounded is { } marker:
                return drawn with { Singular = null, Plural = Glossed(marker, Mood.Imperative, Number.Plural) };
            case ImperativeForms.OneForBoth when any is { } marker:
                var both = Glossed(marker, Mood.Imperative);
                return drawn with { Singular = both, Plural = both };
            default:
                throw NoForm($"An imperative for {forms}");
        }
    }

    public static Commands WithProhibitiveNegator(Commands drawn, bool has) =>
        has ? (drawn.Prohibitive is not null ? drawn : throw NoForm("A prohibitive negator")) : drawn with { Prohibitive = null };

    public static ClauseAnd ClauseAndOf(Conjunctions conjunctions) => conjunctions switch
    {
        { ClauseAnd: null } => ClauseAnd.Juxtaposition,
        { NounAnd.Value: AndWord word } when word.Word == conjunctions.ClauseAnd => ClauseAnd.SameAsNouns,
        _ => ClauseAnd.OwnWord,
    };

    //nouns joined by 'with' need no word; nouns joined otherwise take the clauses' word, or none
    //where clauses have none
    public static Conjunctions WithAndIsWith(Conjunctions drawn, bool with) =>
        (drawn.NounAnd.Value is AndWith) == with ? drawn
        : with ? drawn with { NounAnd = new AndWith() }
        : drawn with { NounAnd = drawn.ClauseAnd is { } word ? new AndWord(word) : new AndJuxtaposed() };

    //clauses set side by side take nouns along, unless they're joined by 'with'; clauses joined like
    //nouns take the nouns' word; and a word of their own needs one apart from the nouns'
    public static Conjunctions WithClauseAnd(Conjunctions drawn, ClauseAnd kind) => (kind, drawn.NounAnd.Value) switch
    {
        _ when ClauseAndOf(drawn) == kind => drawn,
        (ClauseAnd.Juxtaposition, AndWith) => drawn with { ClauseAnd = null },
        (ClauseAnd.Juxtaposition, _) => drawn with { NounAnd = new AndJuxtaposed(), ClauseAnd = null },
        (ClauseAnd.SameAsNouns, AndWord word) => drawn with { ClauseAnd = word.Word },
        _ => throw NoForm($"Clauses joined {kind}"),
    };

    //a subordinator's region keeps its form in its new position, each subordinator with its own meaning
    private static Subordination WithRegion(Subordination subordination, Subordinator subordinator, Morpheme kept, ExponentPosition position)
    {
        var form = kept.Form;
        var markers = new Dictionary<Subordinator, SubordinatorMarking>(subordination.Markers);
        foreach (var s in subordination.RegionOf(subordinator))
        {
            List<Grammeme> meaning = [Linkage.For(s)];
            markers[s] = markers[s] with
            {
                Marker = position == ExponentPosition.Suffix ? new Affix(form, meaning, AccentOf(kept)) : new Particle(form, meaning),
                Position = position,
            };
        }
        return subordination with { Markers = markers };
    }

    public static Subordination WithSubordinatorPosition(Subordination drawn, Subordinator subordinator, ExponentPosition position) =>
        drawn.Markers[subordinator].Position == position ? drawn : WithRegion(drawn, subordinator, drawn.Markers[subordinator].Marker, position);

    //joining two regions gives them the first one's marker; parting them would need a form for the second
    public static Subordination WithSubordinatorEdge(Subordination drawn, (Subordinator First, Subordinator Second) edge, bool joined)
    {
        if (drawn.Joined.Contains(edge) == joined)
        {
            return drawn;
        }
        if (!joined)
        {
            throw NoForm($"{edge.First} apart from {edge.Second}");
        }
        var kept = drawn.Markers[edge.First];
        var result = drawn with { Joined = [.. SubordinatorMap.Edges.Select(e => (e.First, e.Second)).Where(e => e == edge || drawn.Joined.Contains(e))] };
        return WithRegion(result, edge.First, kept.Marker, kept.Position);
    }

    public static Subordination WithBalance(Subordination drawn, IReadOnlyList<Subordinator> subordinators, Balance balance) =>
        drawn with { Markers = drawn.Markers.ToDictionary(m => m.Key, m => subordinators.Contains(m.Key) ? m.Value with { Balance = balance } : m.Value) };

    /*
      a complementizer keeps its form in its new position. One that goes unsaid can't mark a
      quotation, which keeps the complementizer's word as a word of its own on the side it was on,
      as Ewe bé marks a quotation where it's no complementizer
     */
    public static Complementation WithComplementizer(Complementation drawn, ComplementKind kind, ComplementizerPosition position)
    {
        var marking = drawn.MarkingOf(kind);
        if (marking.Position == position)
        {
            return drawn;
        }
        /*
          a complementizer keeps its form wherever it goes. One said where the draw left it unsaid
          takes the other kind's, as Romance que serves both, or else the quotative word's, as words
          for 'say' so often become complementizers, and with neither the draw is passed over
         */
        var other = drawn.MarkingOf(kind == ComplementKind.Proposition ? ComplementKind.Event : ComplementKind.Proposition).Marker;
        var source = marking.Marker?.Marker ?? other?.Marker ?? (drawn.Quotative is { Value: Free word } ? (Morpheme?)word.Particle : null);
        var marker = position == ComplementizerPosition.None ? null
            : source is { } kept ? Generator.Complementizer(kept.Form, position, AccentOf(kept))
            : throw NoForm($"A complementizer for {(kind == ComplementKind.Proposition ? "a proposition" : "an event")}");
        var written = kind == ComplementKind.Proposition ? drawn with { Proposition = marking with { Marker = marker } } : drawn with { Event = marking with { Marker = marker } };
        return kind == ComplementKind.Proposition && marker is null && drawn.QuotativeKind == QuotativeKind.Complementizer && marking.Marker is { } unsaid
            ? written with { Quotative = QuotativeWord(unsaid) }
            : written;
    }

    //a quotative word made from a complementizer, on its side of the clause
    private static Free QuotativeWord(Exponent complementizer) =>
        new(new Particle(complementizer.Marker.Form, [Linkage.Quotative]), complementizer.Value is Free { Side: Side.Before } ? Side.Before : Side.After);

    /*
      a quotation marked with the complementizer needs one that's said, so a proposition's unsaid one
      is said at the clause's start, as most are. A word of its own is the complementizer's, where the
      quotation was marked with that, on its side, and there's none to give it otherwise
     */
    public static Complementation WithQuotative(Complementation drawn, QuotativeKind kind)
    {
        if (drawn.QuotativeKind == kind)
        {
            return drawn;
        }
        var proposition = drawn.Proposition;
        return kind switch
        {
            QuotativeKind.None => drawn with { Quotative = null },
            QuotativeKind.Word when drawn.QuotativeKind == QuotativeKind.Complementizer && proposition.Marker is { } complementizer =>
                drawn with { Quotative = QuotativeWord(complementizer) },
            QuotativeKind.Word => throw NoForm("A quotative word"),
            //written before the quotative changes, so that a quotative word's form can be the complementizer's
            QuotativeKind.Complementizer when proposition.Position == ComplementizerPosition.None =>
                WithComplementizer(drawn, ComplementKind.Proposition, ComplementizerPosition.Initial) with { Quotative = new QuotativeComplementizer() },
            QuotativeKind.Complementizer => drawn with { Quotative = new QuotativeComplementizer() },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    //a relativizer keeps its form between an affix and a word; an affix made from a word is a suffix, as most are
    public static Relativization WithRelativizer(Relativization drawn, RelativizerKind kind) => drawn.RelativizerKind == kind ? drawn : drawn with
    {
        Relativizer = (kind, drawn.Relativizer?.Value) switch
        {
            (RelativizerKind.None, _) => null,
            (RelativizerKind.Affix, Particle p) => new Bound(new Affix(p.Form, [Linkage.Relative], AffixAccent.None), Side.After),
            (RelativizerKind.Particle, Bound b) => new Particle(b.Affix.Form, [Linkage.Relative]),
            (RelativizerKind.Affix or RelativizerKind.Particle, _) => throw NoForm($"A relativizer {kind.ToString().ToLowerInvariant()}"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        },
    };

    //'want' keeps its marker's form and side between an affix and a word of its own; a verb has no form to give either
    public static Complementation WithDesiderative(Complementation drawn, DesiderativeKind kind) => drawn.DesiderativeKind == kind ? drawn : drawn with
    {
        Desiderative = kind switch
        {
            DesiderativeKind.Verb => null,
            DesiderativeKind.Affix => AsAffix(drawn.Desiderative, [Volition.Desiderative], "A desiderative affix"),
            DesiderativeKind.Particle => AsParticle(drawn.Desiderative, [Volition.Desiderative], "A desiderative particle"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        },
    };


    private static NoFormException NoForm(string value) =>
        new($"{value} can't be written in, since what was drawn has no form to reuse, and no draw gave it. A draw gives it at least 2% of the time, so this shouldn't happen.");

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
        var markers = drawn.Markers.Select(m => m.Marker).ToList();
        return new Negation(strategy, [.. Generator.NegationPositions(strategy).Select((position, i) =>
        {
            var kept = markers[Math.Min(i, markers.Count - 1)];
            return Exponent.At(position, kept.Form, grammemes, AccentOf(kept));
        })]);
    }

    /// <summary>The type ordinals are, as a spec names it.</summary>
    public static OrdinalType TypeOf(Ordinals ordinals) => ordinals switch
    {
        CardinalOrdinals => OrdinalType.Cardinal,
        FirstThenCardinalOrdinals => OrdinalType.FirstThenCardinal,
        DerivedOrdinals => OrdinalType.Derived,
        FirstThenDerivedOrdinals => OrdinalType.FirstThenDerived,
        FirstSecondThenDerivedOrdinals => OrdinalType.FirstSecondThenDerived,
    };

    /// <summary>Which noun possession is marked on, as a spec names it.</summary>
    public static PossessiveMarking MarkingOf(Possession possession) => (possession.MarksPossessor, possession.MarksPossessed) switch
    {
        (true, false) => PossessiveMarking.Dependent,
        (false, true) => PossessiveMarking.Head,
        (true, true) => PossessiveMarking.Double,
        (false, false) => PossessiveMarking.None,
    };

    //marking the possessed noun needs the affixes it was drawn with, and marking only the possessor,
    //or nothing, has none
    public static Possession WithPossession(Possession drawn, PossessiveMarking marking) => marking switch
    {
        PossessiveMarking.Head or PossessiveMarking.Double when drawn.Agreement is null => throw NoForm($"{marking} possession"),
        PossessiveMarking.Head or PossessiveMarking.Double => drawn with { MarksPossessor = marking == PossessiveMarking.Double },
        PossessiveMarking.Dependent or PossessiveMarking.None => drawn with { MarksPossessor = marking == PossessiveMarking.Dependent, Agreement = null },
        _ => throw new ArgumentOutOfRangeException(nameof(marking), marking, null),
    };

    //the side of the possessed noun its affixes go on, which is a suffix's where it has none
    public static ExponentPosition PossessiveAffixesOf(Possession possession) =>
        possession.Agreement is { Side: Side.Before } ? ExponentPosition.Prefix : ExponentPosition.Suffix;

    public static Possession WithPossessiveAffixes(Possession drawn, ExponentPosition position) => (drawn.Agreement, position) switch
    {
        ({ } agreement, ExponentPosition.Prefix or ExponentPosition.Suffix) => drawn with { Agreement = agreement with { Side = position == ExponentPosition.Prefix ? Side.Before : Side.After } },
        (null, ExponentPosition.Suffix) => drawn,
        (null, ExponentPosition.Prefix) => throw NoForm("A possessive prefix"),
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null),
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
        List<Affix> kept = [.. drawn.Values.Skip(1).Select(g => drawn.Markers[g])];
        return new GenderSystem
        {
            Genders = genders,
            Assignment = system.Assignment,
            Markers = system.Values.Select((gender, i) => (gender, i)).ToDictionary(g => g.gender,
                g => g.i == 0 ? new Affix([], [g.gender], AffixAccent.None) : kept[(g.i - 1) % kept.Count] with { Grammemes = [g.gender] }),
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
        var accent = drawn.Polar.Value is QuestionAffix { Marker: var marker } ? marker.Accent : AffixAccent.None;
        IReadOnlyList<Phoneme> Form() => form ?? throw NoForm($"{polar} for polar questions");
        PolarQuestionMarking marking = polar switch
        {
            PolarQuestion.InitialParticle => new QuestionParticle(new Particle(Form(), [Mood.Interrogative]), QuestionParticlePosition.Initial),
            PolarQuestion.FinalParticle => new QuestionParticle(new Particle(Form(), [Mood.Interrogative]), QuestionParticlePosition.Final),
            PolarQuestion.SecondParticle => new QuestionParticle(new Particle(Form(), [Mood.Interrogative]), QuestionParticlePosition.Second),
            PolarQuestion.Prefix => new QuestionAffix(new Affix(Form(), [Mood.Interrogative], accent), ExponentPosition.Prefix),
            PolarQuestion.Suffix => new QuestionAffix(new Affix(Form(), [Mood.Interrogative], accent), ExponentPosition.Suffix),
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
        var root = drawn.Value switch
        {
            ParticleComparison p => p.Than.Root,
            ExceedComparison e => e.Exceed.Root,
            _ => null,
        };
        return kind switch
        {
            ComparisonKind.Locational => new LocationalComparison(Role.Source),
            ComparisonKind.Conjoined => new ConjoinedComparison(),
            ComparisonKind.Exceed => new ExceedComparison(new Lexeme((root ?? throw NoForm("Exceed comparison")) with { Meaning = "exceed" }, PartOfSpeech.Verb)),
            _ => drawn.Value is ParticleComparison ? drawn
                : throw NoForm("Particle comparison"),
        };
    }

    //a word written over an affix would need the side of the adjective it goes on, which only the
    //word order says, so only an affix can be written over a word
    public static DegreeMarking WithComparative(DegreeMarking drawn, MarkerKind kind) => (kind, drawn.Marker.Value) switch
    {
        (MarkerKind.Affix, DegreeWord word) => drawn with { Marker = new DegreeAffix(new Affix(word.Marker.Form, word.Marker.Grammemes, AffixAccent.None), ExponentPosition.Suffix) },
        (MarkerKind.Word, DegreeAffix) => throw NoForm("A comparative word"),
        _ => drawn,
    };

    public static Similative WithSimilative(Similative drawn, MarkerKind kind) => (kind, drawn.Value) switch
    {
        (MarkerKind.Affix, SimilativeWord word) => new SimilativeAffix(new Affix(word.Like.Root.Form, [Case.Similative], AccentOf(word.Like.Root)), ExponentPosition.Suffix),
        (MarkerKind.Word, SimilativeAffix) => throw NoForm("A similative word"),
        _ => drawn,
    };

    public static Passive? WithPassive(Passive? drawn, bool has) => has ? drawn ?? throw NoForm("A passive") : null;

    //a marker as an affix, keeping the form and side of the word it was, if it was one
    private static Bound AsAffix(Exponent? drawn, IReadOnlyList<Grammeme> grammemes, string value) => drawn?.Value switch
    {
        Bound b => b with { Affix = b.Affix with { Grammemes = grammemes } },
        Free f => new Bound(new Affix(f.Particle.Form, grammemes, AffixAccent.None), f.Side),
        _ => throw NoForm(value),
    };

    //a marker as a word of its own, keeping the form and side of the affix it was, if it was one
    private static Free AsParticle(Exponent? drawn, IReadOnlyList<Grammeme> grammemes, string value) => drawn?.Value switch
    {
        Free f => f with { Particle = f.Particle with { Grammemes = grammemes } },
        Bound b => new Free(new Particle(b.Affix.Form, grammemes), b.Side),
        _ => throw NoForm(value),
    };

    /*
      an aspect's marker keeps its form and side between an affix and a word of its own. An
      auxiliary, which has no form to keep, goes on the side of the verb its marker was on, or else
      where the language's other aspects' words go, 'finish' aside, which always follows. A marker
      that was none has no form to give an affix or a word
     */
    public static AspectMarking? WithAspectKind(Aspects aspects, AspectMarking? drawn, Grammeme meaning, AspectKind kind)
    {
        if (KindOf(drawn) == kind)
        {
            return drawn;
        }
        Exponent? exponent = drawn?.Value switch
        {
            Bound b => b,
            Free f => f,
            _ => null,
        };
        var value = $"The {meaning.Name.ToLowerInvariant()} as {kind.ToString().ToLowerInvariant()}";
        static Side? WordSide(AspectMarking? marking) => marking?.Value switch
        {
            Free f => f.Side,
            AspectAuxiliary { Verb: not AspectVerb.Finish } a => a.BeforeVerb ? Side.Before : Side.After,
            _ => null,
        };
        AspectAuxiliary Auxiliary(AspectVerb verb) => AspectAuxiliary.Of(verb,
            (exponent?.Side ?? WordSide(drawn) ?? aspects.All.Select(WordSide).FirstOrDefault(s => s is not null) ?? throw NoForm(value)) == Side.Before);
        return kind switch
        {
            AspectKind.None => null,
            AspectKind.Affix => AsAffix(exponent, [meaning], value),
            AspectKind.Particle => AsParticle(exponent, [meaning], value),
            AspectKind.Have => Auxiliary(AspectVerb.Have),
            AspectKind.Be => Auxiliary(AspectVerb.Be),
            AspectKind.Finish => Auxiliary(AspectVerb.Finish),
            AspectKind.Sit => Auxiliary(AspectVerb.Sit),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    /// <summary>The kind a marking is, as a spec names it.</summary>
    public static AspectKind KindOf(AspectMarking? marking) => marking is not { } m ? AspectKind.None : m switch
    {
        Bound => AspectKind.Affix,
        Free => AspectKind.Particle,
        AspectAuxiliary { Verb: AspectVerb.Have } => AspectKind.Have,
        AspectAuxiliary { Verb: AspectVerb.Be } => AspectKind.Be,
        AspectAuxiliary { Verb: AspectVerb.Finish } => AspectKind.Finish,
        AspectAuxiliary { Verb: AspectVerb.Sit } => AspectKind.Sit,
        AspectAuxiliary a => throw new ArgumentOutOfRangeException(nameof(marking), a.Verb, null),
    };

    public static ModalKind KindOf(ModalMarking marking) => marking switch
    {
        Bound => ModalKind.Affix,
        ModalVerb => ModalKind.Verb,
        Free => ModalKind.Particle,
    };

    /*
      the accent a marker written in as an affix carries over from the morpheme it's made from: an
      affix's own, a root's stored accent as its own, and none from a particle, which has no stress
      to give. A written-in marker can't draw one, since writing in never draws
     */
    private static AffixAccent AccentOf(Morpheme source) => source switch
    {
        Root r => r.Accent is { } vowel ? AffixAccent.Own(vowel) : AffixAccent.None,
        Affix a => a.Accent,
        Particle => AffixAccent.None,
    };

    //and back, for a root made from an affix: only an accent on its own vowel is one a root can store
    private static int? RootAccentOf(AffixAccent accent) => accent.Kind switch
    {
        AffixAccentKind.Own => accent.Vowel,
        AffixAccentKind.None or AffixAccentKind.Before => null,
        _ => throw new ArgumentOutOfRangeException(nameof(accent), accent.Kind, null),
    };

    //a marker's form, and its accent as an affix would carry it
    private static (IReadOnlyList<Phoneme> Form, AffixAccent Accent) FormOf(ModalMarking marking) => marking switch
    {
        Bound b => (b.Affix.Form, b.Affix.Accent),
        ModalVerb v => (v.Verb.Root.Form, AccentOf(v.Verb.Root)),
        Free f => (f.Particle.Form, AffixAccent.None),
    };

    private static bool? BeforeVerbOf(ModalMarking marking) => marking switch
    {
        ModalVerb v => v.BeforeVerb,
        Free f => f.Side == Side.Before,
        Bound => null,
    };

    //each modality of a region has a marker of the region's kind and form, glossed for itself, as
    //the generator gives them
    private static Modals WithRegion(Modals modals, Modality modality, ModalKind kind, (IReadOnlyList<Phoneme> Form, AffixAccent Accent) form, Side affixSide, bool beforeVerb)
    {
        var markers = new Dictionary<Modality, ModalMarking>(modals.Markers);
        foreach (var m in modals.RegionOf(modality))
        {
            markers[m] = kind switch
            {
                ModalKind.Affix => new Bound(new Affix(form.Form, [m], form.Accent), affixSide),
                ModalKind.Verb => new ModalVerb(new Lexeme(new Root(form.Form, ModalityMap.VerbGloss(m), RootAccentOf(form.Accent)), PartOfSpeech.Verb), beforeVerb),
                _ => new Free(new Particle(form.Form, [m]), beforeVerb ? Side.Before : Side.After),
            };
        }
        return modals with { Markers = markers };
    }

    /*
      a modality's region keeps its form in its new kind. An affix goes where the language's other
      modal affixes go, or last; a verb or particle on the side the language's other modal words
      go, which only the word order says when it has none
     */
    public static Modals WithModalKind(Modals drawn, Modality modality, ModalKind kind)
    {
        var marker = drawn.Markers[modality];
        if (KindOf(marker) == kind)
        {
            return drawn;
        }
        var affixSide = drawn.Markers.Values.Select(m => m.Value).OfType<Bound>().Select(b => (Side?)b.Side).FirstOrDefault() ?? Side.After;
        var beforeVerb = drawn.Markers.Values.Select(BeforeVerbOf).FirstOrDefault(b => b is not null)
            ?? (kind == ModalKind.Affix ? false : throw NoForm($"A modal {kind.ToString().ToLowerInvariant()} where every modal is an affix"));
        return WithRegion(drawn, modality, kind, FormOf(marker), affixSide, beforeVerb);
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
            kept.Value is Bound affix ? affix.Side : Side.After, BeforeVerbOf(kept) ?? false);
    }

    /*
      indefinite pronouns move between kinds keeping the forms the draw has, and a kind needing forms
      it lacks passes the draw over. A mixed one that wasn't drawn mixed makes persons from nouns and
      things from question words, as Semelai does
     */
    public static Indefinites WithIndefinitePronouns(Indefinites drawn, IndefinitePronouns type)
    {
        if (type == drawn.Type)
        {
            return drawn;
        }
        //question words' markers serve persons and things alike, but words of their own are a kind's, and a generic noun needs none
        var markers = drawn.Persons.Value as InterrogativeIndefinites ?? drawn.Things.Value as InterrogativeIndefinites;
        IndefiniteSource Interrogative() => markers ?? throw NoForm("Indefinites made from question words");
        IndefiniteSource Special(IndefiniteSource source) => source.Value as SpecialIndefinites ?? throw NoForm("Indefinites of their own");
        return type switch
        {
            IndefinitePronouns.InterrogativeBased => drawn with { Persons = Interrogative(), Things = Interrogative() },
            IndefinitePronouns.GenericNounBased => drawn with { Persons = new GenericNounIndefinites(), Things = new GenericNounIndefinites() },
            IndefinitePronouns.Special => drawn with { Persons = Special(drawn.Persons), Things = Special(drawn.Things) },
            IndefinitePronouns.Mixed => drawn with { Persons = new GenericNounIndefinites(), Things = Interrogative() },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }
}

/// <summary>
/// Thrown where a value can't be written into what a stage drew, since the draw has no form for it,
/// so a draw missing it has to be passed over for one that has it.
/// </summary>
public sealed class NoFormException(string message) : InvalidOperationException(message);
