import type * as v from "valibot";

import { API_PREFIX, getCooldown, HTTP_UNAUTHORIZED } from "./client.ts";
import {
  CooldownSchema,
  JobRemovedSchema,
  JobSchema,
  parseJsonWith,
  SnapshotSchema,
} from "./schemas.ts";
import { asyncIterator, finished } from "../iterators.ts";

export const EventNames = {
  snapshot: "snapshot",
  job: "job",
  jobRemoved: "job-removed",
  cooldown: "cooldown",
} as const;

export const DISCONNECTED = "disconnected";

const apiEventSchemas = {
  [EventNames.snapshot]: SnapshotSchema,
  [EventNames.job]: JobSchema,
  [EventNames.jobRemoved]: JobRemovedSchema,
  [EventNames.cooldown]: CooldownSchema,
};

const RECONNECT_DELAYS_MS: readonly number[] = [1000, 2000, 5000];
const MAX_RECONNECT_DELAY_MS = 15_000;

type EventSchemas<TSchemas> = {
  readonly [TName in keyof TSchemas]: TName extends typeof DISCONNECTED ? never : v.GenericSchema;
};

export type ServerEvent<
  TSchemas extends EventSchemas<TSchemas>,
  TName extends keyof TSchemas & string = keyof TSchemas & string,
> = {
  [TKey in TName]: { readonly event: TKey; readonly data: v.InferOutput<TSchemas[TKey]> };
}[TName];

/** Marks a lost connection. The events that follow come from a new connection. */
export interface Disconnected {
  readonly event: typeof DISCONNECTED;
}

export type StreamItem<TSchemas extends EventSchemas<TSchemas>> =
  | ServerEvent<TSchemas>
  | Disconnected;

export type ApiEvent = StreamItem<typeof apiEventSchemas>;

type RawItem<TName extends string> =
  | { readonly kind: "message"; readonly event: TName; readonly text: string }
  | { readonly kind: "disconnected" };

interface Queue<TItem> {
  readonly push: (item: TItem) => void;
  readonly close: () => void;
  readonly pull: () => Promise<TItem | undefined>;
}

const disconnected: Disconnected = { event: DISCONNECTED };

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
  event: TName,
  text: string,
): ServerEvent<TSchemas, TName> {
  return { event, data: parseJsonWith(schemas[event], text) };
}

// Resolves after the delay, at once when the page becomes visible, or when the signal aborts
function pause(milliseconds: number, signal: AbortSignal): Promise<void> {
  if (signal.aborted) {
    return Promise.resolve();
  }
  return new Promise((resolve) => {
    const listening = new AbortController();
    const resume = () => {
      clearTimeout(timer);
      listening.abort();
      resolve();
    };
    const timer = setTimeout(resume, milliseconds);
    const options = { signal: listening.signal };
    signal.addEventListener("abort", resume, options);
    document.addEventListener(
      "visibilitychange",
      () => {
        if (document.visibilityState === "visible") {
          resume();
        }
      },
      options,
    );
  });
}

// Calls `onClosed` when the stream closes for good, and `onLost` once per lost connection, since
// the browser fires an error on each failed retry
function watchConnection(source: EventSource, onClosed: () => void, onLost: () => void) {
  let reported = false;
  source.addEventListener("open", () => {
    reported = false;
  });
  source.addEventListener("error", () => {
    if (source.readyState === EventSource.CLOSED) {
      onClosed();
    } else if (!reported) {
      reported = true;
      onLost();
    }
  });
}

/**
 * Yields the events of a server-sent stream, each parsed with the schema of its name.
 *
 * The browser reconnects a dropped stream by itself and the stream yields `Disconnected` once per
 * lost connection. The server's first event may then repeat. The iteration ends when the stream
 * closes for good, such as after a refused request. An event that does not match its schema is a
 * contract bug: it closes the stream and throws a `ValiError`. Stopping the iteration closes the
 * stream.
 */
export function streamEvents<TSchemas extends EventSchemas<TSchemas>>(
  url: string,
  schemas: TSchemas,
): AsyncIterableIterator<StreamItem<TSchemas>, void> {
  const source = new EventSource(url);
  const queue = createQueue<RawItem<keyof TSchemas & string>>();
  const close = () => {
    source.close();
    queue.close();
  };
  for (const event in schemas) {
    source.addEventListener(event, (message: MessageEvent<string>) => {
      queue.push({ kind: "message", event, text: message.data });
    });
  }
  watchConnection(source, close, () => {
    queue.push({ kind: "disconnected" });
  });

  const read = (raw: RawItem<keyof TSchemas & string>) => {
    if (raw.kind === "disconnected") {
      return disconnected;
    }
    try {
      return decode(schemas, raw.event, raw.text);
    } catch (cause) {
      close();
      throw cause;
    }
  };

  return asyncIterator(async () => {
    const raw = await queue.pull();
    return raw === undefined ? finished : { done: false, value: read(raw) };
  }, close);
}

/**
 * Yields the items of `connect`, and opens a new stream when one ends for good.
 *
 * A closed stream yields `Disconnected`. Before each new stream, `canReconnect` decides whether to
 * go on. The wait before it follows `RECONNECT_DELAYS_MS`, then `MAX_RECONNECT_DELAY_MS`, and ends
 * early when the page becomes visible. An item from the server resets the wait. Stopping the
 * iteration closes the open stream.
 */
export function reconnecting<TSchemas extends EventSchemas<TSchemas>>(
  connect: () => AsyncIterator<StreamItem<TSchemas>, void>,
  canReconnect: () => Promise<boolean>,
): AsyncIterableIterator<StreamItem<TSchemas>, void> {
  const stop = new AbortController();
  let stream: AsyncIterator<StreamItem<TSchemas>, void> | undefined = connect();
  let failures = 0;

  const waitToReconnect = async () => {
    if (!(await canReconnect())) {
      return false;
    }
    const delay = RECONNECT_DELAYS_MS[failures] ?? MAX_RECONNECT_DELAY_MS;
    failures += 1;
    await pause(delay, stop.signal);
    return true;
  };

  const next = async (): Promise<IteratorResult<StreamItem<TSchemas>, void>> => {
    if (stream === undefined) {
      const willReconnect = await waitToReconnect();
      if (!willReconnect || stop.signal.aborted) {
        return finished;
      }
      stream = connect();
    }
    const result = await stream.next();
    if (stop.signal.aborted) {
      return finished;
    }
    if (result.done === true) {
      stream = undefined;
      return { done: false, value: disconnected };
    }
    if (result.value.event !== DISCONNECTED) {
      failures = 0;
    }
    return result;
  };

  return asyncIterator(next, async () => {
    stop.abort();
    await stream?.return?.();
  });
}

// A refused session ends the stream for good, since `request` already sent the page to the login
async function isSignedIn(): Promise<boolean> {
  const result = await getCooldown();
  return result.kind !== "problem" || result.status !== HTTP_UNAUTHORIZED;
}

export const apiEvents = () =>
  reconnecting(() => streamEvents(`${API_PREFIX}/events`, apiEventSchemas), isSignedIn);
