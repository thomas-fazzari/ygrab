import type { Job } from "@/shared/api/schemas.ts";

const YOUTU_BE = "youtu.be";
const YOUTUBE_MUSIC = "music.youtube.com";
const WWW = "www.";
const TITLE_SUFFIX = /\s[([]/u;

/** Returns the link without its scheme and `www.`, as a user would type it. */
export function displayLink(url: string) {
  const link = new URL(url);
  const host = link.hostname.startsWith(WWW) ? link.hostname.slice(WWW.length) : link.hostname;
  return `${host}${link.pathname}${link.search}`;
}

export const isYouTubeMusicLink = (text: string) =>
  URL.canParse(text) && new URL(text).hostname === YOUTUBE_MUSIC;

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

/** Returns the album of a track job, or null when its title only repeats the track title. */
export function albumOf(job: Job) {
  const track = titleOf(job);
  if (!isTrackJob(job) || job.title === null || track === null) {
    return null;
  }
  const album = baseTitleOf(job.title);
  const base = baseTitleOf(track);
  return album === base || album.startsWith(`${base} `) ? null : job.title;
}

function baseTitleOf(title: string) {
  const end = title.search(TITLE_SUFFIX);
  return (end === -1 ? title : title.slice(0, end)).trim().toLowerCase();
}
