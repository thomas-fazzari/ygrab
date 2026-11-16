import {
  createMemo,
  createProjection,
  createSignal,
  For,
  Loading,
  onSettled,
  Show,
} from "solid-js";
import { match } from "ts-pattern";

import { getStatus } from "@/shared/api/client.ts";
import { logEvents } from "@/shared/api/events.ts";
import type { LogEvent } from "@/shared/api/events.ts";
import { failureOf } from "@/shared/api/failures.ts";
import { createMinuteClock } from "@/shared/clock.ts";
import { scan } from "@/shared/iterators.ts";
import { emptyFeed, watchJobs } from "@/shared/jobs/jobFeed.ts";
import type { JobFeed } from "@/shared/jobs/jobFeed.ts";
import { Button } from "@/shared/ui/Button.tsx";
import { PageTitle } from "@/shared/ui/PageTitle.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import styles from "./DiagnosticsPage.module.css";
import { applyLogEvent, emptyLogFeed } from "./logFeed.ts";
import type { LogFeed } from "./logFeed.ts";
import { LogSection } from "./LogSection.tsx";
import { reportText } from "./logText.ts";
import { appFacts, queueFacts, toolFacts } from "./serverFacts.ts";
import type { FactGroup } from "./serverFacts.ts";

export const DIAGNOSTICS_ROUTE = "/diagnostics";

const COPIED_MS = 2000;

const watchLogs = () => scan<LogEvent, LogFeed>(logEvents(), emptyLogFeed, applyLogEvent);

export function DiagnosticsPage() {
  const jobs = createProjection<JobFeed>(watchJobs, emptyFeed);
  const logs = createProjection<LogFeed>(watchLogs, emptyLogFeed);
  const status = createMemo(() => getStatus());
  const now = createMinuteClock();
  const groups = createMemo((): readonly FactGroup[] => {
    const result = status();
    return result.kind === "ok"
      ? [appFacts(result.value, now()), toolFacts(result.value), queueFacts(jobs, now())]
      : [];
  });

  return (
    <>
      <PageTitle>Diagnostics</PageTitle>
      <Loading>
        <section>
          <SectionHeading
            title="Server"
            aside={<CopyReportButton report={() => reportText(groups(), logs.lines)} />}
          />
          <Show when={failureOf(status())} fallback={<FactCards groups={groups()} />}>
            {(failure) => <p class={styles.failure}>{failure()}</p>}
          </Show>
        </section>
        <LogSection feed={logs} />
      </Loading>
    </>
  );
}

function FactCards(props: { readonly groups: readonly FactGroup[] }) {
  return (
    <div class={styles.cards}>
      <For each={props.groups}>
        {(group) => (
          <dl class={styles.card}>
            <For each={group}>
              {(fact) => (
                <div class={styles.fact}>
                  <dt>{fact.key}</dt>
                  <dd>{fact.value}</dd>
                </div>
              )}
            </For>
          </dl>
        )}
      </For>
    </div>
  );
}

type CopyState = "idle" | "copied" | "failed";

// The clipboard API exists only in a secure context, such as HTTPS or localhost
function CopyReportButton(props: { readonly report: () => string }) {
  const [state, setState] = createSignal<CopyState>("idle");
  let timer: ReturnType<typeof setTimeout> | undefined;
  onSettled(() => () => {
    clearTimeout(timer);
  });
  const show = (next: CopyState) => {
    setState(next);
    clearTimeout(timer);
    timer = setTimeout(() => {
      setState("idle");
    }, COPIED_MS);
  };
  const copy = () => {
    navigator.clipboard.writeText(props.report()).then(
      () => {
        show("copied");
      },
      () => {
        show("failed");
      },
    );
  };
  return (
    <Show when={window.isSecureContext}>
      <Button onClick={copy}>
        {match(state())
          .with("idle", () => "Copy report")
          .with("copied", () => "Copied")
          .with("failed", () => "Cannot copy")
          .exhaustive()}
      </Button>
    </Show>
  );
}
