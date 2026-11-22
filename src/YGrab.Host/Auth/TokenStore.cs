// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace YGrab.Host.Auth;

/// <summary>
/// Holds the token that the clients present, and compares against it in constant time.
/// </summary>
/// <remarks>
/// <para>
/// The token of the options wins. Without one, <see cref="Initialize"/> reads the token file of
/// the data folder, or creates it with a random token that only its owner can read.
/// </para>
/// <para>
/// The comparison hashes both sides first, so the length of the token does not leak.
/// </para>
/// </remarks>
internal sealed partial class TokenStore(
    IOptionsMonitor<AuthOptions> options,
    HostStorage storage,
    ILogger<TokenStore> logger
)
{
    internal const int GeneratedTokenBytes = 32;

    // A generated token belongs only in the token file and the console
    internal static readonly string LogCategory = typeof(TokenStore).FullName!;

    // A reload can remove the token of the options while the host runs
    private readonly Lazy<string> _fileToken = new(() => LoadOrCreate(storage, logger));

    /// <summary>
    /// Reads or creates the token file when the options hold no token.
    /// </summary>
    public void Initialize()
    {
        if (options.CurrentValue.Token is null)
            _ = _fileToken.Value;
    }

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

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static bool Equal(byte[] left, byte[] right) =>
        CryptographicOperations.FixedTimeEquals(left, right);

    private string Current() => options.CurrentValue.Token ?? _fileToken.Value;

    private static string LoadOrCreate(HostStorage storage, ILogger logger)
    {
        var existing = File.Exists(storage.TokenFile)
            ? File.ReadAllText(storage.TokenFile).Trim()
            : null;
        if (!string.IsNullOrWhiteSpace(existing))
            return existing;

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(GeneratedTokenBytes));
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
        LogGenerated(logger, storage.TokenFile, token);
        return token;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No auth token is set, so one was generated and saved in {Path}: {Token}"
    )]
    private static partial void LogGenerated(ILogger logger, string path, string token);
}
