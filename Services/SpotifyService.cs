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

        public SpotifyService(IConfiguration configuration)
        {
            _clientId = configuration["Spotify:ClientId"] ?? throw new ArgumentNullException("Spotify:ClientId");
            _clientSecret = configuration["Spotify:ClientSecret"] ?? throw new ArgumentNullException("Spotify:ClientSecret");
        }

        public async Task<List<TrackResponse>> GetRecommendationsAsync(MusicSearchRequest request)
        {
            var limit = request.Count;
            if (limit <= 0) limit = 10;
            if (limit > 100) limit = 100;

            var config = SpotifyClientConfig.CreateDefault();
            var authRequest = new ClientCredentialsRequest(_clientId, _clientSecret);
            var response = await new OAuthClient(config).RequestToken(authRequest);
            var spotify = new SpotifyClient(config.WithToken(response.AccessToken));

            var keywords = MoodMapper.GetSearchKeywords(request.Mood);
            var genre = request.Genre.ToLowerInvariant().Trim();
            
            var allFetchedTracks = new List<FullTrack>();
            
            // To guarantee MAXIMUM relevance (solving the "loose match" problem at high offsets),
            // we will query Spotify for EACH synonym independently but ONLY take the absolute top most relevant tracks from each query.
            if (keywords.Count == 0)
            {
                // Fallback to purely genre
                var tracks = await FetchTracks(spotify, $"genre:{genre}", 40);
                allFetchedTracks.AddRange(tracks);
            }
            else
            {
                var tasks = new List<Task<List<FullTrack>>>();
                foreach (var kw in keywords)
                {
                    // Fetch top perfectly relevant tracks per mood keyword
                    // We explicitly exclude the primary keyword from the track title! 
                    tasks.Add(FetchTracks(spotify, $"{kw} genre:{genre} NOT track:{kw}", 60)); // Fetch 60 to have a healthy pool for local trimming
                }
                
                var results = await Task.WhenAll(tasks);
                foreach (var list in results)
                {
                    // LOCAL C# FILTERING: Spotify API crashes if we send too many NOT exclusions.
                    // Instead, we manually drop ANY track that contains ANY of the mood synonyms in its title.
                    // This elegantly guarantees the user never sees "Sad But True" for "sad" queries.
                    var filtered = list.Where(t => !keywords.Any(k => t.Name.ToLowerInvariant().Contains(k)));
                    allFetchedTracks.AddRange(filtered);
                }
            }

            // Fallback absolute if keywords resulted in empty tracks combined
            if (allFetchedTracks.Count == 0)
            {
                var tracks = await FetchTracks(spotify, $"genre:{genre}", 40);
                allFetchedTracks.AddRange(tracks);
            }

            var rnd = new Random();
            var resultTracks = new List<TrackResponse>();

            // Distinct by ID, completely shuffle our pool of highly-relevant tracks
            var uniqueTracks = allFetchedTracks
                .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Id))
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .OrderBy(x => rnd.Next())
                .ToList();

            // PADDING LOGIC: If the user searches a niche combination (e.g. "happy metal") and Spotify 
            // natively only finds 1-2 songs that strictly meet every parameter without repeating names,
            // we MUST pad the rest of the list with generic genre tracks so the UI isn't empty!
            if (uniqueTracks.Count < limit)
            {
                var padTracks = await FetchTracks(spotify, $"genre:{genre}", 40);
                var distinctPad = padTracks
                    .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Id))
                    .GroupBy(t => t.Id)
                    .Select(g => g.First())
                    .OrderBy(x => rnd.Next())
                    .ToList();

                foreach (var tr in distinctPad)
                {
                    if (uniqueTracks.Count >= limit) break;
                    if (!uniqueTracks.Any(u => u.Id == tr.Id))
                    {
                        uniqueTracks.Add(tr);
                    }
                }
            }

            uniqueTracks = uniqueTracks.Take(limit).ToList();

            foreach (var track in uniqueTracks)
            {
                resultTracks.Add(new TrackResponse
                {
                    TrackName = track.Name,
                    Artist = track.Artists.FirstOrDefault()?.Name ?? "Unknown Artist",
                    SpotifyUrl = track.ExternalUrls.ContainsKey("spotify") ? track.ExternalUrls["spotify"] : string.Empty,
                    ImageUrl = track.Album?.Images?.FirstOrDefault()?.Url
                });
            }

            return resultTracks;
        }

        private async Task<List<FullTrack>> FetchTracks(SpotifyClient spotify, string query, int requestedCount)
        {
            var result = new List<FullTrack>();
            try
            {
                int offset = 0;
                while (offset < requestedCount)
                {
                    var searchRequest = new SearchRequest(SearchRequest.Types.Track, query) 
                    { 
                        Limit = null, // Must stay null to avoid API bugs
                        Offset = offset 
                    };
                    var searchResponse = await spotify.Search.Item(searchRequest);
                    
                    if (searchResponse.Tracks.Items == null || searchResponse.Tracks.Items.Count == 0) break;
                    
                    result.AddRange(searchResponse.Tracks.Items);
                    offset += 20; // Default spotify page length
                }
            }
            catch { }
            return result;
        }
    }
}
