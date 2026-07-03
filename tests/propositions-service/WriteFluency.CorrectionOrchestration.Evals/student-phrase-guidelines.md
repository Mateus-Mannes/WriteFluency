# Student Phrase Guidelines

These guidelines describe how to generate `tags` and `studentPhrase` values for
listen-and-write correction comparisons.

The goal is to give short, useful feedback that sounds like an English teacher
reviewing what the student wrote against the original text.

## General Rules

- Add at most 3 tags per comparison.
- Use normalized tag names, for example `verb_form`, `word_choice`, or
  `articles_and_small_words`.
- Keep the phrase concise, but allow a little more detail when it clarifies the
  English rule or the meaning in context.
- Prefer specific feedback over generic advice.
- Explain the actual mistake, not only that the texts differ.
- Use the comparison context and the original sentence meaning when it helps.
- If there is no useful extra lesson, keep the phrase minimal.
- Do not end with incomplete explanations like "is a different word" or "is a
  different verb phrase". If you say the user word is different, explain what
  the user word or phrase means.
- Do not use the word "target"; say "the original", "the original says", or
  "the original uses".
- Use quotes around original and user text when repeating words or phrases.
  This applies even for single-word spelling or word-choice feedback.
- Avoid generic phrases like "listen for the exact phrase", "listen for small
  words", or "they shape the phrase" unless there is a concrete lesson attached.
- Avoid broad listening advice when a grammar, spelling, meaning, or context
  explanation is more useful.

## What Good Feedback Should Do

Good feedback should usually answer one of these questions:

- What word, sound, ending, or small word changed?
- What grammar form changed?
- What meaning did the original phrase have?
- Why does the user phrase not fit this context?
- Is the issue spelling, spacing, punctuation, tense, number, word choice, or a
  larger phrase misunderstanding?

## Tags

Use tags to classify the core mistake. Do not force a tag if it does not fit.

Common tags:

- `verb_form`: tense, base verb vs `-ing`, past vs present, adjective state vs
  past action, or incorrect verb ending.
- `singular_plural`: true singular/plural noun or agreement differences.
- `word_choice`: a different word with a different meaning.
- `spelling`: the intended word is clear but written incorrectly.
- `word_boundary`: one word vs two words, compound spacing, or words joined
  incorrectly.
- `articles_and_small_words`: articles, prepositions, auxiliaries, or other
  short function words.
- `missing_or_extra_word`: a word was added or omitted.
- `phrase_heard_incorrectly`: the user wrote a larger phrase that does not match
  the original meaning.
- `proper_noun`: names, titles, places, brands, or capitalization/name forms.
- `punctuation_or_formatting`: punctuation, apostrophes, numbers, or format
  differences.
- `possessive`: possessive apostrophes or possessive forms that change meaning.
- `number_or_unit`: numbers, money units, percentages, measurements, or unit
  wording.

Do not use `singular_plural` for tense or word-meaning changes. For example,
`tries` vs `tried` is `verb_form`, and `cause` vs `cost` is `word_choice`.
When a unit error is also a singular/plural error, use both tags, for example
`number_or_unit` and `singular_plural`.

## Grammar Feedback

Be precise about the grammar point.
When explaining tense, name the tense direction explicitly. Do not only say
"changes the tense"; say whether the student changed present/base form to past,
past to present/base form, present to future, or another clear tense/modal
shift.

Examples:

- `"Have" is present tense; "had" is past tense.`
- `"Tries" is present tense with "she"; "tried" is past tense.`
- `The original says "save" in the present/base form; "saved" is past tense.`
- `"Faced" is past tense; "face" is the present/base form.`
- `The original uses "think" as the main verb; "thinking" is an -ing form and
  would need a different sentence structure.`
- `The original says "has been open", using "open" as an adjective for a state;
  "opened" describes an action in the past.`
- `The original says the pubs are "close" to each other, meaning nearby;
  "closed" means not open.`
- `"Babies" is plural and uses "has" in the present perfect phrase; "baby had"
  is singular and uses past tense.`

Avoid calling a verb-tense difference singular/plural.

## Meaning And Context

When the user phrase is possible English but wrong in this sentence, explain why
the original meaning fits the context.

Examples:

- `"Sits" means "is located here"; "sets" usually means places something
  somewhere or prepares something.`
- `"Cause" means make something happen; "cost" means require money. The original
  is not talking about price here.`
- `"Cause money problems" means make money problems happen; "cost money" would
  mean require money. The original uses "cause" here.`
- `"Take advantage of" means use an opportunity; "advantages" is the plural noun
  for benefits.`
- `"Add up to hundreds" means the total can become hundreds; "adapt a hundred"
  means change one hundred, which does not fit here.`
- `"Thirty-five dollars each" means $35 for each share; "dollar age" changes the
  plural money unit and mistakes "each" for "age".`
- `"Beat" means surpass or do better than a record; "be" only means exist or
  equal something.`
- `"Will" presents the result as expected in the future; "would" makes it sound
  more hypothetical or conditional.`
- `"In healthcare markets" means inside those markets; "and healthcare markets"
  adds healthcare markets as a separate item.`
- `"Products starting at" means products introduced from a certain age; "product
  storing" sounds like keeping a product somewhere.`
- `"Guidance helped" means the advice had a positive effect; "guide in health"
  changes the noun and verb meaning.`
- `"Less in" means a smaller amount in the retirement savings; "lessen" is a
  verb meaning reduce.`
- `"Greenhouse dome" means a dome-shaped greenhouse; "green house stone" sounds
  like a green stone house, which does not fit the property description.`
- `The original says "help through grants", meaning support with grant money;
  "a help" loses that specific meaning.`
- `"When" asks about time; "where" asks about place.`
- `"Developing" means starting to have peanut allergies; "deloping" is not the
  correct spelling.`
- `"And" connects two ideas; "end" means a final point, so it is not
  interchangeable in this sentence.`
- `"Special" means not ordinary and fits the natural phrase here; "especial" is
  rare and usually means particular or exceptional.`

For larger phrase mix-ups, explain the original phrase instead of only saying
the user heard it incorrectly.

Example:

- `"Her heritage from Hawaii" means her cultural background connected to Hawaii;
  the user phrase does not make sense in that context.`

## Word Boundaries And Compounds

When spacing changes meaning, explain both forms if useful.

Examples:

- `"Every day" means each day; "everyday" is an adjective, as in "everyday
  clothes".`
- `"Guesthouse" is a small house for guests, and "a barn, and" separates the
  list; "guessed house" and "barnand" break that meaning.`
- `"Woodwork" and "wood work" can be equivalent in some contexts; do not create
  feedback when the comparison should be removed as equivalent.`

## Spelling

For spelling, keep the lesson direct. If the word may be unfamiliar, add a short
meaning.

Examples:

- `The correct word is "embroidery", meaning decorative stitching on cloth.`
- `The original spelling is "vandalized"; write "vandalized", not "vandalise".`
- `"Maintain" keeps the "ai" spelling after "m"; "mantain" is missing one
  letter.`

Do not add regional spelling explanations unless that is the actual lesson.

## Articles And Small Words

For article or function-word changes, give detail only when it adds value.

Examples:

- `The original pub name starts with "Ye"; writing "the Ye" adds an unnecessary
  extra article before the name.`
- `The original says "caused the"; the user wrote "cause they", missing the
  past-tense "-ed" and changing "the" to "they".`
- `You wrote "a" instead of "the"; this is mainly an article swap.`
- `The original says "important"; the user added the extra article "an".`
- `"By more" shows the amount of change; writing only "more" drops the
  preposition that marks the comparison.`

If the comparison is only `a` vs `the` and no contextual lesson is clear, a
minimal phrase is acceptable.
Avoid vague article or pronoun explanations like "more specific" versus "more
general" unless the context truly needs that distinction. If the mistake is just
a swapped article or pronoun, say that directly.

Example:

- `The original uses "this"; writing "it" swaps the pronoun in this phrase.`

## Possessives

Possessive apostrophes can change meaning. Do not treat them as only formatting
when the original is possessive and the user text is only plural.

Example:

- `"Daughters'" is possessive, meaning belonging to the daughters; "daughters"
  is only plural.`

## Punctuation And Formatting

For punctuation, apostrophes, dates, or numbers, explain the formatting rule only
when useful. Do not over-explain small punctuation if the main learning value is
low.

## Avoid These Patterns

Avoid phrases like:

- `The target includes...`
- `Listen for both the past-tense ending and the small word; together they shape
  the phrase.`
- `Listen for verb endings and tense cues; they show when the action happens or
  who does it.`
- `Small function words are easy to confuse, but they connect the phrase and
  change its exact meaning.`
- `The word is close, but the spelling or sound pattern changes.`

These are too generic. Replace them with the exact grammar, spelling, meaning,
or context issue.
