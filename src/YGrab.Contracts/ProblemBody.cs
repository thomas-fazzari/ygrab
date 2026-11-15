// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Contracts;

/// <summary>
/// The ProblemDetails body of an error answer, with the extra members that the API adds.
/// </summary>
/// <param name="Type">A relative URI that names the problem.</param>
/// <param name="PreviousId">The job that already covers the link, on an already grabbed answer.</param>
/// <param name="Errors">The messages of each invalid field, on a validation answer.</param>
public sealed record ProblemBody(
    string? Type = null,
    string? Title = null,
    int? Status = null,
    string? Detail = null,
    Guid? PreviousId = null,
    IReadOnlyDictionary<string, string[]>? Errors = null
);
