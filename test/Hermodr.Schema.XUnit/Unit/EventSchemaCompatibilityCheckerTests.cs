// Copyright (c) Antonello Provenzano and other contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for details.

namespace Hermodr
{
    [Trait("Category", "Unit")]
    [Trait("Layer", "Domain")]
    [Trait("Feature", "SchemaCompatibility")]
    public static class EventSchemaCompatibilityCheckerTests
    {
        private static IEventSchemaCompatibilityChecker CreateChecker()
            => new EventSchemaCompatibilityChecker();

        [Fact]
        public static void Should_BeFullCompatible_When_SchemasAreIdentical()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Full, result.Level);
            Assert.Empty(result.Changes);
        }

        [Fact]
        public static void Should_BeBackwardCompatible_When_RequiredPropertyAdded()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .AddProperty("email", p => p.OfType("string").Required())
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Backward, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.PropertyAdded, result.Changes[0].ChangeType);
            Assert.True(result.Changes[0].IsBackwardBreaking);
            Assert.False(result.Changes[0].IsForwardBreaking);
        }

        [Fact]
        public static void Should_BeFullCompatible_When_OptionalPropertyAdded()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .AddProperty("email", p => p.OfType("string"))
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Full, result.Level);
        }

        [Fact]
        public static void Should_BeForwardCompatible_When_PropertyRemoved()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .AddProperty("email", p => p.OfType("string"))
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Forward, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.PropertyRemoved, result.Changes[0].ChangeType);
            Assert.False(result.Changes[0].IsBackwardBreaking);
            Assert.True(result.Changes[0].IsForwardBreaking);
        }

        [Fact]
        public static void Should_BeNoneCompatible_When_PropertyTypeChanged()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("age", p => p.OfType("int"))
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("age", p => p.OfType("string"))
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.None, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.PropertyTypeChanged, result.Changes[0].ChangeType);
        }

        [Fact]
        public static void Should_BeBackwardCompatible_When_PropertyBecomesOptional()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("name", p => p.OfType("string"))
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Forward, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.PropertyRequirementChanged, result.Changes[0].ChangeType);
            Assert.False(result.Changes[0].IsBackwardBreaking);
            Assert.True(result.Changes[0].IsForwardBreaking);
        }

        [Fact]
        public static void Should_BeForwardCompatible_When_PropertyBecomesRequired()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("name", p => p.OfType("string"))
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("name", p => p.OfType("string").Required())
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Backward, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.PropertyRequirementChanged, result.Changes[0].ChangeType);
            Assert.True(result.Changes[0].IsBackwardBreaking);
            Assert.False(result.Changes[0].IsForwardBreaking);
        }

        [Fact]
        public static void Should_BeBackwardCompatible_When_RangeIsNarrowed()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("age", p => p.OfType("int").WithRange<int>(0, 150))
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("age", p => p.OfType("int").WithRange<int>(18, 120))
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Backward, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.ConstraintChanged, result.Changes[0].ChangeType);
            Assert.True(result.Changes[0].IsBackwardBreaking);
            Assert.False(result.Changes[0].IsForwardBreaking);
        }

        [Fact]
        public static void Should_BeForwardCompatible_When_RangeIsWidened()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("age", p => p.OfType("int").WithRange<int>(0, 150))
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("age", p => p.OfType("int").WithRange<int>(-10, 200))
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Forward, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.ConstraintChanged, result.Changes[0].ChangeType);
            Assert.False(result.Changes[0].IsBackwardBreaking);
            Assert.True(result.Changes[0].IsForwardBreaking);
        }

        [Fact]
        public static void Should_BeFullCompatible_When_RangeIsUnchanged()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("age", p => p.OfType("int").WithRange<int>(0, 150))
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("age", p => p.OfType("int").WithRange<int>(0, 150))
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Full, result.Level);
        }

        [Fact]
        public static void Should_BeBackwardCompatible_When_EnumValuesRemoved()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("status", p => p.OfType("string").WithAllowedValues(new[] { "active", "inactive", "pending" }))
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("status", p => p.OfType("string").WithAllowedValues(new[] { "active", "inactive" }))
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Backward, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.ConstraintChanged, result.Changes[0].ChangeType);
            Assert.True(result.Changes[0].IsBackwardBreaking);
            Assert.False(result.Changes[0].IsForwardBreaking);
        }

        [Fact]
        public static void Should_BeForwardCompatible_When_EnumValuesAdded()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("status", p => p.OfType("string").WithAllowedValues(new[] { "active", "inactive" }))
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("status", p => p.OfType("string").WithAllowedValues(new[] { "active", "inactive", "pending" }))
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Forward, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.ConstraintChanged, result.Changes[0].ChangeType);
            Assert.False(result.Changes[0].IsBackwardBreaking);
            Assert.True(result.Changes[0].IsForwardBreaking);
        }

        [Fact]
        public static void Should_BeNoneCompatible_When_EventTypeChanged()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .Build();

            var newer = EventSchema.Build("test.event.v2")
                .WithVersion("2.0")
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.None, result.Level);
            Assert.Single(result.Changes);
            Assert.Equal(SchemaChangeType.PropertyTypeChanged, result.Changes[0].ChangeType);
        }

        [Fact]
        public static void Should_DetectNestedSchemaChanges()
        {
            // Arrange
            var older = EventSchema.Build("test.event")
                .WithVersion("1.0")
                .AddProperty("address", p => p.OfType("object")
                    .AddProperty("city", c => c.OfType("string").Required()))
                .Build();

            var newer = EventSchema.Build("test.event")
                .WithVersion("2.0")
                .AddProperty("address", p => p.OfType("object")
                    .AddProperty("city", c => c.OfType("string").Required())
                    .AddProperty("zip", c => c.OfType("string").Required()))
                .Build();

            // Act
            var result = CreateChecker().Check(older, newer);

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Backward, result.Level);
            Assert.Contains(result.Changes, c => c.ChangeType == SchemaChangeType.PropertyAdded && c.PropertyPath == "address.zip");
        }

        [Fact]
        public static void Should_CheckCompatibility_ViaRegistryExtension()
        {
            // Arrange
            var registry = new EventSchemaRegistry(new EventSchemaFactory());
            registry.Register(CreateSchema("1.0"));
            registry.Register(CreateSchema("2.0"));

            // Act
            var result = registry.CheckCompatibility("test.event", "1.0", "2.0");

            // Assert
            Assert.Equal(SchemaCompatibilityLevel.Backward, result.Level);

            static EventSchema CreateSchema(string version)
            {
                var builder = EventSchema.Build("test.event")
                    .WithVersion(version)
                    .AddProperty("name", p => p.OfType("string").Required());

                if (version == "2.0")
                    builder.AddProperty("email", p => p.OfType("string").Required());

                return builder.Build();
            }
        }

        [Fact]
        public static void Should_Throw_When_SchemaVersionNotRegistered()
        {
            // Arrange
            var registry = new EventSchemaRegistry(new EventSchemaFactory());
            registry.Register(EventSchema.Build("test.event").WithVersion("1.0").Build());

            // Act & Assert
            Assert.Throws<ArgumentException>(() => registry.CheckCompatibility("test.event", "1.0", "2.0"));
        }
    }
}
