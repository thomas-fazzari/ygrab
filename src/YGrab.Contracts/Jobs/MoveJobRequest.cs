// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Contracts.Jobs;

/// <param name="Before">Null moves the job to the end of the queue.</param>
public sealed record MoveJobRequest(Guid? Before);
