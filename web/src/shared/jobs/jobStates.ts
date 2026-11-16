import type { Job } from "@/shared/api/schemas.ts";

const RUNNING: ReadonlySet<Job["state"]> = new Set([
  "resolving",
  "downloading",
  "waiting",
  "packaging",
]);

export const isRunning = (job: Job) => RUNNING.has(job.state);
export const isQueued = (job: Job) => job.state === "queued";
