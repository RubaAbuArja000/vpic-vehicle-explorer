using System.Text.Json.Serialization;

namespace VehicleExplorer.Api.Vpic;

// Raw shapes returned by the NHTSA vPIC API. Kept internal to the Vpic namespace so the
// upstream naming (Make_ID, Model_Name, ...) never leaks into our public API contract.

public sealed record VpicResponse<T>(
    [property: JsonPropertyName("Count")] int Count,
    [property: JsonPropertyName("Message")] string? Message,
    [property: JsonPropertyName("Results")] IReadOnlyList<T>? Results);

public sealed record VpicMake(
    [property: JsonPropertyName("Make_ID")] int MakeId,
    [property: JsonPropertyName("Make_Name")] string? MakeName);

public sealed record VpicVehicleType(
    [property: JsonPropertyName("VehicleTypeId")] int VehicleTypeId,
    [property: JsonPropertyName("VehicleTypeName")] string? VehicleTypeName);

public sealed record VpicModel(
    [property: JsonPropertyName("Make_ID")] int MakeId,
    [property: JsonPropertyName("Make_Name")] string? MakeName,
    [property: JsonPropertyName("Model_ID")] int ModelId,
    [property: JsonPropertyName("Model_Name")] string? ModelName,
    [property: JsonPropertyName("VehicleTypeId")] int? VehicleTypeId,
    [property: JsonPropertyName("VehicleTypeName")] string? VehicleTypeName);
