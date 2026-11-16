import { match } from "ts-pattern";

import { DISCONNECTED, EventNames } from "@/shared/api/events.ts";
import type { ApiEvent } from "@/shared/api/events.ts";
import type { Cooldown, Job } from "@/shared/api/schemas.ts";

export interface JobFeed {
  readonly jobs: Job[];
  readonly cooldown: Cooldown | null;
  readonly offline: boolean;
}

export const emptyFeed: JobFeed = { jobs: [], cooldown: null, offline: false };

const upsert = (jobs: Job[], changed: Job) =>
  jobs.some((job) => job.id === changed.id)
    ? jobs.map((job) => (job.id === changed.id ? changed : job))
    : [changed, ...jobs];

export function applyEvent(feed: JobFeed, event: ApiEvent): JobFeed {
  return match(event)
    .with({ event: EventNames.snapshot }, ({ data }) => ({ ...data, offline: false }))
    .with({ event: EventNames.job }, ({ data }) => ({ ...feed, jobs: upsert(feed.jobs, data) }))
    .with({ event: EventNames.jobRemoved }, ({ data }) => ({
      ...feed,
      jobs: feed.jobs.filter((job) => job.id !== data.id),
    }))
    .with({ event: EventNames.cooldown }, ({ data }) => ({ ...feed, cooldown: data }))
    .with({ event: DISCONNECTED }, () => (feed.offline ? feed : { ...feed, offline: true }))
    .exhaustive();
}
