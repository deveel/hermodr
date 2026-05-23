//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

namespace Hermodr {
	/// <summary>
	/// A factory that creates an <see cref="EventSchema"/> by inspecting
	/// a CLR type annotated with <see cref="EventAttribute"/>.
	/// </summary>
	public interface IEventSchemaFactory {
		/// <summary>
		/// Creates a new <see cref="EventSchema"/> from the CLR type
		/// <paramref name="dataType"/>. The type must be decorated with
		/// <see cref="EventAttribute"/>.
		/// </summary>
		/// <param name="dataType">
		/// The CLR type whose properties and annotations describe the event.
		/// </param>
		/// <returns>
		/// A fully populated <see cref="EventSchema"/> for the event.
		/// </returns>
		/// <exception cref="ArgumentException">
		/// Thrown when <paramref name="dataType"/> is not decorated with
		/// <see cref="EventAttribute"/>.
		/// </exception>
		EventSchema CreateFromType(Type dataType);

		/// <summary>
		/// Creates a new <see cref="EventSchema"/> from the CLR type
		/// <typeparamref name="TData"/>. The type must be decorated with
		/// <see cref="EventAttribute"/>.
		/// </summary>
		/// <typeparam name="TData">
		/// The CLR type whose properties and annotations describe the event.
		/// </typeparam>
		/// <returns>
		/// A fully populated <see cref="EventSchema"/> for the event.
		/// </returns>
		EventSchema CreateFromType<TData>() where TData : class;
	}
}

