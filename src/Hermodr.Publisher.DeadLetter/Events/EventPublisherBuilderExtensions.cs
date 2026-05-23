//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr;

/// <summary>
/// Extensions for the <see cref="EventPublisherBuilder"/> that add dead-letter handling.
/// </summary>
public static class EventPublisherBuilderExtensions
{
    /// <summary>
    /// Adds dead-letter handling to the current publisher pipeline.
    /// </summary>
    public static DeadLetterBuilder AddDeadLetter(this EventPublisherBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return new DeadLetterBuilder(builder);
    }

    /// <summary>
    /// Adds dead-letter handling to the current publisher pipeline and immediately
    /// applies the supplied configuration.
    /// </summary>
    public static DeadLetterBuilder AddDeadLetter(
        this EventPublisherBuilder builder,
        Action<DeadLetterBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var deadLetterBuilder = new DeadLetterBuilder(builder);
        configure(deadLetterBuilder);
        return deadLetterBuilder;
    }
}
