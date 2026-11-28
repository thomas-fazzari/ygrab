// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;

namespace YGrab.Infrastructure.Python.Configuration;

/// <summary>
/// Where the Python packages live.
/// </summary>
/// <remarks>
/// A change needs a restart, since CSnakes reads the environment once when it starts.
/// </remarks>
public sealed class PythonOptions
{
    public const string SECTION_NAME = "Python";

    /// <summary>
    /// The virtual environment that holds the packages of the Python modules.
    /// </summary>
    [Required]
    public string VirtualEnvironment { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ygrab",
            "python"
        );
}

[OptionsValidator]
internal sealed partial class PythonOptionsValidator : IValidateOptions<PythonOptions>;
