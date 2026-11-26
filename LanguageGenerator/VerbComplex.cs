namespace LanguageGenerator;

/*
  a verb as what it says before it's said: its root, the markers it carries from the innermost out,
  and what its outermost verb inflects for. Syntax decides these from the clause, then says them all
  one way, so that what goes nearer the root than what is a matter of their order here, as Baker's
  Mirror Principle has affixes mirror what they scope over
 */

/// <summary>A marker on the verb, as an affix or a word beside it, reaching to the root or the edge.</summary>
public record VerbMarker(Exponent Exponent, Reach Reach);

/// <summary>
/// A verb of its own, as English have in 'has gone' or French pouvoir in il peut nager, which takes
/// the inflection and the markers outside it, with the verb and those inside it bare beside it on
/// the side it's on.
/// </summary>
public record Auxiliary(Lexeme Verb, Side Side);

/// <summary>One of a verb's markers, or an auxiliary taking over from it.</summary>
public union VerbLayer(VerbMarker, Auxiliary);

/// <summary>Inflected for tense, aspect and agreement with its subject.</summary>
public record Finite(Tense Tense, Aspect Aspect, Argument Subject);

/// <summary>A command, with the imperative's marker, which takes no tense or agreement.</summary>
public record Imperative(Morpheme Marker);

/// <summary>Inflected as a singular noun in a case, as a nominalized verb is.</summary>
public record Nominalized(Case Case);

/// <summary>Just the root, as a verb beside an auxiliary or a deranked one is.</summary>
public record Bare;

/// <summary>What a verb's outermost verb inflects for.</summary>
public union Finiteness(Finite, Imperative, Nominalized, Bare);

/// <summary>A verb with its layers, innermost first, and what its outermost verb inflects for.</summary>
public record VerbComplex(Lexeme Verb, IReadOnlyList<VerbLayer> Layers, Finiteness Finiteness)
{
    /// <summary>
    /// The verb that takes the inflection: the outermost auxiliary, or the verb itself where it has
    /// none, which is the one a question moves or a suffix marking the whole clause goes on.
    /// </summary>
    public Lexeme Outermost => Layers.Select(l => l.Value).OfType<Auxiliary>().LastOrDefault()?.Verb ?? Verb;
}
