using System.Net.Http.Json;

namespace VehicleExplorer.Api.Vpic;

public interface IVpicClient
{
    Task<IReadOnlyList<VpicMake>> GetAllMakesAsync(CancellationToken ct);
    Task<IReadOnlyList<VpicVehicleType>> GetVehicleTypesForMakeAsync(int makeId, CancellationToken ct);
    Task<IReadOnlyList<VpicModel>> GetModelsAsync(int makeId, int year, string? vehicleType, CancellationToken ct);
}

/// <summary>Thrown when vPIC cannot be reached or returns something we can't use.</summary>
public sealed class VpicUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>
/// Thin typed HttpClient over vPIC. Only responsible for building URLs and deserializing;
/// caching, sorting and mapping live in <see cref="Services.VehicleService"/>.
/// Retries/timeouts come from the standard resilience handler registered in Program.cs.
/// </summary>
public sealed class VpicClient(HttpClient http, ILogger<VpicClient> logger) : IVpicClient
{
    public Task<IReadOnlyList<VpicMake>> GetAllMakesAsync(CancellationToken ct) =>
        GetResultsAsync<VpicMake>("getallmakes?format=json", ct);

    public Task<IReadOnlyList<VpicVehicleType>> GetVehicleTypesForMakeAsync(int makeId, CancellationToken ct) =>
        GetResultsAsync<VpicVehicleType>($"GetVehicleTypesForMakeId/{makeId}?format=json", ct);

    public Task<IReadOnlyList<VpicModel>> GetModelsAsync(int makeId, int year, string? vehicleType, CancellationToken ct)
    {
        var path = $"GetModelsForMakeIdYear/makeId/{makeId}/modelyear/{year}";
        if (!string.IsNullOrWhiteSpace(vehicleType))
        {
            // Type names contain spaces and parentheses, e.g. "Multipurpose Passenger Vehicle (MPV)".
            path += $"/vehicletype/{Uri.EscapeDataString(vehicleType.Trim())}";
        }
        return GetResultsAsync<VpicModel>(path + "?format=json", ct);
    }

    private async Task<IReadOnlyList<T>> GetResultsAsync<T>(string relativeUrl, CancellationToken ct)
    {
        try
        {
            var response = await http.GetFromJsonAsync<VpicResponse<T>>(relativeUrl, ct);
            return response?.Results ?? [];
        }
        // Covers HttpRequestException, JSON errors, and Polly's timeout / circuit-breaker exceptions.
        // A cancelled *caller* token (client navigated away) is not an upstream failure, so it propagates unchanged.
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "vPIC request failed for {Url}", relativeUrl);
            throw new VpicUnavailableException("The NHTSA vPIC service is currently unavailable.", ex);
        }
    }
}
