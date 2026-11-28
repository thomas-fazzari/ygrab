// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Cli;

internal static class ExitCodes
{
    public const int OK = 0;
    public const int API_ERROR = 1;
    public const int USAGE = 2;
    public const int UNREACHABLE = 3;
    public const int INTERRUPTED = 130;
}
