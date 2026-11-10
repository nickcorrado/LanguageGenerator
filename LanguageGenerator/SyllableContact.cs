namespace LanguageGenerator;

/// <summary>
/// What may meet across a syllable boundary, where one syllable's coda comes right before the next
/// one's onset.
/// </summary>
//Vennemann's Syllable Contact Law: a boundary is better the further sonority falls across it, so
//al.ta is better than at.la. Languages differ in where they draw the line (Gouskova 2004 on the
//typology): some allow anything, as English does in atlas, some only a gentle rise, and some
//nothing rising at all, as Korean, where the n of sin-la becomes l. Languages that also ban a
//level contact, like n.m, are left out, since across obstruents that would ban t.k too
public enum SyllableContact
{
    Any,
    //sonority may rise by at most one step, so s.n or n.l but not t.l or s.j
    NoSteepRise,
    //sonority may not rise at all, so l.t or n.m but not t.n or n.l
    NoRise,
}

public static class SyllableContactExtensions
{
    extension(SyllableContact contact)
    {
        /// <summary>Whether this coda can come right before this onset consonant.</summary>
        public bool Allows(Consonant coda, Consonant onset)
        {
            var rise = SyllableRule.Sonority(onset) - SyllableRule.Sonority(coda);
            return contact switch
            {
                SyllableContact.Any => true,
                SyllableContact.NoSteepRise => rise <= 1,
                SyllableContact.NoRise => rise <= 0,
                _ => throw new ArgumentOutOfRangeException(nameof(contact), contact, null),
            };
        }

        public string Description => contact switch
        {
            SyllableContact.Any => "any coda before any onset",
            SyllableContact.NoSteepRise => "sonority rises at most one step from a coda to the next onset",
            SyllableContact.NoRise => "sonority never rises from a coda to the next onset",
            _ => throw new ArgumentOutOfRangeException(nameof(contact), contact, null),
        };
    }
}
