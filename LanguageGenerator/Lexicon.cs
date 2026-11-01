using System.Collections;

namespace LanguageGenerator;

//a language's words. It isn't a closed list: asking for a meaning it doesn't have coins a root for
//it. Each root depends only on the language and the meaning, so asking for words in any order, or
//for any number of other words first, gives the same roots. That means accidental homophones are
//possible, as they are in real languages; avoiding them would make each root depend on the others.
//Enumerating gives the words coined so far, in the order they were first asked for
public class Lexicon : IEnumerable<Lexeme>
{
    private readonly Func<string, PartOfSpeech, Root> coin;
    private readonly Dictionary<(string Meaning, PartOfSpeech PartOfSpeech), Lexeme> lexemes = [];
    private readonly List<Lexeme> inOrder = [];
    //languages are shared between threads, as in the tests, and asking for a word can add one
    private readonly Lock gate = new();

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
                    lexeme = new Lexeme(coin(meaning, partOfSpeech), partOfSpeech);
                    lexemes.Add((meaning, partOfSpeech), lexeme);
                    inOrder.Add(lexeme);
                }
                return lexeme;
            }
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
