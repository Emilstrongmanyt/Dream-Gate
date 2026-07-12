using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Services.Backend;
using UnityEngine;

namespace DreamGate.Battlegrounds.Networking
{
    public static class MatchSnapshotJson
    {
        public static string ToJson(MatchSnapshot snapshot) => JsonUtility.ToJson(snapshot);

        public static bool TryParse(string json, out MatchSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var normalized = ApiJson.NormalizeResponseJson(json);
            snapshot = ParseWithApiJson(normalized);
            return IsValidSnapshot(snapshot);
        }

        public static bool IsValidSnapshot(MatchSnapshot snapshot) =>
            snapshot != null && snapshot.players != null && snapshot.players.Length > 0;

        public static string BuildActionPayload(Dictionary<string, int> payload)
        {
            if (payload == null || payload.Count == 0)
            {
                return "{}";
            }

            var builder = new StringBuilder("{");
            var first = true;
            foreach (var pair in payload)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append('"').Append(pair.Key).Append("\":").Append(pair.Value);
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static MatchSnapshot ParseWithApiJson(string json)
        {
            var snapshot = new MatchSnapshot
            {
                version = ApiJson.TryGetInt(json, "version"),
                turn = ApiJson.TryGetInt(json, "turn", 1),
                phase = ApiJson.TryGetInt(json, "phase"),
                recruitTimeRemaining = TryGetFloat(json, "recruitTimeRemaining"),
                localSlotIndex = ApiJson.TryGetInt(json, "localSlotIndex"),
                awaitingCombat = ApiJson.TryGetBool(json, "awaitingCombat"),
                matchEnded = ApiJson.TryGetBool(json, "matchEnded"),
                players = ParsePlayers(json),
                pendingCombat = ParseCombat(ApiJson.ExtractNestedObject(json, "pendingCombat")),
                matchEnd = ParseMatchEnd(ApiJson.ExtractNestedObject(json, "matchEnd"))
            };

            return snapshot;
        }

        private static PlayerSnapshot[] ParsePlayers(string json)
        {
            var arrayJson = ExtractArray(json, "players");
            if (string.IsNullOrEmpty(arrayJson))
            {
                return Array.Empty<PlayerSnapshot>();
            }

            var players = new List<PlayerSnapshot>();
            foreach (var chunk in ApiJson.ExtractObjectChunks(arrayJson))
            {
                players.Add(new PlayerSnapshot
                {
                    playerId = ApiJson.TryGetInt(chunk, "playerId"),
                    displayName = ApiJson.TryGetString(chunk, "displayName") ?? string.Empty,
                    heroId = ApiJson.TryGetString(chunk, "heroId") ?? string.Empty,
                    heroName = ApiJson.TryGetString(chunk, "heroName") ?? string.Empty,
                    isHuman = ApiJson.TryGetBool(chunk, "isHuman"),
                    isEliminated = ApiJson.TryGetBool(chunk, "isEliminated"),
                    placement = ApiJson.TryGetInt(chunk, "placement"),
                    heroHealth = ApiJson.TryGetInt(chunk, "heroHealth"),
                    damageDealt = ApiJson.TryGetInt(chunk, "damageDealt"),
                    damageTaken = ApiJson.TryGetInt(chunk, "damageTaken"),
                    gold = ApiJson.TryGetInt(chunk, "gold"),
                    tavernTier = ApiJson.TryGetInt(chunk, "tavernTier"),
                    doomNextCombat = ApiJson.TryGetBool(chunk, "doomNextCombat"),
                    board = ParseMinionArray(chunk, "board", MatchConfig.BoardSize),
                    hand = ParseMinionArray(chunk, "hand", 0),
                    shopCardIds = ParseStringArray(chunk, "shopCardIds")
                });
            }

            return players.ToArray();
        }

        private static CombatSnapshot ParseCombat(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            var events = new List<CombatEventSnapshot>();
            var eventsJson = ExtractArray(json, "events");
            if (!string.IsNullOrEmpty(eventsJson))
            {
                foreach (var chunk in ApiJson.ExtractObjectChunks(eventsJson))
                {
                    events.Add(new CombatEventSnapshot
                    {
                        type = ApiJson.TryGetInt(chunk, "type"),
                        attackerSlot = ApiJson.TryGetInt(chunk, "attackerSlot"),
                        defenderSlot = ApiJson.TryGetInt(chunk, "defenderSlot"),
                        isRecoil = ApiJson.TryGetBool(chunk, "isRecoil"),
                        damage = ApiJson.TryGetInt(chunk, "damage")
                    });
                }
            }

            return new CombatSnapshot
            {
                opponentPlayerId = ApiJson.TryGetInt(json, "opponentPlayerId"),
                opponentDisplayName = ApiJson.TryGetString(json, "opponentDisplayName") ?? string.Empty,
                opponentHeroName = ApiJson.TryGetString(json, "opponentHeroName") ?? string.Empty,
                outcome = ApiJson.TryGetInt(json, "outcome"),
                damageToDefender = ApiJson.TryGetInt(json, "damageToDefender"),
                damageToAttacker = ApiJson.TryGetInt(json, "damageToAttacker"),
                events = events.ToArray()
            };
        }

        private static MatchEndSnapshot ParseMatchEnd(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return new MatchEndSnapshot
            {
                playerWon = ApiJson.TryGetBool(json, "playerWon"),
                placement = ApiJson.TryGetInt(json, "placement"),
                turnsPlayed = ApiJson.TryGetInt(json, "turnsPlayed"),
                finalHeroHealth = ApiJson.TryGetInt(json, "finalHeroHealth"),
                damageDealt = ApiJson.TryGetInt(json, "damageDealt"),
                damageTaken = ApiJson.TryGetInt(json, "damageTaken"),
                heroName = ApiJson.TryGetString(json, "heroName") ?? string.Empty
            };
        }

        private static MinionInstance[] ParseMinionArray(string json, string key, int fixedLength)
        {
            var arrayJson = ExtractArray(json, key);
            if (string.IsNullOrEmpty(arrayJson))
            {
                return fixedLength > 0 ? new MinionInstance[fixedLength] : Array.Empty<MinionInstance>();
            }

            var chunks = ApiJson.ExtractObjectChunks(arrayJson);
            if (fixedLength > 0)
            {
                var board = new MinionInstance[fixedLength];
                for (var i = 0; i < chunks.Count && i < board.Length; i++)
                {
                    board[i] = TryParseMinion(chunks[i]);
                }

                return board;
            }

            var hand = new MinionInstance[chunks.Count];
            for (var i = 0; i < chunks.Count; i++)
            {
                hand[i] = TryParseMinion(chunks[i]);
            }

            return hand;
        }

        private static MinionInstance TryParseMinion(string json)
        {
            if (string.IsNullOrEmpty(json) || json.IndexOf("null", StringComparison.Ordinal) >= 0 && json.Length < 8)
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<MinionInstance>(json);
            }
            catch
            {
                return null;
            }
        }

        private static string[] ParseStringArray(string json, string key)
        {
            var arrayJson = ExtractArray(json, key);
            if (string.IsNullOrEmpty(arrayJson))
            {
                return Array.Empty<string>();
            }

            var values = new List<string>();
            for (var i = 0; i < arrayJson.Length; i++)
            {
                if (arrayJson[i] != '"')
                {
                    continue;
                }

                var start = ++i;
                while (i < arrayJson.Length)
                {
                    if (arrayJson[i] == '"' && arrayJson[i - 1] != '\\')
                    {
                        break;
                    }

                    i++;
                }

                if (i > start)
                {
                    values.Add(arrayJson.Substring(start, i - start));
                }
            }

            return values.ToArray();
        }

        private static string ExtractArray(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            var marker = $"\"{key}\":";
            var markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                return string.Empty;
            }

            var start = json.IndexOf('[', markerIndex);
            if (start < 0)
            {
                return string.Empty;
            }

            var depth = 0;
            for (var i = start; i < json.Length; i++)
            {
                if (json[i] == '[')
                {
                    depth++;
                }
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return json.Substring(start, i - start + 1);
                    }
                }
            }

            return string.Empty;
        }

        private static float TryGetFloat(string json, string key)
        {
            var raw = ApiJson.TryGetString(json, key);
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }
    }
}