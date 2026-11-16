import type { JSX } from "@solidjs/web";
import { createContext, createMemo, createSignal, onSettled, useContext } from "solid-js";
import type { Accessor } from "solid-js";

export interface Router {
  readonly path: Accessor<string>;
  readonly query: Accessor<URLSearchParams>;
  readonly navigate: (to: string, options?: NavigateOptions) => void;
}

export interface NavigateOptions {
  readonly replace?: boolean;
}

export const RouterContext = createContext<Router>();

const currentUrl = () => new URL(location.href);

// Following a link to the page on screen adds no history entry, as in a plain page load
const isCurrent = (to: string) => new URL(to, location.href).href === location.href;

/** Follows the History API: `navigate` pushes or replaces an entry, back and forward update it. */
export function createRouter(): Router {
  const [url, setUrl] = createSignal(currentUrl());
  const path = createMemo(() => url().pathname);
  const query = createMemo(() => url().searchParams);
  const sync = () => {
    setUrl(currentUrl());
  };

  onSettled(() => {
    window.addEventListener("popstate", sync);
    return () => {
      window.removeEventListener("popstate", sync);
    };
  });

  return {
    path,
    query,
    navigate(to, options) {
      if (options?.replace === true || isCurrent(to)) {
        history.replaceState(null, "", to);
      } else {
        history.pushState(null, "", to);
      }
      sync();
      window.scrollTo(0, 0);
    },
  };
}

const isPlainClick = (event: MouseEvent) =>
  event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey;

const isOfThisSite = (href: string) => new URL(href, location.href).origin === location.origin;

type LinkProps = Omit<JSX.AnchorHTMLAttributes<HTMLAnchorElement>, "href" | "onClick"> & {
  readonly href: string;
};

/**
 * Renders an `<a>`. A plain click opens a page of the app without a page load.
 *
 * A link to another site or with `download` stays a plain link.
 */
export function Link(props: LinkProps) {
  const router = useContext(RouterContext);
  const follow = (event: MouseEvent) => {
    if (
      event.defaultPrevented ||
      !isPlainClick(event) ||
      !isOfThisSite(props.href) ||
      props.download !== undefined
    ) {
      return;
    }
    event.preventDefault();
    router.navigate(props.href);
  };
  return <a {...props} onClick={follow} />;
}
