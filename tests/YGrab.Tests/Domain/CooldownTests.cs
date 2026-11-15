// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Tests.Domain;

public sealed class CooldownTests
{
    [Fact]
    public void Each_hit_doubles_the_pause_up_to_the_cap_and_a_clear_resets_it()
    {
        var now = TestTime.Start;

        var first = Cooldown.None.Hit("429", now);
        var second = first.Hit("429", now);
        var capped = Enumerable
            .Range(0, 10)
            .Aggregate(Cooldown.None, (cooldown, _) => cooldown.Hit("429", now));

        first.Until.Should().Be(now + Cooldown.Base);
        second.Until.Should().Be(now + (Cooldown.Base * 2));
        capped.Until.Should().Be(now + Cooldown.Cap);
        capped.Strikes.Should().Be(10);
        capped.Clear().Should().Be(Cooldown.None);
    }

    [Fact]
    public void A_cooldown_is_active_until_its_end()
    {
        var now = TestTime.Start;
        var cooldown = Cooldown.None.Hit("429", now);

        cooldown.IsActiveAt(now).Should().BeTrue();
        cooldown.IsActiveAt(cooldown.Until!.Value).Should().BeFalse();
        Cooldown.None.IsActiveAt(now).Should().BeFalse();
    }
}
