import { createSignal, onSettled } from "solid-js";
import type { Accessor } from "solid-js";

const MINUTE_MS = 60_000;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;

const untilNextMinute = () => MINUTE_MS - (Date.now() % MINUTE_MS);

/** Returns the time in milliseconds, updated at the start of each minute. */
export function createMinuteClock(): Accessor<number> {
  const [now, setNow] = createSignal(Date.now());
  onSettled(() => {
    let timer: ReturnType<typeof setTimeout>;
    const tick = () => {
      setNow(Date.now());
      timer = setTimeout(tick, untilNextMinute());
    };
    timer = setTimeout(tick, untilNextMinute());
    return () => {
      clearTimeout(timer);
    };
  });
  return now;
}

const formatterOf = (format: Intl.DateTimeFormat) => (timestamp: string | number) =>
  format.format(new Date(timestamp));

const CLOCK_OPTIONS = { hour: "2-digit", minute: "2-digit" } as const;

/** Formats a timestamp as a 24-hour `HH:MM` in the local time zone. */
export const formatClock = formatterOf(new Intl.DateTimeFormat("en-GB", CLOCK_OPTIONS));

/** Formats a timestamp as a 24-hour `HH:MM:SS` in the local time zone. */
export const formatTime = formatterOf(
  new Intl.DateTimeFormat("en-GB", { ...CLOCK_OPTIONS, second: "2-digit" }),
);

/** Formats a timestamp as `YYYY-MM-DD` in the local time zone. */
export const formatDay = formatterOf(
  new Intl.DateTimeFormat("en-CA", { year: "numeric", month: "2-digit", day: "2-digit" }),
);

/**
 * Formats a duration as its two largest units, such as `3 d 4 h` or `4 h 12 min`.
 *
 * A negative duration, from a browser clock behind the server, shows as `0 min`.
 */
export function formatDuration(milliseconds: number) {
  const elapsed = Math.max(0, milliseconds);
  const days = Math.floor(elapsed / DAY_MS);
  const hours = Math.floor((elapsed % DAY_MS) / HOUR_MS);
  const minutes = Math.floor((elapsed % HOUR_MS) / MINUTE_MS);
  if (days > 0) {
    return `${days} d ${hours} h`;
  }
  return hours > 0 ? `${hours} h ${minutes} min` : `${minutes} min`;
}

const relativeFormat = new Intl.RelativeTimeFormat("en", { numeric: "always" });

/** Formats a timestamp relative to `now`, in days, or in hours when less than a day away. */
export function formatRelative(timestamp: string, now: number) {
  const offset = Date.parse(timestamp) - now;
  return Math.abs(offset) < DAY_MS
    ? relativeFormat.format(Math.round(offset / HOUR_MS), "hour")
    : relativeFormat.format(Math.round(offset / DAY_MS), "day");
}
