import type { ParentProps } from "solid-js";

import styles from "./Skeleton.module.css";

export function Skeleton(props: ParentProps) {
  return (
    <div class={styles.skeleton} aria-hidden="true">
      {props.children}
    </div>
  );
}

export function SkeletonText(props: { readonly chars: number }) {
  return <span class={styles.text} style={{ "inline-size": `${props.chars}ch` }} />;
}
