export const finished: IteratorReturnResult<void> = { done: true, value: undefined };

/**
 * Builds an async iterator from its `next` step and a `stop` step.
 *
 * Unlike an async generator, stopping it runs `stop` at once, even while `next` waits.
 */
export function asyncIterator<TItem>(
  next: () => Promise<IteratorResult<TItem, void>>,
  stop: () => void | Promise<void>,
): AsyncIterableIterator<TItem, void> {
  const iterator: AsyncIterableIterator<TItem, void> = {
    next,
    async return() {
      await stop();
      return finished;
    },
    [Symbol.asyncIterator]() {
      return iterator;
    },
  };
  return iterator;
}

/**
 * Folds the items of an async iterator into a state and yields each new state.
 *
 * A step that returns the same state yields nothing, so a consumer never sees an unchanged state.
 * Stopping it stops the source at once.
 */
export function scan<TItem, TState>(
  source: AsyncIterator<TItem, void>,
  initial: TState,
  step: (state: TState, item: TItem) => TState,
): AsyncIterableIterator<TState, void> {
  let state = initial;
  const pull = async (): Promise<IteratorResult<TState, void>> => {
    const result = await source.next();
    if (result.done === true) {
      return finished;
    }
    const next = step(state, result.value);
    if (next === state) {
      return pull();
    }
    state = next;
    return { done: false, value: state };
  };
  return asyncIterator(pull, async () => {
    await source.return?.();
  });
}
