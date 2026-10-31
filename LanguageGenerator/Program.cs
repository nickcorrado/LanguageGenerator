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

var lang = new Language(rand);

if (rand.NextDouble() < 0.7)
{
    lang.HasArticles = true;
}

return 0;
