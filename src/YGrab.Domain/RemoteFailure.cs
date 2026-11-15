// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// A failed call to YouTube.
/// </summary>
/// <remarks>
/// The adapters classify the raw error. <see cref="Job.Fail"/> decides what to do with it.
/// </remarks>
public closed record RemoteFailure(string Reason)
{
    /// <summary>
    /// A network error, a timeout or a server error, which a retry can fix.
    /// </summary>
    public sealed record Transient(string Reason) : RemoteFailure(Reason);

    /// <summary>
    /// YouTube refuses to serve this client for a while.
    /// </summary>
    public sealed record RateLimited(string Reason) : RemoteFailure(Reason);

    /// <summary>
    /// The video or the album cannot be read, and a retry will not change that.
    /// </summary>
    public sealed record Unavailable(string Reason) : RemoteFailure(Reason);
}
