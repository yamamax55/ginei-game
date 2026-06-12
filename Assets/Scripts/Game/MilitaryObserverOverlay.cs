using System;
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
    /// 軍 編制オブザーバ（観測層・read-only）。<b>M キー</b>で開閉し、勢力ごとの
    /// <see cref="FleetPool"/>（保有総艦艇）／<see cref="OrderOfBattle"/>（軍集団⊃軍団⊃艦隊の編制ツリー）／
    /// <see cref="FleetRoster"/>（艦隊台帳＝番号・指揮班・兵力）を**そのままダンプ**する。
    /// 指揮班の実効能力（<see cref="CommandStaffRules"/>＝副提督/参謀の補佐込み）も併記する。
    /// 操作はさせない＝軍の Core 純ロジック（#146/#147/#148/#885）が盤面で何を保持しているかを「見える模型」にするだけ。
    /// `CampaignObserverOverlay`（G・国家状態）/ `CoreStateInspector`（J・汎用）と同じ観測層の家族。
    /// 戦略/会戦の両シーンへ自動生成（<see cref="HelpOverlay"/> / <see cref="TimeDisplay"/> と同じ型）。
    /// </summary>
    public class MilitaryObserverOverlay : MonoBehaviour
    {
        // ===== 調整可能なパラメーター =====

        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1091;

        [Tooltip("背景ディマーの不透明度（0〜1）")]
        public float dimAlpha = 0.55f;

        [Tooltip("パネルの幅（ピクセル）")]
        public float panelWidth = 1000f;

        [Tooltip("パネルの最大高さ（ピクセル）")]
        public float panelMaxHeight = 920f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.04f, 0.04f, 0.07f, 0.96f);

        [Tooltip("本文のフォントサイズ")]
        public float bodyFontSize = 19f;

        // ===== 内部状態 =====

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;

        // ===== 自動生成エントリーポイント（HelpOverlay と同型） =====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        /// <summary>戦略/会戦シーンにオブザーバが無ければ生成する（重複生成ガード）。</summary>
        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (UnityEngine.Object.FindAnyObjectByType<MilitaryObserverOverlay>() != null) return;
            new GameObject("MilitaryObserverOverlay").AddComponent<MilitaryObserverOverlay>();
        }

        // ===== Unity ライフサイクル =====

        private void Awake()
        {
            BuildUI();
            SetVisible(false); // 初期は非表示
        }

        private void Update()
        {
            // 開閉（入力は GameInput に集約・#107／共通アクションで戦略/会戦どちらでも）
            if (GameInput.WasPressed(GameAction.軍観測切替))
                Toggle();

            // 表示中だけ毎フレーム最新の状態を流し込む（台帳の変化をライブで眺める）
            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        // ===== 公開API =====

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== ダンプ本体 =====

        /// <summary>
        /// 勢力ごとに艦艇プール／編制ツリー／艦隊台帳をダンプする。台帳・ツリー・プールはすべて
        /// 旧 <see cref="Faction"/> enum をキーに持つ static ストアなので、enum を総当りして集約する。観測のみ＝状態は変えない。
        /// </summary>
        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            sb.Append("<b>軍 編制オブザーバ</b>　艦艇プール・編制ツリー・艦隊台帳　(M で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            bool any = false;
            foreach (Faction f in Enum.GetValues(typeof(Faction)))
            {
                int pool = FleetPool.Get(f);
                IReadOnlyList<FleetUnitData> fleets = FleetRoster.AllFleets(f);
                IReadOnlyList<MilitaryFormation> formations = OrderOfBattle.AllFormations(f);
                bool hasData = pool > 0 || (fleets != null && fleets.Count > 0) || (formations != null && formations.Count > 0);
                if (!hasData) continue;
                any = true;
                AppendFaction(sb, f, pool, fleets, formations);
            }

            if (!any)
            {
                sb.Append("\n<color=#ffcc66>軍の編制データがありません。</color>\n");
                sb.Append("会戦シーン（BattleSetup）が艦隊台帳・編制ツリーを構築すると、\n");
                sb.Append("ここに各勢力の艦艇プール・梯団ツリー・艦隊一覧がライブ表示されます。");
            }
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, Faction f, int pool,
            IReadOnlyList<FleetUnitData> fleets, IReadOnlyList<MilitaryFormation> formations)
        {
            sb.Append('\n').Append("<color=#bfe9c0>◤ ").Append(f).Append("</color>\n");
            sb.Append("  <color=#9fb0c0>艦艇プール（保有総艦艇）</color> ＝ <color=#ffd28a>")
              .Append(pool.ToString("#,0")).Append("</color>\n");

            AppendOrderOfBattle(sb, f, formations);
            AppendRoster(sb, fleets);
        }

        // ----- 編制ツリー（軍集団 ⊃ 軍団 ⊃ 艦隊） -----

        private void AppendOrderOfBattle(StringBuilder sb, Faction f, IReadOnlyList<MilitaryFormation> formations)
        {
            sb.Append("  <color=#9aa7b3>― 編制ツリー OrderOfBattle</color>\n");
            if (formations == null || formations.Count == 0)
            {
                sb.Append("    <color=#7a8694>（梯団なし＝艦隊は直轄）</color>\n");
                return;
            }

            // 親を持たない（最上位）梯団から再帰表示。循環・孤立ガード付き。
            var byId = new Dictionary<int, MilitaryFormation>();
            for (int i = 0; i < formations.Count; i++) byId[formations[i].id] = formations[i];

            var visited = new HashSet<int>();
            for (int i = 0; i < formations.Count; i++)
            {
                MilitaryFormation top = formations[i];
                bool isTop = top.parentId == 0 || !byId.ContainsKey(top.parentId);
                if (isTop) AppendFormationNode(sb, top, byId, 2, visited);
            }
            // 親が同勢力に見つからない等で取り残したノードも拾う（孤立ガード）
            for (int i = 0; i < formations.Count; i++)
                if (!visited.Contains(formations[i].id)) AppendFormationNode(sb, formations[i], byId, 2, visited);
        }

        private void AppendFormationNode(StringBuilder sb, MilitaryFormation node,
            Dictionary<int, MilitaryFormation> byId, int depth, HashSet<int> visited)
        {
            if (node == null || !visited.Add(node.id)) return;
            string indent = new string('　', depth);
            int requiredTier = OrderOfBattle.RequiredTier(node.echelon);
            int under = OrderOfBattle.CountFleetsUnder(node.id);

            // ORBAT-2 規模／ORBAT-4 戦略・作戦・戦術区分（観測表示）
            EchelonProfile prof = CommandCapacityRules.ProfileFor(node.echelon);
            UnitEchelonClass orgClass = OrgClassRules.ClassOf(node.echelon);
            sb.Append(indent).Append("<color=#c9b3ff>[").Append(node.echelon).Append("]</color> ")
              .Append(node.DisplayName);
            sb.Append("　<color=#7f93a6>").Append(orgClass).Append("単位 ").Append(prof.ScaleText).Append("</color>");
            sb.Append("　司令: ").Append(CommanderLabel(node.commander, null, requiredTier));
            sb.Append("　<color=#8aa0b0>配下艦隊 ").Append(under).Append("</color>\n");

            // 直下の艦隊番号
            if (node.fleetNumbers != null && node.fleetNumbers.Count > 0)
            {
                sb.Append(indent).Append("　<color=#7f93a6>直下艦隊: ");
                for (int i = 0; i < node.fleetNumbers.Count; i++)
                {
                    if (i > 0) sb.Append(" / ");
                    sb.Append('#').Append(node.fleetNumbers[i]);
                }
                sb.Append("</color>\n");
            }

            // 下位梯団を再帰
            if (node.childFormationIds != null)
                for (int i = 0; i < node.childFormationIds.Count; i++)
                    if (byId.TryGetValue(node.childFormationIds[i], out var child))
                        AppendFormationNode(sb, child, byId, depth + 1, visited);
        }

        // ----- 艦隊台帳（FleetRoster） -----

        private void AppendRoster(StringBuilder sb, IReadOnlyList<FleetUnitData> fleets)
        {
            sb.Append("  <color=#9aa7b3>― 艦隊台帳 FleetRoster</color>\n");
            if (fleets == null || fleets.Count == 0)
            {
                sb.Append("    <color=#7a8694>（登録艦隊なし）</color>\n");
                return;
            }

            var prm = CommandStaffRules.CommandParams.Default;
            for (int i = 0; i < fleets.Count; i++)
            {
                FleetUnitData u = fleets[i];
                if (u == null) continue;

                string status = u.status == FleetStatus.現役 ? "現役"
                              : u.status == FleetStatus.解隊 ? "<color=#ff9a8a>解隊</color>"
                              : "<color=#ff6b6b>永久欠番</color>";
                sb.Append("    <color=#bfe9c0>◆ ").Append(u.DisplayName).Append("</color>")
                  .Append("　<color=#8aa0b0>[").Append(status).Append(" / ").Append(u.shipRole).Append("]</color>\n");

                // 指揮官（提督）
                if (u.assignedAdmiral != null)
                {
                    int req = OrderOfBattle.RequiredTier(EchelonType.艦隊); // 艦隊司令の参考ゲート（中将7・ORBAT-2 一表）
                    sb.Append("       司令: ").Append(CommanderLabel(u.assignedAdmiral, u.factionData, req)).Append('\n');
                    // 指揮班込みの実効能力（CMD-3＝副提督/参謀の補佐）
                    sb.Append("       <color=#9fb0c0>実効</color> 統率 ").Append(CommandStaffRules.EffectiveLeadership(u, prm))
                      .Append(" ／ 防御 ").Append(CommandStaffRules.EffectiveDefense(u, prm))
                      .Append(" ／ 運営 ").Append(CommandStaffRules.EffectiveOperation(u, prm))
                      .Append(" ／ 情報 ").Append(CommandStaffRules.EffectiveIntelligence(u, prm)).Append('\n');
                }
                else
                {
                    sb.Append("       司令: <color=#7a8694>空席</color>\n");
                }

                // 指揮班（副提督・参謀）＋兵力
                var extra = new StringBuilder();
                if (u.HasVice) extra.Append("副提督: ").Append(ShortName(u.viceCommander));
                if (u.HasChief) { if (extra.Length > 0) extra.Append("　"); extra.Append("参謀: ").Append(ShortName(u.chiefOfStaff)); }
                int strength = u.baseStrength > 0 ? u.baseStrength
                             : (u.assignedAdmiral != null ? u.assignedAdmiral.baseStrength : 0);
                sb.Append("       <color=#8aa0b0>兵力 ").Append(strength.ToString("#,0")).Append("</color>");
                if (extra.Length > 0) sb.Append("　").Append(extra);
                sb.Append('\n');
            }
        }

        // ===== ヘルパー =====

        /// <summary>「階級名 短縮名（要 tierN）」形式で指揮官を表示する（空席は呼ばない）。</summary>
        private static string CommanderLabel(AdmiralData a, FactionData fd, int requiredTier)
        {
            if (a == null) return "<color=#7a8694>空席</color>";
            string rank = RankSystem.ResolveRankNameOrDefault(fd, a.rankTier);
            string rankPart = string.IsNullOrEmpty(rank) ? "" : rank + " ";
            bool gateOk = a.rankTier >= requiredTier;
            string nameCol = gateOk ? "#e6edf3" : "#ffb38a"; // 階級不足は橙で警告（ゲート可視化）
            string body = $"<color={nameCol}>{rankPart}{ShortName(a)}</color>";
            if (requiredTier > 0 && !gateOk) body += $"<color=#ff8a6b>（要 tier{requiredTier}）</color>";
            return body;
        }

        private static string ShortName(AdmiralData a)
            => a == null ? "" : a.ShortName;

        // ===== UI 構築（CampaignObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("MilitaryObserverCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            // 全画面ディマー兼パネル root
            panel = new GameObject("ObserverPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;
            panelRT.anchoredPosition = Vector2.zero;
            Image dimImage = panel.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, dimAlpha);

            BuildContentPanel(panel.transform);
        }

        private void BuildContentPanel(Transform parent)
        {
            // 外枠フレーム（固定サイズ・左寄せ＝盤面を見ながら横で眺められる）
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
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
