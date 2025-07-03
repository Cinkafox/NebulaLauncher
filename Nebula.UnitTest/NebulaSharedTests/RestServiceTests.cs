using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Nebula.Shared.Services;

namespace Nebula.UnitTest.NebulaSharedTests;

[TestFixture]
[TestOf(typeof(RestService))]
public class RestServiceTests : BaseSharedTest
{
    public static readonly TestDto ExpectedObject = new()
    {
        Id = 1,
        Name = "Test",
    };
    
    public static string ObjectString => JsonSerializer.Serialize(ExpectedObject, SerializerOptions);

    public static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    
    public override void BeforeServiceBuild(IServiceCollection services)
    {
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(ObjectString, Encoding.UTF8, "application/json")
            });

        var client = new HttpClient(mockHandler.Object);

        services.AddSingleton(client);
    }

    [SetUp]
    public override void Setup()
    {
        base.Setup();
    }

    [Test]
    public async Task GetTest()
    {
        var restService = _sharedUnit.GetService<RestService>();
        var result = await restService.GetAsync<TestDto>(new Uri("http://localhost/test"), CancellationToken.None);
        
        Assert.NotNull(result);
        Assert.That(result.Id, Is.EqualTo(ExpectedObject.Id));
        Assert.That(result.Name, Is.EqualTo(ExpectedObject.Name));
    }

    [Test]
    public async Task PostTest()
    {
        var restService = _sharedUnit.GetService<RestService>();
        var result = await restService.PostAsync<TestDto, TestDto>(ExpectedObject,new Uri("http://localhost/test"), CancellationToken.None);
        
        Assert.NotNull(result);
        Assert.That(result.Id, Is.EqualTo(ExpectedObject.Id));
        Assert.That(result.Name, Is.EqualTo(ExpectedObject.Name));
    }
    
    public class TestDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}