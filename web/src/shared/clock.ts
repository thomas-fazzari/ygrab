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

const clockFormat = new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit" });

/** Formats a timestamp as a 24-hour `HH:MM` in the local time zone. */
export const formatClock = (timestamp: string) => clockFormat.format(new Date(timestamp));

const relativeFormat = new Intl.RelativeTimeFormat("en", { numeric: "always" });

/** Formats a timestamp relative to `now`, in days, or in hours when less than a day away. */
export function formatRelative(timestamp: string, now: number) {
  const offset = Date.parse(timestamp) - now;
  return Math.abs(offset) < DAY_MS
    ? relativeFormat.format(Math.round(offset / HOUR_MS), "hour")
    : relativeFormat.format(Math.round(offset / DAY_MS), "day");
}
