//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Deveel.Events;

[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
[Trait("Feature", "DeadLetterEntityFramework")]
public static class DeadLetterEntityFrameworkRegistrationTests
{
    [Fact]
    public static void WithEntityFramework_RegistersStoreAndFactory()
    {
        var services = new ServiceCollection();

        services.AddEventPublisher()
            .AddDeadLetter()
            .WithEntityFramework(options => options.UseSqlite("Data Source=:memory:"))
            .WithReplay();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<EntityDeadLetterMessageStore<DbDeadLetterMessage>>());
        Assert.NotNull(provider.GetService<IDeadLetterMessageFactory<DbDeadLetterMessage>>());
        Assert.NotNull(provider.GetService<IDeadLetterMessageStore>());
        Assert.NotNull(provider.GetService<IDeadLetterMessageFactory>());
        Assert.NotNull(provider.GetService<IDeadLetterMessageReplayer>());
    }
}
