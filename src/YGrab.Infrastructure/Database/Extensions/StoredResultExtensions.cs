// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Infrastructure.Database.Extensions;

internal static class StoredResultExtensions
{
    extension<T>(Result<T> result)
    {
        public T GetStoredValue() =>
            result is T value
                ? value
                : throw new InvalidOperationException("A stored value fails its validation.");
    }
}
