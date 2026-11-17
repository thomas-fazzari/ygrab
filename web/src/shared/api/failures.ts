import { match, P } from "ts-pattern";

import type { ApiResult } from "./client.ts";
import type { Problem } from "./schemas.ts";

// A validation problem names its fields in `errors` and has no `detail`
const fieldErrorOf = (problem: Problem) =>
  Object.values(problem.errors ?? {})
    .flat()
    .at(0);

const JOB_PROBLEMS: Readonly<Record<string, string>> = {
  "/problems/not-found": "This job no longer exists.",
  "/problems/not-ready": "The file is not ready yet.",
  "/problems/expired": "The files of this job were deleted.",
  "/problems/not-retryable": "This job can no longer be retried.",
};

const jobProblemOf = (problem: Problem) =>
  problem.type === undefined || problem.type === null ? undefined : JOB_PROBLEMS[problem.type];

/** Describes why a request failed, or returns null when it succeeded. */
export const failureOf = <TValue>(result: ApiResult<TValue>) =>
  match<ApiResult<TValue>, string | null>(result)
    .with({ kind: "ok" }, () => null)
    .with({ kind: "problem", problem: { detail: P.string } }, ({ problem }) => problem.detail)
    .with(
      { kind: "problem" },
      ({ problem, status }) =>
        jobProblemOf(problem) ??
        fieldErrorOf(problem) ??
        problem.title ??
        `The server answered ${status}.`,
    )
    .with({ kind: "unreachable" }, () => "Cannot reach the server.")
    .exhaustive();
