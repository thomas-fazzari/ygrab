import ReorderIcon from "~icons/solar/reorder-linear";

import type { JobId } from "@/shared/api/schemas.ts";

type Side = "before" | "after";

/** Gets null for the end of the queue. */
export type OnMove = (id: JobId, before: JobId | null) => void;

interface DragState {
  dragged: HTMLElement | null;
  marked: HTMLElement | null;
}

const HANDLE = "[data-drag-handle]";
const QUEUED_CARD = 'article[data-state="queued"]:not([data-motion="exit"])';
const DROP = "data-drop";
const DRAGGING = "data-dragging";

const queuedCardOf = (target: EventTarget | null) =>
  target instanceof Element ? target.closest<HTMLElement>(QUEUED_CARD) : null;

const sideOf = (card: HTMLElement, event: DragEvent): Side => {
  const box = card.getBoundingClientRect();
  return event.clientY < box.top + box.height / 2 ? "before" : "after";
};

function nextQueuedId(card: HTMLElement, dragged: JobId) {
  for (let next = card.nextElementSibling; next !== null; next = next.nextElementSibling) {
    if (next instanceof HTMLElement && next.matches(QUEUED_CARD) && next.dataset.job !== dragged) {
      return next.dataset.job ?? null;
    }
  }
  return null;
}

function mark(state: DragState, card: HTMLElement | null, side: Side | null) {
  state.marked?.removeAttribute(DROP);
  state.marked = card;
  if (card !== null && side !== null) {
    card.setAttribute(DROP, side);
  }
}

function end(state: DragState) {
  mark(state, null, null);
  state.dragged?.removeAttribute(DRAGGING);
  state.dragged = null;
}

function targetOf(state: DragState, event: DragEvent) {
  const card = queuedCardOf(event.target);
  return state.dragged === null || card === null || card === state.dragged ? null : card;
}

// A drag whose source left the page sends no dragend, so we clear its state here
function start(state: DragState, event: DragEvent) {
  end(state);
  const handle = event.target instanceof Element ? event.target.closest(HANDLE) : null;
  const card = queuedCardOf(handle);
  if (card === null || event.dataTransfer === null) {
    return;
  }
  const box = card.getBoundingClientRect();
  event.dataTransfer.effectAllowed = "move";
  event.dataTransfer.setData("text/plain", card.dataset.job ?? "");
  event.dataTransfer.setDragImage(card, event.clientX - box.left, event.clientY - box.top);
  card.setAttribute(DRAGGING, "");
  state.dragged = card;
}

function over(state: DragState, event: DragEvent) {
  const card = targetOf(state, event);
  if (card === null) {
    mark(state, null, null);
    return;
  }
  event.preventDefault();
  if (event.dataTransfer !== null) {
    event.dataTransfer.dropEffect = "move";
  }
  mark(state, card, sideOf(card, event));
}

function drop(state: DragState, event: DragEvent, onMove: OnMove) {
  const card = targetOf(state, event);
  const id = state.dragged?.dataset.job;
  end(state);
  if (card === null || id === undefined) {
    return;
  }
  event.preventDefault();
  onMove(
    id,
    sideOf(card, event) === "before" ? (card.dataset.job ?? null) : nextQueuedId(card, id),
  );
}

/** Needs the job id in `data-job` on each card. */
export function createQueueDrag(onMove: OnMove) {
  const state: DragState = { dragged: null, marked: null };
  return {
    onDragStart: (event: DragEvent) => {
      start(state, event);
    },
    onDragOver: (event: DragEvent) => {
      over(state, event);
    },
    onDrop: (event: DragEvent) => {
      drop(state, event, onMove);
    },
    onDragEnd: () => {
      end(state);
    },
  };
}

interface DragHandleProps {
  readonly class: string | undefined;
  readonly hidden: boolean;
}

// Mouse only, so screen readers skip it.
// Hidden rather than unmounted, so a drag keeps its source
export function DragHandle(props: DragHandleProps) {
  return (
    <span
      class={props.class}
      hidden={props.hidden}
      data-drag-handle
      draggable="true"
      aria-hidden="true"
    >
      <ReorderIcon />
    </span>
  );
}
