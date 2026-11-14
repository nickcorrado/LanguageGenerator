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
    public IReadOnlyList<string> Problems() =>
    [
        .. allowed.SelectMany(a => a.Value.Select(a.Key.Problem).OfType<string>().Select(problem => $"{a.Key.Name}: {problem}")),
        .. SpecRules.All.Select(rule => rule.Check(this)).OfType<string>(),
    ];

    /// <summary>What a spec can meet but probably shouldn't, as an inventory too small to keep words apart.</summary>
    public IReadOnlyList<string> Warnings() =>
    [
        .. Allowed(Settings.Consonants) is [IReadOnlyList<Consonant> { Count: <= 6 } consonants]
            ? [$"consonants: with only {consonants.Count}, expect many more words that sound alike than usual."] : Array.Empty<string>(),
        .. Allowed(Settings.Vowels) is [IReadOnlyList<Vowel> { Count: <= 2 } vowels]
            ? [$"vowels: with only {vowels.Count}, expect many more words that sound alike than usual."] : Array.Empty<string>(),
    ];
}

/// <summary>A stage of Generate that a spec can fix something about, and the settings it has.</summary>
public sealed class Stage<T>(string name, Func<Language, T> from)
{
    /// <summary>The name Generate draws the stage under.</summary>
    public string Name { get; } = name;
    /// <summary>Where the stage's result is in a finished language.</summary>
    public Func<Language, T> From { get; } = from;
    public List<ISetting<T>> Settings { get; } = [];

    internal Setting<T, TValue> Declare<TValue>(string setting, string group, Func<T, TValue> get, Func<T, TValue, T> set, ISettingValues<TValue> values)
        where TValue : notnull
    {
        var declared = new Setting<T, TValue> { Name = setting, Group = group, From = From, Get = get, Set = set, Values = values };
        Settings.Add(declared);
        return declared;
    }
}
