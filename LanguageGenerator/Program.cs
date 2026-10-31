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
foreach (var word in lang.Words)
{
    Console.WriteLine(word.Transcription);
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

return 0;
