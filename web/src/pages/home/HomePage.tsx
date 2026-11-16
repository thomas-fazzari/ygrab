import { createProjection, For, Loading, Show } from "solid-js";

import { apiEvents } from "@/shared/api/events.ts";
import type { ApiEvent } from "@/shared/api/events.ts";
import { scan } from "@/shared/iterators.ts";
import { Logo } from "@/shared/ui/Logo.tsx";
import { applyEvent, emptyFeed } from "./jobFeed.ts";
import type { JobFeed } from "./jobFeed.ts";

const watchFeed = () => scan<ApiEvent, JobFeed>(apiEvents(), emptyFeed, applyEvent);

export function HomePage() {
  const feed = createProjection<JobFeed>(watchFeed, emptyFeed);

  return (
    <>
      <Logo />
      <Loading>
        <Show when={feed.offline}>
          <p>offline, reconnecting</p>
        </Show>
        <ul>
          <For each={feed.jobs}>
            {(job) => (
              <li>
                {job.title ?? job.url} {job.state} {job.percent}%
              </li>
            )}
          </For>
        </ul>
      </Loading>
    </>
  );
}
