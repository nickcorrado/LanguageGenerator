namespace LanguageGenerator.Glossing;

/// <summary>What a model is told about the clause format.</summary>
/*
  the roles and glosses come from ClauseFile's own tables, so the prompt keeps up as the format
  grows; the system prompt is the same for every request, so that providers that cache can reuse it,
  and everything that varies is in the user message
 */
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
            invented languages. Each clause is a line of plain English saying what its gloss says, then an
            indented line glossing it: the verb, then its arguments as role:word, separated by two spaces.
            Only the gloss is translated; the English line is the translation shown under the result.

              the hunter saw the wolves
                see.PST.PFV  subject:hunter.DEF  object:wolf.PL.DEF

            Words are English meanings in dictionary form, lowercase even for names: wolf, not wolves; see, not saw; marta, not Marta.
            Features follow a word after periods, in Leipzig glossing style.
            - Verb features: {Values<Tense>()}; {Values<Aspect>()}; {Values<Polarity>()}; PASS (passive);
              {Values<Modality>()}; Q (yes-no question).
            - A modal is a feature of the verb: ABIL for 'can' or 'be able to', PERM for 'may' or 'can'
              meaning allowed, OBLIG for 'must' or 'have to', and ADVS for 'should' or 'ought to'.
              'Could' is ABIL.PST, and 'will be able to' ABIL.FUT. NEG after the modal negates the
              modal, and NEG before it the verb:

                you cannot swim in the river
                  swim.ABIL.NEG  subject:2SG  location.in:river.DEF
                you must not sing
                  sing.NEG.OBLIG  subject:2SG
                you must be careful
                  be.OBLIG  subject:2SG  adjective:careful
                you should not follow them
                  follow.NEG.ADVS  subject:2SG  object:3PL

            - A modal can also be the speaker's judgment of whether something is so: DUB for 'may',
              'might' or 'could' meaning perhaps, and for 'maybe' or 'perhaps', and PRSM for 'must'
              meaning surely, and for 'surely' or 'probably'. These judge the clause whole, so its
              tense and aspect stay what they'd be without the modal, and they can take PRF and PROG.
              'You must find the key' is OBLIG, but 'she must be tired' is PRSM; 'you may go' is PERM,
              but 'it may rain' is DUB:

                he must have left
                  leave.PRF.PRSM  subject:3SG.M
                maybe they are waiting for us
                  wait.PROG.DUB  subject:3PL  object:1PL
                she might not come
                  come.NEG.DUB  subject:3SG.F

            - A passive verb has PASS, its subject is what's done to, and agent: is who does it, if the
              sentence says: 'each light is carried by a small figure' is
              carry.PASS  subject:(light every)  agent:(figure.NDF small). Keep a passive passive.
            - Noun features: {Values<Number>()}; {Values<Definiteness>()}; {Values<Distance>(d => d != Distance.Neutral)}.
            - A noun with 'the' is DEF, and with 'a' or 'an' NDF. A noun with neither is bare, as 'wolves',
              'water' or 'his dogs' are, and so is a possessed noun: 'the king's house' is
              (house possessor:king.DEF), since 'the' goes with 'king'.
            - A noun with 'this' or 'these' is PROX, and with 'that' or 'those' DIST, in place of DEF:
              'this village' is village.PROX, 'those trees' tree.PL.DIST, and 'this small key'
              (key.PROX small). MED is a 'that' near the person spoken to, as in 'that cup in your
              hand', or neither near nor far. Standing alone, 'this' and 'that' are PROX and DIST in
              place of a noun, with PL for 'these' and 'those': 'that is the bargain' is
              be  subject:DIST  noun:bargain.DEF, and 'the old woman gave you this' has object:PROX.
              Keep them: 'this' and 'that' aren't 'the' or 'it'.
            - 'Here' and 'there' fill a role saying where, as where does: location:here and
              location:there, goal:here for 'to here', as in 'come here', and source:there for 'from
              there', with there.MED for a place near the person spoken to. 'The men here' says a
              place inside a noun phrase, which is a relative clause:
              (man.PL.DEF relative:[be  subject:_  location:here]).
            - A quantifier is a word in its noun's phrase: {string.Join(", ", ClauseFile.QuantifierNames)}.
              'Few travelers' is (traveler.PL few), not a number, 'each light' or 'every light'
              (light every), 'most of the men' (man.PL.DEF most), 'all three dogs' (dog.PL.DEF all 3)
              and 'no traveler' (traveler no). Every and no never take DEF, and none takes NDF.
              'All' or 'each' with a pronoun, as in 'they are all smiling' or 'we all want', has no
              gloss yet: leave it out.
            - someone, something, nobody, nothing, everyone and everything fill a role as a noun
              does, and can take a relative clause; somewhere, nowhere and everywhere go in location:,
              goal: or source:, as here does, and sometime, never and always go in time:, as in
              time:never. 'Somebody' is someone, 'everybody' everyone and 'no one' nobody.
              'Anyone', 'anything', 'anywhere' and 'ever' are nobody, nothing, nowhere and never in
              a negated clause, and someone, something, somewhere and sometime in a question. A
              clause with nobody, nothing, nowhere, never or no never takes NEG, which the language
              adds itself where it needs it:

                I didn't see anyone
                  see.PST.PFV  subject:1SG  object:nobody
            - Unmarked defaults: present, imperfective, affirmative, singular. Leave defaults out.
            - 'Has' or 'have' with a past participle is PRF, a perfect, and 'had' with one is PST.PRF:
              'nobody has repaired it' is repair.PRF, 'I have watched them for years' watch.PRF, 'he has
              been dead' be.PRF, and 'they had gone' go.PST.PRF. A simple past, done and over, is
              PST.PFV, and 'have' meaning to own something stays the verb have.
            - 'Be' with an -ing verb is PROG, a progressive: 'a woman is waiting' is wait.PROG and 'she
              was mending a net' mend.PST.PROG. A present that isn't going on right now, as in 'smoke
              rises from the chimneys' or 'the crows tell me everything', has neither.
            - HAB, a habitual, is for what English says is done again and again: 'used to', 'would'
              for a past habit, or a word saying it's usual, as 'usually' or 'every night'. 'She used
              to walk there' is walk.PST.HAB, 'each night he would sit by the fire' sit.PST.HAB, and
              'he usually drinks alone' drink.HAB. 'Would' that isn't a habit, as in 'would it be so
              terrible?', isn't HAB and has no gloss yet.
            - Beginning or ceasing to do something is begin or stop with a to clause, whatever verb
              English uses: 'she began to hum' is begin.PST.PFV object:[to  hum], 'they started
              running' begin.PST.PFV object:[to  run], and 'everyone stops talking' stop
              object:[to  talk]. Begin or stop with no verb after it stays as it is, as in 'the forest
              begins where the fields end' or 'they stop'.
            - Only DUB and PRSM take PRF or PROG, so 'should have gone' or 'should be waiting' can't
              be expressed.

            Pronouns are a person and number instead of a word, as in subject:1SG or object:3PL, then
            optionally .INCL or .EXCL for 'we' (whether it includes the hearer), and a gender for a
            third person pronoun: {Values<Gender>(g => g == Gender.Masculine || g == Gender.Feminine || g == Gender.Neuter)}.
            Give 'he', 'she' and 'it' their gender, and give 'they' one when context shows what it
            refers to. 'You' is 2SG or 2PL, as context shows.

            Roles: {string.Join(", ", roles)}. A recipient is who something is given or said to, a
            goal where something goes, a source where it comes from, a route the way it goes, as in
            'through the forest' or 'along the river', a companion who it's done with, an
            instrument what it's done with, and a beneficiary who or what it's for, as in 'made for
            someone' or 'wax for your ears'.

            A location, goal, source or route can say where relative to its place, written after the
            role with a period: {string.Join(", ", ClauseFile.ConfigurationNames)}, where front is 'in
            front of'. With none, it's plain 'at', 'to', 'from' or 'via'. So 'into the river' is
            goal.in:river.DEF, 'out of your ears' source.in:(ear.PL possessor:2SG), 'from under the
            table' source.under:table.DEF, 'through the forest' route.in:forest.DEF, 'across the
            bridge' route.over:bridge.DEF, 'past the mill' route.beside:mill.DEF, 'on the table'
            location.on:table.DEF and 'among the trees' location.between:tree.PL.DEF. Use one
            whenever the English says more than 'at', 'to' or 'from', since a language may tell
            them apart.

            These roles are for physical places and movement only. English uses the same words for
            much else, and none of it is a place: 'she bites through the thread' cuts the thread, so
            it's object:thread.DEF; 'against the wind' is opposing it, not touching it; 'in a line'
            and 'in silence' say how; 'in armor' says what's worn. Gloss what such a phrase means, or
            leave it out of the gloss and the English line when the meaning mostly survives.

            A noun with adjectives goes in parentheses, noun
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
                be  subject:queen.DEF  location.in:house.DEF

            With adjective:, be can also compare its subject: CMPR on the adjective, in its plain
            form, says 'more', as tall.CMPR for 'taller' or 'more tall', and standard: says what it's
            compared with, if the sentence says:

              Marta is taller than the men
                be  subject:marta  adjective:tall.CMPR  standard:man.PL.DEF
              the road is longer
                be  subject:road.DEF  adjective:long.CMPR

            What something is or does something like goes in like:, with any verb but be:

              it shines like water
                shine  subject:3SG.N  like:water

            A clause can say when, how often, how and for how long, with any number of each, in the
            order they're said, and be takes them too:
            - when: is a word of its own saying when, in its plain English form: when:now,
              when:yesterday, when:then, when:later, when:first.
            - often: is a word of its own saying how often, or whether something still goes on:
              often:again, often:still. 'Never' and 'always' are times, time:never and time:always.
            - manner: is how, written as an adjective even where English has a word of its own:
              manner:careful for 'carefully', manner:sudden for 'suddenly', manner:good for 'well'.
            - time: is when, as a noun phrase: time:night for 'at night', time:(night last) for 'last
              night', time:harvest.DEF for 'at the harvest'.
            - duration: is for how long, as a noun phrase: duration:(year.PL 200) for 'for two hundred
              years', duration:while.NDF for 'for a while'.
            - INTS on any adjective says 'very', or 'much' with CMPR: adjective:tired.INTS for 'very
              tired', manner:slow.INTS for 'very slowly', adjective:small.CMPR.INTS for 'much smaller'.

              later, you wake suddenly
                wake  subject:2SG  when:later  manner:sudden
              you climb carefully
                climb  subject:2SG  manner:careful
              the village was quiet at night
                be.PST  subject:village.DEF  adjective:quiet  time:night
              I am very tired
                be  subject:1SG  adjective:tired.INTS

            A yes-no question has Q on its verb, glossed otherwise as the statement would be, with
            'do' left out. A question asking who, what, where, when, why or how has no Q: who or
            what goes in the role it asks about, where is location:where, goal:where for 'where to'
            or source:where for 'where from', when is time:when, and why and how are written on
            their own. A question asks one thing:

              did you see them?
                see.PST.PFV.Q  subject:2SG  object:3PL
              can you swim?
                swim.ABIL.Q  subject:2SG
              who told you?
                tell.PST.PFV  subject:who  object:2SG
              what do they want?
                want  subject:3PL  object:what
              where did you get that?
                get.PST.PFV  subject:2SG  object:DIST  source:where
              why didn't you come?
                come.PST.PFV.NEG  subject:2SG  why
              when did they arrive?
                arrive.PST.PFV  subject:3PL  time:when
              how did you get here?
                come.PST.PFV  subject:2SG  goal:here  how

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
                  sit  subject:hunter.PL  location.beside:fire.DEF
                  and  argue  subject:hunter.PL

              The English line is still the whole sentence, every clause in it, as here, and so is
              it for a clause under another, below.

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

            - A clause that's the object of a verb goes inside it, in square brackets, starting with
              that or to. that is for what's said, known, thought, seen or heard, which has a time of
              its own; write it even where English leaves 'that' out. to is for what's wanted, tried,
              begun or told to be done, whose time the main verb sets, so its verb has no tense or
              aspect, and it leaves out its subject when that's the main clause's subject:

                I told you that you were late
                  tell.PST.PFV  subject:1SG  recipient:2SG  object:[that  be.PST  subject:2SG  adjective:late]
                who told you that I was coming?
                  tell.PST.PFV  subject:who  recipient:2SG  object:[that  come.PST  subject:1SG]
                they want to go home
                  want  subject:3PL  object:[to  go  goal:home]
                I told you to wait
                  tell.PST.PFV  subject:1SG  recipient:2SG  object:[to  wait  subject:2SG]

            - What someone says word for word, in quotation marks, is quote:[...] on the verb of
              saying, said as the speaker said it, so its 'I' and 'you' are the speaker's, and it can
              be a command or a question. Its English line keeps the quotation marks:

                "you are late," she says
                  say  subject:3SG.F  quote:[be  subject:2SG  adjective:late]
                "sit down," he says
                  say  subject:3SG.M  quote:[sit.IMP]

              quote: is only for what a verb of saying in the same sentence says. A sentence in
              quotation marks with no verb of saying, as in a conversation, is glossed as the
              sentence it is, with no quote: and no quotation marks: '"Where did you get that?"' is

                where did you get that?
                  get.PST.PFV  subject:2SG  object:DIST  source:where
            - A line with no verb, as in headings, item descriptions or replies, is a fragment: its
              gloss line has roles only, and no verb. noun: is a phrase on its own, an oblique role
              one on its own, label: with noun: a heading, interjection: a word like 'hello', 'oh'
              or 'alas', answer:yes or answer:no a 'yes' or 'no', and addressee: alone calls someone. Keep a
              fragment a fragment, never adding 'it is' or 'the item is', and write its English line
              as the gloss says it:

                an old iron key
                  noun:(key.NDF old iron)
                for your ears
                  beneficiary:(ear.PL possessor:2SG)
                item: rusty key
                  label:item  noun:(key rusty)
                new quest: the missing children
                  label:(quest new)  noun:(child.PL.DEF missing)
                stranger!
                  addressee:stranger
                hello, Marta
                  interjection:hello  addressee:marta
                "no," she says quietly
                  say  subject:3SG.F  quote:[answer:no]  manner:quiet

              A fragment says one noun:, one oblique, one interjection or one answer, can be said to someone with
              addressee:, and can be quoted, as in '"for your ears," he says', which is
              say  subject:3SG.M  quote:[beneficiary:(ear.PL possessor:2SG)]. A heading is said to no
              one and can't be quoted, and no line starting with and, or, but or a subordinator can
              follow a fragment.
            - A relative clause goes in its noun's parentheses as relative:[...], a clause of its own
              with _ in the role its noun fills in it, whether English says who, which, that, where
              or nothing. A clause that English shortens, as in 'a bow made of yew', is still a
              whole one. With no noun to go on, as in 'what it was owed' or 'where the fields end',
              write a general one, thing, person or place:

                nobody who goes into the forest at night comes back
                  return  subject:(nobody relative:[go  subject:_  goal.in:forest.DEF  time:night])
                she lifts the net she was mending
                  lift  subject:3SG.F  object:(net.DEF relative:[mend.PST  subject:3SG.F  object:_])
                it stands in a clearing where nothing grows
                  stand  subject:3SG.N  location.in:(clearing.NDF relative:[grow  subject:nothing  location:_])
                the forest begins where the fields end
                  begin  subject:forest.DEF  location:(place.DEF relative:[end  subject:field.PL.DEF  location:_])

              A relative clause has one _, in its own subject, object or an oblique, and not inside
              a noun phrase or clause in it.
            - Noun phrases joined by and or or go in parentheses with the conjunction first:
              object:(and (purse.NDF empty) sword.NDF) for 'an empty purse and a sword', or
              object:(or rope.NDF boat.NDF). Adjectives said together go the same way:
              adjective:(and warm crowded) for 'warm and crowded'.
            - Words for where besides 'here' and 'there', like 'inside', 'upstream' and 'somewhere',
              words like 'only' and 'even', and other conjunctions have no gloss: leave them out of
              the gloss line when the meaning mostly survives. Never put here or there in place of
              one, since 'upstream' isn't 'there': where such a word is the point of the sentence,
              as in 'there is a fallen tree upstream', the sentence can't be expressed.
            - A phrase with 'ago' or 'later' after its noun, as in 'three winters ago' or 'a month
              later', has no gloss either: leave out the whole phrase, not just 'ago' or 'later',
              since time:(winter.PL 3) alone would say 'during three winters'.
            - The English line says what the gloss says and nothing more, since readers take it as the
              translation of what the gloss became, and everything the gloss says, every joined or
              subordinate clause included. Write it fresh from the gloss: lowercase but for
              names, with no final period, though a question keeps its question mark. Keep 'the' and
              'a', and leave out whatever the gloss left out, like 'only', 'so' or 'inside'.
              Never copy the sentence you were given when the gloss says less. For 'Eat only the
              bread.' write:

                eat the bread
                  eat.IMP  object:bread.DEF

              and not 'Eat only the bread.', since nothing in the gloss says 'only'.
            - Paraphrase into what the format can say when that keeps the meaning, as with 'went' for
              walk.PST, or 'lives in' for be with location:.
            - Gloss what a fixed expression means, not its words, since another language has idioms of
              its own and English's would be nonsense in it: 'a sword that has seen better days' is a
              sword that is old or worn, 'the road washed out' is that it collapsed, and 'he kept his
              word' is that he did what he promised. Keep a figure of speech only where anyone could
              follow its picture, as in 'the forest began to take what it was owed' or 'the crows
              tell me everything'.
            - Never drop a modal that has no gloss, like a 'would' or 'could' that imagines
              something: 'would it be so terrible?' doesn't ask 'is it terrible?'.
            - Never drop or paraphrase away a word that negates, like 'nobody', 'nothing', 'never' or 'no':
              'nobody believes him' is subject:nobody, not 'people do not believe him', and
              'she says no' keeps her no, as quote:[answer:no].
            - When a sentence can't be said even loosely, as with a question asking two things, or
              how long, how often, how many, which or whose, or a question inside another clause that isn't quoted (like 'did anyone see what happened?'), a modal other than ABIL,
              PERM, OBLIG, ADVS, DUB or PRSM (like a 'would' or 'could' that imagines something), a relative clause with 'whose' or on a
              pronoun (like 'he who hesitates'), a comparison other than an adjective with be (like 'runs faster than',
              'stronger arms than yours' or 'as tall as'), a comparison with a clause (like 'warmer
              than it should be' or 'slower than before'), a subordinate clause with no main clause
              (like 'if you come back.' on its own), the most or a superlative (like 'the
              tallest'), or a command to anyone but 'you', like 'let's go', write instead one line: {CantExpress} (what's missing): the sentence.
              This is better than a gloss that changes what the sentence means.

            Reply with each numbered sentence's number in brackets, followed on the same line by the
            sentence copied exactly as it was given, then its lines, and nothing else. Answer every
            sentence once, in order, even one you'd split or can't express. The copy is only there to
            check the answers line up; the English line under it is still written fresh from the gloss:

              [1] The two old dogs slept in the house all afternoon.
              the two old dogs slept in the house
                sleep.PST  subject:(dog.DEF 2 old)  location.in:house.DEF
              [2] When did they go?
              {CantExpress} (question with when): When did they go?
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
