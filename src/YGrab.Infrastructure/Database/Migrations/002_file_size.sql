-- SPDX-FileCopyrightText: 2026 Thomas Fazzari
-- SPDX-License-Identifier: AGPL-3.0-or-later

ALTER TABLE jobs ADD COLUMN file_size INTEGER
    CHECK (file_size IS NULL OR (file_size >= 0 AND status IN ('ready', 'incomplete')));
