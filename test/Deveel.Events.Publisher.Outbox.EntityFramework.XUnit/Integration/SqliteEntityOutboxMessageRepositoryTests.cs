//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events.Integration;

/// <summary>
/// Runs <see cref="EntityOutboxMessageRepositoryTestsBase"/> tests against an
/// SQLite in-memory database provided by <see cref="SqliteOutboxFixture"/>.
/// </summary>
/// <remarks>
/// This class runs the same full integration suite as
/// <see cref="MySqlOutboxMessageRepositoryTests"/> (MySQL) but without requiring a
/// Docker container, so it executes on every platform including Windows CI agents.
/// </remarks>
public class SqliteEntityOutboxMessageRepositoryTests
    : EntityOutboxMessageRepositoryTestsBase, IClassFixture<SqliteOutboxFixture>
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly SqliteOutboxFixture _db;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SqliteEntityOutboxMessageRepositoryTests(SqliteOutboxFixture db)
    {
        _db = db;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override OutboxDbContext CreateContext() => _db.CreateContext();
}

