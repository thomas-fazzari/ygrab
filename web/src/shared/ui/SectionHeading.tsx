import type { JSX } from "@solidjs/web";
import { Show } from "solid-js";

import styles from "./SectionHeading.module.css";

interface SectionHeadingProps {
  readonly title: string;
  readonly count?: number | undefined;
  /** Shows at the end of the line, such as a status or a link. */
  readonly aside?: JSX.Element;
}

export function SectionHeading(props: SectionHeadingProps) {
  return (
    <div class={styles.heading}>
      <h2 class={styles.title}>{props.title}</h2>
      <Show when={props.count !== undefined}>
        <span class={styles.count}>{props.count}</span>
      </Show>
      <div class={styles.aside}>{props.aside}</div>
    </div>
  );
}
