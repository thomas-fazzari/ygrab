// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Infrastructure.Configuration;

namespace Microsoft.Extensions.Configuration;

public static class YGrabInfrastructureConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        /// <summary>
        /// The folder of the storage settings, where the host also keeps its own files.
        /// </summary>
        public string GetYGrabDataDirectory()
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
                ?? new StorageOptions();
            return storage.DataDirectory;
        }
    }
}
