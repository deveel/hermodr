//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Deveel.Data;

namespace Deveel.Events;

/// <summary>
/// A deterministic, frozen <see cref="ISystemTime"/> implementation for use in
/// integration tests.  The clock is fixed at construction time and never advances,
/// so any timestamps written by the repository during a test are fully predictable
/// and can be compared in assertions without rounding issues.
/// </summary>
public sealed class TestSystemTime : ISystemTime
{
    private static readonly DateTimeOffset DefaultUtcNow = new(2026, 01, 15, 12, 00, 00, TimeSpan.Zero);

    /// <summary>
    /// Initialises a new instance with the supplied fixed instant as both
    /// <see cref="UtcNow"/> and <see cref="Now"/> (in local time).
    /// </summary>
    /// <param name="utcNow">The frozen UTC instant.  Defaults to a well-known
    /// round second so that database round-trips do not alter the value.</param>
    public TestSystemTime(DateTimeOffset? utcNow = null)
    {
        UtcNow = (utcNow ?? DefaultUtcNow).ToUniversalTime();
        // Truncate to whole seconds to survive MySQL DATETIME(0) storage.
        UtcNow = new DateTimeOffset(
            UtcNow.Year, UtcNow.Month, UtcNow.Day,
            UtcNow.Hour, UtcNow.Minute, UtcNow.Second,
            TimeSpan.Zero);
    }

    /// <inheritdoc/>
    public DateTimeOffset UtcNow { get; }

    /// <inheritdoc/>
    public DateTimeOffset Now => UtcNow.ToLocalTime();
}

