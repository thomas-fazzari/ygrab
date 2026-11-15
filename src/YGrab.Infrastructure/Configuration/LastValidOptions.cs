// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace YGrab.Infrastructure.Configuration;

/// <summary>
/// The latest valid value of a section that reloads live.
/// </summary>
/// <remarks>
/// <para>
/// A reload that fails to bind or to validate logs a warning and keeps the previous value, so a
/// typo in the file does not stop the jobs.
/// </para>
/// <para>
/// The first read happens on creation and throws when the settings are invalid.
/// </para>
/// </remarks>
internal sealed partial class LastValidOptions<T>(
    IOptionsMonitor<T> monitor,
    ILogger<LastValidOptions<T>> logger
)
    where T : class
{
    public T Current
    {
        get
        {
            try
            {
                field = monitor.CurrentValue;
            }
            catch (Exception exception)
                when (exception is OptionsValidationException or InvalidOperationException)
            {
                LogInvalid(typeof(T).Name, exception);
            }

            return field;
        }
    } = monitor.CurrentValue;

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "The {Section} settings are invalid, so the previous ones stay in use"
    )]
    private partial void LogInvalid(string section, Exception exception);
}
