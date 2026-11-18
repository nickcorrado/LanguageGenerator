namespace LanguageGenerator;

/// <summary>
/// What a language must be, as far as anyone has said: the values each fixed setting allows. A
/// setting the spec doesn't mention is free, and drawn as usual.
/// </summary>
//a spec is about one version of the generator: what a seed and a spec give can change as the
//generator does, though a spec keeps fitting a newer generator, whose new settings are just free
public sealed class LanguageSpec
{
    private readonly IReadOnlyDictionary<ISetting, IReadOnlyList<object>> allowed;

    private LanguageSpec(IReadOnlyDictionary<ISetting, IReadOnlyList<object>> allowed) => this.allowed = allowed;

    /// <summary>A spec that fixes nothing, so every setting is drawn.</summary>
    public static LanguageSpec Free { get; } = new(new Dictionary<ISetting, IReadOnlyList<object>>());

    /// <summary>The settings this spec fixes, each with the values it allows.</summary>
    public IReadOnlyDictionary<ISetting, IReadOnlyList<object>> Fixed => allowed;

    /// <summary>The values a setting may take, or null if it's free.</summary>
    public IReadOnlyList<object>? Allowed(ISetting setting) => allowed.GetValueOrDefault(setting);

    /// <summary>This spec with a setting fixed to any of these values, in place of what it allowed before.</summary>
    public LanguageSpec Fix<TStage, TValue>(Setting<TStage, TValue> setting, params TValue[] values) where TValue : notnull =>
        With(setting, [.. values.Select(v => (object)v)]);

    /// <summary>This spec with a setting free again.</summary>
    public LanguageSpec Unfix(ISetting setting) => new(allowed.Where(a => a.Key != setting).ToDictionary());

    /// <summary>
    /// This spec with a setting fixed to any of these values, given without their type, as a form
    /// built from the settings would give them.
    /// </summary>
    public LanguageSpec With(ISetting setting, IReadOnlyList<object> values)
    {
        if (values.Count == 0)
        {
            throw new SpecException($"{setting.Name} allows nothing: leave it out to leave it free.");
        }
        return new(new Dictionary<ISetting, IReadOnlyList<object>>(allowed) { [setting] = values });
    }

    /// <summary>
    /// A spec fixing these settings to what this language has, so that a language drawn with it
    /// shares them, and the same seed gives this language back.
    /// </summary>
    public static LanguageSpec Lock(Language language, IEnumerable<ISetting> settings) =>
        settings.Aggregate(Free, (spec, setting) => spec.With(setting, [setting.Read(language)]));

    /// <summary>
    /// What makes this spec impossible to meet: a value a setting can't have, or settings that
    /// contradict each other. Empty if it can be met.
    /// </summary>
    public IReadOnlyList<string> Problems()
    {
        List<string> values = [.. allowed.SelectMany(a => a.Value.Select(a.Key.Problem).OfType<string>().Select(problem => $"{a.Key.Name}: {problem}"))];
        return values.Count > 0 ? values : SpecRules.Close(this).Problems;
    }

    /// <summary>What a spec can meet but probably shouldn't, as an inventory too small to keep words apart.</summary>
    public IReadOnlyList<string> Warnings() =>
    [
        .. Allowed(Settings.Consonants) is [IReadOnlyList<Consonant> { Count: <= 6 } consonants]
            ? [$"consonants: with only {consonants.Count}, expect many more words that sound alike than usual."] : Array.Empty<string>(),
        .. Allowed(Settings.Vowels) is [IReadOnlyList<Vowel> { Count: <= 2 } vowels]
            ? [$"vowels: with only {vowels.Count}, expect many more words that sound alike than usual."] : Array.Empty<string>(),
    ];
}

/// <summary>A stage of Generate, apart from what it draws.</summary>
public interface IStage
{
    string Name { get; }
    /// <summary>What this stage drew for a finished language.</summary>
    object Read(Language language);

    /// <summary>
    /// Why no result of this stage can keep the spec's fixed settings and meet these conditions,
    /// or null if one can, or if the stage mends its results to meet them instead.
    /// </summary>
    string? Unmet(LanguageSpec spec, IReadOnlyList<ICondition> conditions);
}

/// <summary>
/// A stage of Generate that a spec can fix something about, and the settings it has. Blank is for a
/// stage whose settings are yes or no, drawn independently of each other, and make up its whole
/// result, as the edges a semantic map joins do: a result to write values into. It's null for any
/// other stage.
/// </summary>
public sealed class Stage<T>(string name, Func<Language, T> from, Func<T>? blank) : IStage
{
    /// <summary>The name Generate draws the stage under.</summary>
    public string Name { get; } = name;
    /// <summary>Where the stage's result is in a finished language.</summary>
    public Func<Language, T> From { get; } = from;
    public Func<T>? Blank { get; } = blank;
    public List<ISetting<T>> Settings { get; } = [];

    public object Read(Language language) => From(language)!;

    /// <summary>
    /// For a stage with a Blank: of the results that keep the spec's fixed settings and meet the
    /// conditions, the one that changes the fewest settings of this one, or null if there's none.
    /// </summary>
    //every combination of the values the spec allows is tried, which is quick for the dozen or so
    //yes-or-no settings a semantic map has
    public T? Nearest(T drawn, LanguageSpec spec, IReadOnlyList<Condition<T>> conditions, Earlier earlier)
    {
        var (best, fewest) = (default(T), int.MaxValue);
        void Try(int index, T result, int changes)
        {
            if (changes >= fewest)
            {
                return;
            }
            if (index == Settings.Count)
            {
                if (conditions.All(c => c.Holds(result, earlier)))
                {
                    (best, fewest) = (result, changes);
                }
                return;
            }
            var setting = Settings[index];
            foreach (var value in spec.Allowed(setting) ?? setting.AllValues!)
            {
                var same = setting.Matches(drawn, [value]);
                Try(index + 1, same ? result : setting.Write(result, [value]), changes + (same ? 0 : 1));
            }
        }
        Try(0, drawn, 0);
        return fewest == int.MaxValue ? default : best;
    }

    public string? Unmet(LanguageSpec spec, IReadOnlyList<ICondition> conditions)
    {
        if (Blank is null)
        {
            return null;
        }
        var typed = conditions.Cast<Condition<T>>().ToList();
        var blank = Blank();
        if (Nearest(blank, spec, typed, Earlier.None) is not null)
        {
            return null;
        }
        List<string> alone = [.. typed.Where(c => Nearest(blank, spec, [c], Earlier.None) is null).Select(c => c.Says)];
        return alone.Count > 0 ? string.Join(" ", alone) : $"These can't all hold at once: {string.Join(" ", typed.Select(c => c.Says))}";
    }

    internal Setting<T, TValue> Declare<TValue>(string setting, string group, Func<T, TValue> get, Func<T, TValue, T> set, ISettingValues<TValue> values)
        where TValue : notnull
    {
        var declared = new Setting<T, TValue> { Name = setting, Group = group, From = From, Get = get, Set = set, Values = values };
        Settings.Add(declared);
        return declared;
    }
}
