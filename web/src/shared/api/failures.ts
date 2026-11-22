import { isMatching, match, P } from "ts-pattern";

import { HTTP_CONFLICT } from "./client.ts";
import type { ApiResult } from "./client.ts";
import type { Problem } from "./schemas.ts";

const VALIDATION_PROBLEM = "/problems/validation";

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
  "/problems/not-movable": "This job or its new place has left the queue.",
};

const jobProblemOf = (problem: Problem) =>
  problem.type === undefined || problem.type === null ? undefined : JOB_PROBLEMS[problem.type];

/** The job that already holds the link of a refused start, or null for any other result. */
export const previousIdOf = <TValue>(result: ApiResult<TValue>) =>
  match(result)
    .with(
      { kind: "problem", status: HTTP_CONFLICT, problem: { previousId: P.string } },
      ({ problem }) => problem.previousId,
    )
    .otherwise(() => null);

/** Whether the server refused the input of a request. */
export const isRefusedInput = <TValue>(result: ApiResult<TValue>) =>
  isMatching({ kind: "problem", problem: { type: VALIDATION_PROBLEM } }, result);

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
