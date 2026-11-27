# Dear Clanker

This repository contains **YGrab**, a .NET 11 app that grabs albums and tracks from YouTube Music, with a Python part for the catalog. Read `ARCHITECTURE.md`.

## Code guidelines

- Prefer early returns and small helpers to compound conditions and nested ternaries.
- No comments/docstrings by default -> add one only for what the code or the signature cannot show (a `//` or `#` comment must stay within 2 lines).
- A docstring has a one-sentence `<summary>`. Details go in `<remarks>` with one `<para>` per idea and a `<list>` for an enumeration.
- Use `string.IsNullOrWhiteSpace`, not `string.IsNullOrEmpty`.
- A fixed set of characters uses `SearchValues`, not a regex.
- SQLite columns and stored JSON hold enums as snake_case text codes, mapped with an exhaustive switch both ways that throws on an unknown code.
- Change only what the task needs, with no interim code. Report unrelated problems instead of fixing them.
- Ask before you:
  - relax an architecture test, an analyzer severity or a banned symbol
  - add a lock or hold one across I/O or another wait
  - change the SQLite schema (migration), the HTTP API or the signature of an exported Python function
  - change a rate-limit setting (pause between tracks, cooldown, backoff, yt-dlp retry and sleep arguments)
  - add or bump a Python dependency
  - stage, commit, switch branches or change the git state in any other way

### Python

- The exported functions of `yt_music.py` return tuples, lists and dicts of primitives, with `Annotated[..., "@Name"]` field names. CSnakes has no dataclass support.
- `YtMusicCatalog` switches on the exception class names `TransientError`, `RateLimitedError` and `UnavailableError`.
- Regenerate `requirements.txt` with `uv export` after you change `uv.lock`.

### Tests

- Write few tests. Add one only when its failure would reveal broken behavior that the code does not show at a glance. Never test obvious code or wiring.
- Never copy a production constant or value into a test -> make it `internal` and use it.
- Drive time with `FakeTimeProvider`. Wait for an effect the test can observe.

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
