// SPDX-FileCopyrightText: 2026 Thomas Fazzari
// SPDX-License-Identifier: AGPL-3.0-or-later

using YGrab.Domain;

namespace YGrab.Engine.Jobs;

/// <summary>
/// The step that the job runner runs now, so that a removal can stop it.
/// </summary>
/// <remarks>
/// There is no lock: an atomic swap replaces the step, which never changes after its start.
/// </remarks>
internal sealed class ActiveStep
{
    private Registration? _current;

    /// <summary>
    /// Registers the step of a job until the result is disposed.
    /// </summary>
    /// <param name="runnerToken">The token that ends every step, which the step token follows.</param>
    public Registration Begin(JobId id, CancellationToken runnerToken)
    {
        var registration = new Registration(this, id, runnerToken);
        Interlocked.Exchange(ref _current, registration);
        return registration;
    }

    /// <summary>
    /// Cancels the step when it runs the job <paramref name="id"/>.
    /// </summary>
    public async ValueTask CancelAsync(JobId id)
    {
        if (Volatile.Read(ref _current) is { } current && current.Id == id)
            await current.CancelAsync();
    }

    internal sealed class Registration : IDisposable
    {
        private readonly ActiveStep _owner;
        private readonly CancellationTokenSource _source;

        internal Registration(ActiveStep owner, JobId id, CancellationToken runnerToken)
        {
            _owner = owner;
            _source = CancellationTokenSource.CreateLinkedTokenSource(runnerToken);
            Id = id;
        }

        public JobId Id { get; }

        public CancellationToken Token => _source.Token;

        public void Dispose()
        {
            Interlocked.CompareExchange(ref _owner._current, value: null, this);
            _source.Dispose();
        }

        internal async ValueTask CancelAsync()
        {
            try
            {
                await _source.CancelAsync();
            }
            catch (ObjectDisposedException)
            {
                // Step ended first, nothing left to cancel
            }
        }
    }
}
