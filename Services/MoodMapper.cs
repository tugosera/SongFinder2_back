using System.Collections.Generic;

namespace SongFinder2_back.Services
{
    public static class MoodMapper
    {
        public static List<string> GetSearchKeywords(string mood)
        {
            var m = mood.ToLowerInvariant().Trim();
            
            // By returning MULTIPLE highly targeted synonyms instead of just one,
            // we can poll the top 20 most relevant tracks from Spotify for EACH word,
            // resulting in a pool of ~80 extremely relevant tracks to shuffle.
            return m switch
            {
                "весёлое" or "happy" or "upbeat" => new List<string> { "happy", "upbeat", "joyful", "party", "cheerful", "summer" },
                "грустное" or "sad" or "melancholy" => new List<string> { "sad", "melancholy", "sorrow", "lonely", "depression", "heartbreak" },
                "энергичное" or "energetic" or "hype" => new List<string> { "energetic", "hype", "power", "fast", "workout", "intense" },
                "спокойное" or "calm" or "chill" => new List<string> { "calm", "chill", "peaceful", "soft", "relaxing", "meditation" },
                "меланхоличное" or "melancholic" or "moody" => new List<string> { "melancholic", "moody", "nostalgic", "acoustic", "rainy", "autumn" },
                "танцевальное" or "dance" or "club" => new List<string> { "dance", "disco", "club", "party", "house", "groove" },
                "агрессивное" or "aggressive" or "angry" => new List<string> { "aggressive", "angry", "dark", "heavy", "powerful", "intense" },
                "романтичное" or "romantic" or "love" => new List<string> { "romantic", "love", "passion", "sweet", "ballad", "soul" },
                "фоновое" or "ambient" or "background" => new List<string> { "ambient", "study", "lo-fi", "focus", "atmosphere", "lounge" },
                _ => string.IsNullOrWhiteSpace(m) ? new List<string>() : new List<string> { m }
            };
        }
    }
}
