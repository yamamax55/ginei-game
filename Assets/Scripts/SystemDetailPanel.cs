using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略マップで星系/惑星をクリック（I キー）した時に、その内政・攻城・所有を表示するモーダル（#759）。
    /// 艦隊の <see cref="FleetDetailPanel"/> の星系版。表示中は <see cref="Time.timeScale"/>=0 でポーズし、
    /// 閉じる/Esc/背景クリックで復帰する。UIは実行時にコード生成。数値ロジックは持たず
    /// <see cref="GovernanceRules"/>/<see cref="Planet"/> など static 窓口を読むだけ。
    /// </summary>
    public class SystemDetailPanel : MonoBehaviour
    {
        private static SystemDetailPanel instance;

        /// <summary>パネルが開いているか（GalaxyView 等が入力を譲るために参照）。</summary>
        public static bool IsOpen => instance != null && instance.isOpen;

        private bool isOpen;
        private float savedTimeScale = 1f;
        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;
        private TextMeshProUGUI stabilityLabel;
        private RectTransform stabilityFill;

        /// <summary>星系の詳細を表示する（必要なら生成）。prov/planet は無くても可（後方互換表示）。</summary>
        public static void Show(StarSystem s, Province prov, int neighborCount, string fleetSummary)
        {
            if (s == null) return;
            if (instance == null)
            {
                GameObject go = new GameObject("SystemDetailPanel");
                instance = go.AddComponent<SystemDetailPanel>();
                instance.Build();
            }
            instance.Display(s, prov, neighborCount, fleetSummary);
        }

        private void Build()
        {
            EnsureEventSystem();

            GameObject canvasObj = new GameObject("SystemDetailCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            // 背景ディマー（クリックで閉じる）
            GameObject dim = new GameObject("Dimmer", typeof(RectTransform));
            dim.transform.SetParent(root.transform, false);
            StretchFull(dim.GetComponent<RectTransform>());
            Image dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);
            Button dimBtn = dim.AddComponent<Button>();
            dimBtn.transition = UnityEngine.UI.Selectable.Transition.None; // Ginei.Selectable と衝突回避
            dimBtn.onClick.AddListener(Close);

            // パネル（中央）
            GameObject panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            RectTransform pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(560f, 600f);
            pRT.anchoredPosition = Vector2.zero;
            Image pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.06f, 0.07f, 0.12f, 0.97f);

            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(26, 26, 22, 22);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            titleText = CreateText(panel.transform, "星系情報", 28f, FontStyles.Bold, TextAlignmentOptions.Center);

            // 安定度バー（ラベル＋色付きフィル）
            BuildStabilityBar(panel.transform);

            bodyText = CreateText(panel.transform, "", 21f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            LayoutElement bodyLE = bodyText.gameObject.AddComponent<LayoutElement>();
            bodyLE.flexibleHeight = 1f;

            CreateButton(panel.transform, "閉じる (Esc)", Close);

            root.SetActive(false);
        }

        private void BuildStabilityBar(Transform parent)
        {
            stabilityLabel = CreateText(parent, "安定度 —", 19f, FontStyles.Bold, TextAlignmentOptions.Left);

            GameObject bg = new GameObject("StabilityBarBg", typeof(RectTransform));
            bg.transform.SetParent(parent, false);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.5f);
            bgImg.raycastTarget = false;
            LayoutElement le = bg.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(bg.transform, false);
            stabilityFill = fill.GetComponent<RectTransform>();
            stabilityFill.anchorMin = new Vector2(0f, 0f);
            stabilityFill.anchorMax = new Vector2(1f, 1f); // Display で anchorMax.x を割合に
            stabilityFill.offsetMin = stabilityFill.offsetMax = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = Color.green;
            fillImg.raycastTarget = false;
        }

        private void Display(StarSystem s, Province prov, int neighborCount, string fleetSummary)
        {
            if (titleText != null) titleText.text = $"{s.systemName}（星系 #{s.id}）";

            // 安定度バー
            float stability = prov != null ? prov.stability : -1f;
            if (stabilityFill != null)
            {
                if (prov != null)
                {
                    float frac = Mathf.Clamp01(stability / GovernanceRules.MaxStability);
                    stabilityFill.anchorMax = new Vector2(frac, 1f);
                    // 緑(高)→黄→赤(低)
                    Image fi = stabilityFill.GetComponent<Image>();
                    if (fi != null) fi.color = StabilityColor(frac);
                }
                else stabilityFill.anchorMax = new Vector2(0f, 1f);
            }
            if (stabilityLabel != null)
            {
                stabilityLabel.text = prov != null
                    ? $"安定度 {Mathf.RoundToInt(stability)}%{(GovernanceRules.IsUnrest(prov) ? "　⚠反乱リスク" : "")}"
                    : "安定度 —（未統治）";
            }

            if (bodyText != null) bodyText.text = BuildInfo(s, prov, neighborCount, fleetSummary);

            if (!isOpen)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                isOpen = true;
            }
            if (root != null) root.SetActive(true);
        }

        /// <summary>パネルを閉じて元の速度へ復帰する。</summary>
        public void Close()
        {
            if (isOpen)
            {
                Time.timeScale = savedTimeScale;
                isOpen = false;
            }
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (!isOpen) return;
            Time.timeScale = 0f; // 倍速キー等で解除されてもポーズ維持
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        private string BuildInfo(StarSystem s, Province prov, int neighborCount, string fleetSummary)
        {
            var sb = new StringBuilder();

            string ownerName = (s.ownerData != null) ? s.ownerData.factionName : s.owner.ToString();
            sb.AppendLine($"所有: {ownerName}");
            sb.AppendLine($"座標: ({s.position.x:0.0}, {s.position.y:0.0})　回廊: {neighborCount}本");

            sb.AppendLine();
            sb.AppendLine("― 内政 ―");
            if (prov != null)
            {
                string ideo = string.IsNullOrEmpty(prov.nativeIdeology) ? "（不明）" : prov.nativeIdeology;
                sb.AppendLine($"住民の思想: {ideo}　人口: {Mathf.RoundToInt(prov.population)}");
                sb.AppendLine($"統合度: {Mathf.RoundToInt(Mathf.Clamp01(prov.integration) * 100f)}%　産出: ×{GovernanceRules.OutputFactor(prov):0.00}");
                if (GovernanceRules.IsUnrest(prov))
                    sb.AppendLine($"反乱圧: {Mathf.RoundToInt(GovernanceRules.RebelPressure(prov) * 100f)}%（安定度が低い）");
            }
            else sb.AppendLine("（未統治＝内政データなし）");

            sb.AppendLine();
            sb.AppendLine("― 惑星防衛 ―");
            Planet p = s.planet;
            if (p != null)
            {
                int ipct = Mathf.FloorToInt(100f * p.invasionProgress / Mathf.Max(1f, p.invasionThreshold));
                sb.AppendLine($"種別: {p.KindName}"); // 惑星/要塞/コロニー（PB-6）
                if (p.maxOrbitalDefense > 0f)
                {
                    int dpct = Mathf.CeilToInt(100f * p.orbitalDefense / Mathf.Max(1f, p.maxOrbitalDefense));
                    sb.AppendLine($"制空権: {dpct}%　{(p.DomainDown ? "（ドメイン・ダウン）" : "（健在＝接近限界）")}");
                }
                else sb.AppendLine("制空権: なし（軌道超兵器なし＝接近限界なし）"); // コロニー
                sb.AppendLine($"侵略値: {ipct}%　{(p.Captured ? "（占領済み）" : "")}");
            }
            else sb.AppendLine("防衛惑星なし（停泊で占領）");

            sb.AppendLine();
            sb.AppendLine("― 在席艦隊 ―");
            sb.AppendLine(string.IsNullOrEmpty(fleetSummary) ? "（なし）" : fleetSummary);

            return sb.ToString();
        }

        private static Color StabilityColor(float frac)
        {
            // 0=赤 / 0.5=黄 / 1=緑
            return frac < 0.5f
                ? Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(0.95f, 0.85f, 0.2f), frac * 2f)
                : Color.Lerp(new Color(0.95f, 0.85f, 0.2f), new Color(0.35f, 0.9f, 0.45f), (frac - 0.5f) * 2f);
        }

        // ===== UI生成ヘルパ（FleetDetailPanel と同作法） =====

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.alignment = align;
            t.color = Color.white; t.raycastTarget = false;
            ApplyJapaneseFont(t);
            return t;
        }

        private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = new GameObject("Button_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.25f, 0.4f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.onClick.AddListener(onClick);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 50f;
            TextMeshProUGUI txt = CreateText(go.transform, label, 24f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFull(txt.rectTransform);
        }

        private void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void OnDestroy()
        {
            if (isOpen) Time.timeScale = savedTimeScale;
            if (instance == this) instance = null;
        }
    }
}
