// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace YGrab.Host.Auth;

internal sealed class TokenStore(IOptionsMonitor<AuthOptions> options, TokenFile file)
    : IHostedService
{
    // A reload can remove the token of the options while the host runs
    private readonly Lazy<string> _fileToken = new(file.LoadOrCreate);

    // A generated token shows in the log before the first request
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        if (!current.Disabled && current.Token is null)
            _ = _fileToken.Value;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public bool Matches(string? candidate) => MatchingHash(candidate) is not null;

    public byte[]? MatchingHash(string? candidate)
    {
        if (candidate is null)
            return null;

        var hash = Hash(candidate);
        return Equal(hash, Hash(Current())) ? hash : null;
    }

    public bool MatchesHash(string base64Hash)
    {
        var hash = new byte[SHA256.HashSizeInBytes];
        return Convert.TryFromBase64String(base64Hash, hash, out var written)
            && written == hash.Length
            && Equal(hash, Hash(Current()));
    }

    // Hashing both sides keeps the length of the token from leaking
    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static bool Equal(byte[] left, byte[] right) =>
        CryptographicOperations.FixedTimeEquals(left, right);

    private string Current() => options.CurrentValue.Token ?? _fileToken.Value;
}

internal sealed partial class TokenFile(HostStorage storage, ILogger<TokenFile> logger)
{
    internal const int GENERATED_TOKEN_BYTES = 32;

    // A generated token belongs only in the token file and the console
    internal static readonly string LogCategory = typeof(TokenFile).FullName!;

    public string LoadOrCreate()
    {
        var existing = File.Exists(storage.TokenFile)
            ? File.ReadAllText(storage.TokenFile).Trim()
            : null;
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(GENERATED_TOKEN_BYTES));
        Directory.CreateDirectory(storage.DataDirectory);
        var fileOptions = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
            fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using var stream = new FileStream(storage.TokenFile, fileOptions);
        stream.Write(Encoding.UTF8.GetBytes(token + Environment.NewLine));
        LogGenerated(storage.TokenFile, token);
        return token;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No auth token is set, so one was generated and saved in {Path}: {Token}"
    )]
    private partial void LogGenerated(string path, string token);
}
