using System.Collections;

namespace LanguageGenerator;

/// <summary>
/// A language's words. It isn't a closed list: asking for a meaning it doesn't have coins a root
/// for it.
/// </summary>
//each root depends only on the language and the meaning, so asking for words in any order, or for
//any number of other words first, gives the same roots. That means accidental homophones are
//possible, as they are in real languages; avoiding them would make each root depend on the others.
//Generator does keep roots from sounding like closed-class words in the same slot, which are all
//known before any root is coined. Enumerating gives the words coined so far, in the order they were
//first asked for
public class Lexicon : IEnumerable<Lexeme>
{
    private readonly Func<string, PartOfSpeech, Root> coin;
    private readonly Dictionary<(string Meaning, PartOfSpeech PartOfSpeech), Lexeme> lexemes = [];
    private readonly List<Lexeme> inOrder = [];
    private readonly Dictionary<string, SemanticClass> classes = [];
    //languages are shared between threads, as in the tests, and asking for a word can add one
    private readonly Lock gate = new();
    private bool frozen;

    public Lexicon(Func<string, PartOfSpeech, Root> coin)
    {
        this.coin = coin;
    }

    //the lexeme with this meaning, like "dog", coining it if it's new. A meaning that's more than
    //one part of speech, like fire, is a separate lexeme for each
    public Lexeme this[string meaning, PartOfSpeech partOfSpeech]
    {
        get
        {
            lock (gate)
            {
                if (!lexemes.TryGetValue((meaning, partOfSpeech), out var lexeme))
                {
                    if (frozen)
                    {
                        throw new InvalidOperationException($"The lexicon is frozen, so it can't coin '{meaning}' ({partOfSpeech}).");
                    }
                    lexeme = new Lexeme(coin(meaning, partOfSpeech), partOfSpeech);
                    lexemes.Add((meaning, partOfSpeech), lexeme);
                    inOrder.Add(lexeme);
                }
                return lexeme;
            }
        }
    }

    //from now on, asking for a meaning it doesn't have throws instead of coining it, for a lexicon
    //that's shared and mustn't grow, like the tests' sample of languages
    public void Freeze()
    {
        lock (gate)
        {
            frozen = true;
        }
    }

    //what these nouns name, as a noun file says. This never touches a root, only what a language
    //makes of it. Texts can each bring their own list, as long as they agree
    public void Classify(IReadOnlyDictionary<string, SemanticClass> nouns)
    {
        lock (gate)
        {
            foreach (var (noun, meaning) in nouns)
            {
                if (classes.TryGetValue(noun, out var earlier) && earlier != meaning)
                {
                    throw new ArgumentException($"'{noun}' is already {earlier}, so it can't be {meaning} too.", nameof(nouns));
                }
            }
            foreach (var (noun, meaning) in nouns)
            {
                classes[noun] = meaning;
            }
        }
    }

    //what a noun names, from a noun file or else from what the generator knows
    public SemanticClass ClassOf(string meaning)
    {
        lock (gate)
        {
            return classes.TryGetValue(meaning, out var known) ? known : Semantics.Of(meaning);
        }
    }

    public Lexeme Noun(string meaning) => this[meaning, PartOfSpeech.Noun];
    public Lexeme Verb(string meaning) => this[meaning, PartOfSpeech.Verb];
    public Lexeme Adjective(string meaning) => this[meaning, PartOfSpeech.Adjective];

    public IEnumerator<Lexeme> GetEnumerator()
    {
        lock (gate)
        {
            return inOrder.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
