using System;
using System.Collections.Generic;
using DreamGate.Battlegrounds.Heroes;
using DreamGate.Battlegrounds.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DreamGate.Battlegrounds.UI
{
    public sealed class HeroSelectionGridUI
    {
        private readonly Transform contentRoot;
        private readonly TextMeshProUGUI statusText;
        private readonly Action<string> onHeroSelected;
        private readonly List<HeroCell> cells = new();
        private string selectedHeroId;

        private HeroSelectionGridUI(
            Transform contentRoot,
            TextMeshProUGUI statusText,
            Action<string> onHeroSelected)
        {
            this.contentRoot = contentRoot;
            this.statusText = statusText;
            this.onHeroSelected = onHeroSelected;
        }

        public static HeroSelectionGridUI Create(
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            Action<string> onHeroSelected)
        {
            var panel = new GameObject("HeroSelectionGrid", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = anchoredPosition;
            panelRect.sizeDelta = size;

            var statusGo = new GameObject("HeroStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
            statusGo.transform.SetParent(panel.transform, false);
            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.anchoredPosition = new Vector2(0f, 8f);
            statusRect.sizeDelta = new Vector2(0f, 36f);
            var statusText = statusGo.GetComponent<TextMeshProUGUI>();
            statusText.fontSize = 18;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = new Color(0.85f, 0.92f, 1f, 1f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(0f, 48f);
            scrollRect.offsetMax = new Vector2(0f, -8f);
            scrollGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.18f, 0.65f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 0f);

            var grid = content.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(132f, 156f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            return new HeroSelectionGridUI(content.transform, statusText, onHeroSelected);
        }

        public void Refresh()
        {
            DreamGateServices.Initialize();
            HeroCollectionService.EnsureStarterCollection();
            selectedHeroId = HeroCollectionService.SelectedHeroId;
            foreach (var cell in cells)
            {
                if (cell.Root != null)
                {
                    UnityEngine.Object.Destroy(cell.Root);
                }
            }

            cells.Clear();

            foreach (var assetName in HeroRegistry.PortraitAssets)
            {
                var heroId = HeroRegistry.BuildPortraitHeroId(assetName);
                var unlocked = HeroCollectionService.IsHeroUnlocked(heroId);
                var cell = CreateHeroCell(heroId, assetName, unlocked);
                cells.Add(cell);
            }

            UpdateStatus();
        }

        private HeroCell CreateHeroCell(string heroId, string assetName, bool unlocked)
        {
            var go = new GameObject($"Hero_{assetName}", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(contentRoot, false);
            var rect = go.GetComponent<RectTransform>();
            var frame = go.GetComponent<Image>();
            frame.color = heroId == selectedHeroId
                ? new Color(0.35f, 0.55f, 0.95f, 0.95f)
                : new Color(0.12f, 0.16f, 0.28f, 0.95f);

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(go.transform, false);
            var portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.1f, 0.22f);
            portraitRect.anchorMax = new Vector2(0.9f, 0.92f);
            portraitRect.offsetMin = Vector2.zero;
            portraitRect.offsetMax = Vector2.zero;
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.sprite = HeroRegistry.LoadPortrait(heroId);
            portrait.color = unlocked ? Color.white : new Color(0.2f, 0.2f, 0.2f, 0.85f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0.2f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            label.fontSize = 14;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.text = assetName.Replace("Hero", string.Empty);

            if (!unlocked)
            {
                var lockGo = new GameObject("Lock", typeof(RectTransform), typeof(TextMeshProUGUI));
                lockGo.transform.SetParent(go.transform, false);
                var lockRect = lockGo.GetComponent<RectTransform>();
                lockRect.anchorMin = Vector2.zero;
                lockRect.anchorMax = Vector2.one;
                lockRect.offsetMin = Vector2.zero;
                lockRect.offsetMax = Vector2.zero;
                var lockText = lockGo.GetComponent<TextMeshProUGUI>();
                lockText.text = "LOCKED";
                lockText.fontSize = 18;
                lockText.fontStyle = FontStyles.Bold;
                lockText.alignment = TextAlignmentOptions.Center;
                lockText.color = new Color(1f, 0.85f, 0.35f, 0.95f);
            }

            var button = go.GetComponent<Button>();
            button.interactable = unlocked;
            button.onClick.AddListener(() =>
            {
                if (!unlocked)
                {
                    return;
                }

                selectedHeroId = heroId;
                HeroCollectionService.SelectedHeroId = heroId;
                onHeroSelected?.Invoke(heroId);
                Refresh();
            });

            return new HeroCell(go);
        }

        private void UpdateStatus()
        {
            if (statusText == null)
            {
                return;
            }

            var selectedName = HeroCollectionService.GetPortraitDisplayName(selectedHeroId);
            statusText.text = $"Equipped hero: {selectedName}";
        }

        private sealed class HeroCell
        {
            public GameObject Root { get; }

            public HeroCell(GameObject root) => Root = root;
        }
    }
}