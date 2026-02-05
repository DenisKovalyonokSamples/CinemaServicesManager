using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using CNM.Movies.API.Services;

namespace CNM.Movies.API.Controllers
{
    [ApiController]
    [Route("movies")]
    public class MoviesController : ControllerBase
    {
        private readonly IImdbClient _imdbClient;
        public MoviesController(IImdbClient imdbClient)
        {
            _imdbClient = imdbClient;
        }

        [HttpGet("ping")]
        public async Task<IActionResult> Ping()
        {
            var ok = await _imdbClient.PingAsync();
            return Ok(new { status = ok ? "ok" : "fail" });
        }

        [HttpGet("{imdbId}")]
        public async Task<IActionResult> GetById(string imdbId, [FromQuery] string apiKey)
        {
            var result = await _imdbClient.GetByIdAsync(imdbId, apiKey);
            return Ok(result);
        }
    }
}
