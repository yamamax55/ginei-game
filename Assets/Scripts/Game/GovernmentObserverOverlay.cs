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
    /// 政府オブザーバ（観測層・read-only）。<b>Alt+G</b>で開閉し、勢力ごとに文民政府の人事と省庁編制を集約してライブダンプする：
    /// 首班（<see cref="PartyRules.Premier"/>＝与党党首）／要職任命（<see cref="GovernmentRegistry.Appointments"/>）／
    /// 省庁ツリー（<see cref="Ministry"/>＝二官八省・#158）。観測専用ゆえ既存フィールドのみ読む＝**状態は変えない**。
    /// `HelpOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class GovernmentObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1104;
        public float dimAlpha = 0.55f;
        public float panelWidth = 980f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);
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
            if (Object.FindAnyObjectByType<GovernmentObserverOverlay>() != null) return;
            new GameObject("GovernmentObserverOverlay").AddComponent<GovernmentObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "政府");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.政府観測切替))
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
            CampaignState c = StrategySession.Campaign;
            GalaxyView gv = GalaxyView.Active;

            sb.Append("<b>政府オブザーバ</b>　要職任命・省庁ツリー・首班　(Alt+G で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を起動すると、各勢力の要職任命・省庁編制がライブ表示されます。");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, gv, s);
            }

            sb.Append("\n<color=#6f8a9a>※ 文民政府の人事（GovernmentRegistry）と省庁編制（二官八省・#158）。首班は与党党首（PartyRules.Premier）。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, GalaxyView gv, FactionState s)
        {
            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(s.faction).Append("</color>\n");

            // 首班：国政選挙で組閣済みならその首相（議席が出所）、未構成なら従来どおり支持率最大の党の党首
            bool elected = ElectoralSystemRules.IsElectoral(s.governmentForm) && ElectionCycleRules.IsSeated(s.politics);
            int premierId = elected
                ? (s.politics.government != null ? s.politics.government.premierPersonId : -1)
                : (s.politics != null && s.politics.parties != null) ? PartyRules.Premier(s.politics.parties) : -1;
            if (elected && premierId < 0)
                sb.Append("  <color=#9fb0c0>首班</color> ＝ <color=#ff7a6a>（空席）</color> ")
                  .Append(s.politics.government != null ? s.politics.government.reason : "").Append('\n');
            else if (premierId >= 0)
                sb.Append("  <color=#9fb0c0>首班</color> ＝ ").Append(FindPersonName(gv, premierId)).Append('\n');
            else
                sb.Append("  <color=#9fb0c0>首班</color> ＝ <color=#9aa7b2>（未組閣・政党結成待ち）</color>\n");

            AppendCabinet(sb, gv, s);

            // 要職任命
            var apps = GovernmentRegistry.Appointments;
            int shown = 0, matched = 0;
            if (apps != null)
            {
                // 件数の事前カウント
                for (int i = 0; i < apps.Count; i++)
                {
                    GovernmentRegistry.Appointment ap = apps[i];
                    if (ap.holder != null && ap.holder.Faction == s.faction) matched++;
                }
                for (int i = 0; i < apps.Count && shown < 12; i++)
                {
                    GovernmentRegistry.Appointment ap = apps[i];
                    if (ap.holder == null || ap.holder.Faction != s.faction) continue;
                    sb.Append("  ").Append(ap.office.officeName)
                      .Append("（").Append(ap.office.domain).Append('/').Append(ap.office.scope).Append("）＝ ")
                      .Append(ap.holder.CharacterName).Append('\n');
                    shown++;
                }
                if (matched > shown)
                    sb.Append("  <color=#9aa7b2>…他 ").Append(matched - shown).Append(" 件</color>\n");
            }
            if (matched == 0)
                sb.Append("  <color=#9aa7b2>要職の任命なし</color>\n");

            // 省庁ツリー
            if (gv != null)
            {
                IReadOnlyList<Ministry> mins = gv.MinistriesOf(s.faction);
                if (mins == null || mins.Count == 0)
                {
                    sb.Append("  <color=#9aa7b2>省庁 未配線</color>\n");
                }
                else
                {
                    var listForCount = mins as List<Ministry> ?? new List<Ministry>(mins);
                    for (int i = 0; i < mins.Count; i++)
                    {
                        Ministry m = mins[i];
                        if (m == null || !m.IsTopLevel) continue;
                        sb.Append("  ").Append(m.ministryName)
                          .Append("（").Append(m.domain).Append("）　配下官僚 ")
                          .Append(MinistryRules.CountStaffUnder(listForCount, m.id)).Append("名\n");
                    }
                }
            }
        }

        /// <summary>試験用：いま表示する本文（観測専用＝状態は変えない）。</summary>
        public string DumpTextForTest => BuildDump();

        [Header("内閣")]
        [Tooltip("内閣の任免履歴を新しい順に表示する件数")]
        public int maxCabinetHistoryShown = 6;

        /// <summary>
        /// 内閣（政治任用）：首相・組閣の状態・職務執行、省ごとの大臣/副大臣/政務官（人物・所属党・就任年・任命理由・空席理由・委任範囲）、
        /// 同じ省の職業官僚（別系統＝政治任用で変わらない）、直近の任免履歴。操作 UI は未配線（表示のみ）。
        /// </summary>
        private void AppendCabinet(StringBuilder sb, GalaxyView gv, FactionState s)
        {
            PoliticsState pol = s.politics;
            CabinetState cab = pol != null ? pol.cabinet : null;
            sb.Append("  <color=#9fb0c0>内閣（政治任用＝首相が任免）</color>");
            if (cab == null || cab.posts == null || cab.posts.Count == 0)
            {
                sb.Append(" <color=#9aa7b2>未組閣（選挙で首班を選ぶ政体で、組閣後に表示）</color>\n");
                return;
            }
            if (cab.premierPersonId >= 0)
                sb.Append(" 首相 ").Append(FindPersonName(gv, cab.premierPersonId)).Append("　組閣 SE").Append(cab.formedYear);
            else sb.Append(" <color=#ff7a6a>首相不在</color>");
            sb.Append("　大臣").Append(CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.大臣))
              .Append("・副大臣").Append(CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.副大臣))
              .Append("・政務官").Append(CabinetAppointmentRules.FilledCount(cab, CabinetPostKind.政務官)).Append("名\n");
            if (cab.caretaker)
                sb.Append("    <color=#ffb070>職務執行内閣：").Append(cab.caretakerReason).Append("</color>\n");
            sb.Append("    <color=#6f8a9a>※大臣＝所管の政策と決裁／副大臣＝明示の委任範囲だけ代行／政務官＝提案・調整。閣僚職は艦隊・軍団の作戦指揮権と国庫の直接支出を含まない。</color>\n");

            IReadOnlyList<Ministry> mins = gv != null ? gv.MinistriesOf(s.faction) : null;
            for (int i = 0; i < cab.posts.Count; i++)
            {
                CabinetPost p = cab.posts[i];
                if (p == null) continue;
                if (p.kind == CabinetPostKind.大臣)
                {
                    sb.Append("    <color=#e7e0b0>").Append(p.ministryName).Append("</color>（").Append(p.domain).Append('）');
                    Ministry m = FindMinistry(mins, p.ministryId);
                    if (m != null)
                        sb.Append("　<color=#9fb0c0>職業官僚</color> ").Append(m.staffIds != null ? m.staffIds.Count : 0)
                          .Append('/').Append(m.staffSlots).Append("名（事務方＝政治任用と別系統）");
                    sb.Append('\n');
                }
                sb.Append("      ").Append(CabinetAppointmentRules.PostTitle(p)).Append(' ');
                if (p.holderId >= 0)
                {
                    sb.Append(FindPersonName(gv, p.holderId));
                    Party party = pol != null ? ElectionCycleRules.FindParty(pol.parties, p.partyId) : null;
                    sb.Append("（").Append(party != null ? party.partyName : "無所属").Append("・SE").Append(p.appointedYear).Append("就任）");
                    if (p.kind == CabinetPostKind.副大臣)
                    {
                        if (p.delegation != CabinetDelegation.なし)
                            sb.Append("　<color=#8ce08c>委任 ").Append(p.delegation).Append("（大臣 ").Append(FindPersonName(gv, p.delegatedById))
                              .Append("・SE").Append(p.delegationEndYear).Append("まで）</color>");
                        else sb.Append("　<color=#9aa7b2>委任なし＝提案・調整のみ</color>");
                    }
                    else sb.Append("　<color=#9aa7b2>").Append(CabinetAppointmentRules.RoleText(p.kind)).Append("</color>");
                    sb.Append('\n');
                    if (!string.IsNullOrEmpty(p.appointmentReason))
                        sb.Append("        <color=#6f8a9a>任命理由：").Append(p.appointmentReason).Append("</color>\n");
                }
                else
                {
                    sb.Append("<color=#ff7a6a>空席</color>");
                    if (!string.IsNullOrEmpty(p.vacancyReason)) sb.Append("　<color=#ffb070>").Append(p.vacancyReason).Append("</color>");
                    sb.Append('\n');
                }
            }

            if (cab.history != null && cab.history.Count > 0)
            {
                sb.Append("    <color=#9fb0c0>任免履歴（新しい順）</color>");
                if (cab.historyDropped > 0) sb.Append(" <color=#9aa7b2>（古い").Append(cab.historyDropped).Append("件は上限で省略）</color>");
                sb.Append('\n');
                int shown = 0;
                for (int i = cab.history.Count - 1; i >= 0 && shown < Mathf.Max(0, maxCabinetHistoryShown); i--, shown++)
                {
                    AppointmentHistoryEntry e = cab.history[i];
                    if (e == null) continue;
                    sb.Append("      SE").Append(e.year).Append(' ').Append(e.action).Append(' ').Append(e.postLabel);
                    if (e.personId >= 0) sb.Append(' ').Append(FindPersonName(gv, e.personId));
                    if (!string.IsNullOrEmpty(e.reason)) sb.Append("　<color=#6f8a9a>").Append(e.reason).Append("</color>");
                    sb.Append('\n');
                }
            }
        }

        private static Ministry FindMinistry(IReadOnlyList<Ministry> mins, int id)
        {
            if (mins == null) return null;
            for (int i = 0; i < mins.Count; i++) if (mins[i] != null && mins[i].id == id) return mins[i];
            return null;
        }

        private static string FindPersonName(GalaxyView gv, int personId)
        {
            if (gv != null)
            {
                IReadOnlyList<Person> civilians = gv.CivilianRoster;
                if (civilians != null)
                {
                    for (int i = 0; i < civilians.Count; i++)
                        if (civilians[i] != null && civilians[i].id == personId) return civilians[i].name;
                }
                IReadOnlyList<Person> commanders = gv.CommanderRoster;
                if (commanders != null)
                {
                    for (int i = 0; i < commanders.Count; i++)
                        if (commanders[i] != null && commanders[i].id == personId) return commanders[i].name;
                }
            }
            return "id " + personId;
        }

        // ===== UI 構築（LaborObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("GovernmentObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "政府", () => SetVisible(false));
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
            UiScrollbars.Attach(scrollRect);   // #H スクロールできることを画面で示す（見えて掴めるバー）

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
