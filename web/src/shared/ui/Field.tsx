import type { ParentProps } from "solid-js";
import ArrowIcon from "~icons/solar/arrow-right-linear";

import styles from "./Field.module.css";

/** Frames an input with its leading icon and trailing buttons, all passed as children. */
export function Field(props: ParentProps) {
  return <div class={styles.field}>{props.children}</div>;
}

/** Submits the form of a `Field`, quiet while the input is empty. */
export function FieldSubmit(props: { readonly label: string; readonly disabled: boolean }) {
  return (
    <button class={styles.submit} type="submit" title={props.label} disabled={props.disabled}>
      <ArrowIcon />
      <span class="visually-hidden">{props.label}</span>
    </button>
  );
}
