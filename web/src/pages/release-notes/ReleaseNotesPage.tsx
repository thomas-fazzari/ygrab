import { For, Show } from "solid-js";

import { commitUrl, profileUrl } from "@/shared/repository.ts";
import { Badge } from "@/shared/ui/Badge.tsx";
import { CURRENT_VERSION } from "@/shared/version.ts";
import { PageTitle } from "@/shared/ui/PageTitle.tsx";
import { SectionHeading } from "@/shared/ui/SectionHeading.tsx";
import { readReleases } from "./changelog.ts";
import type { Change, Release } from "./changelog.ts";
import styles from "./ReleaseNotesPage.module.css";

export const RELEASE_NOTES_ROUTE = "/release-notes";

const SHORT_SHA_LENGTH = 7;

export function ReleaseNotesPage() {
  const releases = readReleases();
  return (
    <>
      <PageTitle>Release notes</PageTitle>
      <For each={releases}>{(release) => <ReleaseSection release={release} />}</For>
    </>
  );
}

function ReleaseSection(props: { readonly release: Release }) {
  return (
    <section>
      <SectionHeading title={props.release.version} detail={props.release.date}>
        <Show when={props.release.version === CURRENT_VERSION}>
          <Badge tone="live">current</Badge>
        </Show>
      </SectionHeading>
      <ul class={styles.changes}>
        <For each={props.release.changes}>{(change) => <ChangeRow change={change} />}</For>
      </ul>
    </section>
  );
}

function ChangeRow(props: { readonly change: Change }) {
  return (
    <li class={styles.change}>
      <span class={styles.type}>{props.change.type}</span>
      <span class={styles.text}>{props.change.text}</span>
      <span class={styles.meta}>
        <ExternalLink href={profileUrl(props.change.author)}>{props.change.author}</ExternalLink>
        <ExternalLink href={commitUrl(props.change.sha)}>
          {props.change.sha.slice(0, SHORT_SHA_LENGTH)}
        </ExternalLink>
      </span>
    </li>
  );
}

function ExternalLink(props: { readonly href: string; readonly children: string }) {
  return (
    <a class={styles.link} href={props.href} target="_blank" rel="noopener noreferrer">
      {props.children}
    </a>
  );
}
