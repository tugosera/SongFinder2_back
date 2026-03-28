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
                "весёлое" or "happy" or "upbeat" => new List<string> { "happy", "upbeat", "joyful", "fun" },
                "грустное" or "sad" or "melancholy" => new List<string> { "sad", "melancholy", "depressing", "heartbreak" },
                "энергичное" or "energetic" or "hype" => new List<string> { "energetic", "hype", "pump", "workout" },
                "спокойное" or "calm" or "chill" => new List<string> { "calm", "chill", "relaxing", "mellow" },
                "меланхоличное" or "melancholic" or "moody" => new List<string> { "melancholic", "nostalgic", "moody", "acoustic" },
                "танцевальное" or "dance" or "club" => new List<string> { "dance", "club", "party", "groove" },
                "агрессивное" or "aggressive" or "angry" => new List<string> { "aggressive", "angry", "heavy", "rage" },
                "романтичное" or "romantic" or "love" => new List<string> { "romantic", "love", "sweet", "affection" },
                "фоновое" or "ambient" or "background" => new List<string> { "ambient", "background", "focus", "study" },
                _ => string.IsNullOrWhiteSpace(m) ? new List<string>() : new List<string> { m }
            };
        }
    }
}
