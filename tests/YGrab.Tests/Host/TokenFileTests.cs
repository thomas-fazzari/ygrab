// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using YGrab.Host.Auth;
using YGrab.Tests.Fakes;

namespace YGrab.Tests.Host;

public sealed class TokenFileTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    private HostStorage Storage => new(_temp.FullName);

    public void Dispose() => _temp.Dispose();

    [Fact]
    [UnsupportedOSPlatform("windows")]
    public void A_created_token_is_readable_by_its_owner_only_and_kept_across_restarts()
    {
        var created = NewTokenFile().LoadOrCreate();

        NewTokenFile().LoadOrCreate().Should().Be(created);
        File.GetUnixFileMode(Storage.TokenFile)
            .Should()
            .Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task A_blank_token_file_gets_a_new_token()
    {
        var cancellation = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Storage.TokenFile, " \n", cancellation);

        var token = NewTokenFile().LoadOrCreate();

        token.Should().NotBeNullOrWhiteSpace();
        var saved = await File.ReadAllTextAsync(Storage.TokenFile, cancellation);
        saved.Trim().Should().Be(token);
    }

    private TokenFile NewTokenFile() => new(Storage, NullLogger<TokenFile>.Instance);
}
