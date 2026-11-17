import type { ParentProps } from "solid-js";

import styles from "./Badge.module.css";

export type BadgeTone = "quiet" | "live" | "done" | "error";

type BadgeProps = ParentProps<{
  readonly tone: BadgeTone;
  readonly class?: string | undefined;
}>;

export function Badge(props: BadgeProps) {
  return (
    <span class={[styles.badge, props.class]} data-tone={props.tone}>
      {props.children}
    </span>
  );
}
