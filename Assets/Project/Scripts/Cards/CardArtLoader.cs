using System.Collections.Generic;
using UnityEngine;

namespace DreamGate.Battlegrounds.Cards
{
    public static class CardArtLoader
    {
        private static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Load(int tier, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            var resourcePath = $"CardArt/Tier{tier}/{fileName}";
            if (Cache.TryGetValue(resourcePath, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                var sprites = Resources.LoadAll<Sprite>(resourcePath);
                if (sprites != null && sprites.Length > 0)
                {
                    sprite = PickBestSprite(sprites, fileName);
                }
            }

            if (sprite != null)
            {
                Cache[resourcePath] = sprite;
            }

            return sprite;
        }

        public static Sprite LoadBackground(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
            {
                return null;
            }

            if (Cache.TryGetValue(resourceName, out var cached))
            {
                return cached;
            }

            var sprites = Resources.LoadAll<Sprite>(resourceName);
            if (sprites == null || sprites.Length == 0)
            {
                var single = Resources.Load<Sprite>(resourceName);
                if (single != null)
                {
                    Cache[resourceName] = single;
                }

                return single;
            }

            Sprite pick = null;
            foreach (var sprite in sprites)
            {
                if (sprite.name.EndsWith("_0") || sprite.name.Contains("Background"))
                {
                    pick = sprite;
                    break;
                }
            }

            pick ??= sprites[0];
            Cache[resourceName] = pick;
            return pick;
        }

        private static Sprite PickBestSprite(Sprite[] sprites, string fileName)
        {
            foreach (var sprite in sprites)
            {
                if (sprite.name == fileName || sprite.name.StartsWith(fileName))
                {
                    return sprite;
                }
            }

            foreach (var sprite in sprites)
            {
                if (sprite.name.EndsWith("_0"))
                {
                    return sprite;
                }
            }

            return sprites[0];
        }
    }
}