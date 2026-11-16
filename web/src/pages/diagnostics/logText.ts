import type { LogLevel } from "@/shared/api/schemas.ts";
import { formatDay, formatTime } from "@/shared/clock.ts";
import type { FactGroup } from "./serverFacts.ts";

export const LEVEL_FILTERS = ["debug", "info", "warn", "error"] as const;
export type LevelFilter = (typeof LEVEL_FILTERS)[number];

export const LEVEL_FILTER_LABELS: Readonly<Record<LevelFilter, string>> = {
  debug: "Debug",
  info: "Info",
  warn: "Warn",
  error: "Error",
};

export const DEFAULT_LEVEL_FILTERS: readonly LevelFilter[] = ["info", "warn", "error"];

export const FILTER_OF_LEVEL: Readonly<Record<LogLevel, LevelFilter>> = {
  trace: "debug",
  debug: "debug",
  information: "info",
  warning: "warn",
  error: "error",
  critical: "error",
};

export const LEVEL_CODES: Readonly<Record<LogLevel, string>> = {
  trace: "TRC",
  debug: "DBG",
  information: "INF",
  warning: "WRN",
  error: "ERR",
  critical: "CRT",
};

const REPORT_LINES = 200;

export interface LogLineText {
  readonly time: string;
  readonly level: LogLevel;
  readonly source: string;
  readonly message: string;
}

/** The text of a line after its time and level, which the filter searches. */
export const bodyOf = (line: LogLineText) => `${line.source}: ${line.message}`;

const lineText = (line: LogLineText) =>
  `${formatDay(line.time)} ${formatTime(line.time)} ${LEVEL_CODES[line.level]} ${bodyOf(line)}`;

/** Writes the lines oldest first, one per entry, a multi-line message on its following lines. */
export const logText = (lines: readonly LogLineText[]) =>
  lines.map((line) => `${lineText(line)}\n`).join("");

/** Writes the facts of each card, then the last lines of the log. */
export function reportText(groups: readonly FactGroup[], lines: readonly LogLineText[]) {
  const facts = groups.flat().map(({ key, value }) => `${key}: ${value}\n`);
  return `${facts.join("")}\n${logText(lines.slice(-REPORT_LINES))}`;
}

export interface Segment {
  readonly text: string;
  readonly match: boolean;
}

/** Splits `text` around each case-insensitive match of `query`. */
export function highlight(text: string, query: string): Segment[] {
  const needle = query.toLowerCase();
  if (needle === "") {
    return [{ text, match: false }];
  }
  const haystack = text.toLowerCase();
  const segments: Segment[] = [];
  let start = 0;
  for (
    let found = haystack.indexOf(needle);
    found !== -1;
    found = haystack.indexOf(needle, start)
  ) {
    segments.push(
      { text: text.slice(start, found), match: false },
      { text: text.slice(found, found + needle.length), match: true },
    );
    start = found + needle.length;
  }
  segments.push({ text: text.slice(start), match: false });
  return segments.filter((segment) => segment.text !== "");
}

/** Saves `text` as a file through the browser's download. */
export function saveText(text: string, fileName: string) {
  const url = URL.createObjectURL(new Blob([text], { type: "text/plain" }));
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
