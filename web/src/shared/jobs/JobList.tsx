import type { JSX } from "@solidjs/web";
import { For, Repeat } from "solid-js";

import type { Job } from "@/shared/api/schemas.ts";
import { Skeleton } from "@/shared/ui/Skeleton.tsx";
import { JobCardSkeleton } from "./JobCard.tsx";
import styles from "./JobList.module.css";

const SKELETON_CARDS = 3;

interface JobListProps {
  readonly jobs: readonly Job[];
  readonly card: (job: Job) => JSX.Element;
}

export function JobList(props: JobListProps) {
  return (
    <div class={styles.cards}>
      <For each={props.jobs}>{props.card}</For>
    </div>
  );
}

export function JobListSkeleton() {
  return (
    <Skeleton>
      <div class={styles.cards}>
        <Repeat count={SKELETON_CARDS}>{() => <JobCardSkeleton />}</Repeat>
      </div>
    </Skeleton>
  );
}
