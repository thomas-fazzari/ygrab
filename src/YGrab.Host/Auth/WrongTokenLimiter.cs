// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;

namespace YGrab.Host.Auth;

internal sealed partial class WrongTokenLimiter(ILogger<WrongTokenLimiter> logger) : IDisposable
{
    internal const int TRIES = 5;
    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // One machine can use every address of its IPv6 /64 network
    private const int IPV6_NETWORK_BYTES = 8;

    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<
        string,
        string
    >(client =>
        RateLimitPartition.GetFixedWindowLimiter(client, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = TRIES,
            Window = Window,
            QueueLimit = 0,
        })
    );

    public bool CanTry(HttpContext context)
    {
        // A request for no permit tells whether any is left, without taking one
        using var lease = _limiter.AttemptAcquire(ClientOf(context), permitCount: 0);
        return lease.IsAcquired;
    }

    public void CountWrong(HttpContext context)
    {
        var client = ClientOf(context);
        _limiter.AttemptAcquire(client).Dispose();
        if (_limiter.GetStatistics(client)?.CurrentAvailablePermits is 0)
            LogLockedOut(client, Window);
    }

    public void Dispose() => _limiter.Dispose();

    private static string ClientOf(HttpContext context)
    {
        if (context.Connection.RemoteIpAddress is not { } address)
            return string.Empty;

        if (address.IsIPv4MappedToIPv6)
            return address.MapToIPv4().ToString();

        if (address.AddressFamily is not AddressFamily.InterNetworkV6)
            return address.ToString();

        var bytes = address.GetAddressBytes();
        bytes.AsSpan(IPV6_NETWORK_BYTES).Clear();
        return new IPAddress(bytes).ToString();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Client {Client} sent too many wrong tokens, so every token from it is refused for {Window}"
    )]
    private partial void LogLockedOut(string client, TimeSpan window);
}
