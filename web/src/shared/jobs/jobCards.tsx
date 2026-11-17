import { createMemo } from "solid-js";
import type { Accessor } from "solid-js";

import type { Job } from "@/shared/api/schemas.ts";
import { JobCard } from "./JobCard.tsx";
import type { JobFeed } from "./jobFeed.ts";
import { isQueued } from "./jobStates.ts";
import { activeJobs, originalOf } from "./jobView.ts";
import type { CardContext } from "./jobView.ts";

/** Renders the card of a feed job, with its queue position, the cooldown and its original. */
export function createJobCards(feed: JobFeed, now: Accessor<number>) {
  const cooldownUntil = createMemo(() => {
    const until = feed.cooldown?.until ?? null;
    return until !== null && Date.parse(until) > now() ? until : null;
  });
  const queued = createMemo(() => activeJobs(feed.jobs).filter((job) => isQueued(job)));
  const contextOf = (job: Job): CardContext => ({
    ahead: queued().indexOf(job),
    paused: cooldownUntil() !== null,
    original: originalOf(feed.jobs, job),
    now: now(),
  });
  const card = (job: Job) => <JobCard job={job} context={contextOf(job)} />;
  return { cooldownUntil, card };
}
