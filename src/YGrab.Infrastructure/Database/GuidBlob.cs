// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Infrastructure.Database;

/// <summary>
/// Stores a Guid as 16 big-endian bytes, so the byte order of a version 7 id is its time order.
/// </summary>
internal static class GuidBlob
{
    public static byte[] ToBlob(Guid value)
    {
        var bytes = new byte[16];
        value.TryWriteBytes(bytes, bigEndian: true, out _);
        return bytes;
    }

    public static Guid FromBlob(byte[] blob) => new(blob, bigEndian: true);
}
