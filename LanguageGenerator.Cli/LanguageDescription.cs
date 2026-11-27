using LanguageGenerator;

namespace LanguageGenerator.Cli;

/// <summary>
/// What the program prints about a language: its features, its paradigms, and a few sentences in
/// it.
/// </summary>
public static class LanguageDescription
{
    //phonetic adds how each example sentence is said beneath it
    public static void Write(Language lang, TextWriter output, bool phonetic)
    {
        lang.Lexicon.Classify(ExampleNouns);
        WriteFeatures(lang, output);
        WriteParadigms(lang, output);
        WriteExamples(lang, output, phonetic);
    }

    //English's ending for an ordinal written in digits, as in 1st, 12th or 22nd
    private static string OrdinalEnding(int n) => (n % 100 / 10 == 1 ? 0 : n % 10) switch
    {
        1 => "st",
        2 => "nd",
        3 => "rd",
        _ => "th",
    };

    private static void WriteFeatures(Language lang, TextWriter output)
    {
        output.WriteLine($"{lang.Name}, 'the people' ({lang.Endonym.Gloss})");
        var phonology = lang.Phonology;
        output.WriteLine(phonology.SyllableRule);
        output.WriteLine($"Sound changes: {(phonology.SoundChanges.Count + phonology.SyllableChanges.Count == 0 ? "none" : string.Join(", ", phonology.SoundChanges.Concat(phonology.SyllableChanges)))}");
        //the allophones the roots are said with, under each phoneme they're said for, in the inventory's order
        List<Phoneme> inventory = [.. phonology.Vowels, .. phonology.Consonants];
        var allophones = lang.Lexicon.SelectMany(l => phonology.Realize(new Word([l.Root])).Segments).Where(s => s.Phoneme != s.Realizes)
            .GroupBy(s => s.Realizes).OrderBy(g => inventory.IndexOf(g.Key))
            .Select(g => $"/{g.Key.IpaSymbol}/ [{string.Join(" ", g.Select(s => s.Phoneme.IpaSymbol).Distinct())}]").ToList();
        if (allophones.Count > 0)
        {
            output.WriteLine($"Allophones: {string.Join(", ", allophones)}");
        }
        var sandhi = phonology.Sandhi;
        List<string> acrossWords = [.. sandhi.AcrossWords.Select(c => c.ToString()!), .. sandhi.FinalConsonantLoss ? ["final consonant loss, said again before a vowel (liaison)"] : Array.Empty<string>()];
        output.WriteLine($"Across words: {(acrossWords.Count == 0 ? "none" : string.Join(", ", acrossWords))}");
        var cliticStress = sandhi.CliticStress switch
        {
            CliticStress.Group => "stressed with their host as one word",
            CliticStress.Host => "keep their host's stress",
            _ => throw new ArgumentOutOfRangeException(nameof(lang), sandhi.CliticStress, null),
        };
        output.WriteLine($"Clitics: {cliticStress}");
        var repair = phonology.ClusterRepair == ClusterRepair.Epenthesis ? $"epenthesis of {phonology.EpentheticVowel.IpaSymbol}" : "deletion";
        output.WriteLine($"Illegal clusters: {repair}");
        output.WriteLine($"Stress: {phonology.Stress.Description}");
        if (phonology.SyllableContact != SyllableContact.Any)
        {
            output.WriteLine($"Syllable contact: {phonology.SyllableContact.Description}");
        }
        var orthography = lang.Orthography;
        var unusual = orthography.Spellings.Where(s => s.Value != s.Key.IpaSymbol).Select(s => $"{s.Key.IpaSymbol} = {s.Value}");
        var stressMark = orthography.Stress switch
        {
            StressSpelling.Unmarked => "",
            StressSpelling.Acute or StressSpelling.Grave => $", {orthography.Stress} where the stress breaks its rule",
            _ => throw new ArgumentOutOfRangeException(nameof(orthography.Stress), orthography.Stress, null),
        };
        output.WriteLine($"Spelling: {orthography.Style?.ToString() ?? "IPA"}{stressMark}");
        output.WriteLine($"  {string.Join(", ", unusual)}");
        //citation forms, spelled and then in IPA by syllable, with the stress marked
        foreach (var lexeme in lang.Lexicon)
        {
            var form = phonology.Realize(new Word([lexeme.Root]));
            var nounGender = lexeme.PartOfSpeech == PartOfSpeech.Noun && lang.GenderOf(lexeme) is { } g ? $", {g.Gloss}" : "";
            output.WriteLine($"{lexeme.Root.Meaning} ({lexeme.PartOfSpeech}{nounGender}): {form.Spell(orthography)} [{form.Transcribed()}]");
        }

        var order = lang.WordOrder;
        output.WriteLine($"{lang.MorphologicalType} ({lang.MorphologicalProfile})");
        output.WriteLine(order.ConstituentWordOrder);
        output.WriteLine($"{order.HeadDirectionality}, {order.VerbPhraseDirectionality}, {order.NounPhraseDirectionality}, {order.AdjectivePhraseDirectionality}, {order.AdpositionalPhraseDirectionality}");
        //each form with what it agrees in, as in "ta (M), la (F)"
        string Forms(Article article, string prefix, string suffix) => string.Join(", ", article.Forms.Select(f =>
        {
            var agreement = string.Join(".", new Grammeme?[] { f.Key.Gender, f.Key.Number }.OfType<Grammeme>().Select(g => g.Gloss));
            return $"{prefix}{f.Value.Spell(lang.Orthography)}{suffix}{(agreement.Length > 0 ? $" ({agreement})" : "")}";
        }));
        string DescribeArticle(Article? article, string name) => article switch
        {
            null => $"no {name} article",
            _ when article.Forms.Values.Any(f => f.Value is Root) => $"'one' as the {name} article",
            { Position: ExponentPosition.Prefix } => $"{name} prefix {Forms(article, "", "-")}",
            { Position: ExponentPosition.Suffix } => $"{name} suffix {Forms(article, "-", "")}",
            { Position: ExponentPosition.PrecedingParticle } => $"{name} article {Forms(article, "", "")} before the noun",
            _ => $"{name} article {Forms(article, "", "")} after the noun",
        };
        var articles = lang.Articles;
        var definite = articles.DefiniteFromDemonstrative ? $"the demonstrative as the definite article, {DescribeArticle(articles.Definite, "definite")}" : DescribeArticle(articles.Definite, "definite");
        output.WriteLine($"Articles: {definite}; {DescribeArticle(articles.Indefinite, "indefinite")}{(articles.WithDemonstrative ? "; the definite article stays beside a demonstrative" : "")}");

        var demonstratives = lang.Demonstratives;
        string Stem(Lexeme lexeme) => phonology.Realize(new Word([lexeme.Root])).Spell(orthography);
        var adnominal = string.Join(", ", demonstratives.Forms.Select(f =>
        {
            var spelled = f.Value.Spell(orthography);
            var form = demonstratives.Position switch { ExponentPosition.Prefix => $"{spelled}-", ExponentPosition.Suffix => $"-{spelled}", _ => spelled };
            return $"{form} ({string.Join(".", new Grammeme?[] { f.Key.Distance, f.Key.Gender, f.Key.Number }.OfType<Grammeme>().Select(g => g.Gloss))})";
        }));
        var where = demonstratives.Position switch
        {
            ExponentPosition.PrecedingParticle => "before the noun",
            ExponentPosition.FollowingParticle => "after the noun",
            _ => "on the noun",
        };
        var standing = demonstratives.Pronouns.Value is OwnStems
            ? $"stems of their own standing alone ({string.Join(", ", demonstratives.Distances.Select(d => $"{d.Gloss} {Stem(demonstratives.Pronoun(d))}"))})"
            : "the same standing alone";
        var places = demonstratives.Places.Value is SharedPlaces ? "made from the demonstratives"
            : $"words of their own ({string.Join(", ", demonstratives.Distances.Select(d => $"{demonstratives.Place(d).Root.Meaning} {Stem(demonstratives.Place(d))}"))})";
        output.WriteLine($"Demonstratives: {demonstratives.Contrast}, {adnominal}, {where}; {standing}; 'here' and 'there' {places}");

        var indefinites = lang.Indefinites;
        string Kind(Ontology kind) => indefinites.SourceOf(kind).Value switch
        {
            InterrogativeIndefinites => "question words",
            GenericNounIndefinites => $"'{Indefinites.NounFor(kind)}'",
            _ => "words of their own",
        };
        var made = indefinites.Persons.Value?.GetType() == indefinites.Things.Value?.GetType() ? $"made from {Kind(Ontology.Person)}"
            : $"'someone' made from {Kind(Ontology.Person)} and 'something' from {Kind(Ontology.Thing)}";
        var indefiniteMarking = "";
        if ((indefinites.Persons.Value as InterrogativeIndefinites ?? indefinites.Things.Value as InterrogativeIndefinites) is { } interrogative)
        {
            var markers = string.Join(", ", interrogative.Markers.Select(m =>
            {
                var spelled = m.Value.Marker.Spell(orthography);
                var form = m.Value.Position switch { ExponentPosition.Prefix => $"{spelled}-", ExponentPosition.Suffix => $"-{spelled}", _ => spelled };
                return $"{form} ({m.Key.ToString().ToLowerInvariant()})";
            }));
            var markerWhere = interrogative.Markers.Values.First().Position switch
            {
                ExponentPosition.PrecedingParticle => " before them",
                ExponentPosition.FollowingParticle => " after them",
                _ => "",
            };
            indefiniteMarking = $", marked {markers}{markerWhere}";
        }
        var negated = indefinites.Negation switch
        {
            NegativeIndefiniteNegation.Always => "'nobody' takes the verb's negation too",
            NegativeIndefiniteNegation.Never => "'nobody' leaves the verb unnegated",
            _ => "'nobody' takes the verb's negation too only where it follows the verb",
        };
        var quantifiers = string.Join(", ", indefinites.Quantifiers.Select(q => $"{q.Key.ToString().ToLowerInvariant()} {Stem(q.Value)}"));
        output.WriteLine($"Indefinites: {made}{indefiniteMarking}; {negated}; quantifiers {quantifiers}, {(indefinites.QuantifiersBeforeNoun ? "before" : "after")} the noun");

        output.WriteLine(lang.MorphosyntacticAlignment);
        output.WriteLine($"Cases: {string.Join(", ", lang.Cases.Select(c => c.Gloss))}");
        foreach (var (role, marker) in lang.RoleMarkers)
        {
            var spelled = marker.Adposition is { } a ? $" ({phonology.Realize(new Word([a.Lexeme.Root])).Spell(orthography)})" : "";
            output.WriteLine($"{role}: {marker}{spelled}");
        }
        output.WriteLine($"Obliques: {lang.WordOrder.ObliquePosition}");
        //each region of the configuration map with its marker, or none where it's said like 'at'
        foreach (var region in lang.Configurations.Regions)
        {
            var names = string.Join("/", region.Configurations);
            var described = lang.Configurations.Markers.TryGetValue(region.Head, out var marker)
                ? marker.Value is SpatialAdposition a ? $"{marker} ({phonology.Realize(new Word([a.Adposition.Lexeme.Root])).Spell(orthography)})"
                    : marker.Value is RelationalNoun n ? $"{marker} ({phonology.Realize(new Word([lang.Lexicon.Noun(n.Meaning).Root])).Spell(orthography)})"
                    : marker.ToString()
                : "unmarked, like at";
            output.WriteLine($"{names}: {described}");
        }

        var pronouns = lang.Pronouns;
        var gender = pronouns.GenderedPronouns == PronounGender.None ? "" : $", gender ({string.Join("/", pronouns.Genders.Select(g => g.Gloss))}) in {pronouns.GenderedPronouns}";
        output.WriteLine($"Pronouns ({pronouns.FirstPersonPlural} 'we'{gender}{(pronouns.DropsSubjects ? ", subjects usually dropped" : "")}):");
        output.WriteLine("  " + string.Join(", ", pronouns.Forms.Values.Distinct().Select(p =>
            $"{p.Root.Meaning} {phonology.Realize(new Word([p.Root])).Spell(orthography)}")));

        //spelled as they're written, not realized: an affix can be a lone consonant, which isn't a word
        string Spelled(ExponentPosition position, Morpheme marker) => marker.Form.Count == 0 ? "∅" : position switch
        {
            ExponentPosition.Prefix => $"{marker.Spell(orthography)}-",
            ExponentPosition.Suffix => $"-{marker.Spell(orthography)}",
            _ => marker.Spell(orthography),
        };
        string Negator(Negation negation) => $"{negation} ({string.Join(" ... ", negation.Markers.Select(m => Spelled(m.Position, m.Marker)))})";
        output.WriteLine($"Negation: {Negator(lang.Negation)}");
        var commands = lang.Commands;
        string Imperative(Morpheme? marker) => marker.HasValue ? Spelled(commands.Position, marker.GetValueOrDefault()) : "second person present";
        output.WriteLine($"Imperative: SG {Imperative(commands.Singular)}, PL {Imperative(commands.Plural)}");
        output.WriteLine($"Prohibitive: {(commands.ProhibitiveUsesImperative ? "the imperative" : "the second person present")} with {(commands.Prohibitive is { } prohibitive ? Negator(prohibitive) : "ordinary negation")}");
        var predication = lang.Predication;
        var copula = phonology.Realize(new Word([predication.Copula.Root])).Spell(orthography);
        var genders = lang.Genders;
        if (genders.Genders != NounGenders.None)
        {
            var markerSpellings = genders.Markers is null ? "no markers to agree with"
                : string.Join(", ", genders.Values.Select(g => $"{g.Gloss} {(genders.Markers.Affixes[g].Form.Count == 0 ? "∅" : ((Morpheme)genders.Markers.Affixes[g]).Spell(orthography))}"));
            var by = genders.Assignment == GenderAssignment.Semantic ? "by meaning" : "by meaning and form";
            output.WriteLine($"Genders: {string.Join("/", genders.Values.Select(g => g.Gloss))}, {by} ({markerSpellings}){(genders.VerbsAgree ? "; verbs agree" : "")}");
        }
        var adjectivePlace = lang.WordOrder.NounPhraseDirectionality == HeadDirectionality.Initial ? "after" : "before";
        output.WriteLine(lang.AdjectiveAgreement switch
        {
            AdjectiveAgreement.Agreement => $"Adjectives go {adjectivePlace} the noun and agree with it",
            AdjectiveAgreement.Phrasal => $"Adjectives go {adjectivePlace} the noun, and the phrase is marked at its edges",
            _ => $"Adjectives go {adjectivePlace} the noun, uninflected",
        });
        output.WriteLine($"Copula: {copula}{(predication.ZeroCopula ? ", dropped in the present" : "")}; adjectives {(predication.Adjectives == PredicativeAdjectives.Verbal ? "are verbs" : "take the copula")}");
        output.WriteLine(predication.LocationalCopula is { } locational
            ? $"Locational copula: {phonology.Realize(new Word([locational.Root])).Spell(orthography)}"
            : "Locations take the copula too");
        output.WriteLine(lang.Passive switch
        {
            null => "No passive",
            { Exponent.Position: ExponentPosition.Prefix } p => $"Passive: prefix {p.Exponent.Marker.Spell(orthography)}-",
            { Exponent.Position: ExponentPosition.Suffix } p => $"Passive: suffix -{p.Exponent.Marker.Spell(orthography)}",
            { Exponent.Position: ExponentPosition.PrecedingParticle } p => $"Passive: {p.Exponent.Marker.Spell(orthography)} before the verb",
            var p => $"Passive: {p.Exponent.Marker.Spell(orthography)} after the verb",
        });
        //an affix as Spelled spells it, or a word with the side of what it marks it goes on
        string On(Exponent exponent, string host) => exponent.Value is Bound
            ? Spelled(exponent.Position, exponent.Marker)
            : $"{exponent.Marker.Spell(orthography)} {(exponent.Side == Side.Before ? "before" : "after")} {host}";
        string OnVerb(Exponent exponent) => On(exponent, "the verb");
        string OnAdjective(Exponent exponent) => exponent.Value is Bound ? $"{On(exponent, "the adjective")} on the adjective" : On(exponent, "the adjective");
        string SpelledWord(Lexeme word) => phonology.Realize(new Word([word.Root])).Spell(orthography);
        var conjunctions = lang.Conjunctions;
        var nounAnd = conjunctions.NounAnd.Value switch
        {
            AndWord and => SpelledWord(and.Word),
            AndWith => "the word for 'with'",
            _ => "nothing, side by side",
        };
        var clauseAnd = conjunctions.ClauseAnd is { } word ? SpelledWord(word) : "nothing, side by side";
        output.WriteLine($"Conjunctions: 'and' {nounAnd} between nouns and {clauseAnd} between clauses; 'or' {SpelledWord(conjunctions.Or)}; 'but' {SpelledWord(conjunctions.But)}");
        var subordinators = string.Join(", ", lang.Subordination.Markers.Select(m =>
        {
            var marker = m.Value.Marker.Spell(orthography);
            var form = m.Value.Position switch
            {
                ExponentPosition.Suffix => $"-{marker}",
                ExponentPosition.PrecedingParticle => $"{marker} …",
                _ => $"… {marker}",
            };
            var balance = m.Value.Balance switch { Balance.Deranked => ", bare verb", Balance.Either => ", bare verb if the subject is shared", _ => "" };
            var role = m.Value.MarkedAs is { } r && m.Value.Balance != Balance.Balanced ? $", marked like the {r.ToString().ToLowerInvariant()} when bare" : "";
            return $"{m.Key.ToString().ToLowerInvariant()} {form} ({(m.Value.BeforeMain ? "first" : "after")}{balance}{role})";
        }));
        output.WriteLine($"Subordinate clauses: {subordinators}");
        var complementation = lang.Complementation;
        string Complements(ComplementKind kind)
        {
            var marking = complementation.MarkingOf(kind);
            var form = marking.Marker is not { } complementizer ? "unmarked" : complementizer.Position switch
            {
                ExponentPosition.Suffix => $"-{complementizer.Marker.Spell(orthography)}",
                ExponentPosition.PrecedingParticle => $"{complementizer.Marker.Spell(orthography)} …",
                _ => $"… {complementizer.Marker.Spell(orthography)}",
            };
            var balance = marking.Balance switch { Balance.Deranked => ", bare verb", Balance.Either => ", bare verb if the subject is shared", _ => "" };
            var role = marking.MarkedAs is { } r && marking.Balance != Balance.Balanced ? $", marked like the {(r == Role.DirectObject ? "object" : r.ToString().ToLowerInvariant())} when bare" : "";
            return $"{form}{balance}{role}";
        }
        var quotative = complementation.QuotativeMarker is var (quote, at)
            ? at switch { ExponentPosition.Suffix => $"-{quote.Spell(orthography)}", ExponentPosition.PrecedingParticle => $"{quote.Spell(orthography)} …", _ => $"… {quote.Spell(orthography)}" }
            : "unmarked";
        var want = complementation.Desiderative?.Value switch
        {
            Bound b => $"'want' is {OnVerb(b)} on the verb",
            Free f => $"'want' is {OnVerb(f)}",
            _ => "'want' is a verb",
        };
        output.WriteLine($"Complement clauses: 'that' {Complements(ComplementKind.Proposition)}; 'to' {Complements(ComplementKind.Event)}; quotations {quotative}; " +
            $"{(complementation.Last ? "last in the clause" : "where the object goes")}; {want}");
        var relativization = lang.Relativization;
        static string Strategy(RelativeStrategy strategy) => strategy switch
        {
            RelativeStrategy.Gap => "a gap",
            RelativeStrategy.Pronoun => "a relative pronoun",
            _ => "a resumptive pronoun",
        };
        var marked = relativization.Relativizer?.Value switch
        {
            Bound affix => $"{Spelled(((Exponent)affix).Position, affix.Affix)} on a participle",
            Particle particle => $"marked by {((Morpheme)particle).Spell(orthography)} beside the noun",
            _ => "unmarked",
        };
        var relativePronoun = relativization.Pronoun.Value is OwnRelativePronoun own ? SpelledWord(own.Word) : "the question words";
        output.WriteLine($"Relative clauses: {(relativization.BeforeNoun ? "before" : "after")} the noun, {marked}; subjects by {Strategy(relativization.Subject)}, " +
            $"objects by {Strategy(relativization.Object)}, obliques by {Strategy(relativization.Oblique)}; relative pronoun {relativePronoun}");
        output.WriteLine("Comparison: " + lang.Comparison switch
        {
            LocationalComparison l => $"the standard is marked like a place, as the {l.Role.ToString().ToLowerInvariant()} is",
            ParticleComparison p => $"the particle {phonology.Realize(new Word([p.Than.Root])).Spell(orthography)} {(p.BeforeStandard ? "before" : "after")} the standard",
            ConjoinedComparison => "two clauses, 'X is big, Y is not big'",
            ExceedComparison e => $"the verb {SpelledWord(e.Exceed)} 'exceed' with the standard as its object",
        });
        output.WriteLine($"Comparatives: {OnAdjective(lang.Degree.Marker)}, {(lang.Degree.WithStandard ? "with or without a standard" : "only without a standard")}");
        output.WriteLine("Similes: " + lang.Similative switch
        {
            SimilativeWord w => $"the word {SpelledWord(w.Like)} {(w.BeforeStandard ? "before" : "after")} what something is like",
            SimilativeAffix a => $"{On(a.Marker, "what something is like")} on what something is like",
        });
        output.WriteLine($"Very: {On(lang.Intensifier, "the adjective")}");
        output.WriteLine($"Manner adverbs: {(lang.Manner is { } manner ? OnAdjective(manner) : "the adjective itself")}");
        string Modal(Modality modality, ModalMarking marking) => marking switch
        {
            Bound b => OnVerb(b),
            Free f => OnVerb(f),
            ModalVerb v when modality is EpistemicModality => $"the verb {SpelledWord(v.Verb)} said impersonally, with the clause as its complement",
            ModalVerb v => $"the verb {SpelledWord(v.Verb)} {(v.BeforeVerb ? "before" : "after")} the bare verb",
        };
        output.WriteLine($"Modals: {string.Join(", ", lang.Modals.Markers.Select(m => $"{m.Key.Gloss} {Modal(m.Key, m.Value)}"))}");
        string Aspectual(Grammeme aspect, AspectMarking? marking, string without) => marking?.Value switch
        {
            null => without,
            Bound b => OnVerb(b),
            Free f => OnVerb(f),
            AspectAuxiliary x when aspect is Aspect auxiliaryOf => $"the verb {SpelledWord(lang.AuxiliaryVerb(x, auxiliaryOf))} {(x.BeforeVerb ? "before" : "after")} the bare verb",
            _ => throw new InvalidOperationException(),
        };
        output.WriteLine($"Perfect: {Aspectual(Aspect.Perfect, lang.Aspects.Perfect, "none, so it's said as a perfective past")}");
        output.WriteLine($"Progressive: {Aspectual(Aspect.Progressive, lang.Aspects.Progressive, "none, so it's said as an imperfective")}");
        output.WriteLine($"Habitual: {Aspectual(Aspect.Habitual, lang.Aspects.Habitual, "none, so it's said as an imperfective")}");
        output.WriteLine($"Inceptive: {Aspectual(Phase.Inceptive, lang.Aspects.MarkingOf(Phase.Inceptive), "none, so it's said with a verb for 'begin'")}");
        output.WriteLine($"Cessative: {Aspectual(Phase.Cessative, lang.Aspects.MarkingOf(Phase.Cessative), "none, so it's said with a verb for 'stop'")}");
        var questions = lang.Questions;
        var polar = questions.Polar switch
        {
            QuestionParticle p => $"the particle {((Morpheme)p.Marker).Spell(orthography)} {p.Position switch
            {
                QuestionParticlePosition.Initial => "first",
                QuestionParticlePosition.Final => "last",
                _ => "after the first word",
            }}",
            QuestionAffix a => $"{OnVerb(a.Marker)} on the verb",
            QuestionInversion => "the verb first",
            QuestionIntonation => "intonation alone",
        };
        var asking = string.Join(", ", [.. questions.Words.Select(w => $"{w.Value.Root.Meaning} {SpelledWord(w.Value)}"), .. questions.Adverbs.Select(a => $"{a.Value.Root.Meaning} {SpelledWord(a.Value)}")]);
        output.WriteLine($"Questions: yes-no by {polar}; question words {(questions.FrontsQuestionWords ? "first" : "in place")}: {asking}");
        var numerals = lang.Numerals;
        string Say(int n) => string.Join(" ", numerals.Say(n).Select(w => phonology.Realize(new Word([w.Root])).Spell(orthography)));
        var counting = numerals.Base == NumeralBase.Restricted
            ? $"up to {numerals.Limit}, then 'many'"
            : $"{numerals.Base.ToString().ToLowerInvariant()}, {(numerals.LargestFirst ? "largest" : "units")} first";
        output.WriteLine($"Numerals ({counting}; {(numerals.BeforeNoun ? "before" : "after")} the noun{(numerals.NounStaysSingular ? ", which stays singular" : "")}):");
        output.WriteLine("  " + string.Join(", ", ((int[])[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 20, 37, 100]).Select(n => $"{n} {Say(n)}")));
        var ordinals = numerals.Ordinals;
        var marking = ordinals.Marker switch
        {
            { Position: ExponentPosition.Prefix } m => $"{m.Marker.Spell(orthography)}- before the numeral",
            { Position: ExponentPosition.Suffix } m => $"-{m.Marker.Spell(orthography)} after the numeral",
            { Position: ExponentPosition.PrecedingParticle } m => $"{m.Marker.Spell(orthography)} before the numeral",
            { Position: ExponentPosition.FollowingParticle } m => $"{m.Marker.Spell(orthography)} after the numeral",
            _ => "the cardinal",
        };
        string SayOrdinal(int n) => string.Join(" ", numerals.SayOrdinal(n).Select(w => phonology.Realize(w).Spell(orthography)));
        var suppletive = ordinals.Second is not null ? "words for 'first' and 'second', then "
            : ordinals.First is not null ? "a word for 'first', then "
            : "";
        output.WriteLine($"Ordinals ({suppletive}{marking}):");
        output.WriteLine("  " + string.Join(", ", ((int[])[1, 2, 3, 4, 10, 37]).Select(n => $"{n}{OrdinalEnding(n)} {SayOrdinal(n)}")));
        var possession = lang.Possession;
        var possessedMarkers = possession.Agreement is not { } agreement ? ""
            : $" ({string.Join(", ", agreement.Affixes.Select(a => $"{Grammeme.JoinGlosses(a.Value.Grammemes)} {(agreement.Side == Side.Before ? $"{((Morpheme)a.Value).Spell(orthography)}-" : $"-{((Morpheme)a.Value).Spell(orthography)}")}"))})";
        output.WriteLine($"Possessors go {(possession.PossessorFirst ? "before" : "after")} the noun, " + (possession.MarksPossessor, possession.MarksPossessed) switch
        {
            (true, false) => $"marked {lang.RoleMarkers[Role.Possessor]}",
            (false, true) => $"and the noun agrees with them{possessedMarkers}",
            (true, true) => $"marked {lang.RoleMarkers[Role.Possessor]}, and the noun agrees with them{possessedMarkers}",
            (false, false) => "unmarked",
        });
    }

    private static void WriteParadigms(Language lang, TextWriter output)
    {
        var phonology = lang.Phonology;
        var orthography = lang.Orthography;
        var pronouns = lang.Pronouns;
        /*
          each paradigm, shown on the first lexeme that uses it, and the pronouns' on 1SG. Spelled surface
          forms come first, with the underlying form after them in IPA wherever a sound change or cluster repair
          changed something. Where stress is free, each form's transcription shows where its stress
          falls, on the first lexeme whose root stores no accent, if any, so its affixes' accents decide
         */
        var free = phonology.Stress.Free;
        var shown = lang.Paradigms.All.Select(p => (Paradigm: p, Lexeme: lang.Lexicon.Where(l => l.PartOfSpeech == p.PartOfSpeech).OrderBy(l => l.Root.Accent is null ? 0 : 1).First()))
            .Append((pronouns.Paradigm, pronouns.For(new(Person.First, Number.Singular))));
        foreach (var (paradigm, lexeme) in shown)
        {
            output.WriteLine();
            var slots = paradigm.Slots.Count == 0 ? "no inflection" : string.Join(", ", paradigm.Slots);
            output.WriteLine($"{paradigm.PartOfSpeech} paradigm: {slots}");
            foreach (var cell in paradigm.Cells)
            {
                var forms = paradigm.Inflect(lexeme, cell).Select(phonology.Realize).ToList();
                var surface = string.Join(" ", forms.Select(f => f.Segmented(orthography)));
                var changed = forms.Any(f => !f.Phonemes.SequenceEqual(f.Word.Morphemes.SelectMany(m => m.Form)));
                var underlying = string.Join(" ", forms.Select(f => f.Word.Segmented(Orthography.Ipa)));
                var gloss = string.Join(" ", forms.Select(f => f.Gloss));
                var stress = free ? $" [{string.Join(" ", forms.Select(f => f.Transcribed()))}]" : "";
                output.WriteLine($"  {surface,-24} {gloss,-20}{stress}{(changed ? $" /{underlying}/" : "")}");
            }
        }
    }

    private static readonly IReadOnlyDictionary<string, SemanticClass> ExampleNouns =
        NounFile.Parse("woman F\nperson HUM\ndog ANIM\ncat ANIM\nbird ANIM\nfish ANIM");

    private static void WriteExamples(Language lang, TextWriter output, bool phonetic)
    {
        //a few sentences, glossed
        NounPhrase Phrase(string noun, Number number) => new NounPhrase(new Noun(noun, number, [], null, null, null, Definiteness.Definite, null), null);
        (Sentence Clause, string Translation)[] examples =
        [
            (Declared(new Event(new Doing("see", Phrase("cat", Number.Singular)), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), "the dog saw the cat"),
            (Declared(new Event(new Doing("sleep", null), Phrase("bird", Number.Plural), [], []), Tense.Present, Aspect.Imperfective, false), "the birds are sleeping"),
            (Declared(new Event(new Doing("see", new NounPhrase(new Noun("cat", Number.Plural, [], null, null, null, null, null), null)), new NounPhrase(new Noun("dog", Number.Singular, [], null, null, null, Definiteness.Indefinite, null), null), [], []), Tense.Past, Aspect.Perfective, false), "a dog saw cats"),
            (Declared(new Event(new Doing("see", Phrase("dog", Number.Plural)), Phrase("person", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), "the person sees the dogs"),
            (Declared(new Event(new Doing("see", new Pronoun(Person.Second, Number.Singular)), new Pronoun(Person.First, Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), "I see you"),
            (Declared(new Event(new Doing("sleep", null), new Pronoun(Person.First, Number.Plural), [], []), Tense.Past, Aspect.Imperfective, false), "we were sleeping"),
            (Declared(new Event(new Doing("see", Phrase("cat", Number.Singular)), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Perfective, true), "the dog didn't see the cat"),
            (Declared(new Event(new Doing("see", new NounPhrase(new Noun("cat", Number.Plural, [new("small", false)], null, null, null, Definiteness.Definite, null), null)), new NounPhrase(new Noun("dog", Number.Singular, [new("big", false)], null, null, null, Definiteness.Definite, null), null), [], []), Tense.Past, Aspect.Perfective, false), "the big dog saw the small cats"),
            (Declared(new Event(new Doing("see", new NounPhrase(new Noun("sun", Number.Singular, [new("big", false)], null, null, null, Definiteness.Definite, null), null)), new NounPhrase(new Noun("woman", Number.Singular, [new("old", false)], null, null, null, Definiteness.Definite, null), null), [], []), Tense.Past, Aspect.Perfective, false), "the old woman saw the big sun"),
            (Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Singular), [new(Role.Location, new NounPhrase(new Noun("house", Number.Singular, [new("big", false)], null, null, null, Definiteness.Definite, null), null), Configuration.In)], []), Tense.Past, Aspect.Imperfective, false), "the dog was sleeping in the big house"),
            (Declared(new Event(new Doing("give", Phrase("fish", Number.Singular)), Phrase("person", Number.Singular), [new(Role.Recipient, Phrase("cat", Number.Plural), Configuration.At)], []), Tense.Past, Aspect.Perfective, false), "the person gave the fish to the cats"),
            (Declared(new Event(new Doing("walk", null), new Pronoun(Person.First, Number.Singular), [new(Role.Source, Phrase("tree", Number.Singular), Configuration.At), new(Role.Goal, Phrase("water", Number.Singular), Configuration.At), new(Role.Companion, new Pronoun(Person.Second, Number.Singular), Configuration.At)], []), Tense.Past, Aspect.Perfective, false), "I walked from the tree to the water with you"),
            (Declared(new Event(new Doing("see", new NounPhrase(new Noun("dog", ClauseFile.NumberFor(3), [new("big", false)], 3, null, null, null, null), null)), Phrase("person", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), "the person saw three big dogs"),
            (Declared(new Event(new Doing("sleep", null), new NounPhrase(new Noun("dog", Number.Singular, [], null, 3, null, Definiteness.Definite, null), null), [new(Role.Location, new NounPhrase(new Noun("house", Number.Singular, [], null, 1, null, Definiteness.Definite, null), null), Configuration.In)], []), Tense.Past, Aspect.Imperfective, false), "the third dog was sleeping in the first house"),
            (Declared(new Event(new Doing("walk", null), Phrase("cat", Number.Singular), [new(Role.Source, Phrase("house", Number.Singular), Configuration.In), new(Role.Route, Phrase("water", Number.Singular), Configuration.Over)], []), Tense.Past, Aspect.Perfective, false), "the cat walked out of the house over the water"),
            (Declared(new Event(new Being(new Oblique(Role.Location, Phrase("tree", Number.Singular), Configuration.Under)), Phrase("bird", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), "the bird is under the tree"),
            (Declared(new Event(new Doing("see", new NounPhrase(new Noun("cat", Number.Plural, [], null, null, null, null, new Pronoun(Person.First, Number.Singular)), null)), new NounPhrase(new Noun("dog", Number.Singular, [], null, null, null, null, Phrase("person", Number.Singular)), null), [], []), Tense.Past, Aspect.Perfective, false), "the person's dog saw my cats"),
        ];
        (Sentence Clause, string Translation)[] copular =
        [
            (Declared(new Event(new Being(new Adjective("big", false)), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), "the dog is big"),
            (Declared(new Event(new Being(new NounPhrase(new Noun("bird", Number.Singular, [], null, null, null, Definiteness.Indefinite, null), null)), new Pronoun(Person.Second, Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), "you are a bird"),
            (Declared(new Event(new Being(new Oblique(Role.Location, Phrase("house", Number.Singular), Configuration.In)), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), "the dog is in the house"),
        ];
        (Sentence Clause, string Translation)[] commands =
        [
            (Told(new Event(new Doing("eat", Phrase("fish", Number.Singular)), new Pronoun(Person.Second, Number.Singular), [], []), false), "eat the fish"),
            (Told(new Event(new Doing("walk", null), new Pronoun(Person.Second, Number.Plural), [new(Role.Goal, Phrase("house", Number.Singular), Configuration.At)], []), false), "go to the house, all of you"),
            (Told(new Event(new Doing("eat", Phrase("fish", Number.Singular)), new Pronoun(Person.Second, Number.Singular), [], []), true), "don't eat the fish"),
        ];
        (Sentence Clause, string Translation)[] copularCommands =
        [
            (Told(new Event(new Being(new Adjective("good", false)), new Pronoun(Person.Second, Number.Singular), [], []), false), "be good"),
        ];
        (Utterance Said, string Translation)[] addressed =
        [
            (new Message((Sentence)Declared(new Event(new Being(new Oblique(Role.Location, Phrase("house", Number.Singular), Configuration.In)), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false),
                new NounPhrase(new Noun("woman", Number.Singular, [new("old", false)], null, null, null, null, null), null)), "old woman, the dog is in the house"),
            (new Message((Sentence)Told(new Event(new Doing("walk", null), new Pronoun(Person.Second, Number.Plural), [new(Role.Goal, Phrase("water", Number.Singular), Configuration.At)], []), false), new NounPhrase(new Noun("person", Number.Plural, [], null, null, null, null, null), null)),
                "people, go to the water"),
        ];
        (Sentence Clause, string Translation)[] coordinated =
        [
            (Declared(new Event(new Doing("see", Phrase("bird", Number.Plural)), new Coordination(Conjunction.And, [Phrase("dog", Number.Singular), Phrase("cat", Number.Singular)]), [], []), Tense.Past, Aspect.Perfective, false),
                "the dog and the cat saw the birds"),
            (new Coordinated<Sentence>(Conjunction.But, Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Imperfective, false),
                Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), Phrase("cat", Number.Singular), [], []), Tense.Past, Aspect.Imperfective, false)), "the dog was sleeping, but the cat was eating the fish"),
            (Declared(new Event(new Being(new AdjectiveCoordination(Conjunction.And, [new("big", false), new("old", false)])), Phrase("house", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false),
                "the house is big and old"),
        ];
        (Sentence Clause, string Translation)[] subordinated =
        [
            (new Subordinated(Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), Subordinator.If,
                Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), false), "if the dog sleeps, the cat eats the fish"),
            (new Subordinated(Declared(new Event(new Doing("walk", null), Phrase("dog", Number.Singular), [new(Role.Goal, Phrase("water", Number.Singular), Configuration.At)], []), Tense.Past, Aspect.Perfective, false),
                Subordinator.Purpose, Declared(new Event(new Doing("drink", null), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), true), "the dog walked to the water to drink"),
            (new Subordinated(Declared(new Event(new Doing("sleep", null), Phrase("cat", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), Subordinator.Because,
                Declared(new Event(new Being(new Adjective("big", false)), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Imperfective, false), false), "the cat slept because the dog was big"),
        ];
        (Sentence Clause, string Translation)[] passives =
        [
            (Declared(new Event(new Done("eat", Phrase("cat", Number.Singular)), Phrase("fish", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), "the fish was eaten by the cat"),
            (Declared(new Event(new Done("see", null), Phrase("dog", Number.Plural), [], []), Tense.Past, Aspect.Perfective, false), "the dogs were seen"),
        ];
        (Sentence Clause, string Translation)[] comparatives =
        [
            (Comparing(Declared(new Event(new Being(new Adjective("big", false)), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), Phrase("cat", Number.Plural)),
                "the dog is bigger than the cats"),
            (Comparing(Declared(new Event(new Being(new Adjective("big", false)), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), null), "the cat is bigger"),
            (Declared(new Event(new Doing("sleep", null), Phrase("cat", Number.Singular), [new(Role.Similative, Phrase("dog", Number.Singular), Configuration.At)], []), Tense.Present, Aspect.Imperfective, false),
                "the cat sleeps like the dog"),
        ];
        (Sentence Clause, string Translation)[] modalized =
        [
            (WithModal(Declared(new Event(new Doing("drink", Phrase("water", Number.Singular)), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), SituationalModality.Ability, true),
                "the dog can't drink the water"),
            (WithModal(Declared(new Event(new Doing("sleep", null), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), SituationalModality.Permission, false), "the cat may sleep"),
            (WithModal(Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), Phrase("dog", Number.Plural), [], []), Tense.Present, Aspect.Imperfective, true), SituationalModality.Obligation, false),
                "the dogs must not eat the fish"),
            (Judging(Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Perfect, false), EpistemicModality.Presumptive, false),
                "the cat must have eaten the fish"),
            (Judging(Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Progressive, false), EpistemicModality.Dubitative, false), "the dog might be sleeping"),
        ];
        (Sentence Clause, string Translation)[] aspects =
        [
            (Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Perfect, false), "the cat has eaten the fish"),
            (Declared(new Event(new Doing("see", Phrase("bird", Number.Plural)), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Perfect, true), "the dog hadn't seen the birds"),
            (Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Plural), [], []), Tense.Present, Aspect.Progressive, false), "the dogs are sleeping"),
            (Asked(Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), Phrase("cat", Number.Singular), [], []), Tense.Past, Aspect.Progressive, false), null),
                "was the cat eating the fish?"),
            (Declared(new Event(new Doing("eat", new NounPhrase(new Noun("fish", Number.Singular, [], null, null, null, null, null), null)), Phrase("dog", Number.Plural), [], []), Tense.Past, Aspect.Habitual, false), "the dogs used to eat fish"),
            (Declared(new Event(new Doing("begin", new ComplementClause(ComplementKind.Event, Declared(new Event(new Doing("sleep", null), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), SharesSubject: true)), Phrase("cat", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false),
                "the cat began to sleep"),
            (Declared(new Event(new Doing("stop", new ComplementClause(ComplementKind.Event, Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), SharesSubject: true)), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false),
                "the dog stops eating the fish"),
        ];
        (Sentence Clause, string Translation)[] questions =
        [
            (Asked(Declared(new Event(new Doing("see", Phrase("cat", Number.Plural)), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), null), "did the dog see the cats?"),
            (Asked(Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), new QuestionWord(InterrogativeWord.Who), [], []), Tense.Present, Aspect.Imperfective, false), null), "who eats the fish?"),
            (Asked(Declared(new Event(new Doing("eat", new QuestionWord(InterrogativeWord.What)), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), null), "what did the dog eat?"),
            (Asked(Declared(new Event(new Being(new Oblique(Role.Location, new QuestionWord(InterrogativeWord.Where), Configuration.At)), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), null),
                "where is the cat?"),
            (Asked(Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, true), QuestionAdverb.Why), "why doesn't the dog sleep?"),
            (Asked(Declared(new Event(new Doing("walk", null), Phrase("bird", Number.Singular), [new(Role.Time, new QuestionWord(InterrogativeWord.When), Configuration.At)], []), Tense.Past, Aspect.Perfective, false), null),
                "when did the bird walk?"),
            (Asked(Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), Phrase("cat", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), QuestionAdverb.How), "how did the cat eat the fish?"),
        ];
        (Sentence Clause, string Translation)[] complements =
        [
            (Declared(new Event(new Doing("say", new ComplementClause(ComplementKind.Proposition,
                Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Imperfective, false), false)), Phrase("person", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), "the person said that the dog was sleeping"),
            (Declared(new Event(new Doing("want", new ComplementClause(ComplementKind.Event,
                Declared(new Event(new Doing("eat", Phrase("fish", Number.Singular)), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), true)), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), "the cat wants to eat the fish"),
            (Declared(new Event(new Doing("say", new Quotation(new Message((Sentence)Told(new Event(new Doing("walk", null), new Pronoun(Person.Second, Number.Singular), [new(Role.Goal, Phrase("house", Number.Singular), Configuration.At)], []), false), null))), Phrase("bird", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), "the bird said, \"go to the house\""),
        ];
        Clause Gapped(string verb, Argument subject, Argument? @object = null, ValueList<Oblique> obliques = default) =>
            Declared(new Event(new Doing(verb, @object), subject, obliques, []), Tense.Past, Aspect.Perfective, false);
        (Sentence Clause, string Translation)[] relatives =
        [
            (Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Singular) with { Relative = Gapped("see", new Gap(null), Phrase("cat", Number.Plural)) }, [], []), Tense.Present, Aspect.Imperfective, false),
                "the dog that saw the cats sleeps"),
            (Declared(new Event(new Doing("see", Phrase("fish", Number.Singular) with { Relative = Gapped("eat", Phrase("dog", Number.Singular), new Gap(null)) }), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false),
                "the cat sees the fish that the dog ate"),
            (Declared(new Event(new Doing("walk", null), Phrase("person", Number.Singular), [new(Role.Goal, Phrase("house", Number.Singular) with { Relative = Gapped("sleep", Phrase("bird", Number.Plural), obliques: [new(Role.Location, new Gap(null), Configuration.In)]) }, Configuration.At)], []), Tense.Past, Aspect.Perfective, false),
                "the person walked to the house that the birds slept in"),
        ];
        (Sentence Clause, string Translation)[] demonstrated =
        [
            (Declared(new Event(new Doing("sleep", null), new NounPhrase(new Noun("dog", Number.Singular, [], null, null, null, Distance.Proximal, null), null), [], []), Tense.Present, Aspect.Imperfective, false), "this dog sleeps"),
            (Declared(new Event(new Doing("see", new NounPhrase(new Noun("bird", Number.Plural, [], null, null, null, Distance.Distal, null), null)), Phrase("cat", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false),
                "the cat sees those birds"),
            (Declared(new Event(new Doing("eat", new DemonstrativePronoun(Distance.Distal, Number.Singular)), Phrase("dog", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), "the dog ate that"),
            (Declared(new Event(new Doing("sleep", null), Phrase("cat", Number.Plural), [new(Role.Location, new DemonstrativePlace(Distance.Proximal), Configuration.At)], []), Tense.Present, Aspect.Imperfective, false),
                "the cats sleep here"),
        ];
        (Sentence Clause, string Translation)[] quantified =
        [
            (Declared(new Event(new Doing("see", Phrase("dog", Number.Singular)), new NounPhrase(new IndefinitePronoun(Quantifier.Some, Ontology.Person), null), [], []), Tense.Present, Aspect.Imperfective, false), "someone sees the dog"),
            (Declared(new Event(new Doing("eat", new NounPhrase(new IndefinitePronoun(Quantifier.No, Ontology.Thing), null)), Phrase("cat", Number.Singular), [], []), Tense.Past, Aspect.Perfective, false), "the cat ate nothing"),
            (Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Singular), [new(Role.Time, new NounPhrase(new IndefinitePronoun(Quantifier.No, Ontology.Time), null), Configuration.At)], []), Tense.Present, Aspect.Imperfective, false),
                "the dog never sleeps"),
            (Declared(new Event(new Doing("sleep", null), new NounPhrase(new Noun("dog", Number.Plural, [], null, null, Quantifier.All, Definiteness.Definite, null), null), [], []), Tense.Present, Aspect.Imperfective, false), "all the dogs sleep"),
            (Declared(new Event(new Doing("see", Phrase("fish", Number.Singular)), new NounPhrase(new Noun("bird", Number.Plural, [], null, null, Quantifier.Few, null, null), null), [], []), Tense.Past, Aspect.Perfective, false),
                "few birds saw the fish"),
        ];
        (Sentence Clause, string Translation)[] adverbs =
        [
            (Declared(new Event(new Doing("sleep", null), Phrase("dog", Number.Singular), [], [new Adverb("now", AdverbKind.When)]), Tense.Present, Aspect.Imperfective, false), "the dog is sleeping now"),
            (Declared(new Event(new Doing("walk", null), Phrase("person", Number.Singular), [], [new Manner(new Adjective("slow", true))]), Tense.Past, Aspect.Perfective, false),
                "the person walked very slowly"),
            (Declared(new Event(new Being(new Adjective("big", true)), Phrase("dog", Number.Singular), [], []), Tense.Present, Aspect.Imperfective, false), "the dog is very big"),
            (Declared(new Event(new Doing("sleep", null), Phrase("cat", Number.Plural), [new(Role.Time, new NounPhrase(new Noun("night", Number.Singular, [], null, null, null, null, null), null), Configuration.At)], []), Tense.Present, Aspect.Imperfective, false),
                "the cats sleep at night"),
            (Declared(new Event(new Doing("sleep", null), Phrase("bird", Number.Singular), [new(Role.Duration, new NounPhrase(new Noun("year", ClauseFile.NumberFor(3), [], 3, null, null, null, null), null), Configuration.At)], []), Tense.Past, Aspect.Perfective, false), "the bird slept for three years"),
        ];
        (Utterance Said, string Translation)[] all =
            [.. Said(examples), .. Said(copular), .. Said(commands), .. Said(copularCommands), .. Said(coordinated), .. Said(subordinated), .. Said(passives), .. Said(comparatives),
                .. addressed, .. Said(modalized), .. Said(aspects), .. Said(questions), .. Said(complements), .. Said(relatives), .. Said(demonstrated), .. Said(quantified), .. Said(adverbs)];
        foreach (var (said, translation) in all)
        {
            output.WriteLine();
            output.WriteLine(Syntax.Realize(lang, said, translation).Interlinear(phonetic));
        }

        //statements said to no one
        static IEnumerable<(Utterance Said, string Translation)> Said(IEnumerable<(Sentence Clause, string Translation)> statements) =>
            statements.Select(s => ((Utterance)new Message(s.Clause, null), s.Translation));
    }

    //a clause stating an event at a time, with an aspect, negated or not, the imperfective being no operator
    private static Clause Declared(Event @event, Tense tense, Aspect aspect, bool negative) => new Declarative(new Proposition(Layered(@event, aspect, negative), tense));

    //a clause telling 'you' to bring an event about, or not to
    private static Clause Told(Event @event, bool negative) => new Directive(negative ? new Negated(@event) : @event);

    //an event with its aspect, then its negation, over it
    private static StateOfAffairs Layered(Event @event, Aspect aspect, bool negative)
    {
        StateOfAffairs layers = aspect == Aspect.Imperfective ? @event : new Aspected(aspect, @event);
        return negative ? new Negated(layers) : layers;
    }

    //a stated clause with a situational modal over what it states, and the modal's own negation over that
    private static Clause WithModal(Clause clause, SituationalModality modality, bool negative) => clause switch
    {
        Declarative stated => stated with { Propositional = stated.Propositional.Rebuilt(p => p with { StateOfAffairs = Negating(new Modalized(modality, p.StateOfAffairs), negative) }) },
        _ => throw new ArgumentException("Only a stated clause takes a modal here.", nameof(clause)),
    };

    private static StateOfAffairs Negating(StateOfAffairs layers, bool negative) => negative ? new Negated(layers) : layers;

    //a stated clause the speaker judges possible or sure, and denies that or not
    private static Clause Judging(Clause clause, EpistemicModality modality, bool denied) => clause switch
    {
        Declarative stated => stated with { Propositional = denied ? new Denied(new Judged(modality, stated.Propositional)) : new Judged(modality, stated.Propositional) },
        _ => throw new ArgumentException("Only a stated clause is judged here.", nameof(clause)),
    };

    //a stated clause asked, with 'why' or 'how' among its adverbs if it asks either
    private static Clause Asked(Clause clause, QuestionAdverb? adverb) => clause switch
    {
        Declarative stated => new Interrogative(adverb is { } why
            ? stated.Propositional.Rebuilt(p => p with { StateOfAffairs = p.StateOfAffairs.Rebuilt(e => e with { Adverbs = [.. e.Adverbs, new QuestionAdverbial(why)] }) })
            : stated.Propositional),
        _ => throw new ArgumentException("Only a stated clause is asked here.", nameof(clause)),
    };

    //a stated clause with be and an adjective, its adjective compared with a standard, or with none
    private static Clause Comparing(Clause clause, Argument? standard) => clause switch
    {
        Declarative stated => stated with
        {
            Propositional = stated.Propositional.Rebuilt(p => p with
            {
                StateOfAffairs = p.StateOfAffairs.Rebuilt(e => e.Predicate.Value is Being { Attribute.Value: Adjective adjective } being
                    ? e with { Predicate = being with { Attribute = new Compared(adjective, standard) } }
                    : throw new ArgumentException("Only an adjective said with be is compared.", nameof(clause))),
            }),
        },
        _ => throw new ArgumentException("Only a stated clause compares here.", nameof(clause)),
    };
}
