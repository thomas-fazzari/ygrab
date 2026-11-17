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
