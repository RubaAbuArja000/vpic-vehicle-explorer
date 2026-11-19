using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VehicleExplorer.Api.Services;
using VehicleExplorer.Api.Vpic;

namespace VehicleExplorer.Api.Tests;

public sealed class VehicleServiceTests
{
    private readonly FakeVpicHandler _handler = new();

    private VehicleService CreateService()
    {
        var http = new HttpClient(_handler) { BaseAddress = new Uri("https://vpic.test/api/vehicles/") };
        var client = new VpicClient(http, NullLogger<VpicClient>.Instance);
        return new VehicleService(client, new MemoryCache(new MemoryCacheOptions()), Options.Create(new CacheOptions()));
    }

    [Fact]
    public async Task SearchMakes_ranks_prefix_matches_before_contains_matches_and_respects_limit()
    {
        _handler.Respond("getallmakes", FakeVpicHandler.Results("""
            [{"Make_ID":1,"Make_Name":"ACHONDA MOTORS"},
             {"Make_ID":474,"Make_Name":"HONDA"},
             {"Make_ID":2,"Make_Name":"TOYOTA"},
             {"Make_ID":3,"Make_Name":"HON-TECH"},
             {"Make_ID":4,"Make_Name":"  "}]
            """));

        var result = await CreateService().SearchMakesAsync("hon", limit: 2, CancellationToken.None);

        Assert.Equal(["HON-TECH", "HONDA"], result.Select(m => m.Name));
    }

    [Fact]
    public async Task SearchMakes_caches_the_full_make_list()
    {
        _handler.Respond("getallmakes", FakeVpicHandler.Results("""[{"Make_ID":474,"Make_Name":"HONDA"}]"""));
        var service = CreateService();

        await service.SearchMakesAsync("ho", 10, CancellationToken.None);
        await service.SearchMakesAsync("hond", 10, CancellationToken.None);

        Assert.Single(_handler.Requests);
    }

    [Fact]
    public async Task GetModels_dedupes_by_model_id_and_sorts_by_name()
    {
        _handler.Respond("GetModelsForMakeIdYear/makeId/474/modelyear/2015", FakeVpicHandler.Results("""
            [{"Make_ID":474,"Make_Name":"HONDA","Model_ID":2,"Model_Name":"Pilot"},
             {"Make_ID":474,"Make_Name":"HONDA","Model_ID":1,"Model_Name":"Accord"},
             {"Make_ID":474,"Make_Name":"HONDA","Model_ID":1,"Model_Name":"Accord"}]
            """));

        var result = await CreateService().GetModelsAsync(474, 2015, null, CancellationToken.None);

        Assert.Equal(["Accord", "Pilot"], result.Select(m => m.Name));
    }

    [Fact]
    public async Task GetModels_url_encodes_vehicle_type_names_with_spaces_and_parentheses()
    {
        _handler.Respond("vehicletype/", FakeVpicHandler.Results("[]"));

        await CreateService().GetModelsAsync(474, 2015, "Multipurpose Passenger Vehicle (MPV)", CancellationToken.None);

        Assert.Contains("/vehicletype/Multipurpose%20Passenger%20Vehicle%20%28MPV%29", Assert.Single(_handler.Requests));
    }

    [Fact]
    public async Task GetModels_caches_per_make_year_and_type()
    {
        _handler.Respond("GetModelsForMakeIdYear", FakeVpicHandler.Results("[]"));
        var service = CreateService();

        await service.GetModelsAsync(474, 2015, "car", CancellationToken.None);
        await service.GetModelsAsync(474, 2015, "CAR ", CancellationToken.None); // same key after normalisation
        await service.GetModelsAsync(474, 2016, "car", CancellationToken.None);

        Assert.Equal(2, _handler.Requests.Count);
    }

    [Fact]
    public async Task Upstream_failure_throws_VpicUnavailable_and_is_not_cached()
    {
        _handler.Respond("GetVehicleTypesForMakeId", "oops", HttpStatusCode.InternalServerError);
        var service = CreateService();

        await Assert.ThrowsAsync<VpicUnavailableException>(() => service.GetVehicleTypesAsync(448, CancellationToken.None));

        _handler.Respond("GetVehicleTypesForMakeId", FakeVpicHandler.Results("""[{"VehicleTypeId":2,"VehicleTypeName":"Passenger Car"}]"""));
        var types = await service.GetVehicleTypesAsync(448, CancellationToken.None);

        Assert.Equal("Passenger Car", Assert.Single(types).Name);
    }
}
