// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Configuration;
using YGrab.Host.Auth;
using YGrab.Host.Proxy;
using YGrab.Infrastructure.Configuration;
using YGrab.Infrastructure.Python.Configuration;

namespace YGrab.Tests.Host;

public sealed class ExampleSettingsTests
{
    private const string FILE_NAME = "ygrab.example.yaml";

    [Fact]
    public void The_example_settings_name_every_option_with_its_default()
    {
        var text = File.ReadAllText(FILE_NAME);
        var configuration = new ConfigurationBuilder()
            .AddYamlFile(Path.GetFullPath(FILE_NAME), optional: false)
            .Build();

        configuration
            .GetSection(StorageOptions.SECTION_NAME)
            .Get<StorageOptions>()
            .Should()
            .BeNull("its options vary with the platform, so they stay in comments");
        configuration
            .GetSection(PythonOptions.SECTION_NAME)
            .Get<PythonOptions>()
            .Should()
            .BeNull("its options vary with the platform, so they stay in comments");
        configuration
            .GetSection(YtDlpOptions.SECTION_NAME)
            .Get<YtDlpOptions>()
            .Should()
            .BeEquivalentTo(new YtDlpOptions());
        configuration
            .GetSection(JobsOptions.SECTION_NAME)
            .Get<JobsOptions>()
            .Should()
            .BeEquivalentTo(new JobsOptions());
        configuration
            .GetSection(AuthOptions.SECTION_NAME)
            .Get<AuthOptions>()
            .Should()
            .BeEquivalentTo(new AuthOptions());
        configuration
            .GetSection(ProxyOptions.SECTION_NAME)
            .Get<ProxyOptions>()
            .Should()
            .BeEquivalentTo(new ProxyOptions());
        foreach (
            var (section, type) in new[]
            {
                (StorageOptions.SECTION_NAME, typeof(StorageOptions)),
                (PythonOptions.SECTION_NAME, typeof(PythonOptions)),
                (YtDlpOptions.SECTION_NAME, typeof(YtDlpOptions)),
                (JobsOptions.SECTION_NAME, typeof(JobsOptions)),
                (AuthOptions.SECTION_NAME, typeof(AuthOptions)),
                (ProxyOptions.SECTION_NAME, typeof(ProxyOptions)),
            }
        )
        {
            text.Should().Contain($"\n{section}:\n");
            foreach (var property in type.GetProperties().Where(p => p.SetMethod?.IsPublic == true))
            {
                text.Should().Contain($" {property.Name}:", $"{section} has {property.Name}");
            }
        }
    }
}
