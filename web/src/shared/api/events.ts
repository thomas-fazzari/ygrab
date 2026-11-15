import type * as v from "valibot";

import { API_PREFIX } from "./client.ts";
import {
  CooldownSchema,
  JobRemovedSchema,
  JobSchema,
  parseJsonWith,
  SnapshotSchema,
} from "./schemas.ts";
import { finished } from "../scan.ts";

export const EventNames = {
  snapshot: "snapshot",
  job: "job",
  jobRemoved: "job-removed",
  cooldown: "cooldown",
} as const;

const apiEventSchemas = {
  [EventNames.snapshot]: SnapshotSchema,
  [EventNames.job]: JobSchema,
  [EventNames.jobRemoved]: JobRemovedSchema,
  [EventNames.cooldown]: CooldownSchema,
};

type EventSchemas<TSchemas> = { readonly [TName in keyof TSchemas]: v.GenericSchema };

export type ServerEvent<
  TSchemas extends EventSchemas<TSchemas>,
  TName extends keyof TSchemas & string = keyof TSchemas & string,
> = {
  [TKey in TName]: { readonly event: TKey; readonly data: v.InferOutput<TSchemas[TKey]> };
}[TName];

export type ApiEvent = ServerEvent<typeof apiEventSchemas>;

interface RawEvent<TName extends string> {
  readonly event: TName;
  readonly text: string;
}

interface Queue<TItem> {
  readonly push: (item: TItem) => void;
  readonly close: () => void;
  readonly pull: () => Promise<TItem | undefined>;
}

// Pulls the items in order, then resolves to undefined once the queue is closed and empty
function createQueue<TItem>(): Queue<TItem> {
  const buffered: TItem[] = [];
  const readers: ((item?: TItem) => void)[] = [];
  let closed = false;
  return {
    push(item) {
      const reader = readers.shift();
      if (reader === undefined) {
        buffered.push(item);
      } else {
        reader(item);
      }
    },
    close() {
      closed = true;
      for (const reader of readers.splice(0)) {
        reader();
      }
    },
    pull() {
      const item = buffered.shift();
      if (item !== undefined || closed) {
        return Promise.resolve(item);
      }
      return new Promise((resolve) => {
        readers.push(resolve);
      });
    },
  };
}

function decode<TSchemas extends EventSchemas<TSchemas>, TName extends keyof TSchemas & string>(
  schemas: TSchemas,
  raw: RawEvent<TName>,
): ServerEvent<TSchemas, TName> {
  return { event: raw.event, data: parseJsonWith(schemas[raw.event], raw.text) };
}

/**
 * Yields the events of a server-sent stream, each parsed with the schema of its name.
 *
 * The browser reconnects a dropped stream by itself, so the server's first event may repeat.
 * The iteration ends when the stream closes for good, such as after a refused request. An event
 * that does not match its schema is a contract bug and throws a `ValiError`. Stopping the
 * iteration closes the stream.
 */
export function streamEvents<TSchemas extends EventSchemas<TSchemas>>(
  url: string,
  schemas: TSchemas,
): AsyncIterableIterator<ServerEvent<TSchemas>, void> {
  const source = new EventSource(url);
  const queue = createQueue<RawEvent<keyof TSchemas & string>>();
  const close = () => {
    source.close();
    queue.close();
  };
  const listen = (event: keyof TSchemas & string) => {
    source.addEventListener(event, (message: MessageEvent<string>) => {
      queue.push({ event, text: message.data });
    });
  };

  for (const event in schemas) {
    listen(event);
  }
  source.addEventListener("error", () => {
    if (source.readyState === EventSource.CLOSED) {
      close();
    }
  });

  const iterator: AsyncIterableIterator<ServerEvent<TSchemas>, void> = {
    async next() {
      const raw = await queue.pull();
      return raw === undefined ? finished : { done: false, value: decode(schemas, raw) };
    },
    return() {
      close();
      return Promise.resolve(finished);
    },
    [Symbol.asyncIterator]() {
      return iterator;
    },
  };
  return iterator;
}

export const apiEvents = () => streamEvents(`${API_PREFIX}/events`, apiEventSchemas);
