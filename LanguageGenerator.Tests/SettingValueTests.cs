namespace LanguageGenerator.Tests;

//every value of every choice setting, fixed alone, in a class of its own since it grows with every
//setting, and xUnit runs a class's tests one after another
public class SettingValueTests
{
    [Fact]
    public void Generate_AnyChoiceSettingFixed_HasThatValue()
    {
        foreach (var setting in Settings.All.Where(s => s.AllValues is not null))
        {
            foreach (var value in setting.AllValues!)
            {
                var spec = LanguageSpec.Free.With(setting, [value]);
                //an inventory's sounds and counts are so many that one seed each is enough
                foreach (var seed in setting is ISetting<IReadOnlyList<Vowel>> or ISetting<IReadOnlyList<Consonant>> ? [1] : new[] { 1, 2 })
                {
                    Assert.Equal(value, setting.Read(Generator.Generate(seed, spec)));
                }
            }
        }
    }
}
