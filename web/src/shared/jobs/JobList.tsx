import type { JSX } from "@solidjs/web";
import { For } from "solid-js";

import type { Job } from "@/shared/api/schemas.ts";
import styles from "./JobList.module.css";

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
