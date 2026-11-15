// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using CSnakes.Runtime;
using Microsoft.Extensions.Configuration;
using YGrab.Engine.Jobs;
using YGrab.Infrastructure.Python.Catalog;
using YGrab.Infrastructure.Python.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

public static class YGrabPythonServiceCollectionExtensions
{
    /// <summary>
    /// The version of the CPython redistributable, which <c>requires-python</c> in
    /// <c>Python/pyproject.toml</c> accepts.
    /// </summary>
    internal const string PythonVersion = "3.13";

    private const string RequirementsFileName = "requirements.txt";

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the Python environment and the catalog.
        /// </summary>
        /// <remarks>
        /// The environment is built before the host, so it reads its options from
        /// <paramref name="configuration"/> here.
        /// </remarks>
        public IServiceCollection AddYGrabPython(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services
                .AddOptions<PythonOptions>()
                .BindConfiguration(PythonOptions.SectionName, FailOnUnknownKeys)
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<PythonOptions>, PythonOptionsValidator>();

            var options = configuration.GetSection(PythonOptions.SectionName).Get<PythonOptions>()
                ?? new PythonOptions();
            var home = Path.Combine(AppContext.BaseDirectory, "Python");
            var requirements = Path.Combine(home, RequirementsFileName);
            // CSnakes only warns when the file is missing and then starts without the packages
            if (!File.Exists(requirements))
            {
                throw new FileNotFoundException(
                    $"The Python requirements file is missing: {requirements}",
                    requirements
                );
            }

            services
                .WithPython()
                .WithHome(home)
                .FromRedistributable(PythonVersion)
                .WithVirtualEnvironment(options.VirtualEnvironment)
                .WithUvInstaller(requirements);

            services.AddSingleton<ICatalog, YtMusicCatalog>();
            return services;
        }
    }

    private static void FailOnUnknownKeys(BinderOptions options) =>
        options.ErrorOnUnknownConfiguration = true;
}
