namespace LanguageGenerator.Tests;

public class RoleMarkerTests
{
    private static readonly WordOrder Postpositional = new()
    {
        ConstituentWordOrder = ConstituentWordOrder.SOV,
        NounPhraseDirectionality = HeadDirectionality.Final,
        AdjectivePhraseDirectionality = HeadDirectionality.Final,
        AdpositionalPhraseDirectionality = HeadDirectionality.Final,
    };

    private static Dictionary<CaseUse, string> Markers(MorphosyntacticAlignment alignment, List<Case> cases) =>
        Generator.SelectRoleMarkers(alignment, cases, Postpositional).ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

    [Fact]
    public void SelectRoleMarkers_NominativeAccusative_MarksBothSubjectsAlike()
    {
        var markers = Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Genitive]);

        Assert.Equal("NOM", markers[CaseUse.IntransitiveSubject]);
        Assert.Equal("NOM", markers[CaseUse.TransitiveSubject]);
        Assert.Equal("ACC", markers[CaseUse.DirectObject]);
    }

    [Fact]
    public void SelectRoleMarkers_ErgativeAbsolutive_MarksTheIntransitiveSubjectLikeTheObject()
    {
        var markers = Markers(MorphosyntacticAlignment.ErgativeAbsolutive, [Case.Ergative, Case.Absolutive]);

        Assert.Equal("ABS", markers[CaseUse.IntransitiveSubject]);
        Assert.Equal("ERG", markers[CaseUse.TransitiveSubject]);
        Assert.Equal("ABS", markers[CaseUse.DirectObject]);
    }

    [Fact]
    public void SelectRoleMarkers_NoCoreCases_LeavesCoreRolesUnmarked()
    {
        var markers = Markers(MorphosyntacticAlignment.None, []);

        Assert.Equal("unmarked", markers[CaseUse.TransitiveSubject]);
        Assert.Equal("unmarked", markers[CaseUse.DirectObject]);
    }

    [Fact]
    public void SelectRoleMarkers_LanguageHasTheCase_UsesIt()
    {
        var markers = Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Genitive]);

        Assert.Equal("GEN", markers[CaseUse.Possessive]);
    }

    [Fact]
    public void SelectRoleMarkers_LanguageLacksTheCase_FallsBackOnAnAdpositionGoverningTheCoreCase()
    {
        Assert.Equal("Postposition 'to' + ACC", Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative])[CaseUse.IndirectObject]);
        Assert.Equal("Postposition 'at' + ABS", Markers(MorphosyntacticAlignment.ErgativeAbsolutive, [Case.Ergative, Case.Absolutive])[CaseUse.Location]);
        Assert.Equal("Postposition 'from'", Markers(MorphosyntacticAlignment.None, [])[CaseUse.MovementFrom]);
    }

    [Fact]
    public void SelectRoleMarkers_NoVocative_LeavesAddressUnmarked()
    {
        Assert.Equal("unmarked", Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative])[CaseUse.Address]);
        Assert.Equal("VOC", Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Vocative])[CaseUse.Address]);
    }

    [Fact]
    public void SelectRoleMarkers_AnyLanguage_MarksEveryRole()
    {
        Assert.Equal(Enum.GetValues<CaseUse>().Length, Markers(MorphosyntacticAlignment.None, []).Count);
    }
}
