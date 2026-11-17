import type { Status } from "@/shared/api/schemas.ts";
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

const unitFormat = (unit: string) =>
  new Intl.NumberFormat("en", {
    style: "unit",
    unit,
    unitDisplay: "short",
    maximumFractionDigits: 1,
  });

const BYTES = { size: 1, format: unitFormat("byte") };
const LARGER_SIZES = [
  { size: 1e3, format: unitFormat("kilobyte") },
  { size: 1e6, format: unitFormat("megabyte") },
  { size: 1e9, format: unitFormat("gigabyte") },
  { size: 1e12, format: unitFormat("terabyte") },
];

/** Formats a byte count in the largest decimal unit it reaches (e.g. `1.2 GB`). */
export function formatBytes(bytes: number) {
  const scale = LARGER_SIZES.findLast(({ size }) => bytes >= size) ?? BYTES;
  return scale.format.format(bytes / scale.size);
}

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
