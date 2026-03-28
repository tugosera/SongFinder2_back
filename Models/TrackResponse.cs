namespace SongFinder2_back.Models
{
    public class TrackResponse
    {
        public string TrackName { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string SpotifyUrl { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
    }
}
