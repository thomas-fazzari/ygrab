import { createMemo, createSignal, createUniqueId, For, Show, untrack } from "solid-js";
import { match, P } from "ts-pattern";
import ChevronIcon from "~icons/solar/alt-arrow-right-linear";

import type { Job, Track } from "@/shared/api/schemas.ts";
import { albumOf, displayLink, isTrackJob, nameOf, titleOf } from "./jobNames.ts";
import { STATE_LABELS } from "./jobStates.ts";
import { Badge } from "@/shared/ui/Badge.tsx";
import { SkeletonText } from "@/shared/ui/Skeleton.tsx";
import { JobActions } from "./JobActions.tsx";
import type { ActionFailure } from "./JobActions.tsx";
import styles from "./JobCard.module.css";
import {
  badgeTone,
  cardIdOf,
  coverOf,
  doneTracks,
  metaOf,
  plural,
  stateLineOf,
} from "./jobView.ts";
import type { CardContext, Original, StateLine } from "./jobView.ts";

interface JobCardProps {
  readonly job: Job;
  readonly context: CardContext;
}

const NO_TRACK_LIST: ReadonlySet<Job["state"]> = new Set(["expired", "skipped"]);

export function JobCard(props: JobCardProps) {
  const titleId = createUniqueId();
  const tracksId = createUniqueId();
  // A ready job only lists done tracks, so its list starts folded
  const [tracksOpen, setTracksOpen] = createSignal(untrack(() => props.job.state !== "ready"));
  const [failure, setFailure] = createSignal<ActionFailure | null>(null);
  const hasTrackList = () =>
    !isTrackJob(props.job) && props.job.tracks.length > 0 && !NO_TRACK_LIST.has(props.job.state);
  const line = createMemo((): StateLine => {
    const current = failure();
    return current?.state === props.job.state
      ? { kind: "error", text: current.text }
      : stateLineOf(props.job, props.context);
  });

  return (
    <article
      id={cardIdOf(props.job)}
      class={styles.card}
      data-state={props.job.state}
      aria-labelledby={titleId}
      tabindex="-1"
    >
      <Cover url={coverOf(props.job)} />
      <Title id={titleId} job={props.job} />
      <Badge class={styles.badge} tone={badgeTone(props.job.state)}>
        {STATE_LABELS[props.job.state]}
      </Badge>
      <Meta
        job={props.job}
        toggle={hasTrackList()}
        controls={tracksId}
        open={tracksOpen()}
        onToggle={() => {
          setTracksOpen((open) => !open);
        }}
      />
      <StateLineView line={line()} />
      <JobActions
        job={props.job}
        onFailure={(next) => {
          setFailure(next);
        }}
      />
      <Show when={hasTrackList()}>
        <TrackList id={tracksId} job={props.job} open={tracksOpen()} />
      </Show>
    </article>
  );
}

export function JobCardSkeleton() {
  return (
    <div class={styles.card}>
      <span class={styles.cover} />
      <span class={styles.title}>
        <SkeletonText chars={24} />
      </span>
      <span class={styles.meta}>
        <SkeletonText chars={18} />
      </span>
      <span class={styles.state}>
        <SkeletonText chars={14} />
      </span>
    </div>
  );
}

interface MetaProps {
  readonly job: Job;
  readonly toggle: boolean;
  readonly controls: string;
  readonly open: boolean;
  readonly onToggle: () => void;
}

function Meta(props: MetaProps) {
  return (
    <div class={styles.meta}>
      <For each={metaOf(props.job)}>{(part) => <span>{part}</span>}</For>
      <Show when={albumOf(props.job)}>
        {(album) => <span class={styles.album}>from {album()}</span>}
      </Show>
      <Show when={props.toggle}>
        <TracksToggle
          job={props.job}
          controls={props.controls}
          open={props.open}
          onToggle={props.onToggle}
        />
      </Show>
    </div>
  );
}

function Title(props: { readonly id: string; readonly job: Job }) {
  const title = createMemo(() => titleOf(props.job));
  return (
    <h3 id={props.id} class={styles.title} data-raw={title() === null}>
      {title() ?? displayLink(props.job.url)}
    </h3>
  );
}

function Cover(props: { readonly url: string | null }) {
  return (
    <Show when={props.url} fallback={<span class={styles.cover} />}>
      {(url) => (
        <img class={styles.cover} src={url()} alt="" loading="lazy" referrerpolicy="no-referrer" />
      )}
    </Show>
  );
}

interface TracksToggleProps {
  readonly job: Job;
  readonly controls: string;
  readonly open: boolean;
  readonly onToggle: () => void;
}

function TracksToggle(props: TracksToggleProps) {
  const label = () => {
    const total = props.job.tracks.length;
    return props.job.state === "incomplete"
      ? `${doneTracks(props.job)}/${plural(total, "track")}`
      : plural(total, "track");
  };
  return (
    <button
      class={styles.toggle}
      type="button"
      aria-expanded={props.open ? "true" : "false"}
      aria-controls={props.controls}
      onClick={() => {
        props.onToggle();
      }}
    >
      {label()}
      <ChevronIcon />
    </button>
  );
}

const progressOf = (line: StateLine) => (line.kind === "progress" ? line : null);

const contentOf = (line: StateLine) =>
  match(line)
    .with({ kind: "none" }, { kind: "progress" }, () => "")
    .with({ kind: "text" }, { kind: "error" }, ({ text }) => text)
    .with({ kind: "duplicate" }, ({ original }) => <DuplicateText original={original} />)
    .exhaustive();

function DuplicateText(props: { readonly original: Original | null }) {
  return (
    <Show when={props.original} fallback="already downloaded">
      {(original) => (
        <>
          already in{" "}
          <Show when={original().anchor} fallback={original().name}>
            {(anchor) => <a href={`#${anchor()}`}>{original().name}</a>}
          </Show>
        </>
      )}
    </Show>
  );
}

function StateLineView(props: { readonly line: StateLine }) {
  return (
    <div class={styles.state} data-kind={props.line.kind}>
      <Show when={progressOf(props.line)} fallback={contentOf(props.line)}>
        {(progress) => (
          <>
            <div class={styles.bar}>
              <div style={{ "inline-size": `${progress().percent}%` }} />
            </div>
            <span>{progress().label}</span>
          </>
        )}
      </Show>
    </div>
  );
}

const isCurrent = (job: Job, track: Track) =>
  job.state === "downloading" && track.number === job.currentTrack;

const trackView = (job: Job, track: Track) =>
  match(track)
    .when(
      (pending) => isCurrent(job, pending),
      () => ({ kind: "current", label: "downloading" }),
    )
    .with({ state: P.union("pending", "done") }, ({ state }) => ({ kind: state, label: state }))
    .with({ state: P.union("missing", "refused") }, ({ state, reason }) => ({
      kind: "missing",
      label: reason ?? state,
    }))
    .exhaustive();

const number = (track: Track) => String(track.number).padStart(2, "0");

interface TrackListProps {
  readonly id: string;
  readonly job: Job;
  readonly open: boolean;
}

// Stays mounted while folded, so its height can animate
function TrackList(props: TrackListProps) {
  return (
    <div class={styles.fold} data-open={props.open} inert={!props.open}>
      <div>
        {/* Scrolls past ten tracks, so it takes focus for the keyboard */}
        <ol
          id={props.id}
          class={styles.tracks}
          aria-label={`Tracks of ${nameOf(props.job)}`}
          tabindex="0"
        >
          <For each={props.job.tracks}>
            {(track) => {
              const view = createMemo(() => trackView(props.job, track));
              return (
                <li class={styles.track} data-kind={view().kind}>
                  <span>{number(track)}</span>
                  <span>{track.title}</span>
                  <span>{view().label}</span>
                </li>
              );
            }}
          </For>
        </ol>
      </div>
    </div>
  );
}
