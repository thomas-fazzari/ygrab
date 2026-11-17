import { match } from "ts-pattern";

import type { Job, JobMode } from "@/shared/api/schemas.ts";
import { nameOf } from "@/shared/jobs/jobNames.ts";

export const STATE_FILTERS = [
  "active",
  "ready",
  "incomplete",
  "failed",
  "skipped",
  "expired",
] as const;
export type StateFilter = (typeof STATE_FILTERS)[number];

export const STATE_FILTER_LABELS: Readonly<Record<StateFilter, string>> = {
  active: "Active",
  ready: "Ready",
  incomplete: "Incomplete",
  failed: "Failed",
  skipped: "Skipped",
  expired: "Expired",
};

export interface JobFilters {
  readonly query: string;
  readonly states: readonly StateFilter[];
  readonly modes: readonly JobMode[];
}

const stateFilterOf = (job: Job) =>
  match<Job["state"], StateFilter>(job.state)
    .with("queued", "resolving", "downloading", "waiting", "packaging", () => "active")
    .with("ready", () => "ready")
    .with("incomplete", () => "incomplete")
    .with("failed", () => "failed")
    .with("skipped", () => "skipped")
    .with("expired", () => "expired")
    .exhaustive();

const searchTextOf = (job: Job) => [nameOf(job), job.url, ...job.artists].join("\n").toLowerCase();

const byNewest = (first: Job, second: Job) =>
  Date.parse(second.createdAt) - Date.parse(first.createdAt) || first.id.localeCompare(second.id);

/** Returns the jobs that pass the filters, newest first. */
export function filterJobs(jobs: readonly Job[], filters: JobFilters) {
  const needle = filters.query.trim().toLowerCase();
  const states = new Set(filters.states);
  const modes = new Set(filters.modes);
  return jobs
    .filter(
      (job) =>
        states.has(stateFilterOf(job)) && modes.has(job.mode) && searchTextOf(job).includes(needle),
    )
    .toSorted(byNewest);
}
