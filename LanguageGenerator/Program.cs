using LanguageGenerator;

//pass a seed to regenerate the same language; without one, a fresh seed is picked and printed
int seed;
if (args.Length == 0)
{
    seed = Random.Shared.Next();
}
else if (!int.TryParse(args[0], out seed))
{
    Console.Error.WriteLine($"Usage: LanguageGenerator [seed], where the seed is an integer. Got '{args[0]}'.");
    return 1;
}
Console.WriteLine($"Seed: {seed}");
var rand = new Random(seed);

var lang = Generator.Generate(rand);

var phonology = lang.Phonology;
Console.WriteLine(phonology.SyllableRule);
Console.WriteLine($"Sandhi: {(phonology.SandhiRules.Count == 0 ? "none" : string.Join(", ", phonology.SandhiRules))}");
var repair = phonology.ClusterRepair == ClusterRepair.Epenthesis ? $"epenthesis of {phonology.EpentheticVowel.IpaSymbol}" : "deletion";
Console.WriteLine($"Illegal clusters: {repair}");
var orthography = lang.Orthography;
var unusual = orthography.Spellings.Where(s => s.Value != s.Key.IpaSymbol).Select(s => $"{s.Key.IpaSymbol} = {s.Value}");
Console.WriteLine($"Spelling: {string.Join(", ", unusual)}");
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

//each paradigm, shown on the first lexeme that uses it. Spelled surface forms come first, with the
//underlying form after them in IPA wherever sandhi or cluster repair changed something
foreach (var paradigm in lang.Paradigms.Values)
{
    Console.WriteLine();
    var slots = paradigm.Slots.Count == 0 ? "no inflection" : string.Join(", ", paradigm.Slots);
    Console.WriteLine($"{paradigm.PartOfSpeech} paradigm: {slots}");
    var lexeme = lang.Lexicon.First(l => l.PartOfSpeech == paradigm.PartOfSpeech);
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
NounPhrase Phrase(string noun, Number number) => new(lang.FindLexeme(noun), number);
(Clause Clause, string Translation)[] examples =
[
    (new(lang.FindLexeme("see"), Tense.Past, Aspect.Perfective, Phrase("dog", Number.Singular), Phrase("cat", Number.Singular)), "the dog saw the cat"),
    (new(lang.FindLexeme("sleep"), Tense.Present, Aspect.Imperfective, Phrase("bird", Number.Plural)), "the birds are sleeping"),
    (new(lang.FindLexeme("see"), Tense.Present, Aspect.Imperfective, Phrase("person", Number.Singular), Phrase("dog", Number.Plural)), "the person sees the dogs"),
];
foreach (var (clause, translation) in examples)
{
    Console.WriteLine();
    Console.WriteLine(Syntax.Realize(lang, clause, translation).Interlinear());
}

return 0;
