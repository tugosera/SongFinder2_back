using Microsoft.AspNetCore.Mvc;
using SongFinder2_back.Models;
using SongFinder2_back.Services;
using System.Threading.Tasks;
using System;
using SpotifyAPI.Web;

namespace SongFinder2_back.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MusicController : ControllerBase
    {
        private readonly SpotifyService _musicService;

        public MusicController(SpotifyService musicService)
        {
            _musicService = musicService;
        }

        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] MusicSearchRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Mood) || string.IsNullOrWhiteSpace(request.Genre))
            {
                return BadRequest("Mood and Genre are required.");
            }

            try
            {
                var tracks = await _musicService.GetRecommendationsAsync(request);
                return Ok(tracks);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (APIException ex)
            {
                return StatusCode(500, $"Spotify API error: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(500, $"Configuration error: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
