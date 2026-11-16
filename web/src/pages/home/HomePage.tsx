import type { JSX } from "@solidjs/web";
import { createMemo, createProjection, createSignal, For, Loading, Show } from "solid-js";
import ClockIcon from "~icons/solar/clock-circle-linear";

import { apiEvents } from "@/shared/api/events.ts";
import type { ApiEvent } from "@/shared/api/events.ts";
import type { Job, JobId } from "@/shared/api/schemas.ts";
import { createMinuteClock, formatClock } from "@/shared/clock.ts";
import { scan } from "@/shared/iterators.ts";
import { Logo } from "@/shared/ui/Logo.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import { GrabForm } from "./GrabForm.tsx";
import styles from "./HomePage.module.css";
import { JobCard } from "./JobCard.tsx";
import { applyEvent, emptyFeed } from "./jobFeed.ts";
import type { JobFeed } from "./jobFeed.ts";
import { activeJobs, isQueued, originalOf, recentJobs } from "./jobView.ts";
import type { CardContext } from "./jobView.ts";

const watchFeed = () => scan<ApiEvent, JobFeed>(apiEvents(), emptyFeed, applyEvent);

export function HomePage() {
  const feed = createProjection<JobFeed>(watchFeed, emptyFeed);
  const now = createMinuteClock();
  const [existingId, setExistingId] = createSignal<JobId | null>(null);

  const cooldownUntil = createMemo(() => {
    const until = feed.cooldown?.until ?? null;
    return until !== null && Date.parse(until) > now() ? until : null;
  });
  const active = createMemo(() => activeJobs(feed.jobs));
  const queued = createMemo(() => active().filter((job) => isQueued(job)));
  const recent = createMemo(() => recentJobs(feed.jobs));
  const existing = createMemo(() => feed.jobs.filter((job) => job.id === existingId()));

  const contextOf = (job: Job): CardContext => ({
    ahead: queued().indexOf(job),
    paused: cooldownUntil() !== null,
    original: originalOf(feed.jobs, job),
    now: now(),
  });
  const card = (job: Job) => <JobCard job={job} context={contextOf(job)} />;

  return (
    <>
      <Logo />
      <GrabForm
        onExisting={(id) => {
          setExistingId(id);
        }}
      />
      <Loading>
        <JobSection title="Already grabbed" jobs={existing()} card={card} />
        <JobSection
          title="Active"
          count={active().length}
          aside={<ActiveStatus offline={feed.offline} cooldownUntil={cooldownUntil()} />}
          pinned={feed.offline || cooldownUntil() !== null}
          jobs={active()}
          card={card}
        />
        <JobSection title="Recent" jobs={recent()} card={card} />
        <p class="visually-hidden" role="status">
          {feed.announcement}
        </p>
      </Loading>
    </>
  );
}

interface JobSectionProps {
  readonly title: string;
  readonly count?: number;
  readonly aside?: JSX.Element;
  /** Keeps the section without jobs, for a status in its aside. */
  readonly pinned?: boolean;
  readonly jobs: readonly Job[];
  readonly card: (job: Job) => JSX.Element;
}

function JobSection(props: JobSectionProps) {
  return (
    <Show when={props.jobs.length > 0 || props.pinned === true}>
      <section>
        <SectionHeading title={props.title} detail={props.count} aside={props.aside} />
        <div class={styles.cards}>
          <For each={props.jobs}>{props.card}</For>
        </div>
      </section>
    </Show>
  );
}

interface ActiveStatusProps {
  readonly offline: boolean;
  readonly cooldownUntil: string | null;
}

function ActiveStatus(props: ActiveStatusProps) {
  return (
    <Show when={!props.offline} fallback="offline, reconnecting">
      <Show when={props.cooldownUntil}>
        {(until) => (
          <>
            <ClockIcon />
            cooldown until {formatClock(until())}
          </>
        )}
      </Show>
    </Show>
  );
}
