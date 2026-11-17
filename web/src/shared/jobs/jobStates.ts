import type { Job } from "@/shared/api/schemas.ts";

const RUNNING: ReadonlySet<Job["state"]> = new Set([
  "resolving",
  "downloading",
  "waiting",
  "packaging",
]);

const FINISHED: ReadonlySet<Job["state"]> = new Set([
  "ready",
  "incomplete",
  "failed",
  "skipped",
  "expired",
]);

export const isRunning = (job: Job) => RUNNING.has(job.state);
export const isQueued = (job: Job) => job.state === "queued";
export const isFinished = (job: Job) => FINISHED.has(job.state);
