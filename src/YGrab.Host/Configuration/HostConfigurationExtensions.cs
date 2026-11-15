// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.Extensions.Configuration.EnvironmentVariables;
using YGrab.Host.Auth;

namespace Microsoft.Extensions.Configuration;

internal static class HostConfigurationExtensions
{
    internal const string ConfigFileName = "ygrab.yaml";
    internal const string TokenVariable = "YGRAB_TOKEN";

    extension(IConfigurationBuilder configuration)
    {
        /// <summary>
        /// Adds the optional YAML file, which reloads on change, and the token variable.
        /// </summary>
        /// <remarks>
        /// The environment variables and the command line override the file, and the token
        /// variable overrides everything.
        /// </remarks>
        public IConfigurationBuilder AddYGrabConfiguration()
        {
            configuration.AddYamlFile(ConfigFileName, optional: true, reloadOnChange: true);
            MoveBeforeEnvironment(configuration.Sources);
            if (Environment.GetEnvironmentVariable(TokenVariable) is { } token)
            {
                configuration.AddInMemoryCollection([
                    KeyValuePair.Create<string, string?>(
                        $"{AuthOptions.SectionName}:{nameof(AuthOptions.Token)}",
                        token
                    ),
                ]);
            }

            return configuration;
        }
    }

    private static void MoveBeforeEnvironment(IList<IConfigurationSource> sources)
    {
        for (var index = sources.Count - 2; index >= 0; index--)
        {
            if (
                sources[index] is EnvironmentVariablesConfigurationSource environment
                && string.IsNullOrWhiteSpace(environment.Prefix)
            )
            {
                var last = sources[^1];
                sources.RemoveAt(sources.Count - 1);
                sources.Insert(index, last);
                return;
            }
        }
    }
}
