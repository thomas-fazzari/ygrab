import { dynamic } from "@solidjs/web";
import { createEffect, createMemo, Errored, Show } from "solid-js";
import type { Component } from "solid-js";

import { DIAGNOSTICS_ROUTE, DiagnosticsPage } from "@/pages/diagnostics/DiagnosticsPage.tsx";
import { HomePage } from "@/pages/home/HomePage.tsx";
import { JOBS_ROUTE, JobsPage } from "@/pages/jobs/JobsPage.tsx";
import { LoginPage } from "@/pages/login/LoginPage.tsx";
import { NotFoundPage } from "@/pages/not-found/NotFoundPage.tsx";
import { RELEASE_NOTES_ROUTE, ReleaseNotesPage } from "@/pages/release-notes/ReleaseNotesPage.tsx";
import { createRouter, RouterContext } from "@/shared/router.tsx";
import { HOME_ROUTE, LOGIN_ROUTE } from "@/shared/session.ts";
import { Button } from "@/shared/ui/Button.tsx";
import { PageTitle } from "@/shared/ui/PageTitle.tsx";
import styles from "./App.module.css";
import { Header } from "./Header.tsx";

interface Route {
  readonly page: Component;
  readonly header: boolean;
  readonly title: string | null;
}

const APP_NAME = "YGrab";

const routes = new Map<string, Route>([
  [HOME_ROUTE, { page: HomePage, header: true, title: null }],
  [LOGIN_ROUTE, { page: LoginPage, header: false, title: "Sign in" }],
  [JOBS_ROUTE, { page: JobsPage, header: true, title: "Jobs" }],
  [RELEASE_NOTES_ROUTE, { page: ReleaseNotesPage, header: true, title: "Release notes" }],
  [DIAGNOSTICS_ROUTE, { page: DiagnosticsPage, header: true, title: "Diagnostics" }],
]);

const notFound: Route = { page: NotFoundPage, header: true, title: "Page not found" };

const describe = (cause: unknown) => (cause instanceof Error ? cause.message : String(cause));

export function App() {
  const router = createRouter();
  const route = createMemo(() => routes.get(router.path()) ?? notFound);
  const Page = dynamic(() => route().page);
  createEffect(
    () => route().title,
    (title) => {
      document.title = title === null ? APP_NAME : `${title} · ${APP_NAME}`;
    },
  );

  return (
    <RouterContext value={router}>
      <div class={styles.column}>
        <Show when={route().header}>
          <Header />
        </Show>
        <main>
          <Errored
            fallback={(error, reset) => (
              <>
                <PageTitle>Something broke :(</PageTitle>
                <p class={styles.crash}>{describe(error())}</p>
                <div class={styles.retry}>
                  <Button onClick={reset}>Try again</Button>
                </div>
              </>
            )}
          >
            <Page />
          </Errored>
        </main>
      </div>
    </RouterContext>
  );
}
