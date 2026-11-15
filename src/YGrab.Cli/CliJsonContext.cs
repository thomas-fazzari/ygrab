// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.Json.Serialization;
using YGrab.Cli.Configuration;

namespace YGrab.Cli;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(CliConfig))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
