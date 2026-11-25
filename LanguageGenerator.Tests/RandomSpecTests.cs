namespace LanguageGenerator.Tests;

//random specs, each refused or met, in a class of their own since it's the longest test there is,
//and xUnit runs a class's tests one after another
public class RandomSpecTests
{
    /*
      any spec is either refused, or met: a spec that's neither is missing a rule, or a value is
      written in wrongly. One that isn't met is reported as JSON with its seed, to reproduce with
      the command line
     */
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
            var seed = rand.Next();
            try
            {
                var language = Generator.Generate(seed, spec);
                SpecTests.AssertMeets(language, spec);
                RelatedStageTests.AssertKeepsTheRules(language);
            }
            catch (Exception e)
            {
                Assert.Fail($"""
                    Random spec {i} wasn't met by the language from seed {seed}. To reproduce it, save the spec below as a file and run
                      dotnet run --project LanguageGenerator.Cli -- {seed} --spec <file>

                    {SpecFile.Format(spec)}

                    {e.Message}
                    """);
            }
            met++;
        }

        //sound changes fixed beside a random inventory too small for them are refused often
        Assert.InRange(refused, 1, 130);
        Assert.InRange(met, 170, 299);
    }

    //a random spec written as JSON reads back as the same spec, so a failing one can be reproduced
    //from what the test above reports
    [Fact]
    public void Format_RandomSpecs_ParseBackToTheSameSpec()
    {
        var rand = new Random(2026);
        Assert.All(Enumerable.Range(0, 300).Select(_ => RandomSpec(rand)), spec =>
        {
            var json = SpecFile.Format(spec);
            Assert.Equal(json, SpecFile.Format(SpecFile.Parse(json)));
        });
    }

    //a few settings, each fixed to one value or two, an inventory to a random handful of its sounds in
    //random order, and the dropoffs to random numbers
    private static LanguageSpec RandomSpec(Random rand)
    {
        var spec = LanguageSpec.Free;
        //an inventory is one setting here, given half the time exactly and half the time as a few
        //sounds it requires or forbids, so its sounds don't swamp the rest
        foreach (var setting in Settings.All.Where(s => Settings.InventoryOf(s) is null && rand.NextDouble() < 0.2))
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
