namespace LanguageGenerator;

/// <summary>How a clitic and its host are stressed, once they're said as one word.</summary>
public enum CliticStress
{
    //by the rule, over the whole of them, as Latin virúmque moves the stress of vírum onto -que
    Group,
    //where the host alone has it, as Spanish dígamelo keeps the stress of díga
    Host,
}

/// <summary>What happens to sounds where words meet in a sentence.</summary>
/*
  AcrossWords are those of the language's own sound changes that also apply where one word ends and
  the next begins, as Spanish lenition does in la boca, said laβoka. They change only how a sound is
  said, so a word is spelled the same wherever it stands. FinalConsonantLoss silences a word's last
  obstruent after a vowel, unless a vowel comes next, as French petit is said pəti, but petit ami
  pətitami: liaison. CliticStress is how a word with a root and the clitics leaning on it are
  stressed together
 */
public record Sandhi(IReadOnlyList<SoundChange> AcrossWords, bool FinalConsonantLoss, CliticStress CliticStress);
