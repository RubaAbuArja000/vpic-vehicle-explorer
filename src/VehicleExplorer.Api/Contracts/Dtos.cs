namespace VehicleExplorer.Api.Contracts;

// Public API contract (serialized as camelCase JSON). Deliberately decoupled from vPIC's shapes.

public sealed record MakeDto(int Id, string Name);

public sealed record VehicleTypeDto(int Id, string Name);

public sealed record ModelDto(int Id, string Name, string? VehicleType);
