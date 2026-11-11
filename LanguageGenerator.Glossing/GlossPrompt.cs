namespace LanguageGenerator.Glossing;

/// <summary>What a model is told about the clause format.</summary>
//the roles and glosses come from ClauseFile's own tables, so the prompt keeps up as the format
//grows; the system prompt is the same for every request, so that providers that cache can reuse it,
//and everything that varies is in the user message
public static class GlossPrompt
{
    public const string CantExpress = "# can't express yet";

    public static string System { get; } = BuildSystem();

    private static string Values<T>(Func<T, bool>? keep = null) where T : Grammeme =>
        string.Join(", ", ClauseFile.ValuesOf<T>().Where(v => keep?.Invoke(v) ?? true).Select(v => $"{v.Gloss} ({v.Name.ToLowerInvariant()})"));

    private static string BuildSystem()
    {
        var roles = ClauseFile.RoleNames.Where(r => r is not ("noun" or "adjective"));
        return $"""
            You turn English into clause files for a language generator, which translates them into
            invented languages. Each clause is a line of plain English saying what it means, then an
            indented line glossing it: the verb, then its arguments as role:word, separated by two spaces.

              the hunter saw the wolves
                see.PST.PFV  subject:hunter.DEF  object:wolf.PL.DEF

            Words are English meanings in dictionary form, lowercase even for names: wolf, not wolves; see, not saw; marta, not Marta.
            Features follow a word after periods, in Leipzig glossing style.
            - Verb features: {Values<Tense>()}; {Values<Aspect>()}; {Values<Polarity>()}; PASS (passive);
              {Values<Modality>()}.
            - A modal is a feature of the verb: ABIL for 'can' or 'be able to', PERM for 'may' or 'can'
              meaning allowed, OBLIG for 'must' or 'have to', and ADVS for 'should' or 'ought to'.
              'Could' is ABIL.PST, and 'will be able to' ABIL.FUT. NEG after the modal negates the
              modal, and NEG before it the verb:

                you cannot swim in armor
                  swim.ABIL.NEG  subject:2SG  location:armor
                you must not sing
                  sing.NEG.OBLIG  subject:2SG
                you must be careful
                  be.OBLIG  subject:2SG  adjective:careful
                you should not follow them
                  follow.NEG.ADVS  subject:2SG  object:3PL

            - A passive verb has PASS, its subject is what's done to, and agent: is who does it, if the
              sentence says: 'each light is carried by a small figure' is
              carry.PASS  subject:(light each)  agent:(figure.NDF small). Keep a passive passive.
            - Noun features: {Values<Number>()}; {Values<Definiteness>()}.
            - A noun with 'the' is DEF, and with 'a' or 'an' NDF. A noun with neither is bare, as 'wolves',
              'water' or 'his dogs' are, and so is a possessed noun: 'the king's house' is
              (house possessor:king.DEF), since 'the' goes with 'king'.
            - Unmarked defaults: present, imperfective, affirmative, singular. Leave defaults out.

            Pronouns are a person and number instead of a word, as in subject:1SG or object:3PL, then
            optionally .INCL or .EXCL for 'we' (whether it includes the hearer), and a gender for a
            third person pronoun: {Values<Gender>(g => g == Gender.Masculine || g == Gender.Feminine || g == Gender.Neuter)}.
            Give 'he', 'she' and 'it' their gender, and give 'they' one when context shows what it
            refers to. 'You' is 2SG or 2PL, as context shows.

            Roles: {string.Join(", ", roles)}. A recipient is who something is given or said to, a
            goal where something goes, a source where it comes from, a companion who it's done with,
            and an instrument what it's done with. A noun with adjectives goes in parentheses, noun
            first, as in object:(wolf.PL.DEF big gray). So does a number, in digits from 1 to {NumeralSystem.Largest}, as in
            subject:(wolf 3 gray); it gives the noun its number, so leave that out. So does an
            ordinal, in digits with its English ending, as in object:(door.DEF 2nd) for 'the second door',
            and 'last' and 'next' are adjectives, not ordinals. So does a
            possessor, a noun or pronoun, as in object:(house possessor:king) for 'the king's house' or
            companion:(dog.PL possessor:3SG.M) for 'his dogs'. A possessor can have a possessor of its own,
            in parentheses inside: subject:(son possessor:(sister possessor:1SG)) for 'my sister's son'. An addressee is who a clause is said to,
            called by name or by what they are, a bare noun with no DEF or NDF, as in addressee:marta
            for 'Marta, the wolves are coming' or addressee:(man old) for 'old man, ...'. A command to
            an addressee in the plural, as in addressee:child.PL, is to all of them.

            The verb be says what its subject is, with noun: or adjective: in place of an object, or
            where it is, with location:, and nothing else:

              the gate was locked
                be.PST  subject:gate.DEF  adjective:locked
              the queen is in the house
                be  subject:queen.DEF  location:house.DEF

            With adjective:, be can also say what its subject is compared with, as standard:, with the
            adjective in its plain form, tall and not taller:

              Marta is taller than the men
                be  subject:marta  adjective:tall  standard:man.PL.DEF

            A command to 'you' has IMP on its verb, with no tense or aspect, NEG for 'don't', and no
            subject, unless it's to several people, which is subject:2PL:

              don't drink the red wine
                drink.IMP.NEG  object:(wine.DEF red)
              go to the inn, all of you
                go.IMP  subject:2PL  goal:inn.DEF
              be careful
                be.IMP  adjective:careful

            Rules:
            - One clause per verb. Clauses joined by and, or or but stay one sentence: write the first
              clause, then each one joined to it on an indented line of its own starting with and, or
              or but. Split other sentences with several verbs into several clauses, each with its own
              English line, as in 'the hunter saw the wolves' and 'he ran'.

                hunters sit by the fire and argue
                  sit  subject:hunter.PL  location:fire.DEF
                  and  argue  subject:hunter.PL

            - A clause under another, saying if, when, before, after, until, because or in order to,
              goes on an indented line of its own starting with if, when, before, after, until,
              because or purpose, after the main clause, whichever order the English has. A purpose
              clause can leave out its subject when it's the main clause's:

                if you hear the song, cover your ears
                  cover.IMP  object:(ear.PL possessor:2SG)
                  if  hear  subject:2SG  object:song.DEF
                to cross the river, you need a rope
                  need  subject:2SG  object:rope.NDF
                  purpose  cross  object:river.DEF

            - Noun phrases joined by and or or go in parentheses with the conjunction first:
              object:(and (purse.NDF empty) sword.NDF) for 'an empty purse and a sword', or
              object:(or rope.NDF boat.NDF). Adjectives said together go the same way:
              adjective:(and warm crowded) for 'warm and crowded'.
            - Demonstratives, adverbs and other conjunctions have no gloss: leave them out of the gloss
              line when the meaning mostly survives, and gloss 'this' or 'that' with a noun as DEF. The English line is natural
              English saying what the gloss does, so 'the' stays, but a dropped adverb goes.
            - Paraphrase into what the format can say when that keeps the meaning, as with 'went' for
              walk.PST, or 'lives in' for be with location:.
            - Never drop a modal, like might, maybe or want to, that has no gloss: 'it might rain'
              doesn't say 'it rains'.
            - When a sentence can't be said even loosely, as with a question, a modal other than ABIL,
              PERM, OBLIG or ADVS (like might, maybe or want to), a relative
              clause, a comparison other than an adjective with be (like 'runs faster than' or
              'stronger arms than yours'), or a command to anyone but 'you', like 'let's go', write instead one line: {CantExpress} (what's missing): the sentence.
              This is better than a gloss that changes what the sentence means.

            Reply with each numbered sentence's lines after its number in brackets, and nothing else:

              [1]
              the two dogs slept in the old house
                sleep.PST  subject:(dog.DEF 2)  location:(house.DEF old)
              [2]
              {CantExpress} (question): Where did they go?
            """;
    }

    //what a noun file needs, as a job of its own: each noun is asked about once, for the whole text,
    //so a word can't come out differently from one sentence to the next
    public const string NounSystem = """
        You sort nouns by what they name, for a language generator. Each noun comes with an English
        sentence that uses it. Reply with one line for each noun that names a person or an animal,
        the noun and then its tag, and nothing else:

          marta F
          king M
          hunter HUM
          crow ANIM

        M and F are for a word that itself means a male or a female being, and for personal names:
        'king', 'mother', 'marta'. The sentence can tell you what a name's bearer is, but not a
        word's: 'hunter' is HUM even in a sentence about a woman, since a hunter can be either.
        HUM is any other word for a person: 'child', 'friend', 'miller'.
        ANIM is any other living creature: 'crow', 'rat'.

        Leave out every other noun, like 'river', 'fire' or 'harvest'. If that's all of them, reply
        with just NONE.
        """;

    public static string Nouns(IReadOnlyList<(string Noun, string Translation)> nouns) =>
        string.Join("\n", nouns.Select(n => $"{n.Noun}: {n.Translation}"));

    public static string NounRetry(IReadOnlyList<(string Noun, string Translation)> nouns, string attempt, string error) =>
        $"{Nouns(nouns)}\n\nYour reply was:\n{attempt}\n\nThat can't be used: {error}\nReply again, with only the nouns above.";

    //the sentences to gloss, numbered, after a few before them for context, like who 'she' is
    public static string Batch(IReadOnlyList<string> context, IReadOnlyList<string> sentences)
    {
        var lines = new List<string>();
        if (context.Count > 0)
        {
            lines.Add("Earlier in the text, for context only:");
            lines.AddRange(context.Select(c => $"  {c}"));
            lines.Add("");
        }
        lines.Add("Gloss these:");
        lines.AddRange(sentences.Select((s, i) => $"[{i + 1}] {s}"));
        return string.Join("\n", lines);
    }

    //sentences whose glosses didn't parse, with what was wrong, to try again
    public static string Retry(IReadOnlyList<string> context, IReadOnlyList<(string Sentence, string Attempt, string Error)> failures)
    {
        var lines = new List<string>();
        if (context.Count > 0)
        {
            lines.Add("Earlier in the text, for context only:");
            lines.AddRange(context.Select(c => $"  {c}"));
            lines.Add("");
        }
        lines.Add("These glosses didn't parse. Fix each one, or say it can't be expressed yet.");
        foreach (var (failure, i) in failures.Select((f, i) => (f, i)))
        {
            lines.Add("");
            lines.Add($"[{i + 1}] {failure.Sentence}");
            lines.Add("Your gloss:");
            lines.AddRange(failure.Attempt.Split('\n').Select(l => $"  {l}"));
            lines.Add($"The parser said: {failure.Error}");
        }
        return string.Join("\n", lines);
    }
}
