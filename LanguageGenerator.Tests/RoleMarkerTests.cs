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

    //formless words glossed with their role's English
    private static readonly Dictionary<Role, Lexeme> Adpositions = new()
    {
        [Role.Possessor] = Word("of"),
        [Role.Recipient] = Word("to"),
        [Role.Location] = Word("at"),
        [Role.Goal] = Word("to"),
        [Role.Source] = Word("from"),
        [Role.Companion] = Word("with"),
        [Role.Instrument] = Word("with"),
    };

    private static Lexeme Word(string meaning) => new(new Root([], meaning), PartOfSpeech.Adposition);

    private static Dictionary<Role, string> Markers(MorphosyntacticAlignment alignment, List<Case> cases) =>
        Generator.SelectRoleMarkers(alignment, cases, Postpositional, Adpositions).ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

    [Fact]
    public void SelectRoleMarkers_NominativeAccusative_MarksBothSubjectsAlike()
    {
        var markers = Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Genitive]);

        Assert.Equal("NOM", markers[Role.IntransitiveSubject]);
        Assert.Equal("NOM", markers[Role.TransitiveSubject]);
        Assert.Equal("ACC", markers[Role.DirectObject]);
    }

    [Fact]
    public void SelectRoleMarkers_ErgativeAbsolutive_MarksTheIntransitiveSubjectLikeTheObject()
    {
        var markers = Markers(MorphosyntacticAlignment.ErgativeAbsolutive, [Case.Ergative, Case.Absolutive]);

        Assert.Equal("ABS", markers[Role.IntransitiveSubject]);
        Assert.Equal("ERG", markers[Role.TransitiveSubject]);
        Assert.Equal("ABS", markers[Role.DirectObject]);
    }

    [Fact]
    public void SelectRoleMarkers_NoCoreCases_LeavesCoreRolesUnmarked()
    {
        var markers = Markers(MorphosyntacticAlignment.None, []);

        Assert.Equal("unmarked", markers[Role.TransitiveSubject]);
        Assert.Equal("unmarked", markers[Role.DirectObject]);
    }

    [Fact]
    public void SelectRoleMarkers_LanguageHasTheCase_UsesIt()
    {
        var markers = Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Genitive]);

        Assert.Equal("GEN", markers[Role.Possessor]);
    }

    [Fact]
    public void SelectRoleMarkers_LanguageLacksTheCase_FallsBackOnAnAdpositionGoverningTheCoreCase()
    {
        Assert.Equal("Postposition 'to' + ACC", Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative])[Role.Recipient]);
        Assert.Equal("Postposition 'at' + ABS", Markers(MorphosyntacticAlignment.ErgativeAbsolutive, [Case.Ergative, Case.Absolutive])[Role.Location]);
        Assert.Equal("Postposition 'from'", Markers(MorphosyntacticAlignment.None, [])[Role.Source]);
    }

    [Fact]
    public void SelectRoleMarkers_NoVocative_LeavesAddresseeUnmarked()
    {
        Assert.Equal("unmarked", Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative])[Role.Addressee]);
        Assert.Equal("VOC", Markers(MorphosyntacticAlignment.NominativeAccusative, [Case.Nominative, Case.Accusative, Case.Vocative])[Role.Addressee]);
    }
}
