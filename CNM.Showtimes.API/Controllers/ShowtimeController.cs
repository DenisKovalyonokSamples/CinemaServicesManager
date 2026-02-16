using CNM.Domain.Database; // keep this for DI registration
using CNM.Domain.Repositories;
using DomainDb = CNM.Domain.Interfaces;
using DomainEntities = CNM.Domain.Database.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CNM.Domain.Interfaces;

namespace CNM.Showtimes.API.Controllers
{
    #nullable enable
    [ApiController]
    [Route("showtime")]
    [ApiConventionType(typeof(DefaultApiConventions))]
    public class ShowtimeController : ControllerBase
    {
        private readonly MediatR.IMediator _mediator;
        [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
        public ShowtimeController(MediatR.IMediator mediator)
        {
            _mediator = mediator;
        }

        // Compatibility constructor for existing tests
        public ShowtimeController(IShowtimesRepository repository, IImdbClient imdbClient)
        {
            MediatR.ServiceFactory factory = type =>
            {
                // Map MediatR handler interface types to concrete handlers for compatibility with legacy tests
                if (type == typeof(MediatR.IRequestHandler<CNM.Application.UseCases.Showtimes.GetShowtimesQuery, IEnumerable<DomainEntities.ShowtimeEntity>>))
                    return new CNM.Application.UseCases.Showtimes.GetShowtimesHandler(repository);
                if (type == typeof(MediatR.IRequestHandler<CNM.Application.UseCases.Showtimes.CreateShowtimeCommand, DomainEntities.ShowtimeEntity>))
                    return new CNM.Application.UseCases.Showtimes.CreateShowtimeHandler(repository, imdbClient);
                if (type == typeof(MediatR.IRequestHandler<CNM.Application.UseCases.Showtimes.UpdateShowtimeCommand, DomainEntities.ShowtimeEntity>))
                    return new CNM.Application.UseCases.Showtimes.UpdateShowtimeHandler(repository, imdbClient);
                if (type == typeof(MediatR.IRequestHandler<CNM.Application.UseCases.Showtimes.DeleteShowtimeCommand, bool>))
                    return new CNM.Application.UseCases.Showtimes.DeleteShowtimeHandler(repository);
                // MediatR requests collections (e.g., IEnumerable<IPipelineBehavior<,>>) when building the pipeline.
                // Returning null causes ArgumentNullException("source") when enumerated; return empty arrays instead.
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    var elementType = type.GetGenericArguments()[0];
                    return Array.CreateInstance(elementType, 0);
                }
                return null;
            };
            _mediator = new MediatR.Mediator(factory);
        }

        [HttpGet]
        [Authorize(Policy = "Read")]
        [ProducesResponseType(typeof(List<DomainEntities.ShowtimeEntity>), 200)]
        public async Task<IActionResult> Get([FromQuery] DateTime? date, [FromQuery] string? title)
        {
            var showtimes = await _mediator.Send(new CNM.Application.UseCases.Showtimes.GetShowtimesQuery { Date = date, Title = title });
            return Ok(showtimes.ToList());
        }

        [HttpPost]
        [Authorize(Policy = "Write")]
        [ProducesResponseType(typeof(DomainEntities.ShowtimeEntity), 201)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        public async Task<IActionResult> Post([FromBody] DomainEntities.ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            if (!ModelState.IsValid)
            {
                // Use the first error as detail
                var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "Invalid request body.";
                return this.ProblemCompat(title: "Bad Request", detail: error, statusCode: 400, type: "https://httpstatuses.com/400");
            }
            if (payload == null)
                return this.ProblemCompat(title: "Bad Request", detail: "Request body required", statusCode: 400, type: "https://httpstatuses.com/400");
            if (payload.Movie == null || string.IsNullOrWhiteSpace(payload.Movie.ImdbId))
                return this.ProblemCompat(title: "Bad Request", detail: "movie.imdb_id required", statusCode: 400, type: "https://httpstatuses.com/400");
            if (string.IsNullOrWhiteSpace(imdbApiKey))
                return this.ProblemCompat(title: "Bad Request", detail: "imdb_api_key required", statusCode: 400, type: "https://httpstatuses.com/400");

            var createdShowtime = await _mediator.Send(new CNM.Application.UseCases.Showtimes.CreateShowtimeCommand { Payload = payload, ImdbApiKey = imdbApiKey });
            return Created($"/showtime/{createdShowtime.Id}", createdShowtime);
        }

        [HttpPut]
        [Authorize(Policy = "Write")]
        [ProducesResponseType(typeof(DomainEntities.ShowtimeEntity), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> Put([FromBody] DomainEntities.ShowtimeEntity payload, [FromQuery] string imdbApiKey)
        {
            var updatedShowtime = await _mediator.Send(new CNM.Application.UseCases.Showtimes.UpdateShowtimeCommand { Payload = payload, ImdbApiKey = imdbApiKey });
            if (updatedShowtime == null) return this.ProblemCompat(title: "Not Found", detail: "Showtime not found", statusCode: 404, type: "https://httpstatuses.com/404");
            return new OkObjectResult(updatedShowtime);
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "Write")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        public async Task<IActionResult> Delete(int id)
        {
            var deleted = await _mediator.Send(new CNM.Application.UseCases.Showtimes.DeleteShowtimeCommand { Id = id });
            if (!deleted) return this.ProblemCompat(title: "Not Found", detail: "Showtime not found", statusCode: 404, type: "https://httpstatuses.com/404");
            return NoContent();
        }

        [HttpPatch]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public IActionResult Patch()
        {
            throw new Exception("Test error");
        }
    }
}

namespace Microsoft.AspNetCore.Mvc
{
    // Helper to avoid NRE when ProblemDetailsFactory is missing in tests
    internal static class ControllerBaseProblemCompat
    {
        public static IActionResult ProblemCompat(this ControllerBase controller,
            string title = null,
            string detail = null,
            int? statusCode = null,
            string type = null)
        {
            var httpContext = controller.HttpContext;
            var services = httpContext?.RequestServices;
            ProblemDetails problem;

            try
            {
                var factory = services?.GetService(typeof(Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory))
                    as Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory;
                if (factory != null)
                {
                    problem = factory.CreateProblemDetails(httpContext,
                        statusCode: statusCode,
                        title: title,
                        type: type,
                        detail: detail);
                }
                else
                {
                    problem = new ProblemDetails
                    {
                        Status = statusCode,
                        Title = title,
                        Type = type,
                        Detail = detail
                    };
                }
            }
            catch
            {
                problem = new ProblemDetails
                {
                    Status = statusCode,
                    Title = title,
                    Type = type,
                    Detail = detail
                };
            }

            var objectResult = new ObjectResult(problem)
            {
                StatusCode = statusCode ?? 500
            };
            // Set correct content type for ProblemDetails
            objectResult.ContentTypes.Clear();
            objectResult.ContentTypes.Add("application/problem+json");
            return objectResult;
        }
    }
}
