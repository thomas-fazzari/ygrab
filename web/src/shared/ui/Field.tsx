import type { ParentProps } from "solid-js";

import styles from "./Field.module.css";

/**
 * Frames an input with its leading icon and trailing buttons, all passed as children.
 *
 * A main `IconButton` turns quiet while the input is empty. A small field has the height of a
 * button, for a toolbar.
 */
export function Field(props: ParentProps<{ readonly small?: boolean }>) {
  return (
    <div class={styles.field} data-small={props.small}>
      {props.children}
    </div>
  );
}
