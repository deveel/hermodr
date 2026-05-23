//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Hermodr;

/// <summary>
/// A fluent builder that configures dead-letter replay services and storage.
/// </summary>
public sealed class DeadLetterReplayBuilder
{
    private readonly EventPublisherBuilder _publisherBuilder;

    internal DeadLetterReplayBuilder(EventPublisherBuilder publisherBuilder)
    {
        _publisherBuilder = publisherBuilder;
        Services.AddOptions<DeadLetterReplayOptions>();
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services => _publisherBuilder.Services;

    /// <summary>
    /// Configures on-demand replay through the publisher pipeline.
    /// </summary>
    internal DeadLetterReplayBuilder ConfigureReplay(Action<DeadLetterReplayOptions>? configure = null)
    {
        UseInMemoryDefaults();

        if (configure != null)
            Services.Configure(configure);

        Services.PostConfigure<EventPublisherOptions>(opts => opts.ThrowOnErrors = true);
        Services.TryAddSingleton<IDeadLetterMessageReplayer, DeadLetterMessageReplayer>();
        return this;
    }

    /// <summary>
    /// Configures a background worker that replays persisted dead-letter messages.
    /// </summary>
    internal DeadLetterReplayBuilder ConfigureReplayWorker(Action<DeadLetterReplayOptions>? configure = null)
    {
        ConfigureReplay(configure);
        Services.TryAddSingleton<DeadLetterReplayProcessor>();
        Services.TryAddSingleton<IDeadLetterReplayProcessor>(sp => sp.GetRequiredService<DeadLetterReplayProcessor>());
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, DeadLetterReplayService>());
        return this;
    }

    internal DeadLetterReplayBuilder UseInMemoryDefaults()
    {
        new DeadLetterBuilder(_publisherBuilder).EnsureStorageHandlerRegistered();
        Services.TryAddSingleton<InMemoryDeadLetterMessageStore>();
        Services.TryAddSingleton<IDeadLetterMessageFactory<DeadLetterMessage>, DefaultDeadLetterMessageFactory>();
        Services.TryAddSingleton<IDeadLetterMessageStore>(sp => sp.GetRequiredService<InMemoryDeadLetterMessageStore>());
        Services.TryAddSingleton<IDeadLetterMessageFactory>(sp =>
            new DeadLetterMessageFactoryAdapter<DeadLetterMessage>(sp.GetRequiredService<IDeadLetterMessageFactory<DeadLetterMessage>>()));
        return this;
    }
}
