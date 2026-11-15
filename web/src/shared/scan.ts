export const finished: IteratorReturnResult<void> = { done: true, value: undefined };

/**
 * Folds the items of an async iterator into a state and yields each new state.
 *
 * Unlike an async generator, stopping it stops the source at once, even while the source waits
 * for its next item.
 */
export function scan<TItem, TState>(
  source: AsyncIterator<TItem, void>,
  initial: TState,
  step: (state: TState, item: TItem) => TState,
): AsyncIterableIterator<TState, void> {
  let state = initial;
  const iterator: AsyncIterableIterator<TState, void> = {
    async next() {
      const result = await source.next();
      if (result.done === true) {
        return finished;
      }
      state = step(state, result.value);
      return { done: false, value: state };
    },
    async return() {
      await source.return?.();
      return finished;
    },
    [Symbol.asyncIterator]() {
      return iterator;
    },
  };
  return iterator;
}
