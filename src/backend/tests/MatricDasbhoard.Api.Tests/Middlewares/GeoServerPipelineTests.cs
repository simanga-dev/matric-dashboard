using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using MatricDasbhoard.Api.Tests.Fixtures;
using Polly.Timeout;

namespace MatricDasbhoard.Api.Tests.Middlewares;

/// <summary>Exercises the production geo registration and middleware placement in the full API pipeline.</summary>
public class GeoServerPipelineTests
{
    [Fact]
    public void NamedClient_DisablesCookiesRedirectsAndDefaultCredentials()
    {
        using var factory = new CustomWebApplicationFactory();
        var handler = factory.Services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler("GeoServer");
        while (handler is DelegatingHandler delegating)
        {
            handler = Assert.IsAssignableFrom<HttpMessageHandler>(delegating.InnerHandler);
        }

        var primary = Assert.IsType<HttpClientHandler>(handler);
        Assert.False(primary.UseCookies);
        Assert.False(primary.AllowAutoRedirect);
        Assert.False(primary.UseDefaultCredentials);
        Assert.Null(primary.Credentials);
    }

    [Fact]
    public async Task GeoRequests_ConsumeGlobalRateLimitBeforeContactingUpstream()
    {
        using var handler = new CountingHandler();
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["GeoServer:Enabled"] = "true",
                    ["GeoServer:BaseUrl"] = "http://geoserver:8080/geoserver",
                    ["RateLimiting:Global:PermitLimit"] = "1",
                    ["RateLimiting:Global:Window"] = "01:00:00",
                    ["RateLimiting:Global:QueueLimit"] = "0"
                }));
            builder.ConfigureTestServices(services => services.AddHttpClient("GeoServer")
                .ConfigurePrimaryHttpMessageHandler(() => handler));
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        using var first = await client.GetAsync("/geoserver/wfs?service=WFS&request=GetFeature");
        using var second = await client.GetAsync("/geoserver/wfs?service=WFS&request=GetFeature");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(second.Headers.RetryAfter);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(429, problem?.Status);
        Assert.True(first.Headers.Contains("X-Content-Type-Options"));
        Assert.True(second.Headers.Contains("X-Content-Type-Options"));
    }

    [Fact]
    public async Task RegisteredStandardResilienceTimeout_Returns504Problem()
    {
        using var handler = new DelayedHandler();
        using var baseFactory = new CustomWebApplicationFactory();
        var timeoutTriggered = false;
        using var factory = CreateGeoFactory(baseFactory, handler, configureServices: services =>
            services.ConfigureAll<HttpStandardResilienceOptions>(options =>
            {
                options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(1);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
                options.AttemptTimeout.OnTimeout = _ =>
                {
                    timeoutTriggered = true;
                    return ValueTask.CompletedTask;
                };
            }));
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/geoserver/wfs?request=GetFeature");

        Assert.True(timeoutTriggered);
        Assert.Equal(1, handler.Calls);
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(504, (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Status);
    }

    [Fact]
    public async Task RegisteredStandardResilienceOpenCircuit_Returns502WithoutAnotherUpstreamCall()
    {
        using var handler = new ConnectionFailingHandler();
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = CreateGeoFactory(baseFactory, handler, configureServices: services =>
            services.ConfigureAll<HttpStandardResilienceOptions>(options =>
            {
                options.Retry.ShouldHandle = _ => ValueTask.FromResult(false);
                options.CircuitBreaker.MinimumThroughput = 2;
                options.CircuitBreaker.FailureRatio = 0.5;
            }));
        using var client = CreateClient(factory);

        for (var i = 0; i < 3; i++)
        {
            using var response = await client.GetAsync("/geoserver/wfs?request=GetFeature");
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(502, (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Status);
        }

        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("http")]
    [InlineData("io")]
    [InlineData("timeout")]
    [InlineData("canceled")]
    public async Task MidstreamUpstreamFailure_AbortsClientResponseInsteadOfCompletingTruncatedSuccess(string failure)
    {
        var releaseFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StreamingFailureHandler(failure, releaseFailure.Task);
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = CreateGeoFactory(baseFactory, handler);
        using var client = CreateClient(factory);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            using var response = await client.GetAsync("/geoserver/wfs?request=GetFeature",
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            var prefix = new byte[StreamingFailureContent.Prefix.Length];
            await stream.ReadExactlyAsync(prefix, deadline.Token);
            Assert.Equal(StreamingFailureContent.Prefix, prefix);

            releaseFailure.SetResult();
            var error = await Record.ExceptionAsync(async () =>
            {
                await stream.CopyToAsync(Stream.Null, deadline.Token);
            });

            Assert.NotNull(error);
            Assert.IsAssignableFrom<IOException>(error);
            Assert.False(deadline.IsCancellationRequested);
        }
        finally
        {
            releaseFailure.TrySetResult();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForwardedClients_GetSeparateRateBucketsOnlyFromConfiguredTrustedFrontend(bool trusted)
    {
        using var handler = new CountingHandler();
        using var baseFactory = new CustomWebApplicationFactory();
        using var factory = CreateGeoFactory(baseFactory, handler,
            new Dictionary<string, string?>
            {
                ["RateLimiting:Global:PermitLimit"] = "1",
                ["RateLimiting:Global:Window"] = "01:00:00",
                ["RateLimiting:Global:QueueLimit"] = "0",
                ["Hosting:ReverseProxy:TrustedProxies:0"] = trusted ? "10.20.30.40" : "10.20.30.41"
            }, services => services.AddSingleton<IStartupFilter>(new FrontendConnectionStartupFilter()));
        using var client = CreateClient(factory);

        using var firstClient = await SendForwardedRequestAsync(client, "203.0.113.10");
        using var firstClientAgain = await SendForwardedRequestAsync(client, "203.0.113.10");
        using var secondClient = await SendForwardedRequestAsync(client, "203.0.113.11");
        using var secondClientAgain = await SendForwardedRequestAsync(client, "203.0.113.11");

        Assert.Equal(HttpStatusCode.OK, firstClient.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, firstClientAgain.StatusCode);
        Assert.Equal(trusted ? HttpStatusCode.OK : HttpStatusCode.TooManyRequests, secondClient.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondClientAgain.StatusCode);
        Assert.Equal(trusted ? 2 : 1, handler.Calls);
    }

    private static async Task<HttpResponseMessage> SendForwardedRequestAsync(HttpClient client, string address)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/geoserver/wfs?request=GetFeature");
        request.Headers.Add("X-Forwarded-For", address);
        return await client.SendAsync(request);
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static WebApplicationFactory<Program> CreateGeoFactory(CustomWebApplicationFactory baseFactory,
        HttpMessageHandler handler, Dictionary<string, string?>? extraConfiguration = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["GeoServer:Enabled"] = "true",
            ["GeoServer:BaseUrl"] = "http://geoserver:8080/geoserver"
        };
        foreach (var setting in extraConfiguration ?? [])
        {
            configuration[setting.Key] = setting.Value;
        }

        return baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient("GeoServer").ConfigurePrimaryHttpMessageHandler(() => handler);
                configureServices?.Invoke(services);
            });
        });
    }

    private sealed class FrontendConnectionStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            // TestServer has no TCP peer. Supply the frontend's connection IP before HostingExtensions runs.
            app.Use(async (context, continuation) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("10.20.30.40");
                await continuation(context);
            });
            next(app);
        };
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class ConnectionFailingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("Upstream disconnected"));
        }
    }

    private sealed class StreamingFailureHandler(string failure, Task releaseFailure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamingFailureContent(failure, releaseFailure)
            });
    }

    private sealed class StreamingFailureContent(string failure, Task releaseFailure) : HttpContent
    {
        public static readonly byte[] Prefix = Encoding.UTF8.GetBytes("{\"features\":[");

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(Prefix);
            await stream.FlushAsync();
            await releaseFailure;
            throw failure switch
            {
                "http" => new HttpRequestException("Upstream disconnected midstream"),
                "io" => new IOException("Upstream body read failed"),
                "timeout" => new TimeoutRejectedException("Upstream body timed out"),
                _ => new OperationCanceledException("Upstream body timed out")
            };
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
