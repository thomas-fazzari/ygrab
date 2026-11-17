import { createMemo, createProjection, createSignal, Loading, Show } from "solid-js";

import { JOB_MODES } from "@/shared/api/schemas.ts";
import { createMinuteClock } from "@/shared/clock.ts";
import { createJobCards } from "@/shared/jobs/jobCards.tsx";
import { emptyFeed, watchJobs } from "@/shared/jobs/jobFeed.ts";
import type { JobFeed } from "@/shared/jobs/jobFeed.ts";
import { JobList, JobListSkeleton } from "@/shared/jobs/JobList.tsx";
import { MODE_LABELS, plural } from "@/shared/jobs/jobView.ts";
import { MultiSelect } from "@/shared/ui/MultiSelect.tsx";
import { PageTitle } from "@/shared/ui/PageTitle.tsx";
import { SearchField } from "@/shared/ui/SearchField.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import { filterJobs, STATE_FILTER_LABELS, STATE_FILTERS } from "./jobFilters.ts";
import type { JobFilters } from "./jobFilters.ts";
import styles from "./JobsPage.module.css";

export const JOBS_ROUTE = "/jobs";

const SHOWN_LIMIT = 100;
const TITLE = "History";

const NO_FILTERS: JobFilters = { query: "", states: STATE_FILTERS, modes: JOB_MODES };

export function JobsPage() {
  const feed = createProjection<JobFeed>(watchJobs, emptyFeed);
  const now = createMinuteClock();
  const [filters, setFilters] = createSignal(NO_FILTERS);
  const matching = createMemo(() => filterJobs(feed.jobs, filters()));
  const shown = createMemo(() => matching().slice(0, SHOWN_LIMIT));
  const hidden = createMemo(() => matching().length - shown().length);
  const { card } = createJobCards(feed, shown, now);

  return (
    <>
      <PageTitle>Jobs</PageTitle>
      <Loading fallback={<HistorySkeleton />}>
        <section>
          <SectionHeading
            title={TITLE}
            detail={feed.offline ? "offline, reconnecting" : matching().length}
            aside={
              <FilterBar
                filters={filters()}
                onChange={(value) => {
                  setFilters(value);
                }}
              />
            }
          />
          <JobList jobs={shown()} card={card} />
          <Show when={hidden() > 0}>
            <p class={styles.more}>
              {plural(hidden(), "more job")} not shown. Search or filter to find them.
            </p>
          </Show>
        </section>
        <p class="visually-hidden" role="status">
          {feed.announcement}
        </p>
        <p class="visually-hidden" role="status">
          {plural(matching().length, "job")}
        </p>
      </Loading>
    </>
  );
}

function HistorySkeleton() {
  return (
    <section>
      <SectionHeading title={TITLE} />
      <JobListSkeleton />
    </section>
  );
}

interface FilterBarProps {
  readonly filters: JobFilters;
  readonly onChange: (filters: JobFilters) => void;
}

function FilterBar(props: FilterBarProps) {
  const change = (patch: Partial<JobFilters>) => {
    props.onChange({ ...props.filters, ...patch });
  };
  return (
    <>
      <SearchField
        label="Search"
        value={props.filters.query}
        onInput={(query) => {
          change({ query });
        }}
      />
      <MultiSelect
        name="States"
        choices={STATE_FILTERS}
        labels={STATE_FILTER_LABELS}
        value={props.filters.states}
        onChange={(states) => {
          change({ states });
        }}
      />
      <MultiSelect
        name="Modes"
        choices={JOB_MODES}
        labels={MODE_LABELS}
        value={props.filters.modes}
        onChange={(modes) => {
          change({ modes });
        }}
      />
    </>
  );
}
