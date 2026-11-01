using LanguageGenerator;

//pass a seed to regenerate the same language; without one, a fresh seed is picked and printed.
//"translate <file> [seed]" translates a file of clauses (see ClauseFile) instead of describing
//the language
const string Usage = "Usage: LanguageGenerator [seed], or LanguageGenerator translate <file> [seed], where the seed is an integer.";
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
    Console.WriteLine($"{lexeme.Root.Meaning} ({lexeme.PartOfSpeech}): {form.Spell(orthography)} [{form.Syllabified(Orthography.Ipa)}]");
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
    Console.WriteLine($"{use}: {marker}");
}

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
var adjectivePlace = lang.WordOrder.NounPhraseDirectionality == HeadDirectionality.Initial ? "after" : "before";
Console.WriteLine(lang.AdjectiveAgreement switch
{
    AdjectiveAgreement.Agreement => $"Adjectives go {adjectivePlace} the noun and agree with it",
    AdjectiveAgreement.Phrasal => $"Adjectives go {adjectivePlace} the noun, and the phrase is marked at its edges",
    _ => $"Adjectives go {adjectivePlace} the noun, uninflected",
});
Console.WriteLine($"Copula: {copula}{(predication.ZeroCopula ? ", dropped in the present" : "")}; adjectives {(predication.Adjectives == PredicativeAdjectives.Verbal ? "are verbs" : "take the copula")}");

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
];
(CopularClause Clause, string Translation)[] copular =
[
    (new(new Adjective("big"), Tense.Present, Aspect.Imperfective, Phrase("dog", Number.Singular)), "the dog is big"),
    (new(Phrase("bird", Number.Singular), Tense.Present, Aspect.Imperfective, new Pronoun(Person.Second, Number.Singular)), "you are a bird"),
];
foreach (var (clause, translation) in examples.Select(e => ((Statement)e.Clause, e.Translation)).Concat(copular.Select(e => ((Statement)e.Clause, e.Translation))))
{
    Console.WriteLine();
    Console.WriteLine(Syntax.Realize(lang, clause, translation).Interlinear());
}

return 0;
