// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

public enum EnrichmentFailure
{
    /// <summary>
    /// The provider has no release that is the same product.
    /// </summary>
    NoMatch = 0,

    /// <summary>
    /// The provider did not answer (e.g. network error or time limit).
    /// </summary>
    Unreachable = 1,

    /// <summary>
    /// The provider answered with an error.
    /// </summary>
    ProviderError = 2,
}
