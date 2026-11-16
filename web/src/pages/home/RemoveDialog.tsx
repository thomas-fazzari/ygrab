import { createUniqueId } from "solid-js";

import type { Job } from "@/shared/api/schemas.ts";
import { Button } from "@/shared/ui/Button.tsx";
import styles from "./RemoveDialog.module.css";
import { nameOf, removalDetail } from "./jobView.ts";

const REMOVE = "remove";

interface RemoveDialogProps {
  readonly job: Job;
  readonly ref: (dialog: HTMLDialogElement) => void;
  readonly onConfirm: () => void;
}

/** Asks before removing a job that holds files. Open it with `askToRemove`. */
export function RemoveDialog(props: RemoveDialogProps) {
  const titleId = createUniqueId();
  const detailId = createUniqueId();
  const close = (event: Event & { currentTarget: HTMLDialogElement }) => {
    if (event.currentTarget.returnValue === REMOVE) {
      props.onConfirm();
    }
  };
  return (
    <dialog
      ref={props.ref}
      class={styles.dialog}
      aria-labelledby={titleId}
      aria-describedby={detailId}
      onClose={close}
    >
      <h3 id={titleId} class={styles.title}>
        Remove {nameOf(props.job)}?
      </h3>
      <p id={detailId} class={styles.detail}>
        {removalDetail(props.job)}
      </p>
      <form class={styles.buttons} method="dialog">
        <Button type="submit" value="cancel" autofocus>
          Cancel
        </Button>
        <Button type="submit" value={REMOVE} danger>
          Remove
        </Button>
      </form>
    </dialog>
  );
}

// Escape closes the dialog without a button, which keeps the value of the last close
export function askToRemove(dialog: HTMLDialogElement) {
  dialog.returnValue = "";
  dialog.showModal();
}
