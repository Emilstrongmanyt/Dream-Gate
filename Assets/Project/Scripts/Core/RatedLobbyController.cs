using DreamGate.Battlegrounds.Services;
using DreamGate.Battlegrounds.Services.Backend;
using DreamGate.Battlegrounds.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.Core
{
    public class RatedLobbyController : MonoBehaviour
    {
        private RatedLobbyUI lobbyUi;
        private IMatchmakingService matchmaking;

        private void Start()
        {
            DreamGateServices.Initialize();

            if (!DreamGateServices.IsLoggedIn)
            {
                DreamGateServices.PendingRatedLobbyAfterLogin = true;
                SceneNavigator.LoadHome();
                return;
            }

            EnsureCanvas();

            var uiRoot = CreateUiRoot();
            lobbyUi = uiRoot.gameObject.AddComponent<RatedLobbyUI>();
            lobbyUi.Build(uiRoot);
            lobbyUi.FindMatchClicked += OnFindMatch;
            lobbyUi.CancelClicked += OnCancel;
            lobbyUi.BackClicked += OnBack;

            matchmaking = MatchmakingServiceFactory.Create(this);
            matchmaking.QueueUpdated += OnQueueUpdated;
            matchmaking.MatchFound += OnMatchFound;
            matchmaking.QueueFailed += OnQueueFailed;
        }

        private void OnDestroy()
        {
            if (lobbyUi != null)
            {
                lobbyUi.FindMatchClicked -= OnFindMatch;
                lobbyUi.CancelClicked -= OnCancel;
                lobbyUi.BackClicked -= OnBack;
            }

            if (matchmaking != null)
            {
                matchmaking.QueueUpdated -= OnQueueUpdated;
                matchmaking.MatchFound -= OnMatchFound;
                matchmaking.QueueFailed -= OnQueueFailed;
                matchmaking.CancelQueue();
            }
        }

        private void OnFindMatch()
        {
            lobbyUi.SetSearching(true);
            lobbyUi.SetQueueStatus("Searching for opponents...");
            matchmaking.StartQueue();
        }

        private void OnCancel()
        {
            matchmaking.CancelQueue();
            lobbyUi.SetSearching(false);
            lobbyUi.SetQueueStatus("Queue cancelled");
        }

        private void OnBack()
        {
            matchmaking.CancelQueue();
            SceneNavigator.LoadMainMenu();
        }

        private void OnQueueUpdated(int found, int target)
        {
            lobbyUi.SetQueueStatus($"Searching... {found}/{target} players");
        }

        private void OnMatchFound(MatchmakingResult result)
        {
            lobbyUi.SetSearching(false);
            lobbyUi.SetQueueStatus(result.usedBotFill
                ? $"Match found with {result.humanCount} player(s). Filling remaining slots with bots..."
                : "Match found! Entering the tavern...");

            if (string.IsNullOrWhiteSpace(result.matchServerUrl))
            {
                var settings = BackendSettings.Load();
                result.matchServerUrl = settings?.ResolvedMatchServerUrl;
            }

            MatchSessionContext.BeginRated(result, DreamGateServices.Profile?.mmr ?? PlayerProfile.DefaultMmr);

            SceneNavigator.LoadPracticeGame();
        }

        private void OnQueueFailed(string message)
        {
            lobbyUi.SetSearching(false);
            lobbyUi.SetQueueStatus(message);
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

            UiCanvasSetup.Apply(canvas);
        }

        private static RectTransform CreateUiRoot()
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            var rootGo = new GameObject("RatedLobbyUI", typeof(RectTransform));
            rootGo.transform.SetParent(canvas.transform, false);
            var rect = rootGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            UiCanvasSetup.ApplySafeArea(rect);
            return rect;
        }
    }
}