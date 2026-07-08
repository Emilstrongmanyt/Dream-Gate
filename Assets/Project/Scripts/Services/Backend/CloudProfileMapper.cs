namespace DreamGate.Battlegrounds.Services.Backend
{
    public static class CloudProfileMapper
    {
        public static PlayerProfile FromRestJson(string json, string playerId, string email)
        {
            return new PlayerProfile
            {
                playerId = playerId,
                email = email,
                displayName = ApiJson.TryGetString(json, "display_name") ?? "Dreamer",
                mmr = ApiJson.TryGetInt(json, "mmr", PlayerProfile.DefaultMmr),
                highestMmr = ApiJson.TryGetInt(json, "highest_mmr", PlayerProfile.DefaultMmr),
                ratedGamesPlayed = ApiJson.TryGetInt(json, "rated_games_played"),
                wins = ApiJson.TryGetInt(json, "wins"),
                losses = ApiJson.TryGetInt(json, "losses"),
                top4Finishes = ApiJson.TryGetInt(json, "top4_finishes"),
                currentWinStreak = ApiJson.TryGetInt(json, "current_win_streak"),
                bestWinStreak = ApiJson.TryGetInt(json, "best_win_streak"),
                totalDamageDealt = ApiJson.TryGetInt(json, "total_damage_dealt")
            };
        }

        public static PlayerProfile FromFunctionJson(string json, string playerId, string email)
        {
            var profileJson = ExtractProfileObject(json) ?? json;
            return FromRestJson(profileJson, playerId, email);
        }

        private static string ExtractProfileObject(string json)
        {
            var pattern = "\"profile\":";
            var index = json.IndexOf(pattern, System.StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            index = json.IndexOf('{', index);
            if (index < 0)
            {
                return null;
            }

            var depth = 0;
            for (var i = index; i < json.Length; i++)
            {
                if (json[i] == '{')
                {
                    depth++;
                }
                else if (json[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(index, i - index + 1);
                    }
                }
            }

            return null;
        }
    }
}