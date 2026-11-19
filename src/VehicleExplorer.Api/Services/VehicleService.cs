using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VehicleExplorer.Api.Contracts;
using VehicleExplorer.Api.Vpic;

namespace VehicleExplorer.Api.Services;

public sealed class CacheOptions
{
    public const string Section = "Cache";

    /// <summary>The full make list (~12k rows, ~1 MB) changes rarely.</summary>
    public TimeSpan MakesTtl { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Vehicle types and models per make/year.</summary>
    public TimeSpan LookupTtl { get; set; } = TimeSpan.FromHours(6);
}

public interface IVehicleService
{
    Task<IReadOnlyList<MakeDto>> SearchMakesAsync(string? search, int limit, CancellationToken ct);
    Task<IReadOnlyList<VehicleTypeDto>> GetVehicleTypesAsync(int makeId, CancellationToken ct);
    Task<IReadOnlyList<ModelDto>> GetModelsAsync(int makeId, int year, string? vehicleType, CancellationToken ct);
}

/// <summary>
/// Application logic on top of vPIC: caching, de-duplication, sorting and mapping to our DTOs.
/// </summary>
public sealed class VehicleService(IVpicClient vpic, IMemoryCache cache, IOptions<CacheOptions> options) : IVehicleService
{
    private const string MakesCacheKey = "vpic:makes";
    private readonly CacheOptions _options = options.Value;

    public async Task<IReadOnlyList<MakeDto>> SearchMakesAsync(string? search, int limit, CancellationToken ct)
    {
        var makes = await GetAllMakesAsync(ct);
        var term = search?.Trim();

        IEnumerable<MakeDto> query = makes;
        if (!string.IsNullOrEmpty(term))
        {
            // Prefix matches first ("HON" -> HONDA before "ACHONDA ..."), then contains-matches.
            query = makes
                .Where(m => m.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase);
        }

        return query.Take(limit).ToList();
    }

    public Task<IReadOnlyList<VehicleTypeDto>> GetVehicleTypesAsync(int makeId, CancellationToken ct) =>
        GetOrCreateAsync($"vpic:types:{makeId}", _options.LookupTtl, async () =>
        {
            var types = await vpic.GetVehicleTypesForMakeAsync(makeId, ct);
            return (IReadOnlyList<VehicleTypeDto>)types
                .Where(t => !string.IsNullOrWhiteSpace(t.VehicleTypeName))
                .DistinctBy(t => t.VehicleTypeId)
                .Select(t => new VehicleTypeDto(t.VehicleTypeId, t.VehicleTypeName!.Trim()))
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });

    public Task<IReadOnlyList<ModelDto>> GetModelsAsync(int makeId, int year, string? vehicleType, CancellationToken ct)
    {
        var type = string.IsNullOrWhiteSpace(vehicleType) ? null : vehicleType.Trim();
        var key = $"vpic:models:{makeId}:{year}:{type?.ToLowerInvariant() ?? "*"}";

        return GetOrCreateAsync(key, _options.LookupTtl, async () =>
        {
            var models = await vpic.GetModelsAsync(makeId, year, type, ct);
            return (IReadOnlyList<ModelDto>)models
                .Where(m => !string.IsNullOrWhiteSpace(m.ModelName))
                // vPIC can return the same model more than once for a make/year.
                .DistinctBy(m => m.ModelId)
                .Select(m => new ModelDto(m.ModelId, m.ModelName!.Trim(), m.VehicleTypeName?.Trim()))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });
    }

    private Task<IReadOnlyList<MakeDto>> GetAllMakesAsync(CancellationToken ct) =>
        GetOrCreateAsync(MakesCacheKey, _options.MakesTtl, async () =>
        {
            var makes = await vpic.GetAllMakesAsync(ct);
            return (IReadOnlyList<MakeDto>)makes
                .Where(m => !string.IsNullOrWhiteSpace(m.MakeName))
                .Select(m => new MakeDto(m.MakeId, m.MakeName!.Trim()))
                .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });

    // Failures are not cached (the factory throws), so a vPIC outage doesn't poison the cache.
    // Empty results *are* cached: "no models for that year" is a valid, stable answer.
    private async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
        {
            return cached;
        }

        var value = await factory();
        cache.Set(key, value, ttl);
        return value;
    }
}
