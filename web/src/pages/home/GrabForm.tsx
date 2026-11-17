import { makePersisted } from "@solid-primitives/storage";
import { action, createOptimistic, createSignal, Show } from "solid-js";
import { match, P } from "ts-pattern";
import ArrowIcon from "~icons/solar/arrow-right-linear";
import ClipboardIcon from "~icons/solar/clipboard-linear";
import LinkIcon from "~icons/solar/link-minimalistic-2-linear";

import { startJob } from "@/shared/api/client.ts";
import type { ApiResult } from "@/shared/api/client.ts";
import { failureOf } from "@/shared/api/failures.ts";
import {
  DEFAULT_QUALITY,
  JOB_MODES,
  JobModeSchema,
  VIDEO_QUALITIES,
  VideoQualitySchema,
} from "@/shared/api/schemas.ts";
import type {
  JobId,
  JobMode,
  StartedJob,
  StartJobRequest,
  VideoQuality,
} from "@/shared/api/schemas.ts";
import { isYouTubeMusicLink } from "@/shared/jobs/jobNames.ts";
import { MODE_LABELS, QUALITY_LABELS } from "@/shared/jobs/jobView.ts";
import { storedAs } from "@/shared/storage.ts";
import { Button } from "@/shared/ui/Button.tsx";
import { ChoiceChips, ChoiceSelect } from "@/shared/ui/Choices.tsx";
import { Field } from "@/shared/ui/Field.tsx";
import { IconButton } from "@/shared/ui/IconControl.tsx";
import styles from "./GrabForm.module.css";

const HTTP_CONFLICT = 409;

const [chosenMode, setChosenMode] = makePersisted(
  createSignal<JobMode>("audio"),
  storedAs("home.mode", JobModeSchema),
);
const [chosenQuality, setChosenQuality] = makePersisted(
  createSignal<VideoQuality>(DEFAULT_QUALITY),
  storedAs("home.quality", VideoQualitySchema),
);

const PLACEHOLDERS: Readonly<Record<JobMode, string>> = {
  audio: "paste a YouTube Music link",
  video: "paste a YouTube link",
};

export interface Existing {
  readonly id: JobId;
  /** Starts a new job on the link anyway. */
  readonly downloadAnyway: () => void;
}

interface GrabFormProps {
  /** Shows the job that already holds the link, or clears it with null. */
  readonly onExisting: (existing: Existing | null) => void;
}

type GrabRequest = Omit<StartJobRequest, "force">;

const previousIdOf = (result: ApiResult<StartedJob>) =>
  match(result)
    .with(
      { kind: "problem", status: HTTP_CONFLICT, problem: { previousId: P.string } },
      ({ problem }) => problem.previousId,
    )
    .otherwise(() => null);

function createGrab(props: GrabFormProps, clear: () => void) {
  const [failure, setFailure] = createSignal<string | null>(null);
  const [sending, setSending] = createOptimistic(false);

  const grab = action(async function* (request: GrabRequest, force: boolean) {
    setSending(true);
    const result = await startJob({ ...request, force });
    yield;
    const previousId = previousIdOf(result);
    const downloadAnyway = () => {
      if (!sending()) {
        void grab(request, true);
      }
    };
    props.onExisting(previousId === null ? null : { id: previousId, downloadAnyway });
    setFailure(previousId === null ? failureOf(result) : null);
    if (result.kind === "ok") {
      clear();
    }
  });

  return { failure, sending, grab };
}

export function GrabForm(props: GrabFormProps) {
  const [link, setLink] = createSignal("");
  // Only a link that becomes a music link switches the mode, so a later choice of Video holds
  const changeLink = (text: string) => {
    const becomesMusic = !isYouTubeMusicLink(link().trim()) && isYouTubeMusicLink(text.trim());
    setLink(text);
    if (becomesMusic) {
      setChosenMode("audio");
    }
  };
  const { failure, sending, grab } = createGrab(props, () => {
    changeLink("");
  });

  const submit = (event: SubmitEvent) => {
    event.preventDefault();
    const trimmed = link().trim();
    if (trimmed !== "" && !sending()) {
      void grab({ link: trimmed, mode: chosenMode(), quality: chosenQuality() }, false);
    }
  };

  return (
    <form class={styles.form} novalidate onSubmit={submit}>
      <LinkField link={link()} mode={chosenMode()} sending={sending()} onInput={changeLink} />
      <div class={styles.options}>
        <ChoiceChips
          name="Mode"
          choices={JOB_MODES}
          labels={MODE_LABELS}
          value={chosenMode()}
          onChange={(value) => {
            setChosenMode(value);
          }}
        />
        <Show when={chosenMode() === "video"}>
          <QualityChoice
            value={chosenQuality()}
            onChange={(value) => {
              setChosenQuality(value);
            }}
          />
        </Show>
        <PasteButton onPaste={changeLink} />
      </div>
      <Failure message={failure()} />
    </form>
  );
}

interface LinkFieldProps {
  readonly link: string;
  readonly mode: JobMode;
  readonly sending: boolean;
  readonly onInput: (link: string) => void;
}

function LinkField(props: LinkFieldProps) {
  return (
    <Field>
      <LinkIcon />
      <input
        type="text"
        inputmode="url"
        autocomplete="off"
        aria-label="Link"
        placeholder={PLACEHOLDERS[props.mode]}
        autofocus
        value={props.link}
        onInput={(event) => {
          props.onInput(event.currentTarget.value);
        }}
      />
      <IconButton type="submit" label="Download" main disabled={props.sending}>
        <ArrowIcon />
      </IconButton>
    </Field>
  );
}

function Failure(props: { readonly message: string | null }) {
  return (
    <Show when={props.message}>
      {(message) => (
        <p class={styles.failure} role="alert">
          {message()}
        </p>
      )}
    </Show>
  );
}

interface QualityChoiceProps {
  readonly value: VideoQuality;
  readonly onChange: (value: VideoQuality) => void;
}

// Chips on wide screens and a select on narrow ones, both bound to the same value
function QualityChoice(props: QualityChoiceProps) {
  const choice = { name: "Quality", choices: VIDEO_QUALITIES, labels: QUALITY_LABELS } as const;
  return (
    <>
      <ChoiceChips {...choice} class={styles.wide} value={props.value} onChange={props.onChange} />
      <ChoiceSelect
        {...choice}
        class={styles.narrow}
        value={props.value}
        onChange={props.onChange}
      />
    </>
  );
}

const ignore = () => null;

// The clipboard API exists only in a secure context, such as HTTPS or localhost
function PasteButton(props: { readonly onPaste: (text: string) => void }) {
  const paste = () => {
    void navigator.clipboard.readText().then(props.onPaste, ignore);
  };
  return (
    <Show when={window.isSecureContext}>
      <span class={styles.paste}>
        <Button onClick={paste}>
          <ClipboardIcon />
          Paste
        </Button>
      </span>
    </Show>
  );
}
