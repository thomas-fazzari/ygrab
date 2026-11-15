// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Configuration;
using YGrab.Host.Auth;
using YGrab.Infrastructure.Configuration;
using YGrab.Infrastructure.Python.Configuration;

namespace YGrab.Tests.Host;

public sealed class ExampleSettingsTests
{
    private const string FileName = "ygrab.example.yaml";

    [Fact]
    public void The_example_settings_name_every_option_with_its_default()
    {
        var text = File.ReadAllText(FileName);
        var configuration = new ConfigurationBuilder()
            .AddYamlFile(Path.GetFullPath(FileName), optional: false)
            .Build();

        configuration
            .GetSection(StorageOptions.SectionName)
            .Get<StorageOptions>()
            .Should()
            .BeNull("its options vary with the platform, so they stay in comments");
        configuration
            .GetSection(PythonOptions.SectionName)
            .Get<PythonOptions>()
            .Should()
            .BeNull("its options vary with the platform, so they stay in comments");
        configuration
            .GetSection(YtDlpOptions.SectionName)
            .Get<YtDlpOptions>()
            .Should()
            .BeEquivalentTo(new YtDlpOptions());
        configuration
            .GetSection(JobsOptions.SectionName)
            .Get<JobsOptions>()
            .Should()
            .BeEquivalentTo(new JobsOptions());
        configuration
            .GetSection(AuthOptions.SectionName)
            .Get<AuthOptions>()
            .Should()
            .BeEquivalentTo(new AuthOptions());
        foreach (
            var (section, type) in new[]
            {
                (StorageOptions.SectionName, typeof(StorageOptions)),
                (PythonOptions.SectionName, typeof(PythonOptions)),
                (YtDlpOptions.SectionName, typeof(YtDlpOptions)),
                (JobsOptions.SectionName, typeof(JobsOptions)),
                (AuthOptions.SectionName, typeof(AuthOptions)),
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
