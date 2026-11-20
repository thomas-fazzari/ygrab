// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

/// <summary>
/// The pause after YouTube rate-limits us.
/// </summary>
/// <remarks>
/// <para>
/// Each <see cref="Hit"/> doubles the pause up to <see cref="Cap"/>.
/// </para>
/// <para>
/// A packaged job clears it with <see cref="Clear"/>.
/// </para>
/// <para>
/// A client ends the pause early with <see cref="End"/>, which keeps the strikes, so the next
/// rate limit pauses twice as long.
/// </para>
/// </remarks>
public sealed record Cooldown
{
    internal static readonly TimeSpan Base = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan Cap = TimeSpan.FromHours(6);

    private Cooldown() { }

    public static Cooldown None { get; } = new();

    public int Strikes { get; private init; }

    public DateTimeOffset? Until { get; private init; }

    public string? Reason { get; private init; }

    public static Cooldown Restore(int strikes, DateTimeOffset? until, string? reason) =>
        strikes < 0 || (strikes == 0 && (until is not null || reason is not null))
            ? throw new InvalidOperationException("A stored cooldown breaks its invariants.")
            : new Cooldown
            {
                Strikes = strikes,
                Until = until,
                Reason = reason,
            };

    public bool IsActiveAt(DateTimeOffset now) => Until > now;

    public Cooldown Hit(string reason, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return new Cooldown
        {
            Strikes = Strikes + 1,
            Until = now + Doubling.Delay(Base, Cap, Strikes),
            Reason = reason,
        };
    }

    public Cooldown Clear() => Strikes == 0 ? this : None;

    public Cooldown End() => Until is null ? this : this with { Until = null };
}
