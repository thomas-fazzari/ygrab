import type { JSX } from "@solidjs/web";
import { createMemo, createProjection, createSignal, Loading, Show } from "solid-js";
import ClockIcon from "~icons/solar/clock-circle-linear";

import type { Job } from "@/shared/api/schemas.ts";
import { createMinuteClock, formatClock } from "@/shared/clock.ts";
import { emptyFeed, watchJobs } from "@/shared/jobs/jobFeed.ts";
import type { JobFeed } from "@/shared/jobs/jobFeed.ts";
import { createJobCards } from "@/shared/jobs/jobCards.tsx";
import { JobList, JobListSkeleton } from "@/shared/jobs/JobList.tsx";
import { activeJobs, recentJobs } from "@/shared/jobs/jobView.ts";
import { nameOf } from "@/shared/jobs/jobNames.ts";
import { isFinished } from "@/shared/jobs/jobStates.ts";
import { Link } from "@/shared/router.tsx";
import { JOBS_ROUTE } from "@/shared/session.ts";
import { Button } from "@/shared/ui/Button.tsx";
import { Logo } from "@/shared/ui/Logo.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import { SkeletonText } from "@/shared/ui/Skeleton.tsx";
import { GrabForm } from "./GrabForm.tsx";
import type { Existing } from "./GrabForm.tsx";

export function HomePage() {
  const feed = createProjection<JobFeed>(watchJobs, emptyFeed);
  const now = createMinuteClock();
  const [existing, setExisting] = createSignal<Existing | null>(null);

  const isExisting = (job: Job) => job.id === existing()?.id;
  const existingJobs = createMemo(() => feed.jobs.filter((job) => isExisting(job)));
  const others = createMemo(() => feed.jobs.filter((job) => !isExisting(job)));
  const active = createMemo(() => activeJobs(others()));
  const recent = createMemo(() => recentJobs(others()));
  const shown = createMemo(() => [...existingJobs(), ...active(), ...recent()]);
  const { cooldownUntil, card } = createJobCards(feed, shown, now);

  return (
    <>
      <Logo />
      <GrabForm
        onExisting={(value) => {
          setExisting(value);
        }}
      />
      <Loading fallback={<JobsSkeleton />}>
        <ExistingSection existing={existing()} jobs={existingJobs()} card={card} />
        <JobSection
          title="Active"
          count={active().length}
          aside={<ActiveStatus offline={feed.offline} cooldownUntil={cooldownUntil()} />}
          pinned={feed.offline || cooldownUntil() !== null}
          jobs={active()}
          card={card}
        />
        <JobSection
          title="Recent"
          aside={<Link href={JOBS_ROUTE}>All jobs</Link>}
          jobs={recent()}
          card={card}
        />
        <p class="visually-hidden" role="status">
          {feed.announcement}
        </p>
      </Loading>
    </>
  );
}

interface ExistingSectionProps {
  readonly existing: Existing | null;
  readonly jobs: readonly Job[];
  readonly card: (job: Job) => JSX.Element;
}

// A finished job offers a new download, a running one has nothing to add
function ExistingSection(props: ExistingSectionProps) {
  const finished = () => props.jobs.some((job) => isFinished(job));
  const title = () => (finished() ? "Already downloaded" : "Already in the queue");
  return (
    <>
      <JobSection
        title={title()}
        aside={
          <Show when={finished()}>
            <Button
              onClick={() => {
                props.existing?.downloadAnyway();
              }}
            >
              Download anyway
            </Button>
          </Show>
        }
        jobs={props.jobs}
        card={props.card}
      />
      <p class="visually-hidden" role="status">
        {props.jobs.map((job) => `${title()}: ${nameOf(job)}`)}
      </p>
    </>
  );
}

function JobsSkeleton() {
  return (
    <section>
      <SectionHeading title={<SkeletonText chars={6} />} />
      <JobListSkeleton />
    </section>
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

// Hidden rather than unmounted while empty, so the first card to come in animates
function JobSection(props: JobSectionProps) {
  return (
    <section hidden={props.jobs.length === 0 && props.pinned !== true}>
      <SectionHeading title={props.title} detail={props.count} aside={props.aside} />
      <JobList jobs={props.jobs} card={props.card} />
    </section>
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
            YouTube limit, paused until {formatClock(until())}
          </>
        )}
      </Show>
    </Show>
  );
}
