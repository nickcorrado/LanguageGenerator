namespace LanguageGenerator;

//alignment, cases, adpositions, and how each role is marked
public static partial class Generator
{
    //WALS 98A, by how full noun phrases are case-marked: 98 languages neutral, with no core cases, 46
    //nominative-accusative and 6 more with a marked nominative, and 32 ergative-absolutive. Its 4
    //tripartite and 4 active-inactive languages are left out
    private static readonly (MorphosyntacticAlignment Value, int Languages)[] AlignmentCounts =
    [
        (MorphosyntacticAlignment.None, 98),
        (MorphosyntacticAlignment.NominativeAccusative, 52),
        (MorphosyntacticAlignment.ErgativeAbsolutive, 32),
    ];

    public static MorphosyntacticAlignment SelectAlignment(Random rand) => Sampling.Categorical(rand, AlignmentCounts);

    /// <summary>
    /// How the role map falls into regions, one marker each: each of its edges joins its two roles
    /// with the edge's chance, and the connected parts that leaves are the regions.
    /// </summary>
    //roles can also end up together through a third, so two roles share a marker a little more
    //often than their edge alone says
    public static List<RoleRegion> SelectRegions(Random rand) =>
        RoleMap.Regions(SemanticMap.Joined(rand, RoleMap.Edges));

    //WALS 49A, by how many cases a language has besides its core ones. A language with core cases is
    //drawn from the 137 with two or more, which are the core two and the rest; one without them from
    //all 237 but the 24 with only borderline case marking, where a direct case counts as one. The
    //ranges WALS gives are drawn from evenly, and 10 or more is as many as the map allows
    private static readonly ((int Least, int Most) Obliques, int Languages)[] ObliqueCaseCountsWithCore =
        [((0, 0), 23), ((1, 1), 9), ((2, 2), 9), ((3, 3), 12), ((4, 5), 37), ((6, 7), 23), ((8, int.MaxValue), 24)];
    private static readonly ((int Least, int Most) Obliques, int Languages)[] ObliqueCaseCountsWithoutCore =
        [((0, 0), 100), ((1, 1), 23), ((2, 2), 9), ((3, 3), 9), ((4, 4), 12), ((5, 6), 37), ((7, 8), 23), ((9, int.MaxValue), 24)];

    //how likely a region is to get a case of its own, by its head's place in Blake's hierarchy, so
    //a language with any oblique case most likely has a genitive and a dative. A guess: Blake's
    //hierarchy is a tendency, and an instrumental without a dative is rare but real
    private static readonly int[] CaseWeightsByRank = [8, 6, 5, 4, 3, 2, 2];

    /// <summary>
    /// A language's cases: the core ones its alignment needs, a case for each of some of its regions,
    /// as many as WALS's counts say, and maybe a vocative.
    /// </summary>
    public static List<Case> SelectCases(Random rand, MorphosyntacticAlignment alignment, IReadOnlyList<RoleRegion> regions, double vocativeChance)
    {
        List<Case> cases = alignment switch
        {
            MorphosyntacticAlignment.NominativeAccusative => [Case.Nominative, Case.Accusative],
            MorphosyntacticAlignment.ErgativeAbsolutive => [Case.Ergative, Case.Absolutive],
            _ => [],
        };

        var (least, most) = Sampling.Categorical(rand, cases.Count > 0 ? ObliqueCaseCountsWithCore : ObliqueCaseCountsWithoutCore);
        var left = regions.Where(r => r.Case is not null).ToList();
        var top = Math.Min(most, left.Count);
        var count = least >= top ? top : least + rand.Next(top - least + 1);
        var chosen = new List<RoleRegion>();
        for (int i = 0; i < count; i++)
        {
            var region = Sampling.Categorical(rand, left.Select(r => (r, CaseWeightsByRank[RoleMap.Rank(r.Head)])).ToArray());
            left.Remove(region);
            chosen.Add(region);
        }
        cases.AddRange(chosen.OrderBy(r => RoleMap.Rank(r.Head)).Select(r => r.Case!));

        if (rand.NextDouble() < vocativeChance)
        {
            cases.Add(Case.Vocative);
        }
        return cases;
    }

    /// <summary>
    /// A one-syllable word for each region without a case, glossed in English for its head, so the
    /// roles in a region share their word as they share a case.
    /// </summary>
    //each new word is redrawn a few times to keep it from sounding like one before it; a small
    //inventory may still make homophones, as real languages have
    public static Dictionary<Role, Lexeme> GenerateAdpositions(Random rand, IReadOnlyList<RoleRegion> regions, IReadOnlyList<Case> cases, Phonology phonology, double dropoff)
    {
        var words = new Dictionary<Role, Lexeme>();
        var forms = new DistinctForms(draws: 11);
        foreach (var region in regions.Where(r => r.Case is not { } @case || !cases.Contains(@case)))
        {
            var word = new Lexeme(new Root(forms.Draw(() => GenerateForm(rand, phonology, dropoff, 0, maxSyllables: 1)), region.Gloss), PartOfSpeech.Adposition);
            foreach (var role in region.Roles)
            {
                words[role] = word;
            }
        }
        return words;
    }

    //a role gets its region's case if the language has one, and otherwise its region's adposition. A
    //duration is marked like the role it's like, or with its own word
    public static Dictionary<Role, RoleMarker> SelectRoleMarkers(MorphosyntacticAlignment alignment, IReadOnlyList<Case> cases, IReadOnlyList<RoleRegion> regions, WordOrder wordOrder,
        IReadOnlyDictionary<Role, Lexeme> adpositions, (Role? Like, Lexeme? Word) duration)
    {
        var type = wordOrder.AdpositionalPhraseDirectionality == HeadDirectionality.Initial ? AdpositionType.Preposition : AdpositionType.Postposition;
        //a simplification: real languages often pick the governed case per adposition
        Case? governs = alignment switch
        {
            MorphosyntacticAlignment.NominativeAccusative => Case.Accusative,
            MorphosyntacticAlignment.ErgativeAbsolutive => Case.Absolutive,
            _ => null,
        };

        RoleMarker Core(Case nominativeAccusative, Case ergativeAbsolutive) => alignment switch
        {
            MorphosyntacticAlignment.NominativeAccusative => nominativeAccusative,
            MorphosyntacticAlignment.ErgativeAbsolutive => ergativeAbsolutive,
            _ => new Unmarked(),
        };
        RoleMarker Oblique(Role role) => regions.First(r => r.Contains(role)).Case is { } @case && cases.Contains(@case)
            ? @case
            : new Adposition(adpositions[role], type, governs);

        RoleMarker MarkerFor(Role role) => role switch
        {
            Role.IntransitiveSubject => Core(Case.Nominative, Case.Absolutive),
            Role.TransitiveSubject => Core(Case.Nominative, Case.Ergative),
            Role.DirectObject => Core(Case.Accusative, Case.Absolutive),
            Role.Addressee => cases.Contains(Case.Vocative) ? Case.Vocative : new Unmarked(),
            Role.Duration => duration.Like is { } like ? MarkerFor(like)
                : new Adposition(duration.Word ?? throw new ArgumentException("A duration is like another role or has a word of its own.", nameof(duration)), type, governs),
            _ => Oblique(role),
        };

        //a similative has a marker of its own, outside the map
        return Enum.GetValues<Role>().Where(role => role != Role.Similative).ToDictionary(role => role, MarkerFor);
    }
}
