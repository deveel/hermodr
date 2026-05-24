//
// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.
//

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Hermodr {
	/// <summary>
	/// A factory that creates an <see cref="EventSchema"/> by inspecting
	/// a CLR type annotated with <see cref="EventAttribute"/> and its
	/// members' validation attributes.
	/// </summary>
	/// <remarks>
	/// This class is the injectable counterpart of the static
	/// <see cref="EventSchema.FromDataType(Type)"/> convenience method.
	/// </remarks>
	public class EventSchemaFactory : IEventSchemaFactory {
		/// <summary>
		/// A shared default instance of <see cref="EventSchemaFactory"/>.
		/// </summary>
		public static readonly EventSchemaFactory Default = new EventSchemaFactory();

		/// <inheritdoc/>
		public EventSchema CreateFromType(Type dataType) {
			var attribute = dataType.GetCustomAttribute<EventAttribute>();
			if (attribute == null)
				throw new ArgumentException(
					$"The type {dataType.FullName} is not annotated with [{nameof(EventAttribute)}].",
					nameof(dataType));

			var version = attribute.DataVersion
				?? throw new ArgumentException(
					$"The [{nameof(EventAttribute)}] on {dataType.FullName} does not specify a version.",
					nameof(dataType));

			var contentType = attribute.ContentType ?? "object";

			var schema = new EventSchema(attribute.EventType, version, contentType) {
				Description = attribute.Description
			};

			foreach (var property in GetProperties(dataType, schema.Version)) {
				schema.Properties.Add(property);
			}

			return schema;
		}

		/// <inheritdoc/>
		public EventSchema CreateFromType<TData>() where TData : class
			=> CreateFromType(typeof(TData));

		// ─────────────────────────────────────────────────────────────────────
		// Private helpers
		// ─────────────────────────────────────────────────────────────────────

		private static string? GetEventVersion(MemberInfo member) {
			var baseType = member.DeclaringType;
			var attribute = baseType?.GetCustomAttribute<EventAttribute>();

			while (attribute == null && baseType != null) {
				baseType = baseType.BaseType;
				attribute = baseType?.GetCustomAttribute<EventAttribute>();
			}

			return attribute?.DataVersion;
		}

		private static IEnumerable<EventProperty> GetProperties(Type dataType, Version schemaVersion) {
			return dataType
				.GetMembers(BindingFlags.Instance | BindingFlags.Public)
				.Where(m => m.MemberType is MemberTypes.Property or MemberTypes.Field)
				.Select(m => CreateEventProperty(m, schemaVersion));
		}

		private static EventProperty CreateEventProperty(MemberInfo member, Version schemaVersion) {
			var propertyName = member.Name;
			var version = GetEventVersion(member);
			string? description = null;

			var attribute = member.GetCustomAttribute<EventPropertyAttribute>();
			if (attribute != null) {
				if (!String.IsNullOrWhiteSpace(attribute.Name))
					propertyName = attribute.Name!;
				if (!String.IsNullOrWhiteSpace(attribute.Version))
					version = attribute.Version;
				description = attribute.Description;
			}

			// Fall back to [JsonPropertyName] when no explicit name was given
			if (String.IsNullOrWhiteSpace(propertyName)) {
				var jsonAttr = member.GetCustomAttribute<JsonPropertyNameAttribute>();
				if (jsonAttr != null)
					propertyName = jsonAttr.Name;
			}

			if (String.IsNullOrWhiteSpace(propertyName))
				propertyName = member.Name;

			var memberType = GetMemberType(member);

			// Detect CLR-level nullability: Nullable<T> or nullable reference type
			var underlyingType = Nullable.GetUnderlyingType(memberType);
			bool isNullable = underlyingType != null || IsNullableReferenceType(member);

			var dataType = GetDataType(memberType);
			var property = new EventProperty(propertyName, dataType, version) {
				Description = description,
				IsNullable = isNullable
			};

			foreach (var constraint in GetConstraints(member, memberType))
				property.Constraints.Add(constraint);

			// Recurse into complex (non-primitive, non-enum) types
			if (!IsPrimitiveOrKnownType(memberType) && !memberType.IsEnum) {
				foreach (var subProperty in GetProperties(memberType, schemaVersion))
					property.Properties.Add(subProperty);
			}

			return property;
		}

		private static Type GetMemberType(MemberInfo member) =>
			member.MemberType == MemberTypes.Property
				? ((PropertyInfo) member).PropertyType
				: ((FieldInfo) member).FieldType;

		private static bool IsNullableReferenceType(MemberInfo member) {
			// Only meaningful for properties / fields on reference types
			try {
				var ctx = new NullabilityInfoContext();
				if (member is PropertyInfo pi)
					return ctx.Create(pi).WriteState == NullabilityState.Nullable;
				if (member is FieldInfo fi)
					return ctx.Create(fi).WriteState == NullabilityState.Nullable;
			} catch {
				// NullabilityInfoContext is best-effort
			}
			return false;
		}

		private static bool IsPrimitiveOrKnownType(Type type) {
			var t = Nullable.GetUnderlyingType(type) ?? type;
			return t == typeof(string)
				|| t == typeof(int)
				|| t == typeof(long)
				|| t == typeof(float)
				|| t == typeof(double)
				|| t == typeof(decimal)
				|| t == typeof(bool)
				|| t == typeof(Guid)
				|| t == typeof(DateTime)
				|| t == typeof(DateTimeOffset)
				|| t == typeof(TimeSpan)
				|| t == typeof(DateOnly)
				|| t == typeof(TimeOnly);
		}

		private static string GetDataType(Type propertyType) {
			var nullableType = Nullable.GetUnderlyingType(propertyType);
			if (nullableType != null)
				return GetDataType(nullableType);

			if (propertyType == typeof(string))   return "string";
			if (propertyType == typeof(int))      return "int";
			if (propertyType == typeof(long))     return "long";
			if (propertyType == typeof(float))    return "float";
			if (propertyType == typeof(double))   return "double";
			if (propertyType == typeof(decimal))  return "money";
			if (propertyType == typeof(bool))     return "boolean";
			if (propertyType == typeof(DateTime)) return "dateTime";
			if (propertyType == typeof(DateTimeOffset)) return "dateTimeOffset";
			if (propertyType == typeof(DateOnly)) return "date";
			if (propertyType == typeof(TimeOnly)) return "time";
			if (propertyType == typeof(TimeSpan)) return "duration";
			if (propertyType == typeof(Guid))     return "guid";

			if (propertyType.IsArray) {
				var elementType = propertyType.GetElementType()!;
				return $"{GetDataType(elementType)}[]";
			}

			// Handle generic collection types: IEnumerable<T>, List<T>, IList<T>, etc.
			if (propertyType.IsGenericType) {
				var genericDef = propertyType.GetGenericTypeDefinition();
				if (genericDef == typeof(IEnumerable<>)
					|| genericDef == typeof(ICollection<>)
					|| genericDef == typeof(IList<>)
					|| genericDef == typeof(IReadOnlyList<>)
					|| genericDef == typeof(IReadOnlyCollection<>)
					|| genericDef == typeof(List<>)) {
					var elementType = propertyType.GetGenericArguments()[0];
					return $"{GetDataType(elementType)}[]";
				}
			}

			if (propertyType.IsEnum) {
				// Enums are serialised as their string name by default
				return "string";
			}

			return propertyType.FullName!;
		}

		private static IEnumerable<IEventPropertyConstraint> GetConstraints(MemberInfo member, Type memberType) {
			foreach (var attribute in member.GetCustomAttributes(true)) {
				switch (attribute) {
					case RequiredAttribute:
						yield return new PropertyRequiredConstraint();
						break;
					case RangeAttribute range:
						yield return CreateRangeConstraint(memberType, range);
						break;
				}
			}

			if (IsEnumType(memberType, out var enumType))
				yield return CreateEnumConstraint(enumType);
		}

		private static bool IsEnumType(Type type, [MaybeNullWhen(false)] out Type enumType) {
			enumType = Nullable.GetUnderlyingType(type) ?? type;
			return enumType.IsEnum;
		}

		private static IEventPropertyConstraint CreateEnumConstraint(Type enumType) {
			var values = Enum.GetNames(enumType);
			return new EnumMemberConstraint<string>(values);
		}

		private static IEventPropertyConstraint CreateRangeConstraint(Type memberType, RangeAttribute range) {
			var effectiveType = Nullable.GetUnderlyingType(memberType) ?? memberType;

			if (range.Maximum != null && !effectiveType.IsInstanceOfType(range.Maximum))
				throw new ArgumentException("The maximum value is not compatible with the member type");
			if (range.Minimum != null && !effectiveType.IsInstanceOfType(range.Minimum))
				throw new ArgumentException("The minimum value is not compatible with the member type");

			var constraintType = typeof(RangeConstraint<>).MakeGenericType(effectiveType);
			return (IEventPropertyConstraint) Activator.CreateInstance(constraintType, range.Minimum, range.Maximum)!;
		}
	}
}



