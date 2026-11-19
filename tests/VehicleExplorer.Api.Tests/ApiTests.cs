using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VehicleExplorer.Api.Contracts;
using VehicleExplorer.Api.Services;

namespace VehicleExplorer.Api.Tests;

/// <summary>HTTP-level tests: routing, validation and error mapping, with the service layer stubbed.</summary>
public sealed class ApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private HttpClient CreateClient(IVehicleService service) =>
        factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<IVehicleService>();
            s.AddSingleton(service);
        })).CreateClient();

    [Theory]
    [InlineData("/api/makes/474/models")]             // year missing
    [InlineData("/api/makes/474/models?year=1950")]   // too old
    [InlineData("/api/makes/474/models?year=3000")]   // too far ahead
    [InlineData("/api/makes?limit=0")]
    public async Task Invalid_input_returns_400_problem_details(string url)
    {
        var response = await CreateClient(new StubService()).GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Models_endpoint_passes_year_and_type_through()
    {
        var stub = new StubService();
        var models = await CreateClient(stub)
            .GetFromJsonAsync<List<ModelDto>>("/api/makes/474/models?year=2015&vehicleType=Passenger%20Car");

        Assert.Equal((474, 2015, "Passenger Car"), stub.LastModelsQuery);
        Assert.Equal("Accord", Assert.Single(models!).Name);
    }

    [Fact]
    public async Task Upstream_outage_maps_to_502()
    {
        var response = await CreateClient(new StubService { Fail = true }).GetAsync("/api/makes/448/vehicle-types");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_api_route_is_404_not_the_spa()
    {
        var response = await CreateClient(new StubService()).GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_is_up()
    {
        var response = await CreateClient(new StubService()).GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class StubService : IVehicleService
    {
        public bool Fail { get; init; }
        public (int, int, string?)? LastModelsQuery { get; private set; }

        public Task<IReadOnlyList<MakeDto>> SearchMakesAsync(string? search, int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MakeDto>>([new MakeDto(474, "HONDA")]);

        public Task<IReadOnlyList<VehicleTypeDto>> GetVehicleTypesAsync(int makeId, CancellationToken ct) =>
            Fail
                ? throw new Vpic.VpicUnavailableException("down")
                : Task.FromResult<IReadOnlyList<VehicleTypeDto>>([new VehicleTypeDto(2, "Passenger Car")]);

        public Task<IReadOnlyList<ModelDto>> GetModelsAsync(int makeId, int year, string? vehicleType, CancellationToken ct)
        {
            LastModelsQuery = (makeId, year, vehicleType);
            return Task.FromResult<IReadOnlyList<ModelDto>>([new ModelDto(1861, "Accord", "Passenger Car")]);
        }
    }
}
