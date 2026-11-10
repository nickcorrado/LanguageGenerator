namespace LanguageGenerator.Tests;

public class RoleMarkerTests
{
    private static readonly WordOrder Postpositional = new()
    {
        ConstituentWordOrder = ConstituentWordOrder.SOV,
        NounPhraseDirectionality = HeadDirectionality.Final,
        AdjectivePhraseDirectionality = HeadDirectionality.Final,
        AdpositionalPhraseDirectionality = HeadDirectionality.Final,
        ObliquePosition = ObliquePosition.AfterVerbAndObject,
    };

    //every oblique role in a region of its own
    private static readonly List<RoleRegion> Apart = RoleMap.Regions([]);

    //a formless word for each region, glossed for it
    private static Dictionary<Role, Lexeme> Adpositions(IReadOnlyList<RoleRegion> regions) =>
        regions.SelectMany(r => r.Roles.Select(role => (role, Word: new Lexeme(new Root([], r.Gloss), PartOfSpeech.Adposition))))
            .ToDictionary(x => x.role, x => x.Word);

    private static Dictionary<Role, string> Markers(MorphosyntacticAlignment alignment, List<Case> cases, List<RoleRegion> regions) =>
        Generator.SelectRoleMarkers(alignment, cases, regions, Postpositional, Adpositions(regions)).ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

    [Fact]
    public void SelectRoleMarkers_NominativeAccusative_MarksBothSubjectsAlike()
    {
        var markers = Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Genitive], Apart);

        Assert.Equal("NOM", markers[Role.IntransitiveSubject]);
        Assert.Equal("NOM", markers[Role.TransitiveSubject]);
        Assert.Equal("ACC", markers[Role.DirectObject]);
    }

    [Fact]
    public void SelectRoleMarkers_ErgativeAbsolutive_MarksTheIntransitiveSubjectLikeTheObject()
    {
        var markers = Markers(MorphosyntacticAlignment.ErgativeAbsolutive, [Case.Ergative, Case.Absolutive], Apart);

        Assert.Equal("ABS", markers[Role.IntransitiveSubject]);
        Assert.Equal("ERG", markers[Role.TransitiveSubject]);
        Assert.Equal("ABS", markers[Role.DirectObject]);
    }

    [Fact]
    public void SelectRoleMarkers_NoCoreCases_LeavesCoreRolesUnmarked()
    {
        var markers = Markers(MorphosyntacticAlignment.None, [], Apart);

        Assert.Equal("unmarked", markers[Role.TransitiveSubject]);
        Assert.Equal("unmarked", markers[Role.DirectObject]);
    }

    [Fact]
    public void SelectRoleMarkers_LanguageHasTheCase_UsesIt()
    {
        var markers = Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Genitive], Apart);

        Assert.Equal("GEN", markers[Role.Possessor]);
    }

    [Fact]
    public void SelectRoleMarkers_LanguageLacksTheCase_FallsBackOnAnAdpositionGoverningTheCoreCase()
    {
        Assert.Equal("Postposition 'to' + ACC", Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative], Apart)[Role.Recipient]);
        Assert.Equal("Postposition 'at' + ABS", Markers(MorphosyntacticAlignment.ErgativeAbsolutive, [Case.Ergative, Case.Absolutive], Apart)[Role.Location]);
        Assert.Equal("Postposition 'from'", Markers(MorphosyntacticAlignment.None, [], Apart)[Role.Source]);
    }

    [Fact]
    public void SelectRoleMarkers_RolesInOneRegion_ShareItsCase()
    {
        var regions = RoleMap.Regions([(Role.Recipient, Role.Goal)]);
        var markers = Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Dative], regions);

        Assert.Equal("DAT", markers[Role.Recipient]);
        Assert.Equal("DAT", markers[Role.Goal]);
        Assert.Equal("Postposition 'at' + ACC", markers[Role.Location]);
    }

    [Fact]
    public void SelectRoleMarkers_RolesInOneRegionWithoutACase_ShareItsAdposition()
    {
        var regions = RoleMap.Regions([(Role.Companion, Role.Instrument)]);
        var markers = Markers(MorphosyntacticAlignment.None, [], regions);

        Assert.Equal("Postposition 'with'", markers[Role.Companion]);
        Assert.Equal("Postposition 'with'", markers[Role.Instrument]);
    }

    [Fact]
    public void SelectRoleMarkers_NoVocative_LeavesAddresseeUnmarked()
    {
        Assert.Equal("unmarked", Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative], Apart)[Role.Addressee]);
        Assert.Equal("VOC", Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Vocative], Apart)[Role.Addressee]);
    }
}
