# Web app guidelines

`web/` is a SolidJS 2 front end. Vite builds it into `src/YGrab.Host/wwwroot`. The host serves the build and the API under `/api`.

- Tooling: bun, Vite, oxlint (type-aware, see rules in `oxlint/`), oxfmt, stylelint and TypeScript 7. Run `mise run web:check` before you report work as done. `mise run fix` formats.
- Pin exact versions in `package.json`. Commit `bun.lock`.
- Check an API name in `node_modules/solid-js/CHEATSHEET.md` and the installed typings. Solid 2 differs from Solid 1 and from React.
- Derive values with memos. Do not copy state into signals.
- Load async data through a memo, a projection or a derived store, and render it under `<Loading>`. Use `isPending` for an update in flight. There is no `createResource`.
- Use `createEffect(compute, apply)` only to synchronize with an external system, such as the DOM or a timer.
- Write a store through its draft setter: `setStore((draft) => { draft.x = 1; })`.
- Parse every API answer and event with a valibot schema from `src/shared/api/schemas.ts`, and derive its type with `v.InferOutput`. Add a schema before you add a call.
- Add an endpoint as a one-line function on `request` in `src/shared/api/client.ts`. A client error is a result value. A schema mismatch throws.
- Add an event to `EventNames` and `apiEventSchemas` in `src/shared/api/events.ts`, not to a new stream.
- Branch on a union or on the shape of a value with `match(...).with(...).exhaustive()` from `ts-pattern`, not an `if` chain or a `switch`. Keep `if` for a guard clause or a single condition.
- Use no type assertion, `any`, non-null assertion or `unknown` parameter. Type a value where it enters the app.
- Prefer a generic helper to repeated code.
- Write JSDoc only on `shared` exports whose behavior their signature does not show. Elsewhere, add a one-line comment only for a constraint the code cannot show.
- Fix a lint error in the code. Disable a rule in `oxlint.json` only when it conflicts with Solid or the formatter, and ask first.
