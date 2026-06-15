//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr
{
    /// <summary>
    /// Defines the behavior when schema validation fails.
    /// </summary>
    public enum ValidationFailureBehavior
    {
        /// <summary>
        /// Throw a <see cref="SchemaValidationException"/> when validation fails.
        /// This is the default and recommended behavior for fail-fast validation.
        /// </summary>
        Throw = 0,

        /// <summary>
        /// Log the validation error but continue with publishing.
        /// NOT RECOMMENDED for production use, as it defeats the purpose of validation.
        /// </summary>
        Log = 1
    }
}
