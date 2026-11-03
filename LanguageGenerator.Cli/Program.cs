using LanguageGenerator;

//pass a seed to regenerate the same language; without one, a fresh seed is picked and printed.
//"translate <file> [seed]" translates a file of clauses (see ClauseFile) instead of describing
//the language, and "gloss <english.txt>" writes such a file from English (see Glosser)
if (args is ["gloss", .. var glossArguments])
{
    return await LanguageGenerator.Cli.GlossCommand.Run(glossArguments);
}
const string Usage = "Usage: LanguageGenerator.Cli [seed], or LanguageGenerator.Cli translate <file> [seed], where the seed is an integer, or LanguageGenerator.Cli gloss <english.txt>.";
var translating = args.Length > 0 && args[0] == "translate";
if (translating && args.Length < 2)
{
    Console.Error.WriteLine(Usage);
    return 1;
}
var seedArgument = translating ? args.ElementAtOrDefault(2) : args.ElementAtOrDefault(0);
int seed;
if (seedArgument is null)
{
    seed = Random.Shared.Next();
}
else if (!int.TryParse(seedArgument, out seed))
{
    Console.Error.WriteLine($"{Usage} Got '{seedArgument}'.");
    return 1;
}

IReadOnlyList<ClauseFile.Entry> script = [];
if (translating)
{
    var path = args[1];
    try
    {
        script = ClauseFile.Parse(File.ReadAllText(path));
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException or ClauseFormatException)
    {
        Console.Error.WriteLine($"{path}: {e.Message}");
        return 1;
    }
}

Console.WriteLine($"Seed: {seed}");
var rand = new Random(seed);

var lang = Generator.Generate(rand);

if (translating)
{
    foreach (var entry in script)
    {
        Console.WriteLine();
        Console.WriteLine(Syntax.Realize(lang, entry.Statement, entry.Translation).Interlinear());
    }
    return 0;
}

var phonology = lang.Phonology;
Console.WriteLine(phonology.SyllableRule);
Console.WriteLine($"Sandhi: {(phonology.SandhiRules.Count == 0 ? "none" : string.Join(", ", phonology.SandhiRules))}");
var repair = phonology.ClusterRepair == ClusterRepair.Epenthesis ? $"epenthesis of {phonology.EpentheticVowel.IpaSymbol}" : "deletion";
Console.WriteLine($"Illegal clusters: {repair}");
var orthography = lang.Orthography;
var unusual = orthography.Spellings.Where(s => s.Value != s.Key.IpaSymbol).Select(s => $"{s.Key.IpaSymbol} = {s.Value}");
Console.WriteLine($"Spelling: {orthography.Style?.ToString() ?? "IPA"}");
Console.WriteLine($"  {string.Join(", ", unusual)}");
//citation forms, spelled and then in IPA by syllable
foreach (var lexeme in lang.Lexicon)
{
    var form = phonology.Realize(new Word([lexeme.Root]));
    var nounGender = lexeme.PartOfSpeech == PartOfSpeech.Noun && lang.Genders.Of(lexeme) is { } g ? $", {g.Gloss}" : "";
    Console.WriteLine($"{lexeme.Root.Meaning} ({lexeme.PartOfSpeech}{nounGender}): {form.Spell(orthography)} [{form.Syllabified(Orthography.Ipa)}]");
}

var order = lang.WordOrder;
Console.WriteLine($"{lang.Morphology} ({lang.MorphologicalProfile})");
Console.WriteLine(order.ConstituentWordOrder);
Console.WriteLine($"{order.HeadDirectionality}, {order.VerbPhraseDirectionality}, {order.NounPhraseDirectionality}, {order.AdjectivePhraseDirectionality}, {order.AdpositionalPhraseDirectionality}");
Console.WriteLine(lang.HasArticles ? "Has articles" : "No articles");

Console.WriteLine(lang.MorphosyntacticAlignment);
Console.WriteLine($"Cases: {string.Join(", ", lang.Cases.Select(c => c.Gloss))}");
foreach (var (use, marker) in lang.RoleMarkers)
{
    var spelled = marker.Value is Adposition a ? $" ({phonology.Realize(new Word([a.Lexeme.Root])).Spell(orthography)})" : "";
    Console.WriteLine($"{use}: {marker}{spelled}");
}
Console.WriteLine($"Obliques: {lang.WordOrder.ObliquePosition}");

var pronouns = lang.Pronouns;
var gender = pronouns.GenderedPronouns == PronounGender.None ? "" : $", gender ({string.Join("/", pronouns.Genders.Select(g => g.Gloss))}) in {pronouns.GenderedPronouns}";
Console.WriteLine($"Pronouns ({pronouns.FirstPersonPlural} 'we'{gender}{(pronouns.DropsSubjects ? ", subjects usually dropped" : "")}):");
Console.WriteLine("  " + string.Join(", ", pronouns.Forms.Values.Distinct().Select(p =>
    $"{p.Root.Meaning} {phonology.Realize(new Word([p.Root])).Spell(orthography)}")));

//spelled as they're written, not realized: an affix can be a lone consonant, which isn't a word
var negation = lang.Negation;
var markers = negation.Markers.Select(m => m.Position switch
{
    ExponentPosition.Prefix => $"{m.Marker.Spell(orthography)}-",
    ExponentPosition.Suffix => $"-{m.Marker.Spell(orthography)}",
    _ => m.Marker.Spell(orthography),
});
Console.WriteLine($"Negation: {negation} ({string.Join(" ... ", markers)})");
var predication = lang.Predication;
var copula = phonology.Realize(new Word([predication.Copula.Root])).Spell(orthography);
var genders = lang.Genders;
if (genders.Genders != NounGenders.None)
{
    var markerSpellings = genders.Markers is null ? "no markers to agree with"
        : string.Join(", ", genders.Values.Select(g => $"{g.Gloss} {(genders.Markers[g].Form.Count == 0 ? "∅" : ((Morpheme)genders.Markers[g]).Spell(orthography))}"));
    var by = genders.Assignment == GenderAssignment.Semantic ? "by meaning" : "by meaning and form";
    Console.WriteLine($"Genders: {string.Join("/", genders.Values.Select(g => g.Gloss))}, {by} ({markerSpellings}){(genders.VerbsAgree ? "; verbs agree" : "")}");
}
var adjectivePlace = lang.WordOrder.NounPhraseDirectionality == HeadDirectionality.Initial ? "after" : "before";
Console.WriteLine(lang.AdjectiveAgreement switch
{
    AdjectiveAgreement.Agreement => $"Adjectives go {adjectivePlace} the noun and agree with it",
    AdjectiveAgreement.Phrasal => $"Adjectives go {adjectivePlace} the noun, and the phrase is marked at its edges",
    _ => $"Adjectives go {adjectivePlace} the noun, uninflected",
});
Console.WriteLine($"Copula: {copula}{(predication.ZeroCopula ? ", dropped in the present" : "")}; adjectives {(predication.Adjectives == PredicativeAdjectives.Verbal ? "are verbs" : "take the copula")}");
Console.WriteLine(predication.LocationalCopula is { } locational
    ? $"Locational copula: {phonology.Realize(new Word([locational.Root])).Spell(orthography)}"
    : "Locations take the copula too");
var numerals = lang.Numerals;
string Say(int n) => string.Join(" ", numerals.Say(n).Select(w => phonology.Realize(new Word([w.Root])).Spell(orthography)));
var counting = numerals.Base == NumeralBase.Restricted
    ? $"up to {numerals.Limit}, then 'many'"
    : $"{numerals.Base.ToString().ToLowerInvariant()}, {(numerals.LargestFirst ? "largest" : "units")} first";
Console.WriteLine($"Numerals ({counting}; {(numerals.BeforeNoun ? "before" : "after")} the noun{(numerals.NounStaysSingular ? ", which stays singular" : "")}):");
Console.WriteLine("  " + string.Join(", ", ((int[])[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 20, 37, 100]).Select(n => $"{n} {Say(n)}")));
var possession = lang.Possession;
var possessedMarkers = possession.Affixes is null ? ""
    : $" ({string.Join(", ", possession.Affixes.Select(a => $"{Tagmeme.JoinGlosses(a.Value.Tagmemes)} {(possession.AffixPosition == ExponentPosition.Prefix ? $"{((Morpheme)a.Value).Spell(orthography)}-" : $"-{((Morpheme)a.Value).Spell(orthography)}")}"))})";
Console.WriteLine($"Possessors go {(possession.PossessorFirst ? "before" : "after")} the noun, " + possession.Marking switch
{
    PossessiveMarking.Dependent => $"marked {lang.RoleMarkers[CaseUse.Possessive]}",
    PossessiveMarking.Head => $"and the noun agrees with them{possessedMarkers}",
    PossessiveMarking.Double => $"marked {lang.RoleMarkers[CaseUse.Possessive]}, and the noun agrees with them{possessedMarkers}",
    _ => "unmarked",
});

//each paradigm, shown on the first lexeme that uses it, and the pronouns' on 1SG. Spelled surface
//forms come first, with the underlying form after them in IPA wherever sandhi or cluster repair
//changed something
var shown = lang.Paradigms.Values.Select(p => (Paradigm: p, Lexeme: lang.Lexicon.First(l => l.PartOfSpeech == p.PartOfSpeech)))
    .Append((pronouns.Paradigm, pronouns.For(new(Person.First, Number.Singular))));
foreach (var (paradigm, lexeme) in shown)
{
    Console.WriteLine();
    var slots = paradigm.Slots.Count == 0 ? "no inflection" : string.Join(", ", paradigm.Slots);
    Console.WriteLine($"{paradigm.PartOfSpeech} paradigm: {slots}");
    foreach (var cell in paradigm.Cells)
    {
        var forms = paradigm.Inflect(lexeme, cell).Select(phonology.Realize).ToList();
        var surface = string.Join(" ", forms.Select(f => f.Segmented(orthography)));
        var changed = forms.Any(f => !f.Phonemes.SequenceEqual(f.Word.Morphemes.SelectMany(m => m.Form)));
        var underlying = string.Join(" ", forms.Select(f => f.Word.Segmented(Orthography.Ipa)));
        var gloss = string.Join(" ", forms.Select(f => f.Gloss));
        Console.WriteLine($"  {surface,-24} {gloss,-20}{(changed ? $" /{underlying}/" : "")}");
    }
}

//a few sentences, glossed
NounPhrase Phrase(string noun, Number number) => new(noun, number);
(Clause Clause, string Translation)[] examples =
[
    (new("see", Tense.Past, Aspect.Perfective, Phrase("dog", Number.Singular), Phrase("cat", Number.Singular)), "the dog saw the cat"),
    (new("sleep", Tense.Present, Aspect.Imperfective, Phrase("bird", Number.Plural)), "the birds are sleeping"),
    (new("see", Tense.Present, Aspect.Imperfective, Phrase("person", Number.Singular), Phrase("dog", Number.Plural)), "the person sees the dogs"),
    (new("see", Tense.Present, Aspect.Imperfective, new Pronoun(Person.First, Number.Singular), new Pronoun(Person.Second, Number.Singular)), "I see you"),
    (new("sleep", Tense.Past, Aspect.Imperfective, new Pronoun(Person.First, Number.Plural)), "we were sleeping"),
    (new("see", Tense.Past, Aspect.Perfective, Phrase("dog", Number.Singular), Phrase("cat", Number.Singular), Negative: true), "the dog didn't see the cat"),
    (new("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular, [new("big")]), new NounPhrase("cat", Number.Plural, [new("small")])), "the big dog saw the small cats"),
    (new("see", Tense.Past, Aspect.Perfective, new NounPhrase("woman", Number.Singular, [new("old")]), new NounPhrase("sun", Number.Singular, [new("big")])), "the old woman saw the big sun"),
    (new("sleep", Tense.Past, Aspect.Imperfective, Phrase("dog", Number.Singular), Obliques: [new(CaseUse.Location, new NounPhrase("house", Number.Singular, [new("big")]))]), "the dog was sleeping in the big house"),
    (new("give", Tense.Past, Aspect.Perfective, Phrase("person", Number.Singular), Phrase("fish", Number.Singular), Obliques: [new(CaseUse.IndirectObject, Phrase("cat", Number.Plural))]), "the person gave the fish to the cats"),
    (new("walk", Tense.Past, Aspect.Perfective, new Pronoun(Person.First, Number.Singular), Obliques: [new(CaseUse.MovementFrom, Phrase("tree", Number.Singular)), new(CaseUse.MovementTo, Phrase("water", Number.Singular)), new(CaseUse.Accompaniment, new Pronoun(Person.Second, Number.Singular))]), "I walked from the tree to the water with you"),
    (new("see", Tense.Past, Aspect.Perfective, Phrase("person", Number.Singular), new NounPhrase("dog", ClauseFile.NumberFor(3), [new("big")], Count: 3)), "the person saw three big dogs"),
    (new("see", Tense.Past, Aspect.Perfective, new NounPhrase("dog", Number.Singular, Possessor: Phrase("person", Number.Singular)), new NounPhrase("cat", Number.Plural, Possessor: new Pronoun(Person.First, Number.Singular))), "the person's dog saw my cats"),
];
(CopularClause Clause, string Translation)[] copular =
[
    (new(new Adjective("big"), Tense.Present, Aspect.Imperfective, Phrase("dog", Number.Singular)), "the dog is big"),
    (new(Phrase("bird", Number.Singular), Tense.Present, Aspect.Imperfective, new Pronoun(Person.Second, Number.Singular)), "you are a bird"),
    (new(new Oblique(CaseUse.Location, Phrase("house", Number.Singular)), Tense.Present, Aspect.Imperfective, Phrase("dog", Number.Singular)), "the dog is in the house"),
];
foreach (var (clause, translation) in examples.Select(e => ((Statement)e.Clause, e.Translation)).Concat(copular.Select(e => ((Statement)e.Clause, e.Translation))))
{
    Console.WriteLine();
    Console.WriteLine(Syntax.Realize(lang, clause, translation).Interlinear());
}

return 0;
