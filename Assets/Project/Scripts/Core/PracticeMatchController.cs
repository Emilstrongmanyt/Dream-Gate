using System.Collections;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Networking;
using DreamGate.Battlegrounds.Services;
using DreamGate.Battlegrounds.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.Core
{
    public class PracticeMatchController : MonoBehaviour
    {
        [SerializeField] private bool hideBackgroundDuringUi = true;
        [SerializeField] private float combatStepSeconds = 0.55f;

        private MatchManager matchManager;
        private INetworkMatchHost networkHost;
        private PracticeGameUI practiceUi;
        private Coroutine combatCoroutine;
        private float combatSpeedMultiplier = 1f;

        private void Start()
        {
            EnsureCanvas();
            if (hideBackgroundDuringUi)
            {
                HideLegacyBackground();
            }

            if (!DreamGateServices.IsInitialized)
            {
                DreamGateServices.Initialize();
            }

            matchManager = new MatchManager();
            var mode = MatchSessionContext.Mode;
            if (mode == MatchMode.Rated)
            {
                matchManager.Initialize(mode, 0, MatchSessionContext.MatchSeed);
                networkHost = new NetworkMatchHostStub();
            }
            else
            {
                MatchSessionContext.BeginPractice();
                matchManager.Initialize();
                networkHost = new LocalMatchHost();
            }

            networkHost.InitializeMatch(matchManager);
            matchManager.MatchEnded += OnMatchEnded;

            var uiRoot = CreateUiRoot();
            practiceUi = uiRoot.gameObject.AddComponent<PracticeGameUI>();
            practiceUi.Initialize(matchManager, uiRoot);
            practiceUi.SetCombatSpeedChanged(SetCombatSpeed);

            matchManager.CombatPlaybackReady += OnCombatPlaybackReady;
        }

        private void OnDestroy()
        {
            if (matchManager != null)
            {
                matchManager.CombatPlaybackReady -= OnCombatPlaybackReady;
                matchManager.MatchEnded -= OnMatchEnded;
            }

            networkHost?.Dispose();
        }

        private void Update()
        {
            networkHost?.TickRecruitTimer(Time.deltaTime);
        }

        private void OnMatchEnded()
        {
            if (matchManager.Mode == MatchMode.Rated && matchManager.FinalResult != null)
            {
                DreamGateServices.ApplyRatedResult(matchManager.FinalResult);
            }
        }

        private void SetCombatSpeed(float multiplier)
        {
            combatSpeedMultiplier = Mathf.Clamp(multiplier, 0.5f, 3f);
        }

        private void OnCombatPlaybackReady()
        {
            if (combatCoroutine != null)
            {
                StopCoroutine(combatCoroutine);
            }

            combatCoroutine = StartCoroutine(PlayHumanCombat());
        }

        private IEnumerator PlayHumanCombat()
        {
            var result = matchManager.PendingHumanCombat;
            if (result == null)
            {
                yield break;
            }

            practiceUi.BeginCombatPlayback(matchManager.PendingOpponent, result);

            foreach (var combatEvent in result.combatEvents)
            {
                var delay = combatStepSeconds / combatSpeedMultiplier;
                if (combatEvent.type == CombatEventType.Attack)
                {
                    delay *= 1.2f;
                }
                else if (combatEvent.type == CombatEventType.Death)
                {
                    delay *= 0.85f;
                }

                yield return practiceUi.PlayCombatEvent(combatEvent, delay);
            }

            yield return practiceUi.PlayHeroDamage(result);
            practiceUi.EndCombatPlayback();
            matchManager.CompleteHumanCombat();
            combatCoroutine = null;
        }

        private void EnsureCanvas()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private static RectTransform CreateUiRoot()
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            var rootGo = new GameObject("PracticeGameUI", typeof(RectTransform));
            rootGo.transform.SetParent(canvas.transform, false);
            var rect = rootGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void HideLegacyBackground()
        {
            var background = GameObject.Find("Background");
            if (background != null)
            {
                background.SetActive(false);
            }
        }
    }
}