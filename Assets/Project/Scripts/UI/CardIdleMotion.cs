using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.UI
{
    /// <summary>
    /// Lightweight idle bob for minions on the board. Avoids legacy scene-specific animation clips.
    /// </summary>
    public class CardIdleMotion : MonoBehaviour
    {
        [SerializeField] private float verticalAmplitude = 5f;
        [SerializeField] private float horizontalAmplitude = 2f;
        [SerializeField] private float speed = 1.6f;
        [SerializeField] private float phaseOffset;

        private RectTransform rect;
        private Vector2 basePosition;
        private bool initialized;

        public void Configure(string cardId)
        {
            switch (cardId)
            {
                case "stirge":
                case "jr_stirge":
                    verticalAmplitude = 8f;
                    horizontalAmplitude = 10f;
                    speed = 2.4f;
                    break;
                case "blue_snail":
                    verticalAmplitude = 4f;
                    horizontalAmplitude = 3f;
                    speed = 1.1f;
                    break;
                default:
                    verticalAmplitude = 5f;
                    horizontalAmplitude = 2f;
                    speed = 1.6f;
                    break;
            }

            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        public void SetActiveMotion(bool active)
        {
            enabled = active;
            if (!active)
            {
                ResetPosition();
            }
        }

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            CacheBasePosition();
        }

        private void Update()
        {
            if (!initialized)
            {
                CacheBasePosition();
            }

            var t = Time.time * speed + phaseOffset;
            rect.anchoredPosition = basePosition + new Vector2(
                Mathf.Sin(t * 0.85f) * horizontalAmplitude,
                Mathf.Sin(t) * verticalAmplitude);
        }

        private void CacheBasePosition()
        {
            if (rect == null)
            {
                rect = GetComponent<RectTransform>();
            }

            basePosition = rect.anchoredPosition;
            initialized = true;
        }

        private void ResetPosition()
        {
            if (!initialized || rect == null)
            {
                return;
            }

            rect.anchoredPosition = basePosition;
        }
    }

    /// <summary>
    /// Combat-time lunge, hit shake, and death animations for board minion slots.
    /// </summary>
    public class CombatMinionMotion : MonoBehaviour
    {
        [SerializeField] private float lungeDistance = 42f;
        [SerializeField] private float lungeDuration = 0.16f;
        [SerializeField] private float hitShakeDuration = 0.18f;
        [SerializeField] private float hitShakeMagnitude = 10f;
        [SerializeField] private float deathDuration = 0.32f;

        private RectTransform rect;
        private CanvasGroup canvasGroup;
        private Vector2 basePosition;
        private Vector3 baseScale = Vector3.one;
        private bool initialized;
        private Coroutine activeRoutine;

        public void CacheBase()
        {
            if (rect == null)
            {
                rect = GetComponent<RectTransform>();
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            basePosition = rect.anchoredPosition;
            baseScale = rect.localScale;
            canvasGroup.alpha = 1f;
            initialized = true;
        }

        public void ResetVisual()
        {
            if (!initialized)
            {
                CacheBase();
            }

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            rect.anchoredPosition = basePosition;
            rect.localScale = baseScale;
            canvasGroup.alpha = 1f;
        }

        public IEnumerator PlayAttackLunge(Vector2 direction)
        {
            yield return RunRoutine(LungeRoutine(direction.normalized * lungeDistance));
        }

        public IEnumerator PlayHitShake()
        {
            yield return RunRoutine(HitShakeRoutine());
        }

        public IEnumerator PlayDeath()
        {
            yield return RunRoutine(DeathRoutine());
        }

        private IEnumerator RunRoutine(IEnumerator routine)
        {
            if (!initialized)
            {
                CacheBase();
            }

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            activeRoutine = StartCoroutine(routine);
            yield return activeRoutine;
            activeRoutine = null;
        }

        private IEnumerator LungeRoutine(Vector2 offset)
        {
            var elapsed = 0f;
            while (elapsed < lungeDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / lungeDuration);
                var curve = Mathf.Sin(t * Mathf.PI);
                rect.anchoredPosition = basePosition + offset * curve;
                yield return null;
            }

            rect.anchoredPosition = basePosition;
        }

        private IEnumerator HitShakeRoutine()
        {
            var elapsed = 0f;
            while (elapsed < hitShakeDuration)
            {
                elapsed += Time.deltaTime;
                var decay = 1f - Mathf.Clamp01(elapsed / hitShakeDuration);
                var shake = Random.insideUnitCircle * hitShakeMagnitude * decay;
                rect.anchoredPosition = basePosition + shake;
                yield return null;
            }

            rect.anchoredPosition = basePosition;
        }

        private IEnumerator DeathRoutine()
        {
            var elapsed = 0f;
            while (elapsed < deathDuration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / deathDuration);
                rect.localScale = baseScale * Mathf.Lerp(1f, 0.35f, t);
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, t);
                yield return null;
            }

            rect.localScale = baseScale * 0.35f;
            canvasGroup.alpha = 0f;
        }

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
        }
    }

    /// <summary>
    /// Plays a sprite sequence on a UI Image, then hides it when finished.
    /// </summary>
    public class SpriteSequencePlayer : MonoBehaviour
    {
        [SerializeField] private float framesPerSecond = 24f;

        private Image image;
        private Sprite[] frames = System.Array.Empty<Sprite>();
        private Coroutine activeRoutine;

        public bool IsPlaying => activeRoutine != null;

        public void Configure(Image targetImage, Sprite[] sequence, float fps = 24f)
        {
            image = targetImage;
            frames = sequence ?? System.Array.Empty<Sprite>();
            framesPerSecond = Mathf.Max(1f, fps);
        }

        public void Play()
        {
            if (image == null || frames.Length == 0)
            {
                return;
            }

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            activeRoutine = StartCoroutine(PlayRoutine());
        }

        public void Stop()
        {
            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            if (image != null)
            {
                image.enabled = false;
            }
        }

        private IEnumerator PlayRoutine()
        {
            image.enabled = true;
            var frameDelay = 1f / framesPerSecond;

            for (var i = 0; i < frames.Length; i++)
            {
                image.sprite = frames[i];
                yield return new WaitForSeconds(frameDelay);
            }

            image.enabled = false;
            activeRoutine = null;
        }
    }
}