// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;

namespace YGrab.Host.Proxy;

/// <summary>
/// The networks that a reverse proxy may send the forwarded headers from.
/// </summary>
/// <remarks>
/// <para>
/// The host takes the client address, the scheme and the host of a request from its
/// <c>X-Forwarded-*</c> headers only when the request comes from one of these networks. A client
/// from any other network cannot fake them.
/// </para>
/// <para>
/// The default holds the loopback and the private networks, which a proxy on the same machine or
/// in another container connects from. A change needs a restart.
/// </para>
/// <para>
/// The networks are one comma-separated string, since a list in the settings merges by index
/// across the file and the environment variables.
/// </para>
/// </remarks>
public sealed class ProxyOptions
{
    public const string SECTION_NAME = "Proxy";

    internal const string PRIVATE_NETWORKS =
        "127.0.0.0/8, ::1/128, 10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, fc00::/7";

    public string TrustedNetworks { get; set; } = PRIVATE_NETWORKS;

    internal IEnumerable<string> NetworkTexts =>
        TrustedNetworks.Split(
            ',',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries
        );

    internal IEnumerable<IPNetwork> Networks => NetworkTexts.Select(IPNetwork.Parse);
}

internal sealed class ProxyOptionsValidator : IValidateOptions<ProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var invalid = options.NetworkTexts.Where(text => !IPNetwork.TryParse(text, out _)).ToList();
        return invalid.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{nameof(ProxyOptions.TrustedNetworks)} holds invalid networks: {string.Join(", ", invalid)}."
            );
    }
}
