import { makePersisted } from "@solid-primitives/storage";
import { action, createOptimistic, createSignal, onSettled, Show } from "solid-js";
import ArrowIcon from "~icons/solar/arrow-right-linear";
import ClipboardIcon from "~icons/solar/clipboard-linear";
import LinkIcon from "~icons/solar/link-minimalistic-2-linear";
import LoaderIcon from "~icons/solar/loader-linear";

import { startJob } from "@/shared/api/client.ts";
import { failureOf, isRefusedInput, previousIdOf } from "@/shared/api/failures.ts";
import {
  DEFAULT_QUALITY,
  JOB_MODES,
  JobModeSchema,
  VIDEO_QUALITIES,
  VideoQualitySchema,
} from "@/shared/api/schemas.ts";
import type { JobId, JobMode, StartJobRequest, VideoQuality } from "@/shared/api/schemas.ts";
import { isYouTubeMusicLink } from "@/shared/jobs/jobNames.ts";
import { MODE_LABELS, QUALITY_LABELS } from "@/shared/jobs/jobView.ts";
import { notifyError } from "@/shared/notifications.ts";
import { storedAs } from "@/shared/storage.ts";
import { Button } from "@/shared/ui/Button.tsx";
import { ChoiceChips, ChoiceSelect } from "@/shared/ui/Choices.tsx";
import { Field } from "@/shared/ui/Field.tsx";
import { IconButton } from "@/shared/ui/IconControl.tsx";
import { IconSwap } from "@/shared/ui/IconSwap.tsx";
import styles from "./GrabForm.module.css";

// A paste in one of these stays there
const PASTE_TARGETS =
  "input:not([type=radio], [type=checkbox], [type=button], [type=submit]), textarea, [contenteditable]:not([contenteditable=false]), dialog";

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
    const text = previousId === null ? failureOf(result) : null;
    setFailure(text);
    if (text !== null && !isRefusedInput(result)) {
      notifyError(`Cannot download ${request.link}`, text);
    }
    if (result.kind === "ok") {
      clear();
    }
  });

  return { failure, sending, grab };
}

function createLink() {
  const [link, setLink] = createSignal("");
  // Only a link that becomes a music link switches the mode, so a later choice of Video holds
  const changeLink = (text: string) => {
    const becomesMusic = !isYouTubeMusicLink(link().trim()) && isYouTubeMusicLink(text.trim());
    setLink(text);
    if (becomesMusic) {
      setChosenMode("audio");
    }
  };
  let input: HTMLInputElement | undefined;
  onPasteOutsideFields((text) => {
    changeLink(text);
    input?.focus();
  });
  const bind = (element: HTMLInputElement) => {
    input = element;
  };
  return { link, changeLink, bind };
}

export function GrabForm(props: GrabFormProps) {
  const { link, changeLink, bind } = createLink();
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
      <LinkField
        ref={bind}
        link={link()}
        mode={chosenMode()}
        sending={sending()}
        onInput={changeLink}
      />
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

/** Calls back with the text of a paste outside any text field, such as Ctrl+V on the page. */
function onPasteOutsideFields(onPaste: (text: string) => void) {
  const paste = (event: ClipboardEvent) => {
    const text = event.clipboardData?.getData("text").trim() ?? "";
    const inField = event.target instanceof Element && event.target.closest(PASTE_TARGETS) !== null;
    if (text === "" || inField) {
      return;
    }
    event.preventDefault();
    onPaste(text);
  };
  onSettled(() => {
    document.addEventListener("paste", paste);
    return () => {
      document.removeEventListener("paste", paste);
    };
  });
}

interface LinkFieldProps {
  readonly ref: (element: HTMLInputElement) => void;
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
        ref={props.ref}
        aria-label="Link"
        placeholder={PLACEHOLDERS[props.mode]}
        autofocus
        value={props.link}
        onInput={(event) => {
          props.onInput(event.currentTarget.value);
        }}
      />
      <IconButton type="submit" label="Download" main disabled={props.sending}>
        <IconSwap
          swapped={props.sending}
          icon={<ArrowIcon />}
          swappedIcon={<LoaderIcon class={styles.spinner} />}
        />
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

const notifyPasteFailure = () => {
  notifyError("Cannot paste a link", "The browser refused to read the clipboard.");
};

// The clipboard API exists only in a secure context, such as HTTPS or localhost
function PasteButton(props: { readonly onPaste: (text: string) => void }) {
  const paste = () => {
    void navigator.clipboard.readText().then(props.onPaste, notifyPasteFailure);
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
