// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using CSnakes.Runtime;
using YGrab.Domain;

namespace YGrab.Infrastructure.Python.Extensions;

internal static class PythonInvocationExceptionExtensions
{
    private const string RateLimitedErrorName = "RateLimitedError";
    private const string TransientErrorName = "TransientError";
    private const string UnavailableErrorName = "UnavailableError";

    extension(PythonInvocationException exception)
    {
        /// <summary>Gets the message of the Python exception.</summary>
        internal string Reason => exception.InnerException?.Message ?? exception.Message;

        /// <summary>Maps an error that a YGrab Python module raises to a remote failure.</summary>
        /// <remarks>
        /// <para>
        /// The modules raise <c>RateLimitedError</c>, <c>TransientError</c> and
        /// <c>UnavailableError</c>. Any other exception type returns null.
        /// </para>
        /// </remarks>
        internal RemoteFailure? ToRemoteFailure() =>
            exception.PythonExceptionType switch
            {
                RateLimitedErrorName => new RemoteFailure.RateLimited(exception.Reason),
                TransientErrorName => new RemoteFailure.Transient(exception.Reason),
                UnavailableErrorName => new RemoteFailure.Unavailable(exception.Reason),
                _ => null,
            };
    }
}
