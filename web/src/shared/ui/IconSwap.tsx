import type { JSX } from "@solidjs/web";

import styles from "./IconSwap.module.css";

interface IconSwapProps {
  readonly swapped: boolean;
  readonly icon: JSX.Element;
  readonly swappedIcon: JSX.Element;
}

/** Shows `icon`, or `swappedIcon` while `swapped`, and morphs from one to the other. */
export function IconSwap(props: IconSwapProps) {
  return (
    <span class={styles.swap} data-swapped={props.swapped}>
      <span>{props.icon}</span>
      <span>{props.swappedIcon}</span>
    </span>
  );
}
