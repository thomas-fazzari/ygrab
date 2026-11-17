import type { Status } from "@/shared/api/schemas.ts";
import { formatBytes } from "@/shared/bytes.ts";
import { formatClock, formatDuration } from "@/shared/clock.ts";
import type { JobFeed } from "@/shared/jobs/jobFeed.ts";
import { isQueued, isRunning } from "@/shared/jobs/jobStates.ts";
import { CURRENT_VERSION } from "@/shared/version.ts";

export interface Fact {
  readonly key: string;
  readonly value: string;
}

/** Facts of one card. */
export type FactGroup = readonly Fact[];

const UNKNOWN = "unknown";

export const appFacts = (status: Status, now: number): FactGroup => [
  { key: "YGrab", value: CURRENT_VERSION },
  { key: "Uptime", value: formatDuration(now - Date.parse(status.startedAt)) },
  { key: "Data folder", value: formatBytes(status.dataBytes) },
  { key: "Free space", value: formatBytes(status.freeBytes) },
];

export const toolFacts = (status: Status): FactGroup =>
  status.components.map(({ name, version }) => ({ key: name, value: version ?? UNKNOWN }));

function pausedText(feed: JobFeed, now: number) {
  const until = feed.cooldown?.until ?? null;
  return until !== null && Date.parse(until) > now ? `until ${formatClock(until)}` : "no";
}

export const queueFacts = (feed: JobFeed, now: number): FactGroup => [
  { key: "Running", value: String(feed.jobs.filter((job) => isRunning(job)).length) },
  { key: "Queued", value: String(feed.jobs.filter((job) => isQueued(job)).length) },
  { key: "Paused", value: pausedText(feed, now) },
];
