using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 艦艇オブザーバ（観測層・read-only）。<b>B キー</b>または上メニュー「艦艇」で開閉し、勢力ごとの
    /// <b>艦艇プール（総/割当/残）</b>（<see cref="FleetPool"/>＋<see cref="FleetPoolRules"/>）と
    /// <b>艦隊台帳</b>（<see cref="FleetRoster"/>＝各艦隊の番号・兵力(艦艇数)・役割・状態・指揮班〔提督/副提督/参謀〕）を
    /// ダンプする。指揮班の実効能力（<see cref="CommandStaffRules"/>）も併記。編制ツリー（軍集団⊃軍団）は軍事オブザーバ
    /// （<see cref="MilitaryObserverOverlay"/>・M）が担い、こちらは<b>艦艇の在庫と各艦隊の中身</b>に特化する。
    /// 旧・艦隊編成メニュー（FleetOrganizationPanel）の観測版＝<b>操作はさせない</b>。Strategy/Battle へ自動生成。
    /// </summary>
    public class FleetObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        [Tooltip("オーバーレイ Canvas の描画順（他UIより手前）")]
        public int canvasSortingOrder = 1090;

        [Tooltip("背景ディマーの不透明度（0〜1）")]
        public float dimAlpha = 0.55f;

        [Tooltip("パネルの幅（ピクセル）")]
        public float panelWidth = 1000f;

        [Tooltip("パネルの最大高さ（ピクセル）")]
        public float panelMaxHeight = 920f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.04f, 0.05f, 0.07f, 0.96f);

        [Tooltip("本文のフォントサイズ")]
        public float bodyFontSize = 19f;

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;
        private object escWindowToken; // UIWindowStack 登録トークン（#ウィンドウESC）
        private TMP_FontAsset jpFont;

        // 艦隊名（第N艦隊）のクリック（TMP リンク）→ 艦隊編成カード
        private readonly Dictionary<string, FleetUnitData> linkTargets = new Dictionary<string, FleetUnitData>();
        private int linkSeq;
        private GameObject detailRoot;
        private TextMeshProUGUI detailTitle;
        private TextMeshProUGUI detailBody;
        private object escDetailToken;

        // ===== 自動生成エントリーポイント（HelpOverlay と同型） =====

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
            if (UnityEngine.Object.FindAnyObjectByType<FleetObserverOverlay>() != null) return;
            new GameObject("FleetObserverOverlay").AddComponent<FleetObserverOverlay>();
        }

        // ===== Unity ライフサイクル =====

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            BuildUI();
            BuildDetail();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "艦艇");
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            UIWindowStack.Unregister(escDetailToken);
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.艦艇観測切替))
                Toggle();

            if (panel != null && panel.activeSelf && bodyLabel != null)
            {
                bodyLabel.text = BuildDump();
                HandleLinkClick();
            }
        }

        /// <summary>艦隊名（TMP リンク）をクリックしたら艦隊編成カードを開く。</summary>
        private void HandleLinkClick()
        {
            if (detailRoot != null && detailRoot.activeSelf) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            int li = TMP_TextUtilities.FindIntersectingLink(bodyLabel, Mouse.current.position.ReadValue(), null);
            if (li < 0) return;
            string id = bodyLabel.textInfo.linkInfo[li].GetLinkID();
            if (linkTargets.TryGetValue(id, out FleetUnitData u)) OpenDetail(u);
        }

        private string Link(FleetUnitData u)
        {
            string id = "F" + (linkSeq++);
            linkTargets[id] = u;
            return id;
        }

        // ===== 公開API =====

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== ダンプ本体 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            linkTargets.Clear(); linkSeq = 0; // 艦隊名リンクを毎回採番し直す
            sb.Append("<b>艦艇オブザーバ</b>　艦艇プール（総/割当/残）・艦隊台帳　<color=#8aa0b0>(艦隊名クリックで編成／B で閉じる)</color>\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            bool any = false;
            foreach (Faction f in Enum.GetValues(typeof(Faction)))
            {
                int pool = FleetPool.Get(f);
                IReadOnlyList<FleetUnitData> fleets = FleetRoster.AllFleets(f);
                bool hasData = pool > 0 || (fleets != null && fleets.Count > 0);
                if (!hasData) continue;
                any = true;
                AppendFaction(sb, f, pool, fleets);
            }

            if (!any)
            {
                sb.Append("\n<color=#ffcc66>艦艇データがありません。</color>\n");
                sb.Append("会戦シーン（BattleSetup）／戦略デモが艦隊台帳・艦艇プールを構築すると、\n");
                sb.Append("ここに各勢力の艦艇プールと艦隊一覧（兵力・指揮班）がライブ表示されます。");
            }
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, Faction f, int pool, IReadOnlyList<FleetUnitData> fleets)
        {
            int allocated = FleetPoolRules.Allocated(f);
            int available = FleetPoolRules.Available(f, pool);

            sb.Append('\n').Append("<color=#bfe9c0>◤ ").Append(f).Append("</color>\n");
            sb.Append("  <color=#9fb0c0>艦艇プール</color>　総 <color=#ffd28a>").Append(pool.ToString("#,0")).Append("</color>")
              .Append("　割当 <color=#a0c8ff>").Append(allocated.ToString("#,0")).Append("</color>")
              .Append("　残 <color=#a0e0a0>").Append(available.ToString("#,0")).Append("</color>\n");

            AppendRoster(sb, fleets);
        }

        private void AppendRoster(StringBuilder sb, IReadOnlyList<FleetUnitData> fleets)
        {
            sb.Append("  <color=#9aa7b3>― 艦隊台帳 FleetRoster</color>\n");
            if (fleets == null || fleets.Count == 0)
            {
                sb.Append("    <color=#7a8694>（登録艦隊なし）</color>\n");
                return;
            }

            var prm = CommandStaffRules.CommandParams.Default;
            int activeCount = 0, totalShips = 0;
            for (int i = 0; i < fleets.Count; i++)
            {
                FleetUnitData u = fleets[i];
                if (u == null) continue;

                string status = u.status == FleetStatus.現役 ? "現役"
                              : u.status == FleetStatus.解隊 ? "<color=#ff9a8a>解隊</color>"
                              : "<color=#ff6b6b>永久欠番</color>";
                int strength = u.baseStrength > 0 ? u.baseStrength
                             : (u.assignedAdmiral != null ? u.assignedAdmiral.baseStrength : 0);
                if (u.status == FleetStatus.現役) { activeCount++; totalShips += strength; }

                sb.Append("    <color=#bfe9c0>◆ <link=\"").Append(Link(u)).Append("\">").Append(u.DisplayName).Append("</link></color>")
                  .Append("　<color=#8aa0b0>[").Append(status).Append(" / ").Append(u.shipRole).Append("]</color>")
                  .Append("　<color=#ffd28a>兵力 ").Append(strength.ToString("#,0")).Append("</color>\n");

                // 指揮班（提督・副提督・参謀）
                if (u.assignedAdmiral != null)
                {
                    int req = OrderOfBattle.RequiredTier(EchelonType.艦隊); // 艦隊司令の参考ゲート（ORBAT-2 一表）
                    sb.Append("       司令: ").Append(CommanderLabel(u.assignedAdmiral, u.factionData, req));
                    if (u.HasVice) sb.Append("　副提督: ").Append(ShortName(u.viceCommander));
                    if (u.HasChief) sb.Append("　参謀: ").Append(ShortName(u.chiefOfStaff));
                    sb.Append('\n');
                    sb.Append("       <color=#9fb0c0>実効</color> 統率 ").Append(CommandStaffRules.EffectiveLeadership(u, prm))
                      .Append(" ／ 防御 ").Append(CommandStaffRules.EffectiveDefense(u, prm))
                      .Append(" ／ 運営 ").Append(CommandStaffRules.EffectiveOperation(u, prm))
                      .Append(" ／ 情報 ").Append(CommandStaffRules.EffectiveIntelligence(u, prm)).Append('\n');
                }
                else
                {
                    sb.Append("       司令: <color=#7a8694>空席</color>\n");
                }
            }

            sb.Append("    <color=#6f8a9a>現役 ").Append(activeCount).Append(" 隊・盤面兵力合計 ")
              .Append(totalShips.ToString("#,0")).Append("</color>\n");
        }

        // ===== ヘルパー（MilitaryObserverOverlay と同型） =====

        private static string CommanderLabel(AdmiralData a, FactionData fd, int requiredTier)
        {
            if (a == null) return "<color=#7a8694>空席</color>";
            string rank = RankSystem.ResolveRankNameOrDefault(fd, a.rankTier);
            string rankPart = string.IsNullOrEmpty(rank) ? "" : rank + " ";
            bool gateOk = a.rankTier >= requiredTier;
            string nameCol = gateOk ? "#e6edf3" : "#ffb38a";
            string body = $"<color={nameCol}>{rankPart}{ShortName(a)}</color>";
            if (requiredTier > 0 && !gateOk) body += $"<color=#ff8a6b>（要 tier{requiredTier}）</color>";
            return body;
        }

        private static string ShortName(AdmiralData a) => a == null ? "" : a.ShortName;

        // ===== 艦隊編成カード（艦隊名クリックで開く・添付画像を本システムに落とし込み） =====

        private void OpenDetail(FleetUnitData u)
        {
            if (u == null || detailRoot == null) return;
            FillFleet(u);
            detailRoot.transform.SetAsLastSibling();
            detailRoot.SetActive(true);
        }

        private void CloseDetail() { if (detailRoot != null) detailRoot.SetActive(false); }

        private void FillFleet(FleetUnitData u)
        {
            var prm = CommandStaffRules.CommandParams.Default;
            int strength = u.baseStrength > 0 ? u.baseStrength
                         : (u.assignedAdmiral != null ? u.assignedAdmiral.baseStrength : 0);

            detailTitle.text = $"{u.DisplayName}　<size=70%><color=#9fb0c0>[{u.faction}]</color></size>";

            var sb = new StringBuilder(720);

            // 艦隊司令官
            if (u.assignedAdmiral != null)
            {
                var a = u.assignedAdmiral;
                string rank = RankSystem.ResolveRankNameOrDefault(u.factionData, a.rankTier);
                sb.Append($"<color=#9fb0c0>艦隊司令官</color>　{(string.IsNullOrEmpty(rank) ? "" : rank + " ")}{a.ShortName}\n");
            }
            else sb.Append("<color=#9fb0c0>艦隊司令官</color>　<color=#7a8694>空席</color>\n");

            // 艦隊幕僚（副提督・参謀・提督付き幕僚）
            var staff = new List<string>();
            if (u.HasVice) staff.Add("副提督 " + ShortName(u.viceCommander));
            if (u.HasChief) staff.Add("参謀 " + ShortName(u.chiefOfStaff));
            if (u.assignedAdmiral != null && u.assignedAdmiral.HasStaff) staff.Add("幕僚 " + u.assignedAdmiral.GetStaffNames());
            sb.Append("<color=#9fb0c0>艦隊幕僚</color>　").Append(staff.Count == 0 ? "<color=#7a8694>（新任枠）</color>" : string.Join("　", staff)).Append('\n');

            // 実効能力（指揮班補正込み）
            if (u.assignedAdmiral != null)
                sb.Append($"<color=#9fb0c0>実効</color> 統率 {CommandStaffRules.EffectiveLeadership(u, prm)} ／ 防御 {CommandStaffRules.EffectiveDefense(u, prm)} ／ 運営 {CommandStaffRules.EffectiveOperation(u, prm)} ／ 情報 {CommandStaffRules.EffectiveIntelligence(u, prm)}\n");

            sb.Append('\n');

            // 部隊編成（陣形・役割・状態・旗艦・兵力）
            string flagship = (u.assignedAdmiral != null && !string.IsNullOrEmpty(u.assignedAdmiral.signatureShipName))
                            ? u.assignedAdmiral.signatureShipName : "標準旗艦";
            sb.Append($"<color=#9fb0c0>部隊編成</color>　陣形 {u.formation} ／ 役割 {u.shipRole} ／ 状態 {u.status}\n");
            sb.Append($"<color=#9fb0c0>旗艦</color> {flagship}　<color=#9fb0c0>兵力</color> <color=#ffd28a>{strength:#,0}</color>\n");

            // 所属梯団（OrderOfBattle ツリーでの位置）
            string ech = FindEchelonPath(u.faction, u.fleetNumber);
            if (!string.IsNullOrEmpty(ech)) sb.Append($"<color=#9fb0c0>所属梯団</color>　{ech}\n");

            // 艦種内訳（兵力からの推計＝添付画像の艦種一覧に相当）
            sb.Append("\n<color=#9aa7b3>― 艦種内訳（兵力からの推計）―</color>\n");
            AppendClassEstimate(sb, strength);

            detailBody.text = sb.ToString();
        }

        /// <summary>OrderOfBattle ツリーでこの艦隊が属する梯団パス（軍集団 ⊃ 軍団）を返す。無所属は空。</summary>
        private string FindEchelonPath(Faction f, int fleetNumber)
        {
            IReadOnlyList<MilitaryFormation> forms = OrderOfBattle.AllFormations(f);
            if (forms == null) return "";
            MilitaryFormation owner = null;
            for (int i = 0; i < forms.Count; i++)
                if (forms[i] != null && forms[i].fleetNumbers != null && forms[i].fleetNumbers.Contains(fleetNumber)) { owner = forms[i]; break; }
            if (owner == null) return "";
            var names = new List<string>();
            MilitaryFormation cur = owner;
            int guard = 0;
            while (cur != null && guard++ < 8)
            {
                names.Insert(0, cur.DisplayName);
                cur = cur.parentId != 0 ? OrderOfBattle.Get(cur.parentId) : null;
            }
            return string.Join(" ⊃ ", names);
        }

        private static readonly (string name, float ratio)[] ClassMix =
        {
            ("戦艦", 0.30f), ("高速戦艦", 0.05f), ("巡航艦", 0.15f), ("駆逐艦", 0.30f),
            ("攻撃空母", 0.05f), ("工作艦", 0.05f), ("輸送艦", 0.07f), ("強襲揚陸艦", 0.03f)
        };

        /// <summary>抽象兵力から艦種別の隻数を推計して2列で並べる（添付画像の艦種カウントに相当・あくまで目安）。</summary>
        private void AppendClassEstimate(StringBuilder sb, int strength)
        {
            int ships = Mathf.Max(0, Mathf.RoundToInt(strength / 15f)); // 抽象兵力→隻数の目安
            for (int i = 0; i < ClassMix.Length; i++)
            {
                int c = Mathf.RoundToInt(ships * ClassMix[i].ratio);
                sb.Append($"  {ClassMix[i].name} <color=#e6edf3>{c}</color>");
                if (i % 2 == 1) sb.Append('\n');
            }
            if (ClassMix.Length % 2 == 1) sb.Append('\n');
        }

        // ===== 詳細カード UI =====

        private void BuildDetail()
        {
            detailRoot = new GameObject("FleetDetail");
            detailRoot.transform.SetParent(overlayRoot.transform, false);
            var drt = detailRoot.AddComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
            var dim = detailRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            var dimBtn = detailRoot.AddComponent<Button>();
            dimBtn.transition = UnityEngine.UI.Selectable.Transition.None;
            dimBtn.onClick.AddListener(CloseDetail);

            var frame = new GameObject("Frame");
            frame.transform.SetParent(detailRoot.transform, false);
            var frt = frame.AddComponent<RectTransform>();
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(700f, 470f);
            frame.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.16f, 0.99f);

            detailTitle = MakeLabel(frame.transform, "", 24f, new Color(1f, 0.85f, 0.4f));
            var ttr = detailTitle.rectTransform;
            ttr.anchorMin = new Vector2(0f, 1f); ttr.anchorMax = new Vector2(1f, 1f); ttr.pivot = new Vector2(0f, 1f);
            ttr.offsetMin = new Vector2(20f, -54f); ttr.offsetMax = new Vector2(-20f, -16f);
            detailTitle.alignment = TextAlignmentOptions.TopLeft;

            detailBody = MakeLabel(frame.transform, "", 18f, new Color(0.92f, 0.94f, 0.97f));
            var btr = detailBody.rectTransform;
            btr.anchorMin = Vector2.zero; btr.anchorMax = Vector2.one;
            btr.offsetMin = new Vector2(20f, 56f); btr.offsetMax = new Vector2(-20f, -58f);
            detailBody.alignment = TextAlignmentOptions.TopLeft;
            detailBody.enableWordWrapping = true;

            var btnObj = new GameObject("Close");
            btnObj.transform.SetParent(frame.transform, false);
            var crt = btnObj.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0f); crt.anchorMax = new Vector2(1f, 0f); crt.pivot = new Vector2(1f, 0f);
            crt.sizeDelta = new Vector2(120f, 38f); crt.anchoredPosition = new Vector2(-16f, 14f);
            btnObj.AddComponent<Image>().color = new Color(0.26f, 0.40f, 0.56f, 1f);
            var cbtn = btnObj.AddComponent<Button>();
            cbtn.transition = UnityEngine.UI.Selectable.Transition.None;
            cbtn.onClick.AddListener(CloseDetail);
            var clabel = MakeLabel(btnObj.transform, "閉じる", 18f, Color.white);
            clabel.alignment = TextAlignmentOptions.Center;
            var clrt = clabel.rectTransform;
            clrt.anchorMin = Vector2.zero; clrt.anchorMax = Vector2.one; clrt.offsetMin = Vector2.zero; clrt.offsetMax = Vector2.zero;

            escDetailToken = UIWindowStack.Register(() => detailRoot != null && detailRoot.activeSelf, CloseDetail, canvasSortingOrder + 1, "艦隊編成");
            detailRoot.SetActive(false);
        }

        private TextMeshProUGUI MakeLabel(Transform parent, string text, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.raycastTarget = false; t.richText = true;
            if (jpFont != null) t.font = jpFont;
            return t;
        }

        // ===== UI 構築（MilitaryObserverOverlay と同型） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("FleetObserverCanvas");
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
            WindowChrome.MakeNonModal(dimImage); // 非モーダル（盤面を塞がない）

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

            WindowChrome.AddTitleBarLayout(frameRT, "艦艇", () => SetVisible(false));
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
