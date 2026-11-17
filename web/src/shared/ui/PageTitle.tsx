import type { ParentProps } from "solid-js";

import styles from "./PageTitle.module.css";

export function PageTitle(props: ParentProps) {
  return (
    <h1 class={styles.title} tabindex="-1">
      {props.children}
    </h1>
  );
}
