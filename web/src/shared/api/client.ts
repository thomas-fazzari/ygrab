import { match } from "ts-pattern";
import * as v from "valibot";

import {
  CooldownSchema,
  JobSchema,
  JobsSchema,
  NoContentSchema,
  parseJsonWith,
  ProblemSchema,
  SessionSchema,
  StartedJobSchema,
} from "./schemas.ts";
import type { JobId, LoginRequest, Problem, StartJobRequest } from "./schemas.ts";
import { redirectToLogin } from "../session.ts";

export const API_PREFIX = "/api";
export const HTTP_UNAUTHORIZED = 401;

const LOGIN_PATH = "/login";

export type ApiResult<TValue> =
  | { readonly kind: "ok"; readonly value: TValue }
  | { readonly kind: "problem"; readonly status: number; readonly problem: Problem }
  | { readonly kind: "unreachable" };

type HttpMethod = "GET" | "POST" | "DELETE";
type JsonBody = Readonly<Record<string, string | number | boolean>>;

interface Reply {
  readonly ok: boolean;
  readonly status: number;
  readonly text: string;
}

async function send(
  method: HttpMethod,
  path: string,
  body: JsonBody | undefined,
): Promise<Reply | undefined> {
  const init: RequestInit = { method, credentials: "same-origin" };
  if (body !== undefined) {
    init.headers = { "Content-Type": "application/json" };
    init.body = JSON.stringify(body);
  }
  try {
    const response = await fetch(`${API_PREFIX}${path}`, init);
    return { ok: response.ok, status: response.status, text: await response.text() };
  } catch {
    return undefined;
  }
}

// An error answer may carry no JSON body, such as a rate limit or a proxy page
function readProblem(text: string): Problem {
  const result = v.safeParse(v.pipe(v.string(), v.parseJson(), ProblemSchema), text);
  return result.success ? result.output : {};
}

/**
 * Sends a JSON request and parses the answer with `schema`.
 *
 * An error status or an unreachable host is a result. An answer that does not match `schema`
 * is a contract bug and throws a `ValiError`. An answer without a body reads as JSON `null`,
 * which `NoContentSchema` matches. A 401 outside the login loads the login page.
 */
export async function request<TSchema extends v.GenericSchema>(
  method: HttpMethod,
  path: string,
  schema: TSchema,
  body?: JsonBody,
): Promise<ApiResult<v.InferOutput<TSchema>>> {
  const reply = await send(method, path, body);
  if (reply?.status === HTTP_UNAUTHORIZED && path !== LOGIN_PATH) {
    redirectToLogin();
  }
  return match<Reply | undefined, ApiResult<v.InferOutput<TSchema>>>(reply)
    .with(undefined, () => ({ kind: "unreachable" }))
    .with({ ok: false }, ({ status, text }) => ({
      kind: "problem",
      status,
      problem: readProblem(text),
    }))
    .with({ ok: true, text: "" }, () => ({ kind: "ok", value: parseJsonWith(schema, "null") }))
    .with({ ok: true }, ({ text }) => ({ kind: "ok", value: parseJsonWith(schema, text) }))
    .exhaustive();
}

export const login = (body: LoginRequest) => request("POST", LOGIN_PATH, NoContentSchema, body);
export const logout = () => request("POST", "/logout", NoContentSchema);
export const getSession = () => request("GET", "/session", SessionSchema);
export const startJob = (body: StartJobRequest) => request("POST", "/jobs", StartedJobSchema, body);
export const listJobs = (limit?: number) =>
  request("GET", limit === undefined ? "/jobs" : `/jobs?limit=${limit}`, JobsSchema);
export const getJob = (id: JobId) => request("GET", `/jobs/${id}`, JobSchema);
export const retryJob = (id: JobId) => request("POST", `/jobs/${id}/retry`, NoContentSchema);
export const removeJob = (id: JobId) => request("DELETE", `/jobs/${id}`, NoContentSchema);
export const getCooldown = () => request("GET", "/cooldown", CooldownSchema);

export const jobFileUrl = (id: JobId) => `${API_PREFIX}/jobs/${id}/file`;
