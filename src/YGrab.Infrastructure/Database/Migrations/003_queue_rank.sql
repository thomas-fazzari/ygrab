-- SPDX-FileCopyrightText: 2026 Thomas Fazzari
-- SPDX-License-Identifier: AGPL-3.0-or-later

-- A lower rank runs first
ALTER TABLE jobs ADD COLUMN queue_rank REAL NOT NULL DEFAULT 0;

UPDATE jobs
SET queue_rank = ranked.position
FROM (SELECT id, ROW_NUMBER() OVER (ORDER BY created_at, id) AS position FROM jobs) AS ranked
WHERE jobs.id = ranked.id;
