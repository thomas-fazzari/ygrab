import * as v from "valibot";

export const JOB_MODES = ["audio", "video"] as const;
export const VIDEO_QUALITIES = ["720p", "1080p", "1440p", "2160p", "best", "audio"] as const;
export const JOB_STATES = [
  "queued",
  "resolving",
  "downloading",
  "waiting",
  "packaging",
  "ready",
  "incomplete",
  "failed",
  "skipped",
  "expired",
] as const;
export const TRACK_STATES = ["pending", "done", "missing", "refused"] as const;
export const ENRICHMENT_FAILURES = ["no_match", "unreachable", "provider_error"] as const;

const IdSchema = v.pipe(v.string(), v.uuid());
const TimestampSchema = v.pipe(v.string(), v.isoTimestamp());
const NullableTextSchema = v.nullable(v.string());

export type JobId = v.InferOutput<typeof IdSchema>;

export const JobModeSchema = v.picklist(JOB_MODES);
export const VideoQualitySchema = v.picklist(VIDEO_QUALITIES);
export const JobStateSchema = v.picklist(JOB_STATES);
export const TrackStateSchema = v.picklist(TRACK_STATES);
export const EnrichmentFailureSchema = v.picklist(ENRICHMENT_FAILURES);

export const TrackSchema = v.object({
  number: v.pipe(v.number(), v.integer()),
  title: v.string(),
  state: TrackStateSchema,
  reason: NullableTextSchema,
});
export type Track = v.InferOutput<typeof TrackSchema>;

export const JobSchema = v.object({
  id: IdSchema,
  url: v.pipe(v.string(), v.url()),
  mode: JobModeSchema,
  quality: v.nullable(VideoQualitySchema),
  state: JobStateSchema,
  reason: NullableTextSchema,
  createdAt: TimestampSchema,
  finishedAt: v.nullable(TimestampSchema),
  title: NullableTextSchema,
  artists: v.array(v.string()),
  fileName: NullableTextSchema,
  enrichmentFailure: v.nullable(EnrichmentFailureSchema),
  percent: v.pipe(v.number(), v.integer()),
  tracks: v.array(TrackSchema),
  currentTrack: v.nullable(v.pipe(v.number(), v.integer())),
});
export type Job = v.InferOutput<typeof JobSchema>;

export const JobsSchema = v.array(JobSchema);

export const StartJobRequestSchema = v.object({
  link: v.string(),
  mode: JobModeSchema,
  quality: VideoQualitySchema,
  force: v.boolean(),
});
export type StartJobRequest = v.InferOutput<typeof StartJobRequestSchema>;

export const StartedJobSchema = v.object({ id: IdSchema });
export type StartedJob = v.InferOutput<typeof StartedJobSchema>;

export const JobRemovedSchema = v.object({ id: IdSchema });
export type JobRemoved = v.InferOutput<typeof JobRemovedSchema>;

export const CooldownSchema = v.object({
  strikes: v.pipe(v.number(), v.integer()),
  until: v.nullable(TimestampSchema),
  reason: NullableTextSchema,
});
export type Cooldown = v.InferOutput<typeof CooldownSchema>;

export const SnapshotSchema = v.object({ jobs: JobsSchema, cooldown: CooldownSchema });
export type Snapshot = v.InferOutput<typeof SnapshotSchema>;

export const LoginRequestSchema = v.object({ token: v.string() });
export type LoginRequest = v.InferOutput<typeof LoginRequestSchema>;

export const SessionSchema = v.object({ authEnabled: v.boolean() });
export type Session = v.InferOutput<typeof SessionSchema>;

export const NoContentSchema = v.null();

export const ProblemSchema = v.object({
  type: v.nullish(v.string()),
  title: v.nullish(v.string()),
  status: v.nullish(v.pipe(v.number(), v.integer())),
  detail: v.nullish(v.string()),
  previousId: v.nullish(IdSchema),
  errors: v.nullish(v.record(v.string(), v.array(v.string()))),
});
export type Problem = v.InferOutput<typeof ProblemSchema>;

/** Parses JSON text with `schema` and throws a `ValiError` when the text does not match. */
export function parseJsonWith<TSchema extends v.GenericSchema>(
  schema: TSchema,
  text: string,
): v.InferOutput<TSchema> {
  return v.parse(v.pipe(v.string(), v.parseJson(), schema), text);
}
