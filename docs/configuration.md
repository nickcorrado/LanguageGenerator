# How configuration works

A spec fixes some of a language's settings, and the rest are drawn as usual. [spec-format.md](spec-format.md) describes the format and lists the settings; this describes how a spec is met, for whoever changes the generator. `Settings` declares every setting, `SpecRules` the rules between them, and `Generator.Stages` draws the stages to match.

## Principles

- **Odds stay exact where they can.** A setting left free is drawn with the odds it has given what's fixed: fixing SOV still draws adposition order from the OV counts.
- **A seed that already matches keeps its language.** A stage's first draw is the one it makes with no spec, so fixing a value the seed already has changes nothing.
- **Configuration allows odd languages.** A value no draw reaches is set directly, like a fixed inventory that breaks the usual implications, or verbal adjectives that agree.
- **Only true contradictions are refused,** like a coda restriction with a template that has no codas. A setting that merely does nothing, like affixation in an isolating language, is accepted, or a spec read off a real language would fail. Where a setting can be shaped so a contradiction can't be written, that's better than a rule: core cases follow the alignment, so they aren't settings.

## Drawing a stage to match

Each stage with settings is declared in `Settings` as a `Stage`, with a name, a way to read its result off a language, and its settings, each with a name, a group, its values, how to read it off the stage's result and how to write a value into one. A stage's name is the key its randoms come from, so drawing a stage under the name it had before it had settings leaves free languages as they were.

- **Redrawing.** Most stages are drawn again, each time from a Random of its own, until a draw matches every fixed setting and meets every condition, which keeps the odds of the rest conditional on them. A stage gets 100,000 draws, which finds nearly any value the odds give, about one in 50,000 at the rarest, and gives up on one they can't in a fraction of a second.
- **Writing in.** If no draw matches, the one that matched most has the rest written in, and is then mended to meet any condition it still doesn't. Nothing declares which values are unreachable, which would drift from the odds.
- **Independent stages.** A stage whose settings make up its whole result and are drawn independently, like a semantic map's edges, has its fixed values written straight in, which keeps the seed's other values and their odds. It's drawn again only to meet its conditions.
- **Stages that coin words as they decide,** like negation, pronouns or articles, coin them again with each draw. A write reuses the forms the draw has where it can, so a negative particle's form becomes a prefix's and an imperative's serves another number. A value that needs a form the draw lacks, like a passive where none was drawn, can't be written, and throws; every such value is drawn at least 2% of the time, so redrawing always gives it first.

## Rules

A rule is an implication from a setting to a condition on another setting or on an earlier stage's result. There are two kinds:

- **Narrowings** narrow the values settings can take, as `SpecRule.Together` does for two choice settings that have to go together, or return null to refuse the spec. `SpecRules.Close` applies them until nothing changes, so a rule must return the spec it was given when it has nothing to narrow; after 500 passes `Close` throws, naming the rule that kept changing it.
- **Conditions** are checked against a stage's draws, with a repair for the fallback, for what narrowing one setting can't say, mostly about connectivity: a dative needs no chain of joined edges from the recipient to the possessor.

The rules are used three ways: to refuse a spec that contradicts itself, with the rule's reason; to draw the stages before a fixed value so that a value written in agrees with them, so a spatial case is never written into an isolating language; and, later, for a frontend to grey out what's no longer possible.

A rule that holds of every language the generator makes changes nothing inside a group (below), since every attempt that matches already meets it. Outside a group it gives coherence without exact odds: fixing prefixed negation keeps morphology from being isolating, but doesn't make heavy prefixing likelier. Every language in the free sample must meet every rule, which tests the rules against the generator.

## Stages drawn together

Some settings' odds depend on earlier stages: which cases a language has depends on its alignment and role regions, and how it marks configurations on its morphology and cases. Redrawing one stage alone keeps the earlier ones as they fell, so word order, morphology, alignment, role regions, cases and the configuration decisions are drawn as a group, in one `DrawTogether` call, and fixing a spatial case makes concatenative morphology likelier, as it should.

- **What an attempt redraws.** Attempt 0 draws each stage with its usual key. Attempt n redraws the stages up to the last one that might not match, each with "{stage} #{n}", and reruns the rest with their attempt 0 randoms, so fixing ergative keeps the seed's role regions. An attempt is abandoned at the first stage that misses.
- **The fallback.** After 10,000 attempts the stages are drawn one at a time, each given the ones before it, which the rules have made sure allow it.
- **Which stages.** A group holds a fixed setting's deciding ancestors, the stages whose results change its odds, and groups with an ancestor in common merge, so there's one, listed by hand. Stages outside it, like the paradigms, don't reshape the stages before them: fixing `tense` doesn't change how isolating a language is.
- **Deciding apart from coining.** A stage in a group decides without coining words, since an attempt may be thrown away and coining is the slow part. Configurations decide inside the group, and their adpositions are coined after it under their own key, "configuration words", so the words don't change when the group is redrawn.

## Semantic maps

Roles, configurations, modalities and subordinators are each a semantic map, whose joined edges make regions that share a marker, so they take one shape: a true or false setting for each edge, and a setting for how each node is marked.

- **Regions are the connected parts of the joined edges,** so a region the map can't draw, like possessors and times alone, can't be written. That's the one place configuration doesn't allow the odd, since connectivity is what a region means rather than a tendency.
- **Nodes joined by an edge share a marker,** so they can't be fixed to different ones, and narrowing goes along paths: fixing two edges joined narrows all three nodes together.
- **A marker written in moves its whole region,** keeping its form, and joining two regions gives the second the first's marker. Parting them would need a form for the second, so it's only ever drawn.
- **Cases** are a true or false setting for each oblique case, `configuration-marking` is the preference most configuration regions follow, and `temporal-balance` covers 'when' and the clauses that follow it, as the generator draws them.

## Inventories

An inventory is a list of IPA symbols, and its order is meaningful, as in Mark Rosenfelder's generators: the first sound is the commonest. A fixed inventory needn't follow the implications generated ones do, like z only with s.

- **Required and forbidden sounds.** Each of the 53 sounds has a true or false setting, written `{ "require": [...], "forbid": [...] }` in the inventory's place. The inventory is redrawn until it matches, which keeps the free sounds' odds exact; where no draw does, a required sound goes where its markedness ranks it and a forbidden one is taken out.
- **Counts** are settings of the inventory's stage the same way. A count no draw gives cuts the rarest sounds the spec and its sound changes can spare, or adds the commonest it allows.
- **Sound changes** need sounds and syllables from earlier stages: unstressed raising needs a mid vowel besides i and u, final devoicing a template with codas. What a change needs only gets easier to meet as sounds are added, so a free inventory is redrawn to have it, an exact one without it is refused, and a count is narrowed to leave room for the fewest sounds that meet it.

## Testing

- **The random-spec test** fixes about a quarter of the settings at random, 300 times, and checks that each spec is either refused or met, and that every language it gives keeps every rule, which tests the write-ins. A spec that's neither is missing a rule. It's the longest test there is, in a class of its own.
- **The per-value test** fixes every value of every choice setting alone, and checks the language has it.
- **Free languages** should come out the same after a change that only adds settings: build the command line, generate a few dozen seeds, and compare them with the output from before.
- **Tests about languages with some feature** fix it with a spec rather than filtering the shared sample of 300 for it, since a filter can come up empty whenever a change shifts the draws. Tests that something holds in every language, or of how often something happens, stay on the free sample, since a spec skews the odds by design. `GeneratedLanguages.With` gives a language with some settings fixed.

## What it leaves out

- Fixing a later setting reshapes only the stages drawn in a group with it, so a setting outside the group leaves the stages before it as they fell.
- A seed and a spec give the same language only on the same version of the generator, though a spec keeps fitting a newer generator, whose new settings are just free. Saving a finished language for good would be a feature of its own, as would writing your own roots and affixes.
