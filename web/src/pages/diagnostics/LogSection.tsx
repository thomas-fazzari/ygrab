import { createMemo, createSignal, For, Show } from "solid-js";
import DownloadIcon from "~icons/solar/download-minimalistic-linear";
import SearchIcon from "~icons/solar/magnifer-linear";

import { createMinuteClock, formatDay, formatTime } from "@/shared/clock.ts";
import { Button } from "@/shared/ui/Button.tsx";
import { Field } from "@/shared/ui/Field.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import { LevelSelect } from "./LevelSelect.tsx";
import type { LogFeed, LogLine } from "./logFeed.ts";
import styles from "./LogSection.module.css";
import {
  bodyOf,
  DEFAULT_LEVEL_FILTERS,
  FILTER_OF_LEVEL,
  highlight,
  LEVEL_CODES,
  logText,
  saveText,
} from "./logText.ts";
import type { LevelFilter } from "./logText.ts";

const LOG_FILE_NAME = "ygrab-log.txt";

export function LogSection(props: { readonly feed: LogFeed }) {
  const [query, setQuery] = createSignal("");
  const [levels, setLevels] = createSignal<readonly LevelFilter[]>(DEFAULT_LEVEL_FILTERS);
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
        title="Log"
        detail={props.feed.offline ? "offline, reconnecting" : undefined}
        aside={
          <div class={styles.toolbar}>
            <Field small>
              <SearchIcon />
              <input
                type="text"
                aria-label="Filter"
                placeholder="filter"
                value={query()}
                onInput={(event) => {
                  setQuery(event.currentTarget.value);
                }}
              />
            </Field>
            <LevelSelect
              value={levels()}
              onChange={(value) => {
                setLevels(value);
              }}
            />
            <DownloadButton lines={props.feed.lines} />
          </div>
        }
      />
      <LogLines lines={visible()} query={query()} />
    </section>
  );
}

function DownloadButton(props: { readonly lines: readonly LogLine[] }) {
  const download = () => {
    saveText(logText(props.lines), LOG_FILE_NAME);
  };
  return (
    <Button onClick={download}>
      <DownloadIcon />
      Download
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
      <div class={styles.lines} role="log" aria-live="off" aria-label="Server log" tabindex="0">
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
