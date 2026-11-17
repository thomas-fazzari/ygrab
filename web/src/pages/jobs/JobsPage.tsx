import { createMemo, createProjection, createSignal, Loading, Show } from "solid-js";

import { JOB_MODES } from "@/shared/api/schemas.ts";
import { createMinuteClock } from "@/shared/clock.ts";
import { createJobCards } from "@/shared/jobs/jobCards.tsx";
import { emptyFeed, watchJobs } from "@/shared/jobs/jobFeed.ts";
import type { JobFeed } from "@/shared/jobs/jobFeed.ts";
import { JobList } from "@/shared/jobs/JobList.tsx";
import { MODE_LABELS } from "@/shared/jobs/jobView.ts";
import { MultiSelect } from "@/shared/ui/MultiSelect.tsx";
import { PageTitle } from "@/shared/ui/PageTitle.tsx";
import { SearchField } from "@/shared/ui/SearchField.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import { filterJobs, STATE_FILTER_LABELS, STATE_FILTERS } from "./jobFilters.ts";
import type { JobFilters } from "./jobFilters.ts";
import styles from "./JobsPage.module.css";

export const JOBS_ROUTE = "/jobs";

const SHOWN_LIMIT = 100;

const NO_FILTERS: JobFilters = { query: "", states: STATE_FILTERS, modes: JOB_MODES };

export function JobsPage() {
  const feed = createProjection<JobFeed>(watchJobs, emptyFeed);
  const now = createMinuteClock();
  const { card } = createJobCards(feed, now);
  const [filters, setFilters] = createSignal(NO_FILTERS);
  const matching = createMemo(() => filterJobs(feed.jobs, filters()));
  const hidden = createMemo(() => Math.max(0, matching().length - SHOWN_LIMIT));

  return (
    <>
      <PageTitle>Jobs</PageTitle>
      <Loading>
        <section>
          <SectionHeading
            title="History"
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
          <JobList jobs={matching().slice(0, SHOWN_LIMIT)} card={card} />
          <Show when={hidden() > 0}>
            <p class={styles.more}>{hidden()} more. Search or filter to narrow the list.</p>
          </Show>
        </section>
        <p class="visually-hidden" role="status">
          {feed.announcement}
        </p>
      </Loading>
    </>
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
