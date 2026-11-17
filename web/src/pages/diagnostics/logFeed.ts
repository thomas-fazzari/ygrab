import { match } from "ts-pattern";

import { DISCONNECTED, LogEventNames } from "@/shared/api/events.ts";
import type { LogEvent } from "@/shared/api/events.ts";
import type { LogEntry } from "@/shared/api/schemas.ts";
import { formatDay } from "@/shared/clock.ts";

const MAX_LINES = 1000;

export interface LogLine extends LogEntry {
  readonly id: number;
  readonly day: string;
}

export interface LogFeed {
  /** Oldest first. */
  readonly lines: LogLine[];
  readonly nextId: number;
  readonly offline: boolean;
}

export const emptyLogFeed: LogFeed = { lines: [], nextId: 0, offline: false };

function withLines(feed: LogFeed, entries: readonly LogEntry[], kept: readonly LogLine[]): LogFeed {
  const added = entries.map((entry, index) => ({
    ...entry,
    id: feed.nextId + index,
    day: formatDay(entry.time),
  }));
  return {
    lines: [...kept, ...added].slice(-MAX_LINES),
    nextId: feed.nextId + entries.length,
    offline: false,
  };
}

export const applyLogEvent = (feed: LogFeed, event: LogEvent): LogFeed =>
  match(event)
    .with({ event: LogEventNames.snapshot }, ({ data }) => withLines(feed, data, []))
    .with({ event: LogEventNames.entry }, ({ data }) => withLines(feed, [data], feed.lines))
    .with({ event: DISCONNECTED }, () => (feed.offline ? feed : { ...feed, offline: true }))
    .exhaustive();
