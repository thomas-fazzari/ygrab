// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Cli;

internal static class ExitCodes
{
    public const int Ok = 0;
    public const int ApiError = 1;
    public const int Usage = 2;
    public const int Unreachable = 3;
    public const int Interrupted = 130;
}
