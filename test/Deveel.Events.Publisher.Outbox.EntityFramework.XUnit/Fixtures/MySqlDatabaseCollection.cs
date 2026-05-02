//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Deveel.Events;

/// <summary>
/// xUnit v3 collection definition that groups all outbox Entity Framework
/// integration tests under a single shared <see cref="MySqlDatabaseFixture"/>
/// instance so that the MySQL container is started and stopped only once per
/// test run.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MySqlDatabaseCollection : ICollectionFixture<MySqlDatabaseFixture>
{
    public const string Name = "MySql:OutboxEntityFramework";
}

