// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Tests;

internal static class ResultExtensions
{
    extension<T>(Result<T> result)
    {
        public T ShouldSucceed() => result.Value.Should().BeAssignableTo<T>().Subject;

        public void ShouldFail() => result.Value.Should().BeOfType<ValidationError>();
    }
}
