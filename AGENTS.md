# Dear Clanker

This repository contains **YGrab**, a .NET 11 app that grabs albums and tracks from YouTube Music, with a Python part for the catalog. Read `ARCHITECTURE.md`.

## Code guidelines

- Prefer early returns and small helpers to compound conditions and nested ternaries.
- No comments or docstrings by default. Add one only for what the code or the signature cannot show.
- A `//` or `#` comment stays within 2 lines and has no trailing period.
- An XML doc comment has a one-sentence `<summary>`. Details go in `<remarks>` with one `<para>` per idea and a `<list>` for an enumeration. A Python docstring has a short first line, a blank line, then paragraphs.
- Use `string.IsNullOrWhiteSpace`, never `string.IsNullOrEmpty`.
- Put private methods at the end of the type, before the `[LoggerMessage]` methods.
- The cases of one closed hierarchy or union share a file, one file per slice when the cases belong to several slices. A type shares its file with a component that only it uses, and a port with the types of its signature. Any other type that several types use gets its own file.
- Put code that is not specific to its slice (helpers, outcome cases, mappings, test setup) in a shared place from its first use. Search for an existing one first.
- A fixed set of characters uses `SearchValues`, not a regex.
- SQLite columns and stored JSON hold enum values as snake_case text codes, never as numbers. Map them with an exhaustive switch in both directions, and throw on an unknown code.
- Change only what the task needs, with no interim code. Report unrelated problems instead of fixing them.
- Ask before you:
  - relax an architecture test, an analyzer severity or a banned symbol
  - add a lock or hold one across I/O or another wait
  - change the SQLite schema (add a migration), the HTTP API or the signature of an exported Python function
  - change a rate-limit setting: the pause between tracks, the cooldown, the backoff or the yt-dlp retry and sleep arguments (a wrong value gets the VPS IP flagged and no test shows it)
  - add or bump a Python dependency
  - stage, commit, switch branches or change the git state in any other way

### Python

- The exported functions of `yt_music.py` return tuples, lists and dicts of primitives, with `Annotated[..., "@Name"]` field names. CSnakes has no dataclass support.
- The exception class names `TransientError`, `RateLimitedError` and `UnavailableError` are a contract with `YtMusicCatalog`, which switches on `PythonExceptionType`.
- Regenerate `requirements.txt` with `uv export` after you change `uv.lock`. The `requirements` step of `hk` checks it.
- `ruff` (ALL) and `ty` must pass.

### Tests

- Write few tests. Add one only when its failure would reveal broken behavior that the code does not show at a glance. Never test obvious code or wiring.
- Never copy a production constant or value into a test. Make it `internal` and use it. Fixture inputs such as yt-dlp error lines and URLs stay literal.
- Replace a dependency with a small fake in `Fakes`. No mocking library.
- Drive time with `FakeTimeProvider`. Wait for an effect the test can observe.

## Validation

Before reporting work as done, run `mise run check`. For a Markdown change only, `mise run fmt:check` is enough.

## Branches and commits

Branch from `master`. Name the branch after the type of its main commit and a short subject, such as `fix/cooldown-reset`. `mise run check` must pass before the merge. A pull request is squashed into one commit, and its title becomes the commit message.

Commits follow [Conventional Commits](https://www.conventionalcommits.org/) with a title only, within 50 characters, as in `fix: requeue a rate-limited track`. Put the description in the pull request. The `commit-msg` hook checks each commit, but it cannot see the title of a pull request.

Pick the first type that applies to the main change. Tests, docs or formatting that come with a change of code do not count:

| Order | Question                                                  | Type       |
| ----- | --------------------------------------------------------- | ---------- |
| 1     | Does it fix a bug?                                        | `fix`      |
| 2     | Does it change functionality or the output a client sees? | `feat`     |
| 3     | Does it add or change tests?                              | `test`     |
| 4     | Does it change code style or formatting?                  | `style`    |
| 5     | Does it change documentation?                             | `docs`     |
| 6     | Does it change the build or the deployment?               | `build`    |
| 7     | Does it change infrastructure, DevOps or backups?         | `ops`      |
| 8     | Is it a maintenance or other non-code task?               | `chore`    |
| 9     | Does it restructure code specifically for performance?    | `perf`     |
| 10    | Does it restructure code without changing behavior?       | `refactor` |

## Prose

In comments, docs and pull requests:

- Use short sentences and common words. No semicolons or em dashes.
- Cut a sentence that says nothing instead of rewording it.
- State the specific number, case, command or behavior, never a vague claim.
- No filler ("note that", "in summary"), inflated words ("robust", "seamless", "leverage") or empty adverbs ("simply", "just").
- State the fact, not "not X, but Y".
- Use the present tense and one term per concept.
- Never narrate history or refer to the task or the conversation.
- No emoji or decorative characters.
