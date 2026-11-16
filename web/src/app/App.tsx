import { dynamic } from "@solidjs/web";
import { createMemo, Errored, Show } from "solid-js";
import type { Component } from "solid-js";

import { HomePage } from "@/pages/home/HomePage.tsx";
import { LoginPage } from "@/pages/login/LoginPage.tsx";
import { NotFoundPage } from "@/pages/not-found/NotFoundPage.tsx";
import { createRouter, RouterContext } from "@/shared/router.tsx";
import { HOME_ROUTE, LOGIN_ROUTE } from "@/shared/session.ts";
import { PageTitle } from "@/shared/ui/PageTitle.tsx";
import styles from "./App.module.css";
import { Header } from "./Header.tsx";

interface Route {
  readonly page: Component;
  readonly header: boolean;
}

const routes = new Map<string, Route>([
  [HOME_ROUTE, { page: HomePage, header: true }],
  [LOGIN_ROUTE, { page: LoginPage, header: false }],
]);

const notFound: Route = { page: NotFoundPage, header: true };

const describe = (cause: unknown) => (cause instanceof Error ? cause.message : String(cause));

export function App() {
  const router = createRouter();
  const route = createMemo(() => routes.get(router.path()) ?? notFound);
  const Page = dynamic(() => route().page);

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
                <PageTitle>Something broke</PageTitle>
                <p class={styles.crash}>{describe(error())}</p>
                <button class={styles.retry} type="button" onClick={reset}>
                  Try again
                </button>
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
