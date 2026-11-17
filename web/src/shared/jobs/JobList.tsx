import type { JSX } from "@solidjs/web";
import { children, For, Repeat } from "solid-js";

import type { Job } from "@/shared/api/schemas.ts";
import { createListMotion } from "@/shared/listMotion.ts";
import { Skeleton } from "@/shared/ui/Skeleton.tsx";
import { JobCardSkeleton } from "./JobCard.tsx";
import styles from "./JobList.module.css";

const SKELETON_CARDS = 3;
const EXIT_SIZE = "--exit-size";
const MOTION = "data-motion";

interface JobListProps {
  readonly jobs: readonly Job[];
  readonly card: (job: Job) => JSX.Element;
}

const settled = (card: HTMLElement) =>
  Promise.allSettled(card.getAnimations().map((animation) => animation.finished));

// An exit that starts first keeps its own motion
function enter(card: HTMLElement) {
  card.setAttribute(MOTION, "enter");
  void settled(card).then(() => {
    if (card.getAttribute(MOTION) === "enter") {
      card.removeAttribute(MOTION);
    }
  });
}

// Exit animation collapses the card from its measured height
async function exit(card: HTMLElement) {
  card.style.setProperty(EXIT_SIZE, `${card.offsetHeight}px`);
  card.inert = true;
  card.setAttribute(MOTION, "exit");
  await settled(card);
}

export function JobList(props: JobListProps) {
  const resolved = children(() => <For each={props.jobs}>{props.card}</For>);
  const cards = createListMotion(
    () => resolved.toArray().filter((card) => card instanceof HTMLElement),
    { enter, exit },
  );
  return <div class={styles.cards}>{cards()}</div>;
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
