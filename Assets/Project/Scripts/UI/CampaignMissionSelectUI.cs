using System;
using DreamGate.Battlegrounds.Campaign;
using DreamGate.Battlegrounds.Core;
using DreamGate.Battlegrounds.Heroes;
using DreamGate.Battlegrounds.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.UI
{
    public sealed class CampaignMissionSelectUI
    {
        private readonly GameObject root;
        private readonly Transform missionList;
        private readonly TextMeshProUGUI detailText;
        private CampaignMissionDefinition selectedMission;

        private CampaignMissionSelectUI(
            GameObject root,
            Transform missionList,
            TextMeshProUGUI detailText)
        {
            this.root = root;
            this.missionList = missionList;
            this.detailText = detailText;
        }

        public static CampaignMissionSelectUI Create(Transform parent, Action onBack, Action<CampaignMissionDefinition> onStartMission)
        {
            var root = MenuPageUI.CreateOverlay(parent, "CampaignMissionSelect");
            MenuPageUI.CreateTitle(root.transform, "Campaign");
            MenuPageUI.CreateBody(
                root.transform,
                "CampaignDescription",
                "Choose a rival hero to face. Win to unlock their portrait for your collection.",
                700f,
                70f);

            var scrollGo = new GameObject("MissionScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(root.transform, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRect.pivot = new Vector2(0.5f, 0.5f);
            scrollRect.anchoredPosition = new Vector2(0f, 120f);
            scrollRect.sizeDelta = new Vector2(900f, 520f);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.18f, 0.65f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;

            var detail = MenuPageUI.CreateBody(root.transform, "MissionDetail", string.Empty, -120f, 120f);
            detail.alignment = TextAlignmentOptions.Center;

            var ui = new CampaignMissionSelectUI(root, content.transform, detail);
            MenuPageUI.CreateActionButton(root.transform, "Start Mission", new Vector2(0f, -280f), () =>
            {
                if (ui.selectedMission != null)
                {
                    onStartMission?.Invoke(ui.selectedMission);
                }
            });
            MenuPageUI.CreateBackButton(root.transform, () => onBack?.Invoke(), -380f);
            root.SetActive(false);
            return ui;
        }

        public void Show()
        {
            DreamGateServices.Initialize();
            HeroCollectionService.EnsureStarterCollection();
            RebuildMissionList();
            root.SetActive(true);
            root.transform.SetAsLastSibling();
        }

        public void Hide() => root.SetActive(false);

        private void RebuildMissionList()
        {
            for (var i = missionList.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.Destroy(missionList.GetChild(i).gameObject);
            }

            selectedMission = null;
            detailText.text = "Select a mission to preview details.";
            var highest = HeroCollectionService.CampaignHighestLevel;

            foreach (var mission in CampaignCatalog.All)
            {
                var unlocked = CampaignCatalog.IsMissionUnlocked(highest, mission.level);
                var completed = highest >= mission.level;
                CreateMissionRow(mission, unlocked, completed);
            }
        }

        private void CreateMissionRow(CampaignMissionDefinition mission, bool unlocked, bool completed)
        {
            var row = new GameObject($"Mission_{mission.level}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            row.transform.SetParent(missionList, false);
            row.GetComponent<LayoutElement>().preferredHeight = 108f;
            var image = row.GetComponent<Image>();
            image.color = unlocked
                ? new Color(0.14f, 0.18f, 0.3f, 0.95f)
                : new Color(0.08f, 0.1f, 0.16f, 0.95f);

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(row.transform, false);
            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0f, 0.5f);
            portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(16f, 0f);
            portraitRect.sizeDelta = new Vector2(72f, 72f);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.sprite = HeroRegistry.LoadPortrait(mission.bossHeroId);
            portrait.color = unlocked ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.9f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(row.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.5f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(104f, 8f);
            titleRect.offsetMax = new Vector2(-16f, -8f);
            var title = titleGo.GetComponent<TextMeshProUGUI>();
            title.fontSize = 24;
            title.alignment = TextAlignmentOptions.Left;
            title.text = $"Level {mission.level}: {mission.bossDisplayName}";

            var subtitleGo = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subtitleGo.transform.SetParent(row.transform, false);
            var subtitleRect = subtitleGo.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0f, 0f);
            subtitleRect.anchorMax = new Vector2(1f, 0.5f);
            subtitleRect.offsetMin = new Vector2(104f, 8f);
            subtitleRect.offsetMax = new Vector2(-16f, -8f);
            var subtitle = subtitleGo.GetComponent<TextMeshProUGUI>();
            subtitle.fontSize = 18;
            subtitle.alignment = TextAlignmentOptions.Left;
            subtitle.color = new Color(0.8f, 0.86f, 0.95f, 1f);
            subtitle.text = unlocked
                ? $"{(mission.opponentCount == 1 ? "1v1" : "1v2")} • Reward: {mission.bossDisplayName} portrait{(completed ? " • Cleared" : string.Empty)}"
                : "Complete the previous mission to unlock";

            var button = row.GetComponent<Button>();
            button.interactable = unlocked;
            button.onClick.AddListener(() =>
            {
                selectedMission = mission;
                detailText.text =
                    $"Level {mission.level}: {mission.bossDisplayName}\n" +
                    $"{(mission.opponentCount == 1 ? "Face this hero in a 1v1 duel." : "Face this hero and an ally in a 1v2 battle.")}\n" +
                    $"Boss perks: +{mission.bonusTavernTier} tavern tier, +{mission.bonusGoldPerTurn} gold/turn.\n" +
                    $"Win to unlock {mission.bossDisplayName}.";
            });
        }
    }
}