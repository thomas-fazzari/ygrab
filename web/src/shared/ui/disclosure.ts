import { createEffect, createSignal } from "solid-js";

/**
 * Holds the open state of a terracotta popup and closes it on a press outside its root.
 *
 * A terracotta `Listbox` or `Popover` closes by itself only when the focus moves to another
 * focusable element, so a press on text or on the page would leave it open.
 */
export function createDisclosure() {
  const [isOpen, setOpen] = createSignal(false);
  let root: HTMLElement | undefined;
  const closeOutside = (event: PointerEvent) => {
    if (!(event.target instanceof Node && root?.contains(event.target) === true)) {
      setOpen(false);
    }
  };
  createEffect(isOpen, (open) => {
    const listening = new AbortController();
    if (open) {
      document.addEventListener("pointerdown", closeOutside, { signal: listening.signal });
    }
    return () => {
      listening.abort();
    };
  });
  return {
    isOpen,
    onChange: (open: boolean) => {
      setOpen(open);
    },
    ref: (element: HTMLElement) => {
      root = element;
    },
  };
}
