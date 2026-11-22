// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using YGrab.Contracts.Auth;
using YGrab.Contracts.Diagnostics;
using YGrab.Contracts.Jobs;

namespace YGrab.Contracts;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    UseStringEnumConverter = true,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true
)]
[JsonSerializable(typeof(StartJobRequest))]
[JsonSerializable(typeof(StartedJobDto))]
[JsonSerializable(typeof(MoveJobRequest))]
[JsonSerializable(typeof(JobDto))]
[JsonSerializable(typeof(ImmutableArray<JobDto>))]
[JsonSerializable(typeof(CooldownDto))]
[JsonSerializable(typeof(SnapshotDto))]
[JsonSerializable(typeof(JobRemovedDto))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(SessionDto))]
[JsonSerializable(typeof(ProblemBody))]
[JsonSerializable(typeof(LogEntryDto))]
[JsonSerializable(typeof(ImmutableArray<LogEntryDto>))]
[JsonSerializable(typeof(StatusDto))]
public sealed partial class ApiJsonContext : JsonSerializerContext;
