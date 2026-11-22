// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// The current cooldown in memory.
/// </summary>
/// <remarks>
/// <para>
/// Each change is applied and published under one lock, so the changes keep their order and each
/// change starts from the newest cooldown.
/// </para>
/// <para>
/// Memory changes before the store write, which runs after the lock since no lock covers I/O. The
/// write ignores shutdown, so no call follows a rate limit.
/// </para>
/// </remarks>
internal sealed class CooldownHolder(ChangeFeed feed, IJobStore store)
{
    private readonly Lock _lock = new();

    public Cooldown Current
    {
        get => Volatile.Read(ref field);
        private set;
    } = Cooldown.None;

    /// <summary>
    /// Sets the stored cooldown at start and publishes it, without a store write.
    /// </summary>
    public void Load(Cooldown cooldown)
    {
        lock (_lock)
        {
            Current = cooldown;
            feed.Publish(new CooldownChanged(cooldown));
        }
    }

    /// <summary>
    /// Applies <paramref name="change"/>, then publishes and stores the result when it differs.
    /// </summary>
    public async ValueTask<(Cooldown Current, bool Changed)> UpdateAsync(
        Func<Cooldown, Cooldown> change
    )
    {
        var (updated, changed) = Apply(change);
        if (changed)
            await store.SaveAsync(updated, CancellationToken.None);

        return (updated, changed);
    }

    private (Cooldown Current, bool Changed) Apply(Func<Cooldown, Cooldown> change)
    {
        lock (_lock)
        {
            var updated = change(Current);
            if (updated == Current)
                return (updated, false);

            Current = updated;
            feed.Publish(new CooldownChanged(updated));
            return (updated, true);
        }
    }
}
