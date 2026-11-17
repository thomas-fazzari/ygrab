import { createEffect, createMemo, createUniqueId, For, Show } from "solid-js";
import { Popover, PopoverButton, PopoverPanel } from "terracotta/popover";
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
import { Button } from "@/shared/ui/Button.tsx";
import { createDisclosure } from "@/shared/ui/disclosure.ts";
import { IconButton } from "@/shared/ui/IconControl.tsx";
import controlStyles from "@/shared/ui/IconControl.module.css";
import styles from "./NotificationBell.module.css";

const MAX_COUNT = 9;

const countText = (count: number) => (count > MAX_COUNT ? `${MAX_COUNT}+` : String(count));

const isEmpty = () => notifications().length === 0;

// The bell is inactive without notifications, so the panel closes with the last one
function createPanel() {
  const disclosure = createDisclosure();
  const close = () => {
    disclosure.onChange(false);
  };
  return {
    ref: disclosure.ref,
    isOpen: disclosure.isOpen,
    close,
    change: (open: boolean) => {
      if (open) {
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
      <PopoverPanel class={styles.panel} style={anchor}>
        <NotificationList
          items={notifications()}
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
        {(item) => <NotificationRow item={item()} now={now()} onDismiss={dismiss} />}
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

interface NotificationRowProps {
  readonly item: NotificationItem;
  readonly now: number;
  readonly onDismiss: (id: string) => void;
}

function NotificationRow(props: NotificationRowProps) {
  return (
    <li class={styles.item} data-failure={isFailure(props.item)}>
      <span class={styles.kind}>{kindIcon(props.item.kind)}</span>
      <Show when={props.item.jobId} fallback={<span class={styles.title}>{props.item.title}</span>}>
        {(jobId) => (
          <span class={styles.title}>
            <Link href={`${JOBS_ROUTE}#${cardIdOf({ id: jobId() })}`}>{props.item.title}</Link>
          </span>
        )}
      </Show>
      <time class={styles.time} datetime={props.item.at}>
        {formatAgo(props.item.at, props.now)}
      </time>
      <Show when={props.item.detail}>
        {(detail) => <span class={styles.detail}>{detail()}</span>}
      </Show>
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
