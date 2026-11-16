import { action, createOptimistic, Show } from "solid-js";
import { match } from "ts-pattern";
import DownloadIcon from "~icons/solar/download-minimalistic-linear";
import OpenIcon from "~icons/solar/square-arrow-right-up-linear";
import RestartIcon from "~icons/solar/restart-linear";
import TrashIcon from "~icons/solar/trash-bin-minimalistic-linear";

import { jobFileUrl, removeJob, retryJob, startJob } from "@/shared/api/client.ts";
import type { ApiResult } from "@/shared/api/client.ts";
import { failureOf } from "@/shared/api/failures.ts";
import { DEFAULT_QUALITY } from "@/shared/api/schemas.ts";
import type { Job } from "@/shared/api/schemas.ts";
import { nameOf } from "@/shared/jobs/jobNames.ts";
import { IconButton, IconLink } from "@/shared/ui/IconControl.tsx";
import styles from "./JobCard.module.css";
import { canDownload, canRetry, removalDeletesFiles, siteOf } from "./jobView.ts";
import { askToRemove, RemoveDialog } from "./RemoveDialog.tsx";

/** A failed action, shown on its card until the job leaves the state it failed in. */
export interface ActionFailure {
  readonly state: Job["state"];
  readonly text: string;
}

interface JobActionsProps {
  readonly job: Job;
  readonly onFailure: (failure: ActionFailure | null) => void;
}

type Send = (job: Job) => Promise<ApiResult<unknown>>;

// An expired job has no files left, so it starts a new job on the same link
const grabAgain: Send = (job) =>
  startJob({ link: job.url, mode: job.mode, quality: job.quality ?? DEFAULT_QUALITY, force: true });

const retry: Send = (job) => retryJob(job.id);
const remove: Send = (job) => removeJob(job.id);

const restartOf = (job: Job) =>
  match(job)
    .when(canRetry, () => ({ label: "Retry", send: retry }))
    .with({ state: "expired" }, () => ({ label: "Grab again", send: grabAgain }))
    .otherwise(() => null);

// Sends one request at a time, so a double click starts no second job
function createJobAction(props: JobActionsProps) {
  const [sending, setSending] = createOptimistic(false);
  const run = action(async function* (send: Send) {
    setSending(true);
    const { state } = props.job;
    const text = failureOf(await send(props.job));
    yield;
    props.onFailure(text === null ? null : { state, text });
  });
  return { sending, run: (send: Send) => void run(send) };
}

export function JobActions(props: JobActionsProps) {
  const { sending, run } = createJobAction(props);
  const name = () => nameOf(props.job);

  return (
    <div class={styles.actions}>
      <IconLink label={`Open on ${siteOf(props.job)}`} subject={name()} href={props.job.url} newTab>
        <OpenIcon />
      </IconLink>
      <Show when={restartOf(props.job)}>
        {(restart) => (
          <IconButton
            label={restart().label}
            subject={name()}
            main
            disabled={sending()}
            onClick={() => {
              run(restart().send);
            }}
          >
            <RestartIcon />
          </IconButton>
        )}
      </Show>
      <Show when={canDownload(props.job)}>
        <IconLink label="Download" subject={name()} main href={jobFileUrl(props.job.id)} download>
          <DownloadIcon />
        </IconLink>
      </Show>
      <RemoveButton
        job={props.job}
        disabled={sending()}
        onRemove={() => {
          run(remove);
        }}
      />
    </div>
  );
}

interface RemoveButtonProps {
  readonly job: Job;
  readonly disabled: boolean;
  readonly onRemove: () => void;
}

function RemoveButton(props: RemoveButtonProps) {
  let dialog: HTMLDialogElement | undefined;
  const askOrRemove = () => {
    if (dialog !== undefined && removalDeletesFiles(props.job)) {
      askToRemove(dialog);
    } else {
      props.onRemove();
    }
  };
  return (
    <>
      <IconButton
        label="Remove"
        subject={nameOf(props.job)}
        disabled={props.disabled}
        onClick={askOrRemove}
      >
        <TrashIcon />
      </IconButton>
      <RemoveDialog
        job={props.job}
        ref={(element) => {
          dialog = element;
        }}
        onConfirm={props.onRemove}
      />
    </>
  );
}
