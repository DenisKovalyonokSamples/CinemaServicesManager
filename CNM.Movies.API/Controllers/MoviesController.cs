using CNM.Domain.Interfaces;
using CNM.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CNM.Movies.API.Controllers
{
    [ApiController]
    [Route("movies")]
    public class MoviesController : ControllerBase
    {
        private readonly MoviesService _moviesService;
        public MoviesController(IImdbClient imdbClient)
        {
            _moviesService = new MoviesService(imdbClient);
        }

        [HttpGet("ping")]
        public async Task<IActionResult> Ping()
        {
            var ok = await _moviesService.PingAsync();
            return Ok(new { status = ok ? "ok" : "fail" });
        }

        [HttpGet("{imdbId}")]
        public async Task<IActionResult> GetById(string imdbId, [FromQuery] string apiKey)
        {
            var result = await _moviesService.GetByIdAsync(imdbId, apiKey);
            return Ok(result);
        }
    }
}
