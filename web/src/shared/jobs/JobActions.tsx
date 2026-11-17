import { action, createOptimistic, Show } from "solid-js";
import { match } from "ts-pattern";
import DownloadIcon from "~icons/solar/download-minimalistic-linear";
import OpenIcon from "~icons/solar/square-arrow-right-up-linear";
import RestartIcon from "~icons/solar/restart-linear";
import TrashIcon from "~icons/solar/trash-bin-minimalistic-linear";

import { jobFileUrl, removeJob, retryJob, startJob } from "@/shared/api/client.ts";
import type { ApiResult } from "@/shared/api/client.ts";
import { failureOf, previousIdOf } from "@/shared/api/failures.ts";
import { DEFAULT_QUALITY } from "@/shared/api/schemas.ts";
import type { Job } from "@/shared/api/schemas.ts";
import { nameOf } from "./jobNames.ts";
import { Button } from "@/shared/ui/Button.tsx";
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

type FocusTarget = (card: HTMLElement) => Element | null;

type Run = (send: Send, focusTarget: FocusTarget) => void;

const downloadAgain: Send = (job) =>
  startJob({ link: job.url, mode: job.mode, quality: job.quality ?? DEFAULT_QUALITY, force: true });

const retry: Send = (job) => retryJob(job.id);
const remove: Send = (job) => removeJob(job.id);

// A video job that already holds the sound replaces the failed job as well
const fallBackToVideo: Send = async (job) => {
  const started = await startJob({ link: job.url, mode: "video", quality: "audio", force: false });
  return started.kind === "ok" || previousIdOf(started) !== null ? removeJob(job.id) : started;
};

const restartOf = (job: Job) =>
  match(job)
    .when(canRetry, () => ({ label: "Retry", send: retry }))
    .with({ state: "expired" }, () => ({ label: "Download again", send: downloadAgain }))
    .with({ state: "skipped" }, () => ({ label: "Download anyway", send: downloadAgain }))
    .otherwise(() => null);

// A restart hides its button, so the card itself takes the focus
const theCard: FocusTarget = (card) => card;

// A removal drops the card, so a card next to it or the page title takes the focus
const aNeighbour: FocusTarget = (card) =>
  card.nextElementSibling ?? card.previousElementSibling ?? document.querySelector("h1");

// The target is picked before sending: the disabled button drops the focus and the card may leave
const focusTargetOf = (card: HTMLElement | null, focusTarget: FocusTarget) =>
  card?.contains(document.activeElement) === true ? focusTarget(card) : null;

const focusLost = (card: HTMLElement | null) =>
  document.activeElement === document.body || card?.contains(document.activeElement) === true;

// Sends one request at a time, so a double click starts no second job
function createJobAction(props: JobActionsProps, cardOf: () => HTMLElement | null) {
  const [sending, setSending] = createOptimistic(false);
  const run = action(async function* (send: Send, focusTarget: FocusTarget) {
    const card = cardOf();
    const target = focusTargetOf(card, focusTarget);
    setSending(true);
    const { state } = props.job;
    const text = failureOf(await send(props.job));
    yield;
    props.onFailure(text === null ? null : { state, text });
    if (text === null && target instanceof HTMLElement && focusLost(card)) {
      target.focus();
    }
  });
  return { sending, run: (send: Send, focusTarget: FocusTarget) => void run(send, focusTarget) };
}

export function JobActions(props: JobActionsProps) {
  let actions: HTMLDivElement | undefined;
  const { sending, run } = createJobAction(props, () => actions?.closest("article") ?? null);
  const name = () => nameOf(props.job);

  return (
    <div
      ref={(element) => {
        actions = element;
      }}
      class={styles.actions}
    >
      <FallbackButton job={props.job} disabled={sending()} run={run} />
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
              run(restart().send, theCard);
            }}
          >
            <RestartIcon />
          </IconButton>
        )}
      </Show>
      <Show when={canDownload(props.job)}>
        <IconLink label="Save" subject={name()} main href={jobFileUrl(props.job.id)} download>
          <DownloadIcon />
        </IconLink>
      </Show>
      <RemoveButton
        job={props.job}
        disabled={sending()}
        onRemove={() => {
          run(remove, aNeighbour);
        }}
      />
    </div>
  );
}

interface FallbackButtonProps {
  readonly job: Job;
  readonly disabled: boolean;
  readonly run: Run;
}

function FallbackButton(props: FallbackButtonProps) {
  return (
    <Show when={props.job.canFallBackToVideo}>
      <Button
        quiet
        disabled={props.disabled}
        onClick={() => {
          props.run(fallBackToVideo, aNeighbour);
        }}
      >
        <DownloadIcon />
        Download plain audio
      </Button>
    </Show>
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
