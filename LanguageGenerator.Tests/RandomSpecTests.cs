namespace LanguageGenerator.Tests;

//random specs, each refused or met, in a class of their own since it's the longest test there is,
//and xUnit runs a class's tests one after another
public class RandomSpecTests
{
    //any spec is either refused, or met: a spec that's neither is missing a rule
    [Fact]
    public void Generate_RandomSpecs_AreEachRefusedOrMet()
    {
        var rand = new Random(2026);
        var (refused, met) = (0, 0);
        for (int i = 0; i < 300; i++)
        {
            var spec = RandomSpec(rand);
            if (spec.Problems().Count > 0)
            {
                refused++;
                continue;
            }
            var language = Generator.Generate(rand.Next(), spec);
            SpecTests.AssertMeets(language, spec);
            RelatedStageTests.AssertKeepsTheRules(language);
            met++;
        }

        //sound changes fixed beside a random inventory too small for them are refused often
        Assert.InRange(refused, 1, 130);
        Assert.InRange(met, 170, 299);
    }

    //a few settings, each fixed to one value or two, an inventory to a random handful of its sounds in
    //random order, and the dropoff to a random number
    private static LanguageSpec RandomSpec(Random rand)
    {
        var spec = LanguageSpec.Free;
        //an inventory is one setting here, given half the time exactly and half the time as a few
        //sounds it requires or forbids, so its sounds don't swamp the rest
        foreach (var setting in Settings.All.Where(s => Settings.InventoryOf(s) is null && rand.NextDouble() < 0.25))
        {
            if (Settings.SoundsOf(setting) is [_, ..] sounds && rand.NextDouble() < 0.5)
            {
                spec = sounds.OrderBy(_ => rand.Next()).Take(rand.Next(1, 5)).Aggregate(spec, (s, sound) => s.With(sound.Has, [rand.NextDouble() < 0.5]));
                continue;
            }
            IReadOnlyList<object> values = setting switch
            {
                { AllValues: { } all } => [.. all.OrderBy(_ => rand.Next()).Take(rand.NextDouble() < 0.7 ? 1 : 2)],
                _ when setting == Settings.Consonants => [(IReadOnlyList<Consonant>)[.. Consonant.All.OrderBy(_ => rand.Next()).Take(rand.Next(1, 15))]],
                _ when setting == Settings.Vowels => [(IReadOnlyList<Vowel>)[.. Vowel.All.OrderBy(_ => rand.Next()).Take(rand.Next(1, 8))]],
                _ => [0.05 + rand.NextDouble() * 0.6],
            };
            spec = spec.With(setting, values);
        }
        return spec;
    }
}
