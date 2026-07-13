using System;
using System.Collections.Generic;
using DreamGate.Battlegrounds.Players;
#if !SERVER_BUILD
using UnityEngine;
#endif

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

        public static readonly string[] PortraitAssets =
        {
            "Ayan",
            "Evan",
            "Garnox",
            "GrandmaYeonHero",
            "Grendel",
            "HeenaHero",
            "KentaHero",
            "Luke the Security Guard",
            "Magician",
            "Marco",
            "Olaf",
            "RudiHero",
            "Warrior"
        };

#if !SERVER_BUILD
        private static readonly Dictionary<string, Sprite> PortraitCache = new();
#endif

        public static IReadOnlyList<string> All => DefaultHeroes;

        public static string GetHeroName(int playerId)
        {
            return DefaultHeroes[playerId % DefaultHeroes.Length];
        }

        public static void AssignRandomBotPortrait(PlayerState player, System.Random random)
        {
            if (player == null || random == null || PortraitAssets.Length == 0)
            {
                return;
            }

            var asset = PortraitAssets[random.Next(PortraitAssets.Length)];
            player.heroId = BuildPortraitHeroId(asset);
        }

        public static string BuildPortraitHeroId(string assetName)
        {
            return $"hero_art_{assetName}";
        }

#if !SERVER_BUILD
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
#endif

#if !SERVER_BUILD
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
#endif

        public const string ShopkeeperHeroId = "shopkeeper";
        public const string ShopkeeperHeroName = "Tavern Keeper";

        public static string GetPortraitAssetName(string heroId)
        {
            if (heroId == ShopkeeperHeroId)
            {
                return "Magician";
            }

            const string artPrefix = "hero_art_";
            if (heroId.StartsWith(artPrefix, StringComparison.Ordinal))
            {
                return heroId.Substring(artPrefix.Length);
            }

            if (!heroId.StartsWith("hero_") || !int.TryParse(heroId.Substring(5), out var heroIndex))
            {
                return null;
            }

            if (PortraitAssets.Length == 0)
            {
                return null;
            }

            var normalizedIndex = ((heroIndex % PortraitAssets.Length) + PortraitAssets.Length) % PortraitAssets.Length;
            return PortraitAssets[normalizedIndex];
        }
    }
}