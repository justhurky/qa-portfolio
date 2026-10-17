# Manual testing

Not everything is worth automating, and some things cannot be. This folder holds the part of the
effort a person does with their hands and eyes, kept to the same standard as the automated suites:
written down, repeatable, and traceable to the requirements in [`../docs/requirements.md`](../docs/requirements.md).

| File | What it is |
|------|------------|
| [test-cases.md](test-cases.md) | Scripted manual cases, with steps and expected results, for the things automation covers badly |
| [execution-log.md](execution-log.md) | What was actually run, against which build, and what happened |
| [exploratory-charters.md](exploratory-charters.md) | Session based exploratory testing: charters, timeboxes, notes and findings |

## What is tested by hand, and why

| Area | Why it is not automated |
|------|--------------------------|
| Visual layout and readability | An assertion can check that an element exists; it cannot tell you the error message is lost under the fold or that the table is unreadable at 320 px |
| Error message wording | The automated tests assert the code, not whether a human understands the sentence |
| Keyboard only operation | Automatable in principle, but the cost of asserting focus order reliably is higher than walking it once per release |
| First use without instructions | The value is in watching where someone hesitates, which is not a boolean |
| Exploratory probing of the API contract | The point is to go where no test case exists yet; once something is found it becomes an automated case |

## What manual testing found here

Exploratory session **EX-02** found [DEF-003](../docs/defects/DEF-003.md): the API rejected the
documented priority value `ROUTINE` and accepted only the undocumented `Routine`, while returning
`ROUTINE` in its responses. No automated test caught it, because the automated tests had been
written against the implementation's behaviour rather than against the documented contract. That is
the exact blind spot exploratory testing exists to cover.

It was reported, fixed, and is now locked down by unit test **UT-65**, which asserts that every
value the API renders is a value the API will accept back.
