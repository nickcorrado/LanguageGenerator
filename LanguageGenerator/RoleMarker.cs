namespace LanguageGenerator;

//a word before or after a noun phrase saying its role, glossed by its English meaning, as in an
//interlinear gloss. If the language has cases, the adposition governs one of them
public record Adposition(Lexeme Lexeme, AdpositionType Type, Case? Governs)
{
    public string Gloss => Lexeme.Root.Meaning;
}

//no case and no adposition: the role is recovered from word order (core
//arguments in English) or from context (English vocatives)
public record Unmarked;

//how a language marks one CaseUse. Every role gets exactly one strategy,
//so a language with no locative case falls back on an adposition, and so on
public union RoleMarker(Case, Adposition, Unmarked)
{
    public override string ToString() => this switch
    {
        Case c => c.Gloss,
        Adposition a when a.Governs is { } governed => $"{a.Type} '{a.Gloss}' + {governed.Gloss}",
        Adposition a => $"{a.Type} '{a.Gloss}'",
        Unmarked => "unmarked",
    };
}
