// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;

namespace YGrab.Cli.Configuration;

internal sealed record CliConfig(string? Url = null, string? Token = null);

/// <summary>
/// The config file in the home folder, which environment overrides.
/// </summary>
internal static class CliConfigStore
{
    internal const string URL_VARIABLE = "YGRAB_URL";
    internal const string TOKEN_VARIABLE = "YGRAB_TOKEN";

    public static string FilePath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "ygrab",
            "config.json"
        );

    public static CliConfig Effective()
    {
        var file = Load();
        return new CliConfig(
            Variable(URL_VARIABLE) ?? file.Url,
            Variable(TOKEN_VARIABLE) ?? file.Token
        );
    }

    public static void Save(CliConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var options = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using var stream = new FileStream(FilePath, options);
        JsonSerializer.Serialize(stream, config, CliJsonContext.Default.CliConfig);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static string? Variable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static CliConfig Load()
    {
        if (!File.Exists(FilePath))
            return new CliConfig();

        using var stream = File.OpenRead(FilePath);
        return JsonSerializer.Deserialize(stream, CliJsonContext.Default.CliConfig)
            ?? new CliConfig();
    }
}
