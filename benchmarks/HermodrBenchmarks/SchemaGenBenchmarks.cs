using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Hermodr;

using System.ComponentModel.DataAnnotations;

namespace HermodrBenchmarks
{
    [SimpleJob(RuntimeMoniker.Net60)]
    [SimpleJob(RuntimeMoniker.Net70)]
    [SimpleJob(RuntimeMoniker.Net80)]
    [MemoryDiagnoser]
    [RyuJitX64Job, RyuJitX86Job]
    public class SchemaGenBenchmarks
    {
        [Benchmark]
        public static void CreateSchemaFromData()
        {
            EventSchema.FromDataType<PersonCreated>();
        }

        [Benchmark]
        public static void ManualSchemaCreation()
        {
            var schema = new EventSchema("person.created", "2.0", "contentType")
            {
                Description = "A new person was created",
            };

            var nameProperty = new EventProperty("name", "string", "1.0")
            {
                Description = "The name of the person"
            };
            nameProperty.Constraints.Add(new PropertyRequiredConstraint());

            var ageProperty = new EventProperty("age", "int", "1.2")
            {
                Description = "The age of the person"
            };
            ageProperty.Constraints.Add(new RangeConstraint<int>(0, 110));

            schema.Properties.Add(nameProperty);
            schema.Properties.Add(ageProperty);
        }

        [Event("person.created", "2.0", Description = "A new person was created")]
        [EventAttributes("contentType", "application/json")]
        class PersonCreated
        {
            [EventProperty("name", "1.0", Description = "The name of the person")]
            [Required]
            public string Name { get; set; }

            [EventProperty("age", "1.2", Description = "The age of the person")]
            [Range(0, 110)]
            public int? Age { get; set; }
        }
    }
}
