import type { JSX } from "@solidjs/web";
import type { ParentProps } from "solid-js";

import styles from "./Button.module.css";

type ButtonProps = ParentProps<{
  readonly type?: "button" | "submit";
  /** The value a `<form method="dialog">` returns when this button closes its dialog. */
  readonly value?: string;
  readonly danger?: boolean;
  readonly autofocus?: boolean;
  readonly onClick?: JSX.EventHandler<HTMLButtonElement, MouseEvent>;
}>;

export function Button(props: ButtonProps) {
  return (
    <button
      class={styles.button}
      data-danger={props.danger}
      type={props.type ?? "button"}
      value={props.value}
      autofocus={props.autofocus}
      onClick={props.onClick}
    >
      {props.children}
    </button>
  );
}
