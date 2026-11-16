import { match, P } from "ts-pattern";

import type { ApiResult } from "./client.ts";
import type { Problem } from "./schemas.ts";

// A validation problem names its fields in `errors` and has no `detail`
const fieldErrorOf = (problem: Problem) =>
  Object.values(problem.errors ?? {})
    .flat()
    .at(0);

/** Describes why a request failed, or returns null when it succeeded. */
export const failureOf = <TValue>(result: ApiResult<TValue>) =>
  match<ApiResult<TValue>, string | null>(result)
    .with({ kind: "ok" }, () => null)
    .with({ kind: "problem", problem: { detail: P.string } }, ({ problem }) => problem.detail)
    .with(
      { kind: "problem" },
      ({ problem, status }) =>
        fieldErrorOf(problem) ?? problem.title ?? `The server answered ${status}.`,
    )
    .with({ kind: "unreachable" }, () => "Cannot reach the server.")
    .exhaustive();
