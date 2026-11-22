// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine;

/// <summary>
/// The base of the outcome cases that report a failure several use cases share.
/// </summary>
public closed record Failure;

/// <summary>
/// Outcome case of a command whose input field fails its validation.
/// </summary>
public sealed record Invalid(string Field, string Message) : Failure;

/// <summary>
/// Outcome case of a use case that names something that does not exist.
/// </summary>
public sealed record NotFound : Failure;

/// <summary>
/// Outcome case of a use case that needs a finished job.
/// </summary>
public sealed record NotReady : Failure;

/// <summary>
/// Outcome case of a use case that needs the file of a job whose file is deleted.
/// </summary>
public sealed record Expired : Failure;

/// <param name="Previous">The job that already covers the link.</param>
public sealed record AlreadyGrabbed(Job Previous) : Failure;

public sealed record NotRetryable : Failure;

public sealed record NotMovable : Failure;
