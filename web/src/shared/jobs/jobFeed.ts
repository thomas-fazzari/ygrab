import { createContext } from "solid-js";
import { match } from "ts-pattern";

import { apiEvents, DISCONNECTED, EventNames } from "@/shared/api/events.ts";
import type { ApiEvent } from "@/shared/api/events.ts";
import type { Cooldown, Job } from "@/shared/api/schemas.ts";
import { scan } from "@/shared/iterators.ts";
import { nameOf } from "./jobNames.ts";
import { STATE_LABELS } from "./jobStates.ts";

export interface JobFeed {
  readonly jobs: Job[];
  readonly cooldown: Cooldown | null;
  readonly offline: boolean;
  /** Last state change of a job, for screen readers. */
  readonly announcement: string;
}

export const emptyFeed: JobFeed = { jobs: [], cooldown: null, offline: false, announcement: "" };

/** Feed of the signed-in pages, sharing one event stream. */
export const JobFeedContext = createContext<JobFeed>();

const upsert = (jobs: Job[], changed: Job) =>
  jobs.some((job) => job.id === changed.id)
    ? jobs.map((job) => (job.id === changed.id ? changed : job))
    : [changed, ...jobs];

// A snapshot only repeats what the page shows, so only a job event announces anything
function announce(feed: JobFeed, changed: Job) {
  const previous = feed.jobs.find((job) => job.id === changed.id);
  return previous?.state === changed.state
    ? feed.announcement
    : `${nameOf(changed)}: ${STATE_LABELS[changed.state]}`;
}

function announceRemoval(feed: JobFeed, id: Job["id"]) {
  const removed = feed.jobs.find((job) => job.id === id);
  return removed === undefined ? feed.announcement : `Removed ${nameOf(removed)}`;
}

function applyEvent(feed: JobFeed, event: ApiEvent): JobFeed {
  return match(event)
    .with({ event: EventNames.snapshot }, ({ data }) => ({ ...feed, ...data, offline: false }))
    .with({ event: EventNames.job }, ({ data }) => ({
      ...feed,
      jobs: upsert(feed.jobs, data),
      announcement: announce(feed, data),
    }))
    .with({ event: EventNames.jobRemoved }, ({ data }) => ({
      ...feed,
      jobs: feed.jobs.filter((job) => job.id !== data.id),
      announcement: announceRemoval(feed, data.id),
    }))
    .with({ event: EventNames.cooldown }, ({ data }) => ({ ...feed, cooldown: data }))
    .with({ event: DISCONNECTED }, () => (feed.offline ? feed : { ...feed, offline: true }))
    .exhaustive();
}

/** Folds the job events into the feed, for `createProjection`, and passes each one to `observe`. */
export const watchJobs = (observe: (feed: JobFeed, event: ApiEvent) => void) =>
  scan<ApiEvent, JobFeed>(apiEvents(), emptyFeed, (feed, event) => {
    observe(feed, event);
    return applyEvent(feed, event);
  });
