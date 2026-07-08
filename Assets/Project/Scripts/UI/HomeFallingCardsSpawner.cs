using System.Collections.Generic;
using DreamGate.Battlegrounds.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.UI
{
    /// <summary>
    /// Decorative falling card sprites for the Home menu background.
    /// Lives on the Home scene only and is destroyed when leaving the scene.
    /// </summary>
    public class HomeFallingCardsSpawner : MonoBehaviour
    {
        private const float SpawnIntervalMin = 0.35f;
        private const float SpawnIntervalMax = 1.1f;
        private const float FallSpeedMin = 95f;
        private const float FallSpeedMax = 210f;
        private const float HorizontalPadding = 72f;
        private const float SpawnYOffset = 96f;
        private const float DespawnYOffset = 128f;
        private static readonly Vector2 CardSize = new(118f, 152f);
        private static readonly Vector2 CardSizeVariance = new(36f, 46f);

        private readonly List<Sprite> cardSprites = new();
        private RectTransform spawnLayer;
        private float nextSpawnAt;

        public static HomeFallingCardsSpawner Create(Transform parent)
        {
            var go = new GameObject("HomeFallingCardsSpawner", typeof(RectTransform), typeof(HomeFallingCardsSpawner));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var spawner = go.GetComponent<HomeFallingCardsSpawner>();
            spawner.Initialize();
            return spawner;
        }

        private void Initialize()
        {
            CardRegistry.Initialize();

            foreach (var card in CardRegistry.GetAllShopCards())
            {
                if (card?.cardArt != null && !cardSprites.Contains(card.cardArt))
                {
                    cardSprites.Add(card.cardArt);
                }
            }

            var layerGo = new GameObject("FallingCardsLayer", typeof(RectTransform));
            layerGo.transform.SetParent(transform, false);
            spawnLayer = layerGo.GetComponent<RectTransform>();
            spawnLayer.anchorMin = Vector2.zero;
            spawnLayer.anchorMax = Vector2.one;
            spawnLayer.offsetMin = Vector2.zero;
            spawnLayer.offsetMax = Vector2.zero;
            layerGo.transform.SetAsFirstSibling();

            ScheduleNextSpawn();
        }

        private void Update()
        {
            if (cardSprites.Count == 0 || spawnLayer == null)
            {
                return;
            }

            if (Time.unscaledTime >= nextSpawnAt)
            {
                SpawnCard();
                ScheduleNextSpawn();
            }
        }

        private void ScheduleNextSpawn()
        {
            nextSpawnAt = Time.unscaledTime + Random.Range(SpawnIntervalMin, SpawnIntervalMax);
        }

        private void SpawnCard()
        {
            var sprite = cardSprites[Random.Range(0, cardSprites.Count)];
            var cardGo = new GameObject("FallingCard", typeof(RectTransform), typeof(Image), typeof(HomeFallingCard));
            cardGo.transform.SetParent(spawnLayer, false);

            var rect = cardGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = CardSize + new Vector2(
                Random.Range(-CardSizeVariance.x, CardSizeVariance.x),
                Random.Range(-CardSizeVariance.y, CardSizeVariance.y));

            var halfWidth = spawnLayer.rect.width * 0.5f;
            var x = Random.Range(-halfWidth + HorizontalPadding, halfWidth - HorizontalPadding);
            var topY = spawnLayer.rect.height * 0.5f + rect.sizeDelta.y * 0.5f + SpawnYOffset;
            rect.anchoredPosition = new Vector2(x, topY);
            rect.localEulerAngles = new Vector3(0f, 0f, Random.Range(-18f, 18f));

            var image = cardGo.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, Random.Range(0.55f, 0.9f));

            var fallSpeed = Random.Range(FallSpeedMin, FallSpeedMax);
            cardGo.GetComponent<HomeFallingCard>().Configure(spawnLayer, rect, fallSpeed, DespawnYOffset);
        }
    }

    internal class HomeFallingCard : MonoBehaviour
    {
        private RectTransform layer;
        private RectTransform rect;
        private float fallSpeed;
        private float despawnOffset;

        public void Configure(RectTransform spawnLayer, RectTransform cardRect, float speed, float bottomOffset)
        {
            layer = spawnLayer;
            rect = cardRect;
            fallSpeed = speed;
            despawnOffset = bottomOffset;
        }

        private void Update()
        {
            if (rect == null || layer == null)
            {
                Destroy(gameObject);
                return;
            }

            var position = rect.anchoredPosition;
            position.y -= fallSpeed * Time.unscaledDeltaTime;
            rect.anchoredPosition = position;

            var bottomLimit = -layer.rect.height * 0.5f - rect.sizeDelta.y * 0.5f - despawnOffset;
            if (position.y < bottomLimit)
            {
                Destroy(gameObject);
            }
        }
    }
}