import { makePersisted } from "@solid-primitives/storage";
import { createSignal } from "solid-js";
import { match } from "ts-pattern";
import * as v from "valibot";

import { EventNames } from "@/shared/api/events.ts";
import type { ApiEvent } from "@/shared/api/events.ts";
import { TimestampSchema } from "@/shared/api/schemas.ts";
import type { Cooldown, Job } from "@/shared/api/schemas.ts";
import { formatBytes } from "@/shared/bytes.ts";
import { formatClock } from "@/shared/clock.ts";
import type { JobFeed } from "@/shared/jobs/jobFeed.ts";
import { nameOf } from "@/shared/jobs/jobNames.ts";
import { STATE_LABELS } from "@/shared/jobs/jobStates.ts";
import { missingTracksText } from "@/shared/jobs/jobView.ts";
import { otherTabs, storedAs } from "@/shared/storage.ts";

const MAX_NOTIFICATIONS = 20;

const NotificationSchema = v.object({
  id: v.string(),
  kind: v.picklist(["ready", "incomplete", "failed", "paused", "error"]),
  title: v.string(),
  detail: v.nullable(v.string()),
  at: TimestampSchema,
  jobId: v.nullable(v.string()),
  read: v.boolean(),
});
export type NotificationItem = v.InferOutput<typeof NotificationSchema>;
export type NotificationKind = NotificationItem["kind"];

export const isFailure = (item: NotificationItem) =>
  item.kind !== "ready" && item.kind !== "paused";

// `finishedUntil` is the latest server `finishedAt` seen in milliseconds, null before a snapshot
const InboxSchema = v.object({
  items: v.array(NotificationSchema),
  finishedUntil: v.nullable(v.number()),
});
type Inbox = v.InferOutput<typeof InboxSchema>;

const [inbox, setInbox] = makePersisted(createSignal<Inbox>({ items: [], finishedUntil: null }), {
  ...storedAs("notifications", InboxSchema),
  sync: otherTabs,
});

const [errorAnnouncement, setErrorAnnouncement] = createSignal("");

/** The last error of this tab, for screen readers. */
export { errorAnnouncement };

/** The notifications of this browser, newest first. */
export const notifications = () => inbox().items;

export function markAllRead() {
  setInbox((current) => ({
    ...current,
    items: current.items.map((item) => ({ ...item, read: true })),
  }));
}

export function dismissNotification(id: string) {
  setInbox((current) => ({ ...current, items: current.items.filter((item) => item.id !== id) }));
}

export function clearNotifications() {
  setInbox((current) => ({ ...current, items: [] }));
}

/** Adds an error that answers a click, such as `Cannot remove Discovery`. */
export function notifyError(title: string, detail: string) {
  const at = new Date().toISOString();
  const item: NotificationItem = {
    id: `error:${at}:${title}`,
    kind: "error",
    title,
    detail,
    at,
    jobId: null,
    read: false,
  };
  setInbox((current) => withItems(current, [item]));
  setErrorAnnouncement(`${title}. ${detail}`);
}

/**
 * Adds the notifications of a job event, for `watchJobs`.
 *
 * A job notifies when it becomes ready, incomplete or failed after the latest finish time seen, so
 * a snapshot repeats nothing and still reports the jobs that finished while the page was closed.
 * A first snapshot only sets that time.
 */
export function noteEvent(feed: JobFeed, event: ApiEvent) {
  match(event)
    .with({ event: EventNames.snapshot }, ({ data }) => {
      setInbox((current) => withFinishedJobs(current, data.jobs));
    })
    .with({ event: EventNames.job }, ({ data }) => {
      if (isNotified(data)) {
        setInbox((current) => withFinishedJobs(current, [data]));
      }
    })
    .with({ event: EventNames.cooldown }, ({ data }) => {
      const item = cooldownItem(feed.cooldown, data);
      if (item !== null) {
        setInbox((current) => withItems(current, [item]));
      }
    })
    .otherwise(() => null);
}

// Another tab may have added the same job or cooldown already
function withItems(current: Inbox, added: readonly NotificationItem[]): Inbox {
  const fresh = added.filter((item) => !current.items.some((known) => known.id === item.id));
  return fresh.length === 0
    ? current
    : { ...current, items: [...fresh, ...current.items].slice(0, MAX_NOTIFICATIONS) };
}

type NotifiedJob = Job & { state: "ready" | "incomplete" | "failed"; finishedAt: string };

const isNotified = (job: Job): job is NotifiedJob =>
  job.finishedAt !== null &&
  (job.state === "ready" || job.state === "incomplete" || job.state === "failed");

const finishTimeOf = (job: NotifiedJob) => Date.parse(job.finishedAt);

function withFinishedJobs(current: Inbox, jobs: readonly Job[]): Inbox {
  const finished = jobs.filter((job) => isNotified(job));
  const since = current.finishedUntil;
  const added =
    since === null
      ? []
      : finished.flatMap((job) => (finishTimeOf(job) > since ? [jobItem(job)] : []));
  return {
    ...withItems(current, added),
    finishedUntil: Math.max(since ?? 0, ...finished.map((job) => finishTimeOf(job))),
  };
}

// The id holds the finish time, so a job that fails again after a retry notifies again
const jobItem = (job: NotifiedJob): NotificationItem => ({
  id: `${job.id}:${job.finishedAt}`,
  kind: job.state,
  title: nameOf(job),
  detail: [STATE_LABELS[job.state], jobFact(job)].filter((part) => part !== null).join(" · "),
  at: job.finishedAt,
  jobId: job.id,
  read: false,
});

const jobFact = (job: NotifiedJob) =>
  match(job.state)
    .with("ready", () => (job.fileSize === null ? null : formatBytes(job.fileSize)))
    .with("incomplete", () => missingTracksText(job))
    .with("failed", () => job.reason)
    .exhaustive();

function cooldownItem(previous: Cooldown | null, cooldown: Cooldown): NotificationItem | null {
  const { until } = cooldown;
  if (until === null || until === previous?.until || Date.parse(until) <= Date.now()) {
    return null;
  }
  return {
    id: `cooldown:${until}`,
    kind: "paused",
    title: `Paused until ${formatClock(until)}`,
    detail: cooldown.reason,
    at: new Date().toISOString(),
    jobId: null,
    read: false,
  };
}
