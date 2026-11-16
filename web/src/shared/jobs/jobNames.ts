import type { Job } from "@/shared/api/schemas.ts";

const YOUTU_BE = "youtu.be";
const WWW = "www.";

/** Returns the link without its scheme and `www.`, as a user would type it. */
export function displayLink(url: string) {
  const link = new URL(url);
  const host = link.hostname.startsWith(WWW) ? link.hostname.slice(WWW.length) : link.hostname;
  return `${host}${link.pathname}${link.search}`;
}

export function videoIdOf(url: string) {
  const link = new URL(url);
  return link.hostname === YOUTU_BE ? link.pathname.slice(1) : link.searchParams.get("v");
}

/** A link to one track resolves to its album, so the job holds the album with one track. */
export const isTrackJob = (job: Job) => job.mode === "audio" && videoIdOf(job.url) !== null;

/** Returns the title to show, or null while the job has none and shows its link. */
export function titleOf(job: Job) {
  const [track] = job.tracks;
  return isTrackJob(job) && track !== undefined ? track.title : job.title;
}

export const nameOf = (job: Job) => titleOf(job) ?? displayLink(job.url);
