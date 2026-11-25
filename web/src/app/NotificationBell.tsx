import { createEffect, createMemo, createSignal, createUniqueId, For, Show } from "solid-js";
import { Popover, PopoverButton, PopoverOverlay, PopoverPanel } from "terracotta/popover";
import { match } from "ts-pattern";
import BellIcon from "~icons/solar/bell-linear";
import CheckIcon from "~icons/solar/check-circle-linear";
import ClockIcon from "~icons/solar/clock-circle-linear";
import CloseIcon from "~icons/solar/close-linear";
import ErrorIcon from "~icons/solar/danger-circle-linear";
import WarningIcon from "~icons/solar/danger-triangle-linear";

import { createMinuteClock, formatAgo } from "@/shared/clock.ts";
import { cardIdOf } from "@/shared/jobs/jobView.ts";
import {
  clearNotifications,
  dismissNotification,
  errorAnnouncement,
  isFailure,
  markAllRead,
  notifications,
} from "@/shared/notifications.ts";
import type { NotificationItem, NotificationKind } from "@/shared/notifications.ts";
import { Link } from "@/shared/router.tsx";
import { JOBS_ROUTE } from "@/shared/session.ts";
import { Badge } from "@/shared/ui/Badge.tsx";
import { Button } from "@/shared/ui/Button.tsx";
import { createDisclosure } from "@/shared/ui/disclosure.ts";
import { IconButton } from "@/shared/ui/IconControl.tsx";
import controlStyles from "@/shared/ui/IconControl.module.css";
import styles from "./NotificationBell.module.css";

const MAX_COUNT = 9;

const countText = (count: number) => (count > MAX_COUNT ? `${MAX_COUNT}+` : String(count));

const isEmpty = () => notifications().length === 0;

const unreadIds = () => new Set(notifications().flatMap((item) => (item.read ? [] : [item.id])));

// The bell is inactive without notifications, so the panel closes with the last one
function createPanel() {
  const disclosure = createDisclosure();
  const [fresh, setFresh] = createSignal<ReadonlySet<string>>(new Set());
  const close = () => {
    disclosure.onChange(false);
  };
  return {
    ref: disclosure.ref,
    isOpen: disclosure.isOpen,
    fresh,
    close,
    change: (open: boolean) => {
      if (open) {
        setFresh(unreadIds());
        markAllRead();
      }
      disclosure.onChange(open && !isEmpty());
    },
    clear: () => {
      clearNotifications();
      close();
    },
    dismiss: (id: string) => {
      const isLast = notifications().length === 1;
      dismissNotification(id);
      if (isLast) {
        close();
      }
    },
  };
}

export function NotificationBell() {
  const anchor = { "--anchor": `--bell-${createUniqueId()}` };
  const panel = createPanel();
  const unread = createMemo(() => notifications().filter((item) => !item.read));

  return (
    <Popover ref={panel.ref} class={styles.bell} isOpen={panel.isOpen()} onChange={panel.change}>
      <BellButton anchor={anchor} unread={unread()} empty={isEmpty()} />
      <Show when={panel.isOpen()}>
        <PopoverOverlay class={styles.scrim} />
      </Show>
      <PopoverPanel class={styles.panel} style={anchor}>
        <h2 class={styles.heading}>Notifications</h2>
        <Show when={panel.fresh().size > 0}>
          <span class={styles.new}>{panel.fresh().size} new</span>
        </Show>
        <NotificationList
          items={notifications()}
          fresh={panel.fresh()}
          onFollow={panel.close}
          onDismiss={panel.dismiss}
        />
        <div class={styles.actions}>
          <Button quiet onClick={panel.clear}>
            Clear all
          </Button>
        </div>
      </PopoverPanel>
      <p class="visually-hidden" role="status">
        {errorAnnouncement()}
      </p>
    </Popover>
  );
}

interface BellButtonProps {
  readonly anchor: Record<string, string>;
  readonly unread: readonly NotificationItem[];
  readonly empty: boolean;
}

const isAdded = (items: readonly NotificationItem[], previous?: readonly NotificationItem[]) =>
  items[0] !== undefined &&
  previous !== undefined &&
  !previous.some((item) => item.id === items[0]?.id);

function BellButton(props: BellButtonProps) {
  let button: HTMLElement | undefined;
  createEffect(notifications, (items, previous) => {
    if (isAdded(items, previous) && button !== undefined) {
      button.dataset.ring = button.dataset.ring === "even" ? "odd" : "even";
    }
  });
  const count = () => props.unread.length;
  const label = () => (count() === 0 ? "Notifications" : `Notifications, ${count()} unread`);
  return (
    <PopoverButton
      ref={(element: HTMLElement) => {
        button = element;
      }}
      class={[controlStyles.control, styles.button]}
      style={props.anchor}
      aria-disabled={props.empty ? "true" : undefined}
    >
      <BellIcon />
      <Show when={count() > 0}>
        <span
          class={styles.count}
          data-failure={props.unread.some((item) => isFailure(item))}
          aria-hidden="true"
        >
          {countText(count())}
        </span>
      </Show>
      <span class="visually-hidden">{label()}</span>
    </PopoverButton>
  );
}

interface NotificationListProps {
  readonly items: readonly NotificationItem[];
  readonly fresh: ReadonlySet<string>;
  readonly onFollow: () => void;
  readonly onDismiss: (id: string) => void;
}

const isLinkClick = (event: MouseEvent) =>
  event.target instanceof Element && event.target.closest("a") !== null;

// Terracotta focuses the first focusable element on open. The list takes it, so a second Enter
// does not press Clear all
function NotificationList(props: NotificationListProps) {
  const now = createMinuteClock();
  let list: HTMLOListElement | undefined;
  // The list keeps the focus of the dismiss button that leaves with its row
  const dismiss = (id: string) => {
    list?.focus();
    props.onDismiss(id);
  };
  return (
    <ol
      ref={(element) => {
        list = element;
      }}
      class={styles.list}
      tabindex="0"
      aria-label="Notifications"
      onClick={(event) => {
        if (isLinkClick(event)) {
          props.onFollow();
        }
      }}
    >
      <For each={props.items} keyed={(item) => item.id}>
        {(item) => (
          <NotificationRow
            item={item()}
            unread={props.fresh.has(item().id)}
            now={now()}
            onDismiss={dismiss}
          />
        )}
      </For>
    </ol>
  );
}

const kindIcon = (kind: NotificationKind) =>
  match(kind)
    .with("ready", () => <CheckIcon />)
    .with("incomplete", () => <WarningIcon />)
    .with("failed", "error", () => <ErrorIcon />)
    .with("paused", () => <ClockIcon />)
    .exhaustive();

function Thumbnail(props: { readonly item: NotificationItem }) {
  return (
    <Show
      when={props.item.cover}
      fallback={
        <span class={styles.kind} data-kind={props.item.kind}>
          {kindIcon(props.item.kind)}
        </span>
      }
    >
      {(cover) => (
        <img
          class={styles.cover}
          src={cover()}
          alt=""
          loading="lazy"
          referrerpolicy="no-referrer"
        />
      )}
    </Show>
  );
}

interface NotificationRowProps {
  readonly item: NotificationItem;
  readonly unread: boolean;
  readonly now: number;
  readonly onDismiss: (id: string) => void;
}

function NotificationRow(props: NotificationRowProps) {
  return (
    <li class={styles.item} data-unread={props.unread}>
      <Thumbnail item={props.item} />
      <span class={styles.title}>
        <Show when={props.unread}>
          <span class="visually-hidden">Unread: </span>
        </Show>
        <Show when={props.item.jobId} fallback={props.item.title}>
          {(jobId) => (
            <Link href={`${JOBS_ROUTE}#${cardIdOf({ id: jobId() })}`}>{props.item.title}</Link>
          )}
        </Show>
      </span>
      <Badge class={styles.badge} tone={isFailure(props.item) ? "error" : "live"}>
        {props.item.kind}
      </Badge>
      <span class={styles.meta}>
        <Show when={props.item.detail}>{(detail) => <span>{detail()}</span>}</Show>
        <time datetime={props.item.at}>{formatAgo(props.item.at, props.now)}</time>
      </span>
      <span class={styles.dismiss}>
        <IconButton
          label="Dismiss"
          subject={props.item.title}
          onClick={() => {
            props.onDismiss(props.item.id);
          }}
        >
          <CloseIcon />
        </IconButton>
      </span>
    </li>
  );
}
