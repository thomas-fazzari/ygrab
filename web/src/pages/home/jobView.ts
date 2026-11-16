import { match, P } from "ts-pattern";

import type { Job, VideoQuality } from "@/shared/api/schemas.ts";
import { formatClock, formatRelative } from "@/shared/clock.ts";
import type { BadgeTone } from "@/shared/ui/Badge.tsx";

type JobState = Job["state"];

export type StateLine =
  | { readonly kind: "none" }
  | { readonly kind: "text"; readonly text: string }
  | { readonly kind: "error"; readonly text: string }
  | { readonly kind: "progress"; readonly percent: number; readonly label: string };

/** What a card needs to know about the other jobs. */
export interface CardContext {
  /** The number of queued jobs that run before this one. */
  readonly ahead: number;
  readonly paused: boolean;
  /** The name of the job that a skipped job duplicates, while the page holds it. */
  readonly original: string | null;
  /** The time in milliseconds, for the relative times. */
  readonly now: number;
}

export const QUALITY_LABELS: Readonly<Record<VideoQuality, string>> = {
  "720p": "720p",
  "1080p": "1080p",
  "1440p": "1440p",
  "2160p": "2160p",
  best: "Best",
  audio: "Audio only",
};

const RECENT_LIMIT = 10;
const FULL_PERCENT = 100;
const COUNTER_DIGITS = 2;
const YOUTU_BE = "youtu.be";
const YOUTUBE_MUSIC = "music.youtube.com";
const WWW = "www.";

const RUNNING: ReadonlySet<JobState> = new Set([
  "resolving",
  "downloading",
  "waiting",
  "packaging",
]);
const FINISHED: ReadonlySet<JobState> = new Set([
  "ready",
  "incomplete",
  "failed",
  "skipped",
  "expired",
]);

export const isRunning = (job: Job) => RUNNING.has(job.state);
export const isQueued = (job: Job) => job.state === "queued";

export const plural = (count: number, noun: string) => `${count} ${noun}${count === 1 ? "" : "s"}`;

const time = (timestamp: string) => Date.parse(timestamp);
const finishedTime = (job: Job) => time(job.finishedAt ?? job.createdAt);
const needsAttention = (job: Job) => job.state === "failed" || job.state === "incomplete";

// The order in which the engine runs them: the running job, then the oldest queued one
const byRunOrder = (first: Job, second: Job) =>
  Number(isRunning(second)) - Number(isRunning(first)) ||
  time(first.createdAt) - time(second.createdAt) ||
  first.id.localeCompare(second.id);

export const activeJobs = (jobs: readonly Job[]) =>
  jobs.filter((job) => isRunning(job) || isQueued(job)).toSorted(byRunOrder);

export const recentJobs = (jobs: readonly Job[]) =>
  jobs
    .filter((job) => FINISHED.has(job.state))
    .toSorted((first, second) => finishedTime(second) - finishedTime(first))
    .slice(0, RECENT_LIMIT)
    .toSorted((first, second) => Number(needsAttention(second)) - Number(needsAttention(first)));

/** Returns the link without its scheme and `www.`, as a user would type it. */
export function displayLink(url: string) {
  const link = new URL(url);
  const host = link.hostname.startsWith(WWW) ? link.hostname.slice(WWW.length) : link.hostname;
  return `${host}${link.pathname}${link.search}`;
}

function videoIdOf(url: string) {
  const link = new URL(url);
  return link.hostname === YOUTU_BE ? link.pathname.slice(1) : link.searchParams.get("v");
}

/** A link to one track resolves to its album, so the job holds the album with one track. */
export const isTrackJob = (job: Job) => job.mode === "audio" && videoIdOf(job.url) !== null;

export const siteOf = (job: Job) =>
  new URL(job.url).hostname === YOUTUBE_MUSIC ? "YouTube Music" : "YouTube";

export function coverOf(job: Job) {
  if (job.coverUrl !== null) {
    return job.coverUrl;
  }
  const videoId = videoIdOf(job.url);
  return videoId === null
    ? null
    : `https://i.ytimg.com/vi/${encodeURIComponent(videoId)}/mqdefault.jpg`;
}

/** Returns the title to show, or null while the job has none and shows its link. */
export function titleOf(job: Job) {
  const [track] = job.tracks;
  return isTrackJob(job) && track !== undefined ? track.title : job.title;
}

export const nameOf = (job: Job) => titleOf(job) ?? displayLink(job.url);

/** Returns the name of the job that `job` duplicates, or null when `jobs` lacks it. */
export function originalOf(jobs: readonly Job[], job: Job) {
  const original = jobs.find((other) => other.id === job.duplicateOf);
  return original === undefined ? null : nameOf(original);
}

export const doneTracks = (job: Job) => job.tracks.filter((track) => track.state === "done").length;

const failedTracks = (job: Job) => job.tracks.length - doneTracks(job);

export const badgeTone = (state: JobState) =>
  match<JobState, BadgeTone>(state)
    .with("queued", "waiting", "skipped", "expired", () => "quiet")
    .with("resolving", "downloading", "packaging", () => "live")
    .with("ready", "incomplete", () => "done")
    .with("failed", () => "error")
    .exhaustive();

/** Returns the parts of the line under the title, such as the artists and the kind of job. */
export function metaOf(job: Job) {
  const artists = job.artists.length === 0 ? [] : [job.artists.join(", ")];
  const kind = match(job)
    .with({ mode: "video", quality: "audio" }, () => "audio only")
    .with(
      { mode: "video", quality: P.nonNullable },
      ({ quality }) => `video ${QUALITY_LABELS[quality]}`,
    )
    .with({ title: P.string }, ({ title }) => (isTrackJob(job) ? `from ${title}` : null))
    .otherwise(() => "music");
  return kind === null ? artists : [...artists, kind];
}

export const canRetry = (job: Job) =>
  job.state === "failed" ||
  (job.state === "incomplete" && job.tracks.some((track) => track.state === "refused"));

export const canDownload = (job: Job) => job.state === "ready" || job.state === "incomplete";

/** Whether removing the job deletes files, which calls for a confirmation. */
export const removalDeletesFiles = (job: Job) =>
  job.state !== "expired" && (job.percent > 0 || doneTracks(job) > 0 || canDownload(job));

export function removalDetail(job: Job) {
  const verb = isRunning(job) ? "Stops the download and deletes" : "Deletes";
  return match(job)
    .with({ mode: "video" }, () => `${verb} the video file.`)
    .with({ fileName: P.string.endsWith(".zip") }, () => `${verb} the zip and its tracks.`)
    .when(canDownload, () => `${verb} the audio file.`)
    .when(
      (current) => doneTracks(current) === 0,
      () => `${verb} the partial download.`,
    )
    .otherwise(() => `${verb} the ${plural(doneTracks(job), "track")} already grabbed.`);
}

// The track number is its number on the album, which can skip numbers or start past 1
const currentPosition = (job: Job) => {
  const index = job.tracks.findIndex((track) => track.number === job.currentTrack);
  return index === -1 ? job.tracks.length : index + 1;
};

function counterOf(job: Job, current: number) {
  const total = job.tracks.length;
  const width = Math.max(COUNTER_DIGITS, String(total).length);
  const pad = (count: number) => String(count).padStart(width, "0");
  return job.mode === "video" ? `${job.percent}%` : `${pad(current)}/${pad(total)}`;
}

const positionText = (ahead: number) => (ahead === 0 ? "next" : `after ${plural(ahead, "job")}`);

function queuedLine(job: Job, context: CardContext): StateLine {
  const where = context.paused ? "paused" : positionText(context.ahead);
  if (job.percent === 0 && doneTracks(job) === 0) {
    return { kind: "text", text: where };
  }
  return {
    kind: "progress",
    percent: job.percent,
    label: `${counterOf(job, doneTracks(job))} ${where}`,
  };
}

const waitingText = (job: Job) =>
  job.retryAt === null ? "retrying soon" : `retries at ${formatClock(job.retryAt)}`;

const readyLine = (job: Job, context: CardContext): StateLine =>
  job.expiresAt === null
    ? { kind: "none" }
    : { kind: "text", text: `expires ${formatRelative(job.expiresAt, context.now)}` };

// A longer retention set after the sweep moves the expiry past the deletion
function deletedText(job: Job, context: CardContext) {
  if (job.expiresAt === null || Date.parse(job.expiresAt) > context.now) {
    return "files deleted";
  }
  return `files deleted ${formatRelative(job.expiresAt, context.now)}`;
}

const skippedText = (context: CardContext) =>
  context.original === null ? "already grabbed" : `already in ${context.original}`;

export const stateLineOf = (job: Job, context: CardContext) =>
  match<JobState, StateLine>(job.state)
    .with("queued", () => queuedLine(job, context))
    .with("resolving", () => ({ kind: "text", text: new URL(job.url).hostname }))
    .with("downloading", () => ({
      kind: "progress",
      percent: job.percent,
      label: counterOf(job, currentPosition(job)),
    }))
    .with("waiting", () => ({ kind: "text", text: waitingText(job) }))
    .with("packaging", () => ({ kind: "progress", percent: FULL_PERCENT, label: "packaging" }))
    .with("ready", () => readyLine(job, context))
    .with("incomplete", () => ({
      kind: "error",
      text: `${plural(failedTracks(job), "track")} missing`,
    }))
    .with("failed", () => ({ kind: "error", text: job.reason ?? "failed" }))
    .with("skipped", () => ({ kind: "text", text: skippedText(context) }))
    .with("expired", () => ({ kind: "text", text: deletedText(job, context) }))
    .exhaustive();
