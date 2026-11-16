import { action, createOptimistic, createSignal, Show, useContext } from "solid-js";
import { match, P } from "ts-pattern";
import LockIcon from "~icons/solar/lock-keyhole-minimalistic-linear";

import { HTTP_UNAUTHORIZED, login } from "@/shared/api/client.ts";
import type { ApiResult } from "@/shared/api/client.ts";
import { RouterContext } from "@/shared/router.tsx";
import { nextAfterLogin } from "@/shared/session.ts";
import { Field, FieldSubmit } from "@/shared/ui/Field.tsx";
import { Logo } from "@/shared/ui/Logo.tsx";
import styles from "./LoginPage.module.css";

const HTTP_TOO_MANY_REQUESTS = 429;

const failureOf = (result: ApiResult<null>) =>
  match(result)
    .with({ kind: "ok" }, () => null)
    .with({ kind: "problem", status: HTTP_UNAUTHORIZED }, () => "Wrong token.")
    .with(
      { kind: "problem", status: HTTP_TOO_MANY_REQUESTS },
      () => "Too many attempts. Try again in a minute.",
    )
    .with({ kind: "problem", problem: { title: P.string } }, ({ problem }) => problem.title)
    .with({ kind: "problem" }, ({ status }) => `The server answered ${status}.`)
    .with({ kind: "unreachable" }, () => "Cannot reach the server.")
    .exhaustive();

function createSignIn() {
  const router = useContext(RouterContext);
  const [failure, setFailure] = createSignal<string | null>(null);
  const [sending, setSending] = createOptimistic(false);

  const signIn = action(async function* (token: string) {
    setSending(true);
    const result = await login({ token });
    yield;
    setFailure(failureOf(result));
    if (result.kind === "ok") {
      router.navigate(nextAfterLogin(router.query()), { replace: true });
    }
  });

  return { failure, sending, signIn };
}

function LoginForm() {
  const [token, setToken] = createSignal("");
  const { failure, sending, signIn } = createSignIn();

  const submit = (event: SubmitEvent) => {
    event.preventDefault();
    if (!sending()) {
      void signIn(token());
    }
  };

  return (
    <form onSubmit={submit}>
      <Field>
        <LockIcon />
        <input
          type="password"
          autocomplete="current-password"
          aria-label="Token"
          aria-describedby="login-help"
          placeholder="access token"
          required
          autofocus
          value={token()}
          onInput={(event) => {
            setToken(event.currentTarget.value);
          }}
        />
        <FieldSubmit label="Sign in" disabled={sending()} />
      </Field>
      <Show when={failure()}>
        {(message) => (
          <p class={styles.failure} role="alert">
            {message()}
          </p>
        )}
      </Show>
    </form>
  );
}

export function LoginPage() {
  return (
    <div class={styles.page}>
      <Logo />
      <LoginForm />
      <p id="login-help" class={styles.help}>
        The token is <code>Auth:Token</code> in the settings, or the content of{" "}
        <code>auth-token</code> in the data folder.
      </p>
    </div>
  );
}
