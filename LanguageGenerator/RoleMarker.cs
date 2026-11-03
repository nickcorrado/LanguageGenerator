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
//TODO two simplifications to revisit:
//- Cases are a fixed list named for one use each, and a role with no case of its own gets an
//  adposition. Real cases cover connected regions of a semantic map of roles (Haspelmath):
//  Latin's ablative covers source, instrument and location, and Greek, with no ablative, puts more
//  on its dative. Drawing a language's cases as a partition of that map would let a missing case
//  fall back on its neighbor's, as Features does for number and tense. Merging neighbors is also
//  how a case is lost over time, as with Late Latin's locative, if languages ever get histories
//- A role gets a case or an adposition, but Latin uses both: in urbe 'in the city' with the
//  ablative, in urbem 'into the city' with the accusative, the adposition saying where and the
//  case whether anything moves, and cum taking the ablative by convention. Once there are
//  spatial adpositions like 'under', this should be a record of an optional adposition and a case,
//  each adposition governing its own case, and not a union
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
