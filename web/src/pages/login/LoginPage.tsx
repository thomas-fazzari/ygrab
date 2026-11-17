import { action, createOptimistic, createSignal, Show, useContext } from "solid-js";
import { match } from "ts-pattern";
import ArrowIcon from "~icons/solar/arrow-right-linear";
import LockIcon from "~icons/solar/lock-keyhole-minimalistic-linear";

import { HTTP_UNAUTHORIZED, login } from "@/shared/api/client.ts";
import type { ApiResult } from "@/shared/api/client.ts";
import { failureOf } from "@/shared/api/failures.ts";
import { RouterContext } from "@/shared/router.tsx";
import { nextAfterLogin } from "@/shared/session.ts";
import { Field } from "@/shared/ui/Field.tsx";
import { IconButton } from "@/shared/ui/IconControl.tsx";
import { Logo } from "@/shared/ui/Logo.tsx";
import styles from "./LoginPage.module.css";

const HTTP_TOO_MANY_REQUESTS = 429;

const loginFailureOf = (result: ApiResult<null>) =>
  match(result)
    .with({ kind: "problem", status: HTTP_UNAUTHORIZED }, () => "Wrong token.")
    .with(
      { kind: "problem", status: HTTP_TOO_MANY_REQUESTS },
      () => "Too many attempts. Try again in a minute.",
    )
    .otherwise(failureOf);

function createSignIn() {
  const router = useContext(RouterContext);
  const [failure, setFailure] = createSignal<string | null>(null);
  const [sending, setSending] = createOptimistic(false);

  const signIn = action(async function* (token: string) {
    setSending(true);
    const result = await login({ token });
    yield;
    setFailure(loginFailureOf(result));
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
          placeholder="token"
          required
          autofocus
          value={token()}
          onInput={(event) => {
            setToken(event.currentTarget.value);
          }}
        />
        <IconButton type="submit" label="Sign in" main disabled={sending()}>
          <ArrowIcon />
        </IconButton>
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
        Set the token with <code>YGRAB_TOKEN</code>, or as <code>Token</code> under{" "}
        <code>Auth</code> in <code>ygrab.yaml</code>. Without either, the token is in the{" "}
        <code>auth-token</code> file of the data folder.
      </p>
    </div>
  );
}
