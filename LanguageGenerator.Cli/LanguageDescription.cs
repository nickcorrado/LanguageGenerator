using LanguageGenerator;

namespace LanguageGenerator.Cli;

/// <summary>
/// What the program prints about a language: its features, its paradigms, and a few sentences in
/// it.
/// </summary>
public static class LanguageDescription
{
    public static void Write(Language lang, TextWriter output)
    {
        lang.Lexicon.Classify(ExampleNouns);
        WriteFeatures(lang, output);
        WriteParadigms(lang, output);
        WriteExamples(lang, output);
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
        var repair = phonology.ClusterRepair == ClusterRepair.Epenthesis ? $"epenthesis of {phonology.EpentheticVowel.IpaSymbol}" : "deletion";
        output.WriteLine($"Illegal clusters: {repair}");
        output.WriteLine($"Stress: {phonology.Stress.Description}");
        if (phonology.SyllableContact != SyllableContact.Any)
        {
            output.WriteLine($"Syllable contact: {phonology.SyllableContact.Description}");
        }
        var orthography = lang.Orthography;
        var unusual = orthography.Spellings.Where(s => s.Value != s.Key.IpaSymbol).Select(s => $"{s.Key.IpaSymbol} = {s.Value}");
        output.WriteLine($"Spelling: {orthography.Style?.ToString() ?? "IPA"}");
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
        output.WriteLine($"Articles: {DescribeArticle(lang.Articles.Definite, "definite")}; {DescribeArticle(lang.Articles.Indefinite, "indefinite")}");

        output.WriteLine(lang.MorphosyntacticAlignment);
        output.WriteLine($"Cases: {string.Join(", ", lang.Cases.Select(c => c.Gloss))}");
        foreach (var (role, marker) in lang.RoleMarkers)
        {
            var spelled = marker.Value is Adposition a ? $" ({phonology.Realize(new Word([a.Lexeme.Root])).Spell(orthography)})" : "";
            output.WriteLine($"{role}: {marker}{spelled}");
        }
        output.WriteLine($"Obliques: {lang.WordOrder.ObliquePosition}");

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
                : string.Join(", ", genders.Values.Select(g => $"{g.Gloss} {(genders.Markers[g].Form.Count == 0 ? "∅" : ((Morpheme)genders.Markers[g]).Spell(orthography))}"));
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
            { Position: ExponentPosition.Prefix } p => $"Passive: prefix {p.Marker.Spell(orthography)}-",
            { Position: ExponentPosition.Suffix } p => $"Passive: suffix -{p.Marker.Spell(orthography)}",
            { Position: ExponentPosition.PrecedingParticle } p => $"Passive: {p.Marker.Spell(orthography)} before the verb",
            var p => $"Passive: {p.Marker.Spell(orthography)} after the verb",
        });
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
            return $"{m.Key.ToString().ToLowerInvariant()} {form} ({(m.Value.BeforeMain ? "first" : "after")}{balance})";
        }));
        output.WriteLine($"Subordinate clauses: {subordinators}");
        output.WriteLine("Comparison: " + lang.Comparison switch
        {
            LocationalComparison l => $"the standard is marked like a place, as the {l.Role.ToString().ToLowerInvariant()} is",
            ParticleComparison p => $"the particle {phonology.Realize(new Word([p.Than.Root])).Spell(orthography)} {(p.BeforeStandard ? "before" : "after")} the standard",
            ConjoinedComparison => "two clauses, 'X is big, Y is not big'",
        });
        var numerals = lang.Numerals;
        string Say(int n) => string.Join(" ", numerals.Say(n).Select(w => phonology.Realize(new Word([w.Root])).Spell(orthography)));
        var counting = numerals.Base == NumeralBase.Restricted
            ? $"up to {numerals.Limit}, then 'many'"
            : $"{numerals.Base.ToString().ToLowerInvariant()}, {(numerals.LargestFirst ? "largest" : "units")} first";
        output.WriteLine($"Numerals ({counting}; {(numerals.BeforeNoun ? "before" : "after")} the noun{(numerals.NounStaysSingular ? ", which stays singular" : "")}):");
        output.WriteLine("  " + string.Join(", ", ((int[])[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 20, 37, 100]).Select(n => $"{n} {Say(n)}")));
        var ordinals = numerals.Ordinals;
        var marking = ordinals.Marking switch
        {
            (ExponentPosition.Prefix, var marker) => $"{marker.Spell(orthography)}- before the numeral",
            (ExponentPosition.Suffix, var marker) => $"-{marker.Spell(orthography)} after the numeral",
            (ExponentPosition.PrecedingParticle, var marker) => $"{marker.Spell(orthography)} before the numeral",
            (ExponentPosition.FollowingParticle, var marker) => $"{marker.Spell(orthography)} after the numeral",
            _ => "the cardinal",
        };
        string SayOrdinal(int n) => string.Join(" ", numerals.SayOrdinal(n).Select(w => phonology.Realize(w).Spell(orthography)));
        var suppletive = ordinals.Type switch
        {
            OrdinalType.FirstThenCardinal or OrdinalType.FirstThenDerived => "a word for 'first', then ",
            OrdinalType.FirstSecondThenDerived => "words for 'first' and 'second', then ",
            _ => "",
        };
        output.WriteLine($"Ordinals ({suppletive}{marking}):");
        output.WriteLine("  " + string.Join(", ", ((int[])[1, 2, 3, 4, 10, 37]).Select(n => $"{n}{OrdinalEnding(n)} {SayOrdinal(n)}")));
        var possession = lang.Possession;
        var possessedMarkers = possession.Affixes is null ? ""
            : $" ({string.Join(", ", possession.Affixes.Select(a => $"{Grammeme.JoinGlosses(a.Value.Grammemes)} {(possession.AffixPosition == ExponentPosition.Prefix ? $"{((Morpheme)a.Value).Spell(orthography)}-" : $"-{((Morpheme)a.Value).Spell(orthography)}")}"))})";
        output.WriteLine($"Possessors go {(possession.PossessorFirst ? "before" : "after")} the noun, " + possession.Marking switch
        {
            PossessiveMarking.Dependent => $"marked {lang.RoleMarkers[Role.Possessor]}",
            PossessiveMarking.Head => $"and the noun agrees with them{possessedMarkers}",
            PossessiveMarking.Double => $"marked {lang.RoleMarkers[Role.Possessor]}, and the noun agrees with them{possessedMarkers}",
            _ => "unmarked",
        });
    }

    private static void WriteParadigms(Language lang, TextWriter output)
    {
        var phonology = lang.Phonology;
        var orthography = lang.Orthography;
        var pronouns = lang.Pronouns;
        //each paradigm, shown on the first lexeme that uses it, and the pronouns' on 1SG. Spelled surface
        //forms come first, with the underlying form after them in IPA wherever sandhi or cluster repair
        //changed something
        var shown = lang.Paradigms.All.Select(p => (Paradigm: p, Lexeme: lang.Lexicon.First(l => l.PartOfSpeech == p.PartOfSpeech)))
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
                output.WriteLine($"  {surface,-24} {gloss,-20}{(changed ? $" /{underlying}/" : "")}");
            }
        }
    }

    private static readonly IReadOnlyDictionary<string, SemanticClass> ExampleNouns =
        NounFile.Parse("woman F\nperson HUM\ndog ANIM\ncat ANIM\nbird ANIM\nfish ANIM");

    private static void WriteExamples(Language lang, TextWriter output)
    {
        //a few sentences, glossed
        NounPhrase Phrase(string noun, Number number) => new(noun, number, Definiteness: Definiteness.Definite);
        (Clause Clause, string Translation)[] examples =
        [
            (new("see", Tense.Past, Aspect.Perfective, Phrase("dog", Number.Singular), Phrase("cat", Number.Singular)), "the dog saw the cat"),
            (new("sleep", Tense.Present, Aspect.Imperfective, Phrase("bird", Number.Plural)), "the birds are sleeping"),
            (new("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular, Definiteness: Definiteness.Indefinite), new NounPhrase("cat", Number.Plural)), "a dog saw cats"),
            (new("see", Tense.Present, Aspect.Imperfective, Phrase("person", Number.Singular), Phrase("dog", Number.Plural)), "the person sees the dogs"),
            (new("see", Tense.Present, Aspect.Imperfective, new Pronoun(Person.First, Number.Singular), new Pronoun(Person.Second, Number.Singular)), "I see you"),
            (new("sleep", Tense.Past, Aspect.Imperfective, new Pronoun(Person.First, Number.Plural)), "we were sleeping"),
            (new("see", Tense.Past, Aspect.Perfective, Phrase("dog", Number.Singular), Phrase("cat", Number.Singular), Negative: true), "the dog didn't see the cat"),
            (new("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular, [new("big")], Definiteness: Definiteness.Definite), new NounPhrase("cat", Number.Plural, [new("small")], Definiteness: Definiteness.Definite)), "the big dog saw the small cats"),
            (new("see", Tense.Past, Aspect.Perfective, new NounPhrase("woman", Number.Singular, [new("old")], Definiteness: Definiteness.Definite), new NounPhrase("sun", Number.Singular, [new("big")], Definiteness: Definiteness.Definite)), "the old woman saw the big sun"),
            (new("sleep", Tense.Past, Aspect.Imperfective, Phrase("dog", Number.Singular), Obliques: [new(Role.Location, new NounPhrase("house", Number.Singular, [new("big")], Definiteness: Definiteness.Definite))]), "the dog was sleeping in the big house"),
            (new("give", Tense.Past, Aspect.Perfective, Phrase("person", Number.Singular), Phrase("fish", Number.Singular), Obliques: [new(Role.Recipient, Phrase("cat", Number.Plural))]), "the person gave the fish to the cats"),
            (new("walk", Tense.Past, Aspect.Perfective, new Pronoun(Person.First, Number.Singular), Obliques: [new(Role.Source, Phrase("tree", Number.Singular)), new(Role.Goal, Phrase("water", Number.Singular)), new(Role.Companion, new Pronoun(Person.Second, Number.Singular))]), "I walked from the tree to the water with you"),
            (new("see", Tense.Past, Aspect.Perfective, Phrase("person", Number.Singular), new NounPhrase("dog", ClauseFile.NumberFor(3), [new("big")], Count: 3)), "the person saw three big dogs"),
            (new("sleep", Tense.Past, Aspect.Imperfective, new NounPhrase("dog", Number.Singular, Ordinal: 3, Definiteness: Definiteness.Definite), Obliques: [new(Role.Location, new NounPhrase("house", Number.Singular, Ordinal: 1, Definiteness: Definiteness.Definite))]), "the third dog was sleeping in the first house"),
            (new("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular, Possessor: Phrase("person", Number.Singular)), new NounPhrase("cat", Number.Plural, Possessor: new Pronoun(Person.First, Number.Singular))), "the person's dog saw my cats"),
        ];
        (CopularClause Clause, string Translation)[] copular =
        [
            (new(new Adjective("big"), Tense.Present, Aspect.Imperfective, Phrase("dog", Number.Singular)), "the dog is big"),
            (new(new NounPhrase("bird", Number.Singular, Definiteness: Definiteness.Indefinite), Tense.Present, Aspect.Imperfective, new Pronoun(Person.Second, Number.Singular)), "you are a bird"),
            (new(new Oblique(Role.Location, Phrase("house", Number.Singular)), Tense.Present, Aspect.Imperfective, Phrase("dog", Number.Singular)), "the dog is in the house"),
        ];
        (Command Clause, string Translation)[] commands =
        [
            (new("eat", Number.Singular, Phrase("fish", Number.Singular)), "eat the fish"),
            (new("walk", Number.Plural, Obliques: [new(Role.Goal, Phrase("house", Number.Singular))]), "go to the house, all of you"),
            (new("eat", Number.Singular, Phrase("fish", Number.Singular), Negative: true), "don't eat the fish"),
        ];
        (CopularCommand Clause, string Translation)[] copularCommands =
        [
            (new(new Adjective("good"), Number.Singular, false), "be good"),
        ];
        (Addressed Clause, string Translation)[] addressed =
        [
            (new(new CopularClause(new Oblique(Role.Location, Phrase("house", Number.Singular)), Tense.Present, Aspect.Imperfective, Phrase("dog", Number.Singular)),
                new NounPhrase("woman", Number.Singular, [new("old")])), "old woman, the dog is in the house"),
            (new(new Command("walk", Number.Plural, Obliques: [new(Role.Goal, Phrase("water", Number.Singular))]), new NounPhrase("person", Number.Plural)),
                "people, go to the water"),
        ];
        (Statement Clause, string Translation)[] coordinated =
        [
            (new Clause("see", Tense.Past, Aspect.Perfective, new Coordination(Conjunction.And, [Phrase("dog", Number.Singular), Phrase("cat", Number.Singular)]), Phrase("bird", Number.Plural)),
                "the dog and the cat saw the birds"),
            (new Coordinated(Conjunction.But, new Clause("sleep", Tense.Past, Aspect.Imperfective, Phrase("dog", Number.Singular)),
                new Clause("eat", Tense.Past, Aspect.Imperfective, Phrase("cat", Number.Singular), Phrase("fish", Number.Singular))), "the dog was sleeping, but the cat was eating the fish"),
            (new CopularClause(new AdjectiveCoordination(Conjunction.And, [new("big"), new("old")]), Tense.Present, Aspect.Imperfective, Phrase("house", Number.Singular)),
                "the house is big and old"),
        ];
        (Statement Clause, string Translation)[] subordinated =
        [
            (new Subordinated(new Clause("eat", Tense.Present, Aspect.Imperfective, Phrase("cat", Number.Singular), Phrase("fish", Number.Singular)), Subordinator.If,
                new Clause("sleep", Tense.Present, Aspect.Imperfective, Phrase("dog", Number.Singular)), false), "if the dog sleeps, the cat eats the fish"),
            (new Subordinated(new Clause("walk", Tense.Past, Aspect.Perfective, Phrase("dog", Number.Singular), Obliques: [new(Role.Goal, Phrase("water", Number.Singular))]),
                Subordinator.Purpose, new Clause("drink", Tense.Past, Aspect.Perfective, Phrase("dog", Number.Singular)), true), "the dog walked to the water to drink"),
            (new Subordinated(new Clause("sleep", Tense.Past, Aspect.Perfective, Phrase("cat", Number.Singular)), Subordinator.Because,
                new CopularClause(new Adjective("big"), Tense.Past, Aspect.Imperfective, Phrase("dog", Number.Singular)), false), "the cat slept because the dog was big"),
        ];
        (PassiveClause Clause, string Translation)[] passives =
        [
            (new("eat", Tense.Past, Aspect.Perfective, Phrase("fish", Number.Singular), Phrase("cat", Number.Singular), false, []), "the fish was eaten by the cat"),
            (new("see", Tense.Past, Aspect.Perfective, Phrase("dog", Number.Plural), null, false, []), "the dogs were seen"),
        ];
        (Comparative Clause, string Translation)[] comparatives =
        [
            (new(new CopularClause(new Adjective("big"), Tense.Present, Aspect.Imperfective, Phrase("dog", Number.Singular)), Phrase("cat", Number.Plural)),
                "the dog is bigger than the cats"),
        ];
        foreach (var (clause, translation) in examples.Select(e => ((Statement)e.Clause, e.Translation)).Concat(copular.Select(e => ((Statement)e.Clause, e.Translation)))
            .Concat(commands.Select(e => ((Statement)e.Clause, e.Translation))).Concat(copularCommands.Select(e => ((Statement)e.Clause, e.Translation))).Concat(coordinated).Concat(subordinated).Concat(passives.Select(e => ((Statement)e.Clause, e.Translation))).Concat(comparatives.Select(e => ((Statement)e.Clause, e.Translation))).Concat(addressed.Select(e => ((Statement)e.Clause, e.Translation))))
        {
            output.WriteLine();
            output.WriteLine(Syntax.Realize(lang, clause, translation).Interlinear());
        }
    }
}
