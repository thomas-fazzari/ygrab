import { createMemo, Loading, Show, useContext } from "solid-js";
import GitHubIcon from "~icons/simple-icons/github";
import HomeIcon from "~icons/solar/home-linear";
import JobsIcon from "~icons/solar/list-linear";
import LogoutIcon from "~icons/solar/logout-2-linear";
import NotesIcon from "~icons/solar/megaphone-linear";
import PulseIcon from "~icons/solar/pulse-linear";

import { DIAGNOSTICS_ROUTE } from "@/pages/diagnostics/DiagnosticsPage.tsx";
import { RELEASE_NOTES_ROUTE } from "@/pages/release-notes/ReleaseNotesPage.tsx";
import { getSession, logout } from "@/shared/api/client.ts";
import { REPOSITORY_URL } from "@/shared/repository.ts";
import { RouterContext } from "@/shared/router.tsx";
import { HOME_ROUTE, JOBS_ROUTE, LOGIN_ROUTE } from "@/shared/session.ts";
import { IconButton, IconLink } from "@/shared/ui/IconControl.tsx";
import styles from "./Header.module.css";

async function signOut() {
  const result = await logout();
  if (result.kind === "ok") {
    location.assign(LOGIN_ROUTE);
  }
}

// A host without a token has no session to end
function SignOut() {
  const session = createMemo(() => getSession());
  const authEnabled = () => {
    const result = session();
    return result.kind === "ok" && result.value.authEnabled;
  };
  return (
    <Show when={authEnabled()}>
      <IconButton
        label="Sign out"
        onClick={() => {
          void signOut();
        }}
      >
        <LogoutIcon />
      </IconButton>
    </Show>
  );
}

export function Header() {
  const router = useContext(RouterContext);

  return (
    <header class={styles.header}>
      <nav class={styles.nav} aria-label="Main">
        <IconLink label="Home" href={HOME_ROUTE} current={router.path() === HOME_ROUTE}>
          <HomeIcon />
        </IconLink>
        <IconLink label="Jobs" href={JOBS_ROUTE} current={router.path() === JOBS_ROUTE}>
          <JobsIcon />
        </IconLink>
      </nav>
      <IconLink
        label="Release notes"
        href={RELEASE_NOTES_ROUTE}
        current={router.path() === RELEASE_NOTES_ROUTE}
      >
        <NotesIcon />
      </IconLink>
      <IconLink
        label="Diagnostics"
        href={DIAGNOSTICS_ROUTE}
        current={router.path() === DIAGNOSTICS_ROUTE}
      >
        <PulseIcon />
      </IconLink>
      <IconLink label="GitHub" href={REPOSITORY_URL} newTab>
        <GitHubIcon />
      </IconLink>
      <Loading fallback={null}>
        <SignOut />
      </Loading>
    </header>
  );
}
