// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// The current cooldown in memory.
/// </summary>
/// <remarks>
/// Each change publishes under the lock, so the changes keep their order.
/// </remarks>
internal sealed class CooldownHolder(ChangeFeed feed)
{
    private readonly Lock _lock = new();

    public Cooldown Current
    {
        get => Volatile.Read(ref field);
        private set;
    } = Cooldown.None;

    public void Set(Cooldown cooldown)
    {
        lock (_lock)
        {
            Current = cooldown;
            feed.Publish(new CooldownChanged(cooldown));
        }
    }
}
