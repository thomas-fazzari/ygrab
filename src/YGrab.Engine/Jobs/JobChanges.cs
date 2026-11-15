// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

public sealed record JobChanged(Job Job) : Change;

public sealed record JobRemoved(Guid Id) : Change;

public sealed record CooldownChanged(Cooldown Cooldown) : Change;
