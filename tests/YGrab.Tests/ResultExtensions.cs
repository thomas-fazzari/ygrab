// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Tests;

internal static class ResultExtensions
{
    extension<T>(Result<T> result)
    {
        public T ShouldSucceed()
        {
            result.TryGetValue(out T? value).Should().BeTrue();
            return value!;
        }

        public void ShouldFail() => result.TryGetValue(out ValidationError? _).Should().BeTrue();
    }
}
