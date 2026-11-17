import { makePersisted } from "@solid-primitives/storage";
import { createMemo, createSignal, For, Show } from "solid-js";
import DownloadIcon from "~icons/solar/download-minimalistic-linear";

import { createMinuteClock, formatDay, formatTime } from "@/shared/clock.ts";
import { storedAs } from "@/shared/storage.ts";
import { Button } from "@/shared/ui/Button.tsx";
import { MultiSelect } from "@/shared/ui/MultiSelect.tsx";
import { SearchField } from "@/shared/ui/SearchField.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import { Skeleton, SkeletonText } from "@/shared/ui/Skeleton.tsx";
import type { LogFeed, LogLine } from "./logFeed.ts";
import styles from "./LogSection.module.css";
import {
  bodyOf,
  DEFAULT_LEVEL_FILTERS,
  FILTER_OF_LEVEL,
  highlight,
  LEVEL_CODES,
  LEVEL_FILTER_LABELS,
  LEVEL_FILTERS,
  LevelFiltersSchema,
  logText,
  saveText,
} from "./logText.ts";
import type { LevelFilter } from "./logText.ts";

const LOG_FILE_NAME = "ygrab-log.txt";
const TITLE = "Log Stream";
const SKELETON_LINES = [56, 41, 68, 47, 33, 61, 44, 52];

const [levels, setLevels] = makePersisted(
  createSignal<readonly LevelFilter[]>(DEFAULT_LEVEL_FILTERS),
  storedAs("diagnostics.levels", LevelFiltersSchema),
);

export function LogSection(props: { readonly feed: LogFeed }) {
  const [query, setQuery] = createSignal("");
  const visible = createMemo(() => {
    const needle = query().toLowerCase();
    const shown = new Set(levels());
    return props.feed.lines
      .filter(
        (line) =>
          shown.has(FILTER_OF_LEVEL[line.level]) && bodyOf(line).toLowerCase().includes(needle),
      )
      .toReversed();
  });

  return (
    <section>
      <SectionHeading
        title={TITLE}
        detail={props.feed.offline ? "offline, reconnecting" : undefined}
        aside={
          <>
            <SearchField
              label="Search"
              value={query()}
              onInput={(value) => {
                setQuery(value);
              }}
            />
            <MultiSelect
              name="Levels"
              choices={LEVEL_FILTERS}
              labels={LEVEL_FILTER_LABELS}
              value={levels()}
              onChange={(value) => {
                setLevels(value);
              }}
            />
            <SaveButton lines={props.feed.lines} />
          </>
        }
      />
      <LogLines lines={visible()} query={query()} />
    </section>
  );
}

export function LogSectionSkeleton() {
  return (
    <section>
      <SectionHeading title={TITLE} />
      <Skeleton>
        <div class={styles.lines}>
          <For each={SKELETON_LINES}>
            {(chars) => (
              <div class={styles.line}>
                <SkeletonText chars={chars} />
              </div>
            )}
          </For>
        </div>
      </Skeleton>
    </section>
  );
}

function SaveButton(props: { readonly lines: readonly LogLine[] }) {
  const save = () => {
    saveText(logText(props.lines), LOG_FILE_NAME);
  };
  return (
    <Button onClick={save}>
      <DownloadIcon />
      Save
    </Button>
  );
}

interface LogLinesProps {
  /** Newest first. */
  readonly lines: readonly LogLine[];
  readonly query: string;
}

// A day line comes before the newest line of each day but today
function LogLines(props: LogLinesProps) {
  const now = createMinuteClock();
  const dayStarts = createMemo(() => {
    const today = formatDay(now());
    return new Set(
      props.lines.flatMap((line, index) =>
        line.day === today || props.lines[index - 1]?.day === line.day ? [] : [line.id],
      ),
    );
  });
  return (
    <Show when={props.lines.length > 0}>
      <div class={styles.lines} role="log" aria-live="off" aria-label={TITLE} tabindex="0">
        <For each={props.lines}>
          {(line) => (
            <>
              <Show when={dayStarts().has(line.id)}>
                <div class={styles.day}>{line.day}</div>
              </Show>
              <LogRow line={line} query={props.query} />
            </>
          )}
        </For>
      </div>
    </Show>
  );
}

function LogRow(props: { readonly line: LogLine; readonly query: string }) {
  return (
    <div class={styles.line} data-level={FILTER_OF_LEVEL[props.line.level]}>
      <span class={styles.meta}>
        {formatTime(props.line.time)} {LEVEL_CODES[props.line.level]}
      </span>{" "}
      <For each={highlight(bodyOf(props.line), props.query)}>
        {(segment) => (segment.match ? <mark>{segment.text}</mark> : segment.text)}
      </For>
    </div>
  );
}
