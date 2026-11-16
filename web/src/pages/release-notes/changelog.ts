import * as v from "valibot";

import changelog from "@/changelog.json";

const CHANGE_TYPES = ["feat", "fix", "perf"] as const;
const SHA_LENGTH = 40;

const ChangeSchema = v.object({
  type: v.picklist(CHANGE_TYPES),
  text: v.string(),
  author: v.string(),
  sha: v.pipe(v.string(), v.hexadecimal(), v.length(SHA_LENGTH)),
});
export type Change = v.InferOutput<typeof ChangeSchema>;

const ReleaseSchema = v.object({
  version: v.string(),
  date: v.pipe(v.string(), v.isoDate()),
  changes: v.array(ChangeSchema),
});
export type Release = v.InferOutput<typeof ReleaseSchema>;

/** The version of this build, from the `VERSION` argument of the image build. */
export const CURRENT_VERSION = import.meta.env.VITE_VERSION?.replace(/^v/u, "") ?? "unreleased";

/** Reads the releases that `mise run release` writes, newest first. */
export const readReleases = () => v.parse(v.array(ReleaseSchema), changelog);
