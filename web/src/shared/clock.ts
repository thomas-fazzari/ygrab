import { createSignal, onSettled } from "solid-js";
import type { Accessor } from "solid-js";

const MINUTE_MS = 60_000;

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
