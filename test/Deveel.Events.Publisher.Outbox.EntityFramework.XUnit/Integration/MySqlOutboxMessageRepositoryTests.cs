//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events.Integration;

/// <summary>
/// Runs <see cref="EntityOutboxMessageRepositoryTestsBase"/> tests against a real
/// MySQL database started by Testcontainers via <see cref="MySqlDatabaseFixture"/>.
/// </summary>
[Collection(MySqlDatabaseCollection.Name)]
[Trait("DisableCICD", "Windows")]
public class MySqlOutboxMessageRepositoryTests : EntityOutboxMessageRepositoryTestsBase
{
    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly MySqlDatabaseFixture _db;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MySqlOutboxMessageRepositoryTests(MySqlDatabaseFixture db)
    {
        _db = db;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    protected override OutboxDbContext CreateContext() => _db.CreateContext();
}

