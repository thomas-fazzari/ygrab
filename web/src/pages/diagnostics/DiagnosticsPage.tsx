import {
  createMemo,
  createProjection,
  createSignal,
  For,
  Loading,
  onSettled,
  Repeat,
  Show,
  useContext,
} from "solid-js";
import { match } from "ts-pattern";
import CheckIcon from "~icons/solar/check-linear";
import CopyIcon from "~icons/solar/copy-linear";
import DownloadIcon from "~icons/solar/download-minimalistic-linear";

import { getStatus } from "@/shared/api/client.ts";
import { logEvents } from "@/shared/api/events.ts";
import type { LogEvent } from "@/shared/api/events.ts";
import { failureOf } from "@/shared/api/failures.ts";
import { createMinuteClock } from "@/shared/clock.ts";
import { scan } from "@/shared/iterators.ts";
import { JobFeedContext } from "@/shared/jobs/jobFeed.ts";
import { Button } from "@/shared/ui/Button.tsx";
import { IconSwap } from "@/shared/ui/IconSwap.tsx";
import { PageTitle } from "@/shared/ui/PageTitle.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import { Skeleton, SkeletonText } from "@/shared/ui/Skeleton.tsx";
import styles from "./DiagnosticsPage.module.css";
import { applyLogEvent, emptyLogFeed } from "./logFeed.ts";
import type { LogFeed } from "./logFeed.ts";
import { LogSection, LogSectionSkeleton } from "./LogSection.tsx";
import { reportText, saveText } from "./logText.ts";
import { appFacts, queueFacts, toolFacts } from "./serverFacts.ts";
import type { FactGroup } from "./serverFacts.ts";

export const DIAGNOSTICS_ROUTE = "/diagnostics";

const COPIED_MS = 2000;
const REPORT_FILE_NAME = "ygrab-report.txt";
const SERVER_TITLE = "Server";
const SKELETON_GROUPS = 3;
const SKELETON_FACTS = 4;

const watchLogs = () => scan<LogEvent, LogFeed>(logEvents(), emptyLogFeed, applyLogEvent);

export function DiagnosticsPage() {
  const jobs = useContext(JobFeedContext);
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
      <Loading
        fallback={
          <>
            <ServerSectionSkeleton />
            <LogSectionSkeleton />
          </>
        }
      >
        <section>
          <SectionHeading
            title={SERVER_TITLE}
            aside={<ReportButton report={() => reportText(groups(), logs.lines)} />}
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

function ServerSectionSkeleton() {
  return (
    <section>
      <SectionHeading title={SERVER_TITLE} />
      <Skeleton>
        <div class={styles.cards}>
          <Repeat count={SKELETON_GROUPS}>
            {() => (
              <div class={styles.card}>
                <Repeat count={SKELETON_FACTS}>
                  {() => (
                    <div class={styles.fact}>
                      <SkeletonText chars={8} />
                      <SkeletonText chars={10} />
                    </div>
                  )}
                </Repeat>
              </div>
            )}
          </Repeat>
        </div>
      </Skeleton>
    </section>
  );
}

type CopyState = "idle" | "copied" | "failed";

interface ReportButtonProps {
  readonly report: () => string;
}

// The clipboard API exists only in a secure context, such as HTTPS or localhost
function ReportButton(props: ReportButtonProps) {
  const save = () => {
    saveText(props.report(), REPORT_FILE_NAME);
  };
  return (
    <Show
      when={window.isSecureContext}
      fallback={
        <Button onClick={save}>
          <DownloadIcon />
          Save report
        </Button>
      }
    >
      <CopyReportButton report={props.report} />
    </Show>
  );
}

const copyStatusOf = (state: CopyState) =>
  match(state)
    .with("idle", () => "")
    .with("copied", () => "Report copied")
    .with("failed", () => "Cannot copy the report")
    .exhaustive();

function CopyReportButton(props: ReportButtonProps) {
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
    <>
      <Button onClick={copy}>
        <IconSwap swapped={state() === "copied"} icon={<CopyIcon />} swappedIcon={<CheckIcon />} />
        {state() === "failed" ? "Cannot copy" : "Copy report"}
      </Button>
      <span class="visually-hidden" role="status">
        {copyStatusOf(state())}
      </span>
    </>
  );
}
