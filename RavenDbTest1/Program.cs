using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Raven.Client.Documents;
using Raven.Client.Json.Serialization;
using Raven.Client.Json.Serialization.NewtonsoftJson;
using Raven.TestDriver;
using Xunit;
using Xunit.Abstractions;

namespace RavenDbTest1
{

    public class RavenDBTestDriver : RavenTestDriver
    {
        private readonly ITestOutputHelper _testOutputHelper;
        public RavenDBTestDriver(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            ConfigureServer(new TestServerOptions
            {
                DataDirectory = "C:\\RavenDBTestDir"
            });
        }
        protected override void PreInitialize(IDocumentStore documentStore)
        {
            documentStore.Conventions.MaxNumberOfRequestsPerSession = 50;
            documentStore.Conventions.IdentityPartsSeparator = '-';
            
            var serializationConventions = new NewtonsoftJsonSerializationConventions
            {
                CustomizeJsonSerializer = x => { x.ObjectCreationHandling = ObjectCreationHandling.Replace; },
            };
            serializationConventions.JsonContractResolver = new CustomContractResolver(serializationConventions);
            documentStore.Conventions.Serialization = serializationConventions;
        }

        [Fact]
        public void MyFirstTest()
        {
            var propertyValue = "test value";

            using var store = GetDocumentStore();
            
            using (var session = store.OpenSession())
            {
                session.Store(new TestDocument
                {
                    PropertyWithJsonPropertyAttribute = propertyValue,
                    PropertyWithoutJsonPropertyAttribute = propertyValue
                });
                session.SaveChanges();
            }

            WaitForIndexing(store);

            using (var session = store.OpenSession())
            {
                var query1 = session.Query<TestDocument>()
                    .Where(x => x.PropertyWithoutJsonPropertyAttribute == propertyValue).ToList();
                
                var query2 = session.Query<TestDocument>()
                    .Where(x => x.PropertyWithJsonPropertyAttribute == propertyValue).ToList();
                
                //WaitForUserToContinueTheTest(store);
                _testOutputHelper.WriteLine($"query1 count = {query1.Count}");
                _testOutputHelper.WriteLine($"query2 count = {query2.Count}");
                Assert.Single(query1);
                Assert.Single(query2);
            }
        }
    }

    public class TestDocument
    {
        [JsonProperty("propertyWithJsonPropertyAttribute")]
        public string PropertyWithJsonPropertyAttribute { get; set; }
        public string PropertyWithoutJsonPropertyAttribute { get; set; }
    }
    
    public class CustomContractResolver : DefaultRavenContractResolver
    {
        public CustomContractResolver(ISerializationConventions conventions) : base(conventions)
        {
        }
        public static bool CanWrite(MemberInfo memberInfo)
        {
            switch (memberInfo)
            {
                case PropertyInfo _:
                    return ((PropertyInfo)memberInfo).CanWrite;
                case FieldInfo _:
                    return true;
            }

            throw new NotSupportedException("Cannot calculate CanWrite on " + memberInfo);
        }

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            // Let the base class create all the properties. If [JsonProperty] attribute exists the name in the attribute will be assigned to the property
            var properties = base.CreateProperties(type, memberSerialization);

            if (type.FullName != typeof(TestDocument).FullName
                && (type.DeclaringType == null || type.DeclaringType.FullName != typeof(TestDocument).FullName)) return properties;
            
            // Now inspect each property and replace the name with the C# property name which is PascalCase
            foreach (var property in properties)
            {
                property.PropertyName = property.UnderlyingName;
            }

            return properties;
        }
    }
}