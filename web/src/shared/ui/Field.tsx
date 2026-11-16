import type { ParentProps } from "solid-js";

import styles from "./Field.module.css";

/**
 * Frames an input with its leading icon and trailing buttons, all passed as children.
 *
 * A main `IconButton` turns quiet while the input is empty.
 */
export function Field(props: ParentProps) {
  return <div class={styles.field}>{props.children}</div>;
}
