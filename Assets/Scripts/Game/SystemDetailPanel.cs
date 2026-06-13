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
            canvas.sortingOrder = 950; // 通知/マップより前・観測窓(1090)より後ろの前面ウィンドウ
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            // root：全画面の透明コンテナ（★ディマー無し＝非モーダル＝背後のマップ操作を塞がない）。
            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            // 枠ウィンドウ（中央・タイトルバーをつかんでドラッグ移動）。
            GameObject panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            RectTransform pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(560f, 600f);
            pRT.anchoredPosition = Vector2.zero;
            Image pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.06f, 0.07f, 0.12f, 0.97f);
            Outline border = panel.AddComponent<Outline>();
            border.effectColor = new Color(1f, 0.84f, 0.36f, 0.5f);
            border.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup outer = panel.AddComponent<VerticalLayoutGroup>();
            outer.padding = new RectOffset(0, 0, 0, 0);
            outer.spacing = 0f;
            outer.childControlWidth = true; outer.childForceExpandWidth = true;
            outer.childControlHeight = true; outer.childForceExpandHeight = false;

            BuildTitleBar(panel.transform, pRT);

            // 内容コンテナ（内側パディング・残り高さを埋める）
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(panel.transform, false);
            LayoutElement contentLE = content.AddComponent<LayoutElement>();
            contentLE.flexibleHeight = 1f;
            VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(26, 26, 16, 18);
            vlg.spacing = 10f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            titleText = CreateText(content.transform, "星系情報", 24f, FontStyles.Bold, TextAlignmentOptions.Center);

            // 安定度バー（ラベル＋色付きフィル）
            BuildStabilityBar(content.transform);

            bodyText = CreateText(content.transform, "", 21f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            LayoutElement bodyLE = bodyText.gameObject.AddComponent<LayoutElement>();
            bodyLE.flexibleHeight = 1f;

            root.SetActive(false);
        }

        /// <summary>タイトルバー（Windows 風・つかんでドラッグ移動＋×で閉じる）。観測窓と同型。</summary>
        private void BuildTitleBar(Transform parent, RectTransform windowRT)
        {
            GameObject bar = new GameObject("TitleBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            Image img = bar.AddComponent<Image>();
            img.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            LayoutElement le = bar.AddComponent<LayoutElement>();
            le.minHeight = 30f; le.preferredHeight = 30f;
            UIDragMove drag = bar.AddComponent<UIDragMove>();
            drag.target = windowRT;

            TextMeshProUGUI cap = CreateText(bar.transform, "≡ 星系情報　（ドラッグで移動）", 15f, FontStyles.Normal, TextAlignmentOptions.Left);
            cap.color = new Color(1f, 0.84f, 0.36f);
            RectTransform crt = cap.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(12f, 0f); crt.offsetMax = new Vector2(-42f, 0f);

            GameObject cb = new GameObject("Close", typeof(RectTransform));
            cb.transform.SetParent(bar.transform, false);
            RectTransform cbrt = cb.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(1f, 0f); cbrt.anchorMax = new Vector2(1f, 1f);
            cbrt.pivot = new Vector2(1f, 0.5f); cbrt.sizeDelta = new Vector2(34f, 0f);
            cbrt.anchoredPosition = new Vector2(-3f, 0f);
            Image cimg = cb.AddComponent<Image>();
            cimg.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            Button cbtn = cb.AddComponent<Button>();
            cbtn.transition = UnityEngine.UI.Selectable.Transition.None;
            cbtn.onClick.AddListener(Close);
            TextMeshProUGUI glyph = CreateText(cb.transform, "×", 18f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFull(glyph.rectTransform);
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
                    ? $"安定度 {Mathf.RoundToInt(stability)}%{(GovernanceRules.IsUnrest(prov) ? "　▲反乱リスク" : "")}"
                    : "安定度 —（未統治）";
            }

            if (bodyText != null) bodyText.text = BuildInfo(s, prov, neighborCount, fleetSummary);

            isOpen = true; // 非モーダル（ポーズしない）＝開いたままマップ操作・進行が続く
            if (root != null) root.SetActive(true);
        }

        /// <summary>ウィンドウを閉じる。</summary>
        public void Close()
        {
            isOpen = false;
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (!isOpen) return;
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

                // 経済（#93 を惑星層へ #767）＝SystemView と同じ Core 窓口を読むだけ（数式は二重実装しない）。
                sb.AppendLine($"経済類型: {prov.systemType}");
                float sup = ResourceProductionRules.ProvinceRate(prov, ResourceType.物資);
                float amm = ResourceProductionRules.ProvinceRate(prov, ResourceType.弾薬);
                float fue = ResourceProductionRules.ProvinceRate(prov, ResourceType.燃料);
                sb.AppendLine($"資源産出/秒: 物資 {sup:0.#} / 弾薬 {amm:0.#} / 燃料 {fue:0.#}");
                if (prov.hasStrategicResource) // 希少資源の鉱床（#178・偏在＝一部の惑星のみ）
                {
                    StrategicResourceInfo info = StrategicResourceRules.Info(prov.strategicResource);
                    float srate = StrategicResourceRules.ProvinceRate(prov);
                    sb.AppendLine($"希少資源: {info.displayName}（豊富さ {Mathf.RoundToInt(prov.strategicAbundance * 100f)}%・/秒 {srate:0.##}）");
                }

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
            if (instance == this) instance = null;
        }
    }
}
