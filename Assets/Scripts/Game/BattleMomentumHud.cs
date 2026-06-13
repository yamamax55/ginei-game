using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 戦局モメンタムの表示（#2183）。画面上部に「自軍 ⇔ 敵軍」の優勢度バーを出す（読みやすさ・高揚）。
    /// 値は `BattleMomentumRules`（Core）。撃沈/寝返り/捨てがまり等の名場面は既存 `NotificationFeed` が担う。
    /// Battle シーンに自動生成・手置き不要。観測専用＝状態は変えない。
    /// </summary>
    public class BattleMomentumHud : MonoBehaviour
    {
        public float updateInterval = 0.5f;
        private float nextUpdate;
        private RectTransform fill;
        private Image fillImage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle") return;
            if (FindAnyObjectByType<BattleMomentumHud>() != null) return;
            new GameObject("BattleMomentumHud").AddComponent<BattleMomentumHud>();
        }

        private void Start() => BuildBar();

        private void BuildBar()
        {
            var canvasGo = new GameObject("MomentumCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // 背景バー（中央上部）。
            var bg = NewImage("MomentumBG", canvasGo.transform, new Color(0.15f, 0.15f, 0.15f, 0.7f));
            var bgRt = bg.rectTransform;
            bgRt.anchorMin = new Vector2(0.5f, 1f); bgRt.anchorMax = new Vector2(0.5f, 1f);
            bgRt.pivot = new Vector2(0.5f, 1f);
            bgRt.anchoredPosition = new Vector2(0f, -8f);
            bgRt.sizeDelta = new Vector2(520f, 16f);

            // 優勢フィル（左＝自軍色）。
            var fillImg = NewImage("MomentumFill", bg.transform, new Color(0.3f, 0.6f, 1f, 0.9f));
            fill = fillImg.rectTransform;
            fillImage = fillImg;
            fill.anchorMin = new Vector2(0f, 0f); fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.anchoredPosition = Vector2.zero;
            fill.sizeDelta = new Vector2(260f, 0f);
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private void Update()
        {
            if (fill == null) return;
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + Mathf.Max(0.1f, updateInterval);
            Refresh();
        }

        private void Refresh()
        {
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            FactionData playerData = GameSettings.Instance != null ? GameSettings.Instance.playerFactionData : null;

            int myCount = 0, enCount = 0;
            float myStr = 0f, enStr = 0f;
            IReadOnlyList<FleetStrength> flags = FleetRegistry.AllFlagships;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength f = flags[i];
                if (f == null || !f.IsAlive || !f.IsCombatant) continue;
                if (FactionRelations.IsHostile(playerData, player, f.FactionData, f.Faction)) { enCount++; enStr += Mathf.Max(0, f.strength); }
                else { myCount++; myStr += Mathf.Max(0, f.strength); }
            }

            float adv = BattleMomentumRules.Advantage(
                BattleMomentumRules.Power(myCount, myStr),
                BattleMomentumRules.Power(enCount, enStr));

            float fullWidth = 520f;
            fill.sizeDelta = new Vector2(fullWidth * Mathf.Clamp01(adv), 0f);
            // 優勢で青→緑、劣勢で青→赤に寄せる（読みやすさ）。
            if (fillImage != null)
                fillImage.color = Color.Lerp(new Color(1f, 0.35f, 0.3f, 0.9f), new Color(0.35f, 0.9f, 0.5f, 0.9f), adv);
        }
    }
}
