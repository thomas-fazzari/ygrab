import { createProjection, For, Loading } from "solid-js";
import { match } from "ts-pattern";

import { apiEvents, EventNames } from "@/shared/api/events.ts";
import type { ApiEvent } from "@/shared/api/events.ts";
import type { Job } from "@/shared/api/schemas.ts";
import { scan } from "@/shared/scan.ts";

function applyEvent(jobs: Job[], event: ApiEvent): Job[] {
  return match(event)
    .with({ event: EventNames.snapshot }, ({ data }) => data.jobs)
    .with({ event: EventNames.job }, ({ data: changed }) =>
      jobs.some((job) => job.id === changed.id)
        ? jobs.map((job) => (job.id === changed.id ? changed : job))
        : [changed, ...jobs],
    )
    .with({ event: EventNames.jobRemoved }, ({ data: removed }) =>
      jobs.filter((job) => job.id !== removed.id),
    )
    .with({ event: EventNames.cooldown }, () => jobs)
    .exhaustive();
}

const watchJobs = () => scan<ApiEvent, Job[]>(apiEvents(), [], applyEvent);

export function App() {
  const jobs = createProjection<Job[]>(watchJobs, [], { key: "id" });

  return (
    <main>
      <h1>YGrab</h1>
      <Loading fallback={<p>Loading jobs</p>}>
        <ul>
          <For each={jobs}>
            {(job) => (
              <li>
                {job.title ?? job.url} {job.state} {job.percent}%
              </li>
            )}
          </For>
        </ul>
      </Loading>
    </main>
  );
}
