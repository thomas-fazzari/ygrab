// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Domain;

public readonly record struct JobId(Guid Value) : IComparable<JobId>
{
    public static bool operator <(JobId left, JobId right) => left.CompareTo(right) < 0;

    public static bool operator <=(JobId left, JobId right) => left.CompareTo(right) <= 0;

    public static bool operator >(JobId left, JobId right) => left.CompareTo(right) > 0;

    public static bool operator >=(JobId left, JobId right) => left.CompareTo(right) >= 0;

    public int CompareTo(JobId other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString();
}
