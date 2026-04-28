//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.Linq.Expressions;

namespace Deveel.Events
{
    /// <summary>
    /// Non-generic marker interface implemented by <see cref="TypedEventDataFilter{TEvent}"/>,
    /// allowing visitors and serializers to inspect it without knowing the concrete type parameter.
    /// </summary>
    public interface ITypedEventDataFilter
    {
        /// <summary>Gets the CLR type the event data is deserialized into.</summary>
        Type EventType { get; }

        /// <summary>Gets the backing predicate as an untyped <see cref="LambdaExpression"/>.</summary>
        LambdaExpression PredicateExpression { get; }
    }
}

