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
    /// 官僚機構オブザーバ（観測層・read-only）。<b>Alt+K</b>で開閉し、勢力ごとに<b>官僚の組織構造</b>＝省庁の
    /// 編制ツリー（太政官 ⊃ 式部省/民部省/大蔵省/兵部省 …＝<see cref="Ministry"/> #158）を<b>入れ子のまま</b>
    /// 全段ダンプする。各省庁ごとに 所掌／配属定員と充足率／省益（縦割り抵抗 <see cref="Ministry.institutionalInterest"/>）／
    /// 行政効率（<see cref="MinistryAdminRules.StaffingEfficiency"/>）と、<b>配属官僚（文民）を名前と文才つきで一覧</b>。
    /// 朝廷の権威（<see cref="CourtAuthority"/>）と名実の乖離（<see cref="RitsuryoFormalizationRules"/>＝官職が
    /// 実効を持つ度合い）も併記する。政府オブザーバ（Alt+G＝要職任命・首班）が「政治任用の人事」を映すのに対し、
    /// こちらは「常勤の官僚機構そのもの」に特化＝省 ⊃ 庁/局の入れ子と配属官僚を最後まで降りて見せる。
    /// 観測専用ゆえ既存フィールドのみ読む＝<b>状態は変えない</b>。`GovernmentObserverOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class BureaucracyObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1105;
        public float dimAlpha = 0.55f;
        public float panelWidth = 1020f;
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
            if (Object.FindAnyObjectByType<BureaucracyObserverOverlay>() != null) return;
            new GameObject("BureaucracyObserverOverlay").AddComponent<BureaucracyObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "官僚機構");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.官僚機構観測切替))
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
            var sb = new StringBuilder(4096);
            CampaignState c = StrategySession.Campaign;
            GalaxyView gv = GalaxyView.Active;

            sb.Append("<b>官僚機構オブザーバ</b>　省庁ツリー（省⊃庁/局）と配属官僚　(Alt+K で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            // 朝廷の権威（名実の乖離＝官職が実効を持つ度合い）。盤面で唯一の中央権威。
            CourtAuthority court = gv != null ? gv.Court : null;
            if (court != null)
            {
                float a = court.authority;
                RitsuryoPhase phase = RitsuryoFormalizationRules.PhaseOf(a);
                float factor = RitsuryoFormalizationRules.OfficeAuthorityFactor(a);
                sb.Append("<color=#9fb0c0>朝廷の権威</color> ").Append(Bar(a, 12)).Append(' ')
                  .Append((a * 100f).ToString("0")).Append("%　（")
                  .Append(phase).Append("・官職の実効 ").Append((factor * 100f).ToString("0")).Append("%）\n");
            }

            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を起動すると、各勢力の省庁編制と配属官僚がライブ表示されます。");
                return sb.ToString();
            }

            if (gv == null)
            {
                sb.Append("\n<color=#ffcc66>省庁データは戦略マップ（GalaxyView）でのみ表示されます。</color>");
                return sb.ToString();
            }

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, gv, s);
            }

            sb.Append("\n<color=#6f8a9a>※ 常勤の官僚機構＝省庁ツリー（二官八省・#158）と配属官僚。行政効率＝配下官僚の平均文才×定員充足率。\n");
            sb.Append("　 省益が高い省庁ほど横断政策に抵抗する（縦割り）。要職任命・首班は政府オブザーバ（Alt+G）。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, GalaxyView gv, FactionState s)
        {
            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(s.faction).Append("</color>\n");

            IReadOnlyList<Ministry> mins = gv.MinistriesOf(s.faction);
            if (mins == null || mins.Count == 0)
            {
                sb.Append("  <color=#9aa7b2>省庁 未配線</color>\n");
                return;
            }

            var tree = mins as List<Ministry> ?? new List<Ministry>(mins);

            // 最上位（省＝親なし）から再帰で全段降りる。
            bool anyTop = false;
            for (int i = 0; i < tree.Count; i++)
            {
                Ministry m = tree[i];
                if (m == null || !m.IsTopLevel) continue;
                anyTop = true;
                AppendMinistry(sb, gv, tree, m, 1);
            }
            if (!anyTop)
                sb.Append("  <color=#9aa7b2>最上位の省が見つかりません</color>\n");
        }

        /// <summary>省庁を1つ描画し、子（庁/局）を再帰で降りる。depth＝インデント段（1＝省）。</summary>
        private void AppendMinistry(StringBuilder sb, GalaxyView gv, List<Ministry> tree, Ministry m, int depth)
        {
            string indent = new string('　', depth); // 全角スペースで段付け
            string branch = depth <= 1 ? "■ " : "└ ";

            // ヘッダ行：省庁名（所掌）・臨時フラグ
            sb.Append(indent).Append("<color=#cfe0f0>").Append(branch).Append(m.ministryName).Append("</color>")
              .Append(" <color=#7f8a96>(").Append(m.domain).Append(')').Append("</color>");
            if (m.isTemporary) sb.Append(" <color=#e8a060>［臨時］</color>");
            sb.Append('\n');

            // 大臣／長官（headOfficeId は Office.id。在任者は GovernmentRegistry から引く）
            string headName = HeadOfficeHolderName(m);

            // 充足率（この省庁単体）・行政効率（配下含む）・省益
            int assigned = m.staffIds != null ? m.staffIds.Count : 0;
            int slots = Mathf.Max(0, m.staffSlots);
            float fill = slots > 0 ? Mathf.Clamp01((float)assigned / slots) : 0f;
            float eff = MinistryAdminRules.StaffingEfficiency(m, tree, FindPerson(gv));

            sb.Append(indent).Append("　<color=#9aa7b2>配属</color> ").Append(assigned).Append('/').Append(slots)
              .Append(' ').Append(Bar(fill, 8))
              .Append("　<color=#9aa7b2>行政効率</color> ").Append((eff * 100f).ToString("0")).Append('%')
              .Append("　<color=#9aa7b2>省益</color> ").Append((m.institutionalInterest * 100f).ToString("0")).Append('%');
            if (!string.IsNullOrEmpty(headName))
                sb.Append("　<color=#9aa7b2>長</color> ").Append(headName);
            sb.Append('\n');

            // 配属官僚（文民）を名前と文才つきで列挙
            if (m.staffIds != null && m.staffIds.Count > 0)
            {
                for (int i = 0; i < m.staffIds.Count; i++)
                {
                    Person p = FindPersonById(gv, m.staffIds[i]);
                    sb.Append(indent).Append("　・");
                    if (p != null)
                        sb.Append(p.name).Append(" <color=#7f8a96>(文才 ").Append(p.CivilAptitude.ToString("0")).Append(')').Append("</color>");
                    else
                        sb.Append("<color=#9aa7b2>id ").Append(m.staffIds[i]).Append("</color>");
                    sb.Append('\n');
                }
            }

            // 子（庁/局）を再帰
            if (m.childIds != null)
            {
                for (int i = 0; i < m.childIds.Count; i++)
                {
                    Ministry child = MinistryRules.Get(tree, m.childIds[i]);
                    if (child != null) AppendMinistry(sb, gv, tree, child, depth + 1);
                }
            }
        }

        private static string HeadOfficeHolderName(Ministry m)
        {
            if (m.headOfficeId < 0) return "";
            var apps = GovernmentRegistry.Appointments;
            if (apps == null) return "";
            for (int i = 0; i < apps.Count; i++)
            {
                GovernmentRegistry.Appointment ap = apps[i];
                if (ap.office != null && ap.office.id == m.headOfficeId && ap.holder != null)
                    return ap.holder.CharacterName;
            }
            return "";
        }

        // ===== Person 解決 =====

        private System.Func<int, Person> FindPerson(GalaxyView gv) => id => FindPersonById(gv, id);

        private static Person FindPersonById(GalaxyView gv, int personId)
        {
            if (gv == null) return null;
            IReadOnlyList<Person> civilians = gv.CivilianRoster;
            if (civilians != null)
                for (int i = 0; i < civilians.Count; i++)
                    if (civilians[i] != null && civilians[i].id == personId) return civilians[i];
            IReadOnlyList<Person> commanders = gv.CommanderRoster;
            if (commanders != null)
                for (int i = 0; i < commanders.Count; i++)
                    if (commanders[i] != null && commanders[i].id == personId) return commanders[i];
            return null;
        }

        /// <summary>0..1 を文字バーに（観測層の手仕上げ表示）。</summary>
        private static string Bar(float v, int width)
        {
            v = Mathf.Clamp01(v);
            int filled = Mathf.RoundToInt(v * width);
            var sb = new StringBuilder(width + 16);
            sb.Append("<color=#6fae6f>");
            for (int i = 0; i < filled; i++) sb.Append('▮');
            sb.Append("</color><color=#3a3f44>");
            for (int i = filled; i < width; i++) sb.Append('▯');
            sb.Append("</color>");
            return sb.ToString();
        }

        // ===== UI 構築（GovernmentObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("BureaucracyObserverCanvas");
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

            WindowChrome.AddTitleBarLayout(frameRT, "官僚機構", () => SetVisible(false));
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
