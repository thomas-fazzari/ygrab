export const LOGIN_ROUTE = "/login";
export const HOME_ROUTE = "/";

const NEXT_PARAM = "next";

function loginHref(next: string): string {
  return `${LOGIN_ROUTE}?${new URLSearchParams({ [NEXT_PARAM]: next }).toString()}`;
}

/**
 * Loads the login page, which then returns to the current page.
 *
 * The login replaces the current entry, since going back to a page that needs the session would
 * load the login again.
 */
export function redirectToLogin(): void {
  if (location.pathname === LOGIN_ROUTE) {
    return;
  }
  location.replace(loginHref(`${location.pathname}${location.search}`));
}

/**
 * Reads the page to return to after the login.
 *
 * Only a path on this origin is kept, so a crafted link cannot send the user to another site. A
 * path that starts with `//`, such as the one `/.//host` resolves to, would name another host.
 */
export function nextAfterLogin(query: URLSearchParams): string {
  const next = new URL(query.get(NEXT_PARAM) ?? HOME_ROUTE, location.origin);
  return isPageOfThisSite(next) ? `${next.pathname}${next.search}${next.hash}` : HOME_ROUTE;
}

const isPageOfThisSite = (url: URL) =>
  url.origin === location.origin && !url.pathname.startsWith("//") && url.pathname !== LOGIN_ROUTE;
