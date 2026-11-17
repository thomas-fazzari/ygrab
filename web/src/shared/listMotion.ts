import { createEffect, createMemo, createSignal } from "solid-js";
import type { Accessor } from "solid-js";

export interface ListMotion<T> {
  readonly enter: (item: T) => void;
  /** Starts the exit of an item and settles once it ends. */
  readonly exit: (item: T) => Promise<void>;
}

interface Frame<T> {
  readonly items: readonly T[];
  readonly kept: ReadonlySet<T>;
}

// Puts each item that left back before its next neighbour, or at the end, until its exit ends
function withLeaving<T extends object>(
  previous: readonly T[],
  current: readonly T[],
  exited: WeakSet<T>,
) {
  const kept = new Set(current);
  const items = [...current];
  let at = items.length;
  for (const item of previous.toReversed()) {
    if (kept.has(item)) {
      at = items.indexOf(item);
    } else if (!exited.has(item)) {
      items.splice(at, 0, item);
    }
  }
  return items;
}

/**
 * Lists the items of `source` plus the ones that left it and are still exiting.
 *
 * The items of the first list do not enter. `enter` and `exit` run once the list is in the page,
 * so they can measure and animate its elements.
 */
export function createListMotion<T extends object>(
  source: Accessor<readonly T[]>,
  motion: ListMotion<T>,
): Accessor<readonly T[]> {
  // A weak set forgets the items once the list drops them
  const [exited, setExited] = createSignal(new WeakSet<T>(), { equals: false });
  const items = createMemo((previous: readonly T[] | undefined) =>
    withLeaving(previous ?? [], source(), exited()),
  );

  createEffect(
    (): Frame<T> => ({ items: items(), kept: new Set(source()) }),
    (frame, previous) => {
      if (previous === undefined) {
        return;
      }
      const shown = new Set(previous.items);
      for (const item of frame.items) {
        if (!shown.has(item)) {
          motion.enter(item);
        } else if (!frame.kept.has(item) && previous.kept.has(item)) {
          void motion.exit(item).then(() => {
            setExited((done) => done.add(item));
          });
        }
      }
    },
  );

  return items;
}
