using Microsoft.Extensions.Configuration;
using SongFinder2_back.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;
using System;

namespace SongFinder2_back.Services
{
    public class SpotifyService
    {
        private readonly string _clientId;
        private readonly string _clientSecret;

        // Static cache to avoid requesting a new token for every user search
        private static string? _cachedToken;
        private static DateTime _tokenExpiry = DateTime.MinValue;
        private static readonly object _lock = new object();

        public SpotifyService(IConfiguration configuration)
        {
            _clientId = configuration["Spotify:ClientId"] ?? throw new ArgumentNullException("Spotify:ClientId");
            _clientSecret = configuration["Spotify:ClientSecret"] ?? throw new ArgumentNullException("Spotify:ClientSecret");
        }

        private async Task<SpotifyClient> GetSpotifyClientAsync()
        {
            lock (_lock)
            {
                if (_cachedToken != null && DateTime.Now < _tokenExpiry)
                {
                    var config = SpotifyClientConfig.CreateDefault().WithToken(_cachedToken);
                    return new SpotifyClient(config);
                }
            }

            // If we're here, we need a new token
            var configForAuth = SpotifyClientConfig.CreateDefault();
            var authRequest = new ClientCredentialsRequest(_clientId, _clientSecret);
            var oauth = new OAuthClient(configForAuth);
            
            try
            {
                var response = await oauth.RequestToken(authRequest);
                lock (_lock)
                {
                    _cachedToken = response.AccessToken;
                    // Expire slightly early to be safe (5 minutes early)
                    _tokenExpiry = DateTime.Now.AddSeconds(response.ExpiresIn).AddMinutes(-5);
                }
                return new SpotifyClient(configForAuth.WithToken(response.AccessToken));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get Spotify Token: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TrackResponse>> GetRecommendationsAsync(MusicSearchRequest request)
        {
            var limit = request.Count;
            if (limit <= 0) limit = 10;
            if (limit > 50) limit = 50; 

            var spotify = await GetSpotifyClientAsync();

            var keywords = MoodMapper.GetSearchKeywords(request.Mood);
            var genre = request.Genre.ToLowerInvariant().Trim();
            var yearFilter = !string.IsNullOrWhiteSpace(request.YearRange) ? $" year:{request.YearRange}" : string.Empty;
            
            var allFetchedTracks = new List<FullTrack>();
            var rnd = new Random();
            
            int poolSizePerQuery = Math.Max(30, limit * 2);

            if (keywords.Count == 0)
            {
                // Simplified query: no quotes for single-word genre
                var query = $"genre:{genre}{yearFilter}";
                var tracks = await FetchTracks(spotify, query, poolSizePerQuery, rnd.Next(0, 100));
                allFetchedTracks.AddRange(tracks);
            }
            else
            {
                var selectedKeywords = keywords.OrderBy(x => rnd.Next()).Take(3).ToList();
                
                foreach (var kw in selectedKeywords)
                {
                    // Quoting the keyword but leaving genre/year filters bare if they are simple
                    var query = $"\"{kw}\" genre:{genre}{yearFilter}";
                    
                    var maxSafeOffset = Math.Max(0, 1000 - poolSizePerQuery - 5);
                    var tracks = await FetchTracks(spotify, query, poolSizePerQuery, rnd.Next(0, Math.Min(100, maxSafeOffset)));
                    
                    var filtered = tracks.Where(t => !t.Name.ToLowerInvariant().Contains(kw.ToLowerInvariant())).ToList();
                    allFetchedTracks.AddRange(filtered.Count > 0 ? filtered : tracks);
                }
            }

            if (allFetchedTracks.Count < limit)
            {
                var fallbackQuery = $"genre:{genre}{yearFilter}";
                var maxSafeOffset = Math.Max(0, 1000 - 50 - 5);
                var extraTracks = await FetchTracks(spotify, fallbackQuery, 50, rnd.Next(100, Math.Min(500, maxSafeOffset)));
                allFetchedTracks.AddRange(extraTracks);
            }

            var uniqueTracks = allFetchedTracks
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Id))
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .OrderBy(x => rnd.Next())
                .Take(limit)
                .ToList();

            return uniqueTracks.Select(track => new TrackResponse
            {
                TrackName = track.Name,
                Artist = track.Artists.FirstOrDefault()?.Name ?? "Unknown Artist",
                SpotifyUrl = track.ExternalUrls.ContainsKey("spotify") ? track.ExternalUrls["spotify"] : string.Empty,
                ImageUrl = track.Album?.Images?.FirstOrDefault()?.Url
            }).ToList();
        }

        private async Task<List<FullTrack>> FetchTracks(SpotifyClient spotify, string query, int requestedCount, int startOffset = 0)
        {
            var result = new List<FullTrack>();
            try
            {
                int currentOffset = Math.Min(startOffset, 1000 - 20);
                int fetchedSoFar = 0;
                
                while (fetchedSoFar < requestedCount)
                {
                    var searchRequest = new SearchRequest(SearchRequest.Types.Track, query) 
                    { 
                        Limit = null, // Using null to avoid 'Invalid limit' API error
                        Offset = currentOffset 
                    };
                    var searchResponse = await spotify.Search.Item(searchRequest);
                    
                    if (searchResponse.Tracks.Items == null || searchResponse.Tracks.Items.Count == 0) break;
                    
                    result.AddRange(searchResponse.Tracks.Items);
                    fetchedSoFar += searchResponse.Tracks.Items.Count;
                    currentOffset += 20;

                    if (currentOffset > 980) break; 
                }
            }
            catch (APIException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Spotify API Error: {ex.Message} (Status: {ex.Response?.StatusCode})");
                if (ex.Response?.Body != null) System.Diagnostics.Debug.WriteLine($"Body: {ex.Response.Body}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General Error in FetchTracks: {ex.Message}");
            }
            return result;
        }
    }
}
