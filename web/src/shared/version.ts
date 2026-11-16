/** The version of this build, from the `VERSION` argument of the image. */
export const CURRENT_VERSION = import.meta.env.VITE_VERSION?.replace(/^v/u, "") ?? "unreleased";
