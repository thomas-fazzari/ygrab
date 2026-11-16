import type { JSX } from "@solidjs/web";
import { Show } from "solid-js";
import type { ParentProps } from "solid-js";

import styles from "./SectionHeading.module.css";

type SectionHeadingProps = ParentProps<{
  readonly title: string;
  /** Shows beside the title in a quiet style, such as a count or a date. */
  readonly detail?: number | string | undefined;
  /** Shows at the end of the line, such as a status or a link. */
  readonly aside?: JSX.Element;
}>;

/** Renders a section title, then its detail, its children and its aside. */
export function SectionHeading(props: SectionHeadingProps) {
  return (
    <div class={styles.heading}>
      <h2 class={styles.title}>{props.title}</h2>
      <Show when={props.detail !== undefined}>
        <span class={styles.detail}>{props.detail}</span>
      </Show>
      {props.children}
      <div class={styles.aside}>{props.aside}</div>
    </div>
  );
}
