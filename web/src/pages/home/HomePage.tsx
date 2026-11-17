import type { JSX } from "@solidjs/web";
import { createMemo, createProjection, createSignal, Loading, Show } from "solid-js";
import ClockIcon from "~icons/solar/clock-circle-linear";

import type { Job, JobId } from "@/shared/api/schemas.ts";
import { createMinuteClock, formatClock } from "@/shared/clock.ts";
import { emptyFeed, watchJobs } from "@/shared/jobs/jobFeed.ts";
import type { JobFeed } from "@/shared/jobs/jobFeed.ts";
import { createJobCards } from "@/shared/jobs/jobCards.tsx";
import { JobList } from "@/shared/jobs/JobList.tsx";
import { activeJobs, recentJobs } from "@/shared/jobs/jobView.ts";
import { Logo } from "@/shared/ui/Logo.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import { GrabForm } from "./GrabForm.tsx";

export function HomePage() {
  const feed = createProjection<JobFeed>(watchJobs, emptyFeed);
  const now = createMinuteClock();
  const [existingId, setExistingId] = createSignal<JobId | null>(null);

  const { cooldownUntil, card } = createJobCards(feed, now);
  const active = createMemo(() => activeJobs(feed.jobs));
  const recent = createMemo(() => recentJobs(feed.jobs));
  const existing = createMemo(() => feed.jobs.filter((job) => job.id === existingId()));

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
        <JobList jobs={props.jobs} card={props.card} />
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
