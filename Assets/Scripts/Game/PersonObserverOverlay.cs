using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 人物名鑑オーバーレイ（観測層・read-only）。<b>P キー</b>で開閉し、提督（<see cref="AdmiralData"/>）の
    /// 能力・階級・参謀・得意陣形を一覧表示する。観測専用＝状態は変えない。`CampaignObserverOverlay`（G）/
    /// `CoreStateInspector`（J）/決裁ボード（K）と同じ観測層の家族。
    /// 提督は Assets/Data/Admirals（Resources外）にあり実行時に直接列挙できないため、
    /// <b>シナリオ（Resources内）が参照する提督</b>を <see cref="ContentDatabase.AllScenarios"/> 経由で集約する。
    /// Strategy/Battle へ自動生成（`HelpOverlay`/`TimeDisplay` と同型）。
    /// </summary>
    public class PersonObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1092;
        public float dimAlpha = 0.92f;
        public float bodyFontSize = 18f;
        [Tooltip("一覧に出す最大人数（超過分は『他N名』と表示）")]
        public int maxPersons = 30;

        private GameObject root;
        private TextMeshProUGUI bodyLabel;
        private TMP_FontAsset jpFont;

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
            if (UnityEngine.Object.FindAnyObjectByType<PersonObserverOverlay>() != null) return;
            new GameObject("PersonObserverOverlay").AddComponent<PersonObserverOverlay>();
        }

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            BuildUI();
            SetVisible(false);
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.人物名鑑切替)) Toggle();
            if (root != null && root.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() { SetVisible(root != null && !root.activeSelf); }
        public void SetVisible(bool v) { if (root != null) root.SetActive(v); }

        // ===== 集約＋整形 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            sb.Append("<b>人物名鑑</b>　提督の能力・階級・参謀　(P で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            // 提督は ContentDatabase に集約済み（シナリオ参照グラフ経由で索引・Resources 外でも拾える）。
            var admirals = ContentDatabase.AllAdmirals();
            int count = admirals != null ? admirals.Count : 0;

            if (count == 0)
            {
                sb.Append("\n<color=#ffcc66>人物データがありません。</color>\n");
                sb.Append("シナリオ（Resources）に提督（AdmiralData）が登録されていません。\n");
                sb.Append("会戦シナリオを作成すると、その提督がここに一覧表示されます。");
                AppendRuntimeCivilians(sb); // 実行時の文官ネームド（あれば）は提督アセットが無くても出す
                return sb.ToString();
            }

            int shown = Mathf.Min(count, maxPersons);
            for (int i = 0; i < shown; i++) AppendPerson(sb, admirals[i]);
            if (count > shown) sb.Append($"\n<color=#8aa0b0>…他 {count - shown} 名</color>");
            sb.Append($"\n\n<color=#8aa0b0>計 {count} 名</color>");
            AppendRuntimeCivilians(sb); // 実行時に生成・叙位された文官ネームド（律令の官人）
            return sb.ToString();
        }

        /// <summary>
        /// 実行時に生成・叙位された文官ネームド（律令の官人）を一覧する。Strategy の <see cref="GalaxyView"/> から
        /// 文民ロスターを read-only で読み、位階・考第・文才を出す。GalaxyView が無い（Battle 等）なら何もしない。
        /// </summary>
        private void AppendRuntimeCivilians(StringBuilder sb)
        {
            var gv = UnityEngine.Object.FindAnyObjectByType<GalaxyView>();
            if (gv == null) return;
            var civs = gv.CivilianRoster;
            if (civs == null || civs.Count == 0) return;

            float authority = gv.Court != null ? gv.Court.authority : 0f;
            sb.Append("\n\n<color=#5b6b7a>──── 文官（律令の官人・実行時）────</color>\n");
            sb.Append($"<color=#8aa0b0>朝廷の権威 {authority:0.00}（{RitsuryoFormalizationRules.PhaseOf(authority)}）＝官位の実権はこの権威で減衰</color>\n");

            int shown = Mathf.Min(civs.Count, maxPersons);
            for (int i = 0; i < shown; i++) AppendCivil(sb, civs[i], gv);
            if (civs.Count > shown) sb.Append($"\n<color=#8aa0b0>…他 {civs.Count - shown} 名</color>");
            sb.Append($"\n<color=#8aa0b0>文官 計 {civs.Count} 名</color>");
        }

        private void AppendCivil(StringBuilder sb, Person p, GalaxyView gv)
        {
            if (p == null) return;
            string ikai = JapaneseCourtRankRules.Name(p.courtRank);
            string kou = p.merit != null ? p.merit.lastRating.ToString() : "未評定";
            string noble = JapaneseCourtRankRules.IsNobility(p.courtRank) ? "　<color=#ffd54a>貴族</color>" : "";
            string post = OfficeHeldBy(gv, p);
            string postPart = string.IsNullOrEmpty(post) ? "" : $"　<color=#ffd54a>在任:{post}</color>";
            sb.Append($"\n<color=#bfe9c0>◆ {ikai} {p.name}</color>　<color=#9fb0c0>[{p.faction}]</color>　考第:{kou}{noble}{postPart}\n");
            sb.Append($"  運営 {p.operation} ／ 情報 {p.intelligence}\n");
        }

        /// <summary>その文官が就いている文官官職名（宰相/総督。無ければ空）。GalaxyView が map 込みで解決する。</summary>
        private string OfficeHeldBy(GalaxyView gv, Person p)
            => gv != null ? gv.CivilPostOf(p) : "";

        private void AppendPerson(StringBuilder sb, AdmiralData a)
        {
            if (a == null) return;
            // 勢力ごとの階級表があればそれを使いたいが、AdmiralData は enum faction のみ持つ＝既定ラダーで解決。
            string rank = RankSystem.ResolveRankNameOrDefault(null, a.rankTier);
            string rankPart = string.IsNullOrEmpty(rank) ? "" : rank + " ";
            string fac = a.faction.ToString();
            string proto = a.isProtagonist ? "　<color=#ffd54a>★主人公</color>" : "";

            sb.Append($"\n<color=#bfe9c0>◆ {rankPart}{a.EpithetName}</color>　<color=#9fb0c0>[{fac}]</color>{proto}\n");
            sb.Append($"  統率 {a.EffectiveLeadership} ／ 攻撃 {a.EffectiveAttack} ／ 防御 {a.EffectiveDefense}");
            sb.Append($" ／ 機動 {a.EffectiveMobility} ／ 運営 {a.EffectiveOperation} ／ 情報 {a.EffectiveIntelligence}\n");

            // RANKCMD-1：人物は固定兵力を持たない。階級から「指揮できる規模」を表示する（CommandCapacityRules）。
            string extra = $"  指揮可能規模 〜{CommandCapacityRules.MaxStrengthForTier(a.rankTier):#,0}隻";
            if (a.HasStaff) extra += $"　参謀: {a.GetStaffNames()}";
            if (a.hasPreferredFormation) extra += $"　得意陣形: {a.preferredFormation}";
            sb.Append(extra).Append('\n');
        }

        // ===== UI =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("PersonObserverCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root");
            root.transform.SetParent(canvasObj.transform, false);
            var rrt = root.AddComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            root.AddComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, dimAlpha);

            // パネル（画面の大半）
            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.06f, 0.06f); prt.anchorMax = new Vector2(0.94f, 0.94f);
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.96f);
            panel.AddComponent<RectMask2D>(); // はみ出しはクリップ

            // 本文ラベル（上詰め・余白）
            var labelGo = new GameObject("Body");
            labelGo.transform.SetParent(panel.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(20f, 20f); lrt.offsetMax = new Vector2(-20f, -16f);
            bodyLabel = labelGo.AddComponent<TextMeshProUGUI>();
            bodyLabel.fontSize = bodyFontSize;
            bodyLabel.color = new Color(0.92f, 0.94f, 0.97f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.enableWordWrapping = true;
            bodyLabel.raycastTarget = false;
            if (jpFont != null) bodyLabel.font = jpFont;

            root.SetActive(false);
        }
    }
}
