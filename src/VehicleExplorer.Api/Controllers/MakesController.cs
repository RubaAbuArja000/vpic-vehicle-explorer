using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using VehicleExplorer.Api.Contracts;
using VehicleExplorer.Api.Services;

namespace VehicleExplorer.Api.Controllers;

[ApiController]
[Route("api/makes")]
[Produces("application/json")]
public sealed class MakesController(IVehicleService vehicles, TimeProvider clock) : ControllerBase
{
    /// <summary>vPIC model-year data is only meaningful from the 17-character VIN era onwards.</summary>
    public const int MinYear = 1981;

    /// <summary>Searches makes by name. The full list is ~12k entries, so it is filtered server-side.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MakeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MakeDto>>> Search(
        [FromQuery, StringLength(100)] string? search,
        [FromQuery, Range(1, 100)] int limit = 20,
        CancellationToken ct = default) =>
        Ok(await vehicles.SearchMakesAsync(search, limit, ct));

    [HttpGet("{makeId:int:min(1)}/vehicle-types")]
    [ProducesResponseType<IReadOnlyList<VehicleTypeDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehicleTypeDto>>> GetVehicleTypes(int makeId, CancellationToken ct) =>
        Ok(await vehicles.GetVehicleTypesAsync(makeId, ct));

    [HttpGet("{makeId:int:min(1)}/models")]
    [ProducesResponseType<IReadOnlyList<ModelDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ModelDto>>> GetModels(
        int makeId,
        [FromQuery, Required] int? year,
        [FromQuery, StringLength(100)] string? vehicleType,
        CancellationToken ct)
    {
        var maxYear = clock.GetUtcNow().Year + 1; // next year's models are usually already listed
        if (year < MinYear || year > maxYear)
        {
            ModelState.AddModelError(nameof(year), $"Year must be between {MinYear} and {maxYear}.");
            return ValidationProblem(ModelState);
        }

        return Ok(await vehicles.GetModelsAsync(makeId, year!.Value, vehicleType, ct));
    }
}
