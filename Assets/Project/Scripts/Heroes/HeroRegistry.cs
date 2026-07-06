using System.Collections.Generic;
using UnityEngine;

namespace DreamGate.Battlegrounds.Heroes
{
    public static class HeroRegistry
    {
        private static readonly string[] DefaultHeroes =
        {
            "Dream Warden",
            "Gate Keeper",
            "Snail Sage",
            "Stirge Knight",
            "Mushmom Mystic",
            "Teddy Tactician",
            "Coin Baron",
            "Board Breaker"
        };

        private static readonly string[] PortraitAssets =
        {
            "Warrior",
            "Magician",
            "Evan"
        };

        private static readonly Dictionary<string, Sprite> PortraitCache = new();

        public static IReadOnlyList<string> All => DefaultHeroes;

        public static string GetHeroName(int playerId)
        {
            return DefaultHeroes[playerId % DefaultHeroes.Length];
        }

        public static Sprite LoadPortrait(string heroId)
        {
            if (string.IsNullOrEmpty(heroId))
            {
                return null;
            }

            if (PortraitCache.TryGetValue(heroId, out var cached))
            {
                return cached;
            }

            var assetName = GetPortraitAssetName(heroId);
            if (string.IsNullOrEmpty(assetName))
            {
                return null;
            }

            var resourcePath = $"HeroArt/{assetName}";
            var sprite = LoadPortraitSprite(resourcePath, assetName);
            if (sprite != null)
            {
                PortraitCache[heroId] = sprite;
            }

            return sprite;
        }

        private static Sprite LoadPortraitSprite(string resourcePath, string assetName)
        {
            var sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites == null || sprites.Length == 0)
            {
                return Resources.Load<Sprite>(resourcePath);
            }

            var preferredName = $"{assetName}_0";
            foreach (var candidate in sprites)
            {
                if (candidate != null && candidate.name == preferredName)
                {
                    return candidate;
                }
            }

            foreach (var candidate in sprites)
            {
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        public const string ShopkeeperHeroId = "shopkeeper";
        public const string ShopkeeperHeroName = "Tavern Keeper";

        private static string GetPortraitAssetName(string heroId)
        {
            if (heroId == ShopkeeperHeroId)
            {
                return "Magician";
            }

            if (!heroId.StartsWith("hero_") || !int.TryParse(heroId.Substring(5), out var heroIndex))
            {
                return null;
            }

            if (heroIndex < 0 || heroIndex >= PortraitAssets.Length)
            {
                return null;
            }

            return PortraitAssets[heroIndex];
        }
    }
}