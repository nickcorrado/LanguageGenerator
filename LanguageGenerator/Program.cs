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

Console.WriteLine(lang.SyllableRule);
foreach (var lexeme in lang.Lexicon)
{
    Console.WriteLine($"{lexeme.Root.Meaning} ({lexeme.PartOfSpeech}): {new Word([lexeme.Root]).Transcription}");
}

var order = lang.WordOrder;
Console.WriteLine(lang.Morphology);
Console.WriteLine(order.ConstituentWordOrder);
Console.WriteLine($"{order.HeadDirectionality}, {order.VerbPhraseDirectionality}, {order.NounPhraseDirectionality}, {order.AdjectivePhraseDirectionality}, {order.AdpositionalPhraseDirectionality}");
Console.WriteLine(lang.HasArticles ? "Has articles" : "No articles");

Console.WriteLine(lang.MorphosyntacticAlignment);
Console.WriteLine($"Cases: {string.Join(", ", lang.Cases.Select(c => c.Gloss))}");
foreach (var (use, marker) in lang.RoleMarkers)
{
    Console.WriteLine($"{use}: {marker}");
}

//each paradigm, shown on the first lexeme that uses it
foreach (var paradigm in lang.Paradigms.Values)
{
    Console.WriteLine();
    var slots = paradigm.Slots.Count == 0 ? "no inflection" : string.Join(", ", paradigm.Slots);
    Console.WriteLine($"{paradigm.PartOfSpeech} paradigm: {slots}");
    var lexeme = lang.Lexicon.First(l => l.PartOfSpeech == paradigm.PartOfSpeech);
    foreach (var cell in paradigm.Cells)
    {
        var words = paradigm.Inflect(lexeme, cell);
        Console.WriteLine($"  {string.Join(" ", words.Select(w => w.Segmented)),-24} {string.Join(" ", words.Select(w => w.Gloss))}");
    }
}

return 0;
