using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 稟議オブザーバ（観測層・read-only）。<b>Alt+I キー</b>で開閉し、進行中の稟議（<see cref="Petition"/>）を
    /// <b>起案者（drafterId）と決裁者（addresseeId）の対応</b>で一覧する。税の稟議（<see cref="RingiDirector.Ledger"/>）と
    /// 編制の稟議（<see cref="FleetRingiDirector.Ledger"/>）の両在庫を集約し、各件の 状態（<see cref="WorkflowRules"/>）・
    /// 出自（建白/諮問/注入）・宛先の箱（国王/政治家/地方）・中継者を毎フレームライブダンプする。
    /// 起案者/決裁者の人物 id は <see cref="GalaxyView"/> の人物ロスター（指揮官＋文民）で名前解決し、
    /// 箱宛て（id=0＝プレイヤーの越階回路）はその箱を決裁者として表示する。決裁の Kanban 管理は K（<see cref="DecisionBoardPanel"/>）、
    /// 本窓は<b>「誰が起案し誰が裁可するか」の見取り図</b>に特化＝操作はさせない（観測専用＝状態は変えない）。
    /// `DiplomacyObserverOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class RingiObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1114;

        [Tooltip("背景ディマーの不透明度（0〜1）")]
        public float dimAlpha = 0.55f;

        [Tooltip("パネルの幅（ピクセル）")]
        public float panelWidth = 1020f;

        [Tooltip("パネルの最大高さ（ピクセル）")]
        public float panelMaxHeight = 900f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);

        [Tooltip("本文のフォントサイズ")]
        public float bodyFontSize = 20f;

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;
        private object escWindowToken;

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
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (Object.FindAnyObjectByType<RingiObserverOverlay>() != null) return;
            new GameObject("RingiObserverOverlay").AddComponent<RingiObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "稟議");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.稟議観測切替))
                Toggle();

            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== ダンプ本体 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(2048);
            sb.Append("<b>稟議オブザーバ</b>　進行中の稟議の 起案者 → 決裁者　(Alt+I で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            // 人物名の解決元（戦略マップの人物ロスター）。1回だけ取得して各件で使い回す。
            GalaxyView gv = Object.FindAnyObjectByType<GalaxyView>();

            int totalActive = AppendLedger(sb, "◤ 税の稟議（建白＝減税/増税）", RingiDirector.Ledger, "—（制度発議）", gv);
            totalActive += AppendLedger(sb, "◤ 編制の稟議（艦隊の設立/解散）", FleetRingiDirector.Ledger, "参謀本部", gv);

            if (totalActive == 0)
                sb.Append("\n<color=#9aa7b2>現在、進行中の稟議はありません（戦略マップで時おり建白が起きます）。</color>\n");

            sb.Append("\n<color=#6f8a9a>※ 起案者＝建白を起こした人物（id=0 は匿名/制度発議）。決裁者＝宛先＝裁可する人物、");
            sb.Append("id=0 は箱宛て＝プレイヤーの越階回路（あなたが右下の決裁デスクで裁可）。</color>");
            return sb.ToString();
        }

        /// <summary>1つの在庫（ledger）の進行中の稟議を起案者→決裁者で列挙する。進行中の件数を返す。</summary>
        private int AppendLedger(StringBuilder sb, string heading, PetitionLedger ledger, string anonymousDrafter, GalaxyView gv)
        {
            sb.Append("\n<color=#e7e0b0>").Append(heading).Append("</color>\n");
            if (ledger == null)
            {
                sb.Append("  <color=#9aa7b2>（在庫なし）</color>\n");
                return 0;
            }

            List<Petition> active = ledger.Active();
            if (active.Count == 0)
            {
                sb.Append("  <color=#9aa7b2>（進行中なし）</color>");
                if (ledger.droppedCount > 0)
                    sb.Append("　<color=#6f8a9a>（古い決着済 ").Append(ledger.droppedCount).Append(" 件を掃き出し）</color>");
                sb.Append('\n');
                return 0;
            }

            for (int i = 0; i < active.Count; i++)
            {
                Petition p = active[i];
                if (p == null) continue;

                sb.Append("  <color=").Append(StatusColor(p.status)).Append(">【").Append(p.status).Append("】</color> ");
                sb.Append("<color=#e6e6e6>").Append(string.IsNullOrEmpty(p.title) ? "（無題）" : p.title).Append("</color>\n");

                string drafter = ResolveDrafter(p, anonymousDrafter, gv);
                string approver = ResolveApprover(p, gv);
                sb.Append("      起案 <color=#9ad0ff>").Append(drafter).Append("</color>")
                  .Append("　<color=#ffd28a>→</color>　決裁 <color=#ffcc66>").Append(approver).Append("</color>\n");

                sb.Append("      <color=#6f8a9a>").Append(p.faction).Append(" / 出自:").Append(p.origin)
                  .Append(" / ").Append(p.box).Append("箱");
                if (!string.IsNullOrEmpty(p.regionKey)) sb.Append('(').Append(p.regionKey).Append(')');
                string carrier = ResolveCarrier(p, gv);
                if (!string.IsNullOrEmpty(carrier)) sb.Append(" / 中継:").Append(carrier);
                if (p.hops != null && p.hops.Count > 0) sb.Append(" / 経由").Append(p.hops.Count).Append("名");
                if (p.distorted) sb.Append(" / <color=#ff9a8a>歪曲</color>");
                sb.Append("</color>\n");
            }
            return active.Count;
        }

        // ----- 起案者/決裁者/中継者の名前解決 -----

        private static string ResolveDrafter(Petition p, string anonymousLabel, GalaxyView gv)
        {
            if (p.drafterId == 0) return anonymousLabel;
            return ResolveName(p.drafterId, gv) ?? $"人物#{p.drafterId}";
        }

        private static string ResolveApprover(Petition p, GalaxyView gv)
        {
            if (p.addresseeId != 0)
                return ResolveName(p.addresseeId, gv) ?? $"人物#{p.addresseeId}";
            // 箱宛て（id=0）＝プレイヤーの越階回路。どの箱が裁可するかを示す。
            switch (p.box)
            {
                case BoxKind.国王:   return "国王箱（越階＝あなた）";
                case BoxKind.政治家: return "政治家箱（越階＝あなた）";
                case BoxKind.地方:   return string.IsNullOrEmpty(p.regionKey)
                    ? "地方箱（越階＝あなた）"
                    : $"地方箱・{p.regionKey}（越階＝あなた）";
                default: return "（箱宛て）";
            }
        }

        private static string ResolveCarrier(Petition p, GalaxyView gv)
        {
            if (p.carrierId == 0 || p.carrierId == p.drafterId) return null;
            return ResolveName(p.carrierId, gv) ?? $"人物#{p.carrierId}";
        }

        /// <summary>人物 id（ICharacter.Id）を <see cref="GalaxyView"/> の指揮官/文民ロスターで名前解決（無ければ null）。</summary>
        private static string ResolveName(int id, GalaxyView gv)
        {
            if (id == 0 || gv == null) return null;
            string n = FindInRoster(id, gv.CommanderRoster);
            if (n != null) return n;
            return FindInRoster(id, gv.CivilianRoster);
        }

        private static string FindInRoster(int id, IReadOnlyList<Person> roster)
        {
            if (roster == null) return null;
            for (int i = 0; i < roster.Count; i++)
            {
                Person person = roster[i];
                if (person != null && person.id == id) return person.name;
            }
            return null;
        }

        private static string StatusColor(PetitionStatus s)
        {
            switch (s)
            {
                case PetitionStatus.決裁待ち: return "#ffcc66";
                case PetitionStatus.承認:     return "#8ce08c";
                case PetitionStatus.却下:     return "#ff7a6a";
                case PetitionStatus.再浮上:   return "#c9a0ff";
                case PetitionStatus.執行済:   return "#7fd4ff";
                default: return "#9aa7b2"; // 起案/伝播中/黙殺
            }
        }

        // ===== UI 構築（DiplomacyObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("RingiObserverCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            panel = new GameObject("ObserverPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;
            panelRT.anchoredPosition = Vector2.zero;
            Image dimImage = panel.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, dimAlpha);
            WindowChrome.MakeNonModal(dimImage);

            BuildContentPanel(panel.transform);
        }

        private void BuildContentPanel(Transform parent)
        {
            GameObject frame = new GameObject("ObserverFrame");
            frame.transform.SetParent(parent, false);
            RectTransform frameRT = frame.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0f, 0.5f);
            frameRT.anchorMax = new Vector2(0f, 0.5f);
            frameRT.pivot = new Vector2(0f, 0.5f);
            frameRT.anchoredPosition = new Vector2(24f, 0f);
            frameRT.sizeDelta = new Vector2(panelWidth, panelMaxHeight);

            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = panelColor;

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            WindowChrome.AddTitleBarLayout(frameRT, "稟議", () => SetVisible(false));
            BuildScrollBody(frame.transform);
        }

        private void BuildScrollBody(Transform parent)
        {
            GameObject scrollObj = new GameObject("ObserverScrollRect");
            scrollObj.transform.SetParent(parent, false);
            scrollObj.AddComponent<RectTransform>();
            LayoutElement scrollLE = scrollObj.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            viewportRT.anchoredPosition = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(8, 8, 4, 4);
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;

            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(content.transform, false);
            bodyLabel = bodyObj.AddComponent<TextMeshProUGUI>();
            bodyLabel.text = "";
            bodyLabel.fontSize = bodyFontSize;
            bodyLabel.color = new Color(0.9f, 0.93f, 0.96f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.richText = true;
            bodyLabel.raycastTarget = false;
            ApplyJapaneseFont(bodyLabel);
        }

        private static void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
