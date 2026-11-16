import { createMemo, Loading, Show, useContext } from "solid-js";
import GitHubIcon from "~icons/simple-icons/github";
import HomeIcon from "~icons/solar/home-linear";
import LogoutIcon from "~icons/solar/logout-2-linear";

import { getSession, logout } from "@/shared/api/client.ts";
import { RouterContext } from "@/shared/router.tsx";
import { HOME_ROUTE, LOGIN_ROUTE } from "@/shared/session.ts";
import { IconButton, IconLink } from "@/shared/ui/IconControl.tsx";
import styles from "./Header.module.css";

const REPOSITORY_URL = "https://github.com/thomas-fazzari/ygrab";

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
        label="Log out"
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
      </nav>
      <IconLink label="GitHub" href={REPOSITORY_URL}>
        <GitHubIcon />
      </IconLink>
      <Loading fallback={null}>
        <SignOut />
      </Loading>
    </header>
  );
}
