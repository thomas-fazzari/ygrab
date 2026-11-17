import { storageSync } from "@solid-primitives/storage";
import type { PersistenceSyncAPI } from "@solid-primitives/storage";
import type * as v from "valibot";

import { parseJsonWith } from "@/shared/api/schemas.ts";

const KEY_PREFIX = "ygrab.";

/**
 * Returns the options of `makePersisted` that keep a signal in local storage under `ygrab.<key>`.
 *
 * A stored value that does not match `schema` leaves the signal at its initial value. Call
 * `makePersisted` at module level: restoring the stored value writes to the signal, which Solid
 * refuses inside a component.
 */
export const storedAs = <TValue>(key: string, schema: v.GenericSchema<unknown, TValue>) => ({
  name: `${KEY_PREFIX}${key}`,
  deserialize: (text: string) => parseJsonWith(schema, text),
});

/**
 * Syncs a persisted signal with the writes of the other tabs, for the `sync` option.
 *
 * `makePersisted` drops a `storageSync` event whose `url` is another page, so this one passes
 * the event on without it.
 */
export const otherTabs: PersistenceSyncAPI = [
  (subscriber) =>
    storageSync[0]((event) => {
      subscriber({ key: event.key, newValue: event.newValue, timeStamp: event.timeStamp });
    }),
  storageSync[1],
];
