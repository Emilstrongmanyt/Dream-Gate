using System.Collections;
using DreamGate.Battlegrounds.Cards;
using DreamGate.Battlegrounds.Combat;
using DreamGate.Battlegrounds.Networking;
using DreamGate.Battlegrounds.Services;
using DreamGate.Battlegrounds.Services.Backend;
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
        private const float CombatSpeedMultiplier = 1f;

        private void Start()
        {
            var canvas = EnsureCanvas();
            CreateFullScreenBackground(canvas);
            var musicMode = MatchSessionContext.Mode;
            GameMusicPlayer.StartMatchMusic(musicMode);
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
                matchManager.Initialize(
                    mode,
                    MatchSessionContext.HumanSlotIndex,
                    MatchSessionContext.MatchSeed,
                    MatchSessionContext.Slots,
                    deferRecruitStart: true);
                networkHost = CreateRatedNetworkHost();
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

            matchManager.CombatPlaybackReady += OnCombatPlaybackReady;
        }

        private INetworkMatchHost CreateRatedNetworkHost()
        {
            var matchServerUrl = MatchSessionContext.MatchServerUrl;
            if (string.IsNullOrWhiteSpace(matchServerUrl))
            {
                matchServerUrl = BackendSettings.Load()?.ResolvedMatchServerUrl;
            }

            if (!string.IsNullOrWhiteSpace(matchServerUrl))
            {
                var remote = new RemoteMatchClient(
                    matchServerUrl,
                    MatchSessionContext.LobbyId,
                    MatchSessionContext.HumanSlotIndex,
                    MatchSessionContext.Slots,
                    MatchSessionContext.MatchSeed);
                remote.CombatPlaybackRequested += OnCombatPlaybackReady;
                return remote;
            }

            return new NetworkMatchHostStub();
        }

        private void OnDestroy()
        {
            if (matchManager != null)
            {
                matchManager.CombatPlaybackReady -= OnCombatPlaybackReady;
                matchManager.MatchEnded -= OnMatchEnded;
            }

            if (networkHost is RemoteMatchClient remote)
            {
                remote.CombatPlaybackRequested -= OnCombatPlaybackReady;
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
                var delay = combatStepSeconds / CombatSpeedMultiplier;
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

        private Canvas EnsureCanvas()
        {
            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            UiCanvasSetup.Apply(canvas);
            return canvas;
        }

        private static void CreateFullScreenBackground(Canvas canvas)
        {
            var sprite = CardArtLoader.LoadBackground("PracticeGameBackground1");
            if (sprite == null)
            {
                return;
            }

            UiBackgroundFit.CreateCanvasCoverBackground(canvas, sprite, "BoardBackground");
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
            UiCanvasSetup.ApplySafeArea(rect);
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