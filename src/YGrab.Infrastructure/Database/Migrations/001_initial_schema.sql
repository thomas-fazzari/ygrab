-- SPDX-FileCopyrightText: 2026 Thomas Fazzari
-- SPDX-License-Identifier: AGPL-3.0-or-later

CREATE TABLE jobs (
    id           BLOB    PRIMARY KEY,
    url          TEXT    NOT NULL,
    link_kind    TEXT    NOT NULL CHECK (link_kind IN ('album', 'album_playlist', 'track', 'video')),
    link_id      TEXT    NOT NULL,
    quality      TEXT    CHECK (quality IN ('720p', '1080p', '1440p', '2160p', 'best', 'audio')),
    force        INTEGER NOT NULL CHECK (force IN (0, 1)),
    status       TEXT    NOT NULL CHECK (status IN (
        'queued', 'resolving', 'downloading', 'waiting', 'packaging',
        'ready', 'incomplete', 'failed', 'skipped', 'expired')),
    created_at   INTEGER NOT NULL,
    -- JSON: browse_id, title, artists, year, cover_url, track_count
    release      TEXT,
    -- JSON array of video_id, title, artists, number, seconds, explicit, state (pending, done, missing, refused), error
    tracks       TEXT,
    -- The video that yt-dlp kept, which only a video job has
    video_title   TEXT,
    video_channel TEXT,
    attempt      INTEGER NOT NULL,
    retry_at     INTEGER,
    error        TEXT,
    file_name    TEXT,
    duplicate_of BLOB,
    finished_at  INTEGER,
    -- Why the files lack the provider tags, which only a packaged audio job can have
    enrichment_failure TEXT CHECK (enrichment_failure IN ('no_match', 'unreachable', 'provider_error')),
    CHECK ((link_kind = 'video') = (quality IS NOT NULL)),
    CHECK (link_kind = 'video' OR (video_title IS NULL AND video_channel IS NULL)),
    CHECK (video_channel IS NULL OR video_title IS NOT NULL),
    CHECK (link_kind <> 'video' OR (release IS NULL AND tracks IS NULL)),
    CHECK (link_kind <> 'video' OR enrichment_failure IS NULL),
    CHECK (enrichment_failure IS NULL OR status IN ('ready', 'incomplete', 'expired')),
    CHECK (release IS NULL OR json_valid(release)),
    CHECK (tracks IS NULL OR json_valid(tracks))
) STRICT;

CREATE TABLE cooldown (
    id      INTEGER PRIMARY KEY CHECK (id = 1),
    strikes INTEGER NOT NULL,
    until   INTEGER,
    reason  TEXT
) STRICT;
