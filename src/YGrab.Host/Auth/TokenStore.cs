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

    private string? _generated;

    /// <summary>
    /// Reads or creates the token file when the options hold no token.
    /// </summary>
    public void Initialize()
    {
        if (options.CurrentValue.Token is null)
        {
            Volatile.Write(ref _generated, LoadOrCreate());
        }
    }

    public bool Matches(string? candidate) =>
        candidate is not null && Current() is { } current && Equal(Hash(candidate), Hash(current));

    public bool MatchesHash(string base64Hash)
    {
        var hash = new byte[SHA256.HashSizeInBytes];
        return Current() is { } current
            && Convert.TryFromBase64String(base64Hash, hash, out var written)
            && written == hash.Length
            && Equal(hash, Hash(current));
    }

    public byte[]? CurrentHash() => Current() is { } current ? Hash(current) : null;

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static bool Equal(byte[] left, byte[] right) =>
        CryptographicOperations.FixedTimeEquals(left, right);

    private string? Current() => options.CurrentValue.Token ?? Volatile.Read(ref _generated);

    private string LoadOrCreate()
    {
        var existing = File.Exists(storage.TokenFile)
            ? File.ReadAllText(storage.TokenFile).Trim()
            : null;
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(GeneratedTokenBytes));
        Directory.CreateDirectory(storage.DataDirectory);
        var fileOptions = new FileStreamOptions
        {
            Mode = FileMode.Create,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!OperatingSystem.IsWindows())
        {
            fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

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
