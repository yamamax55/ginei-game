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
    /// 社交場（観測層・read-only）。上メニュー「社交場」で開閉し、各勢力の提督・要人が集う「場」を眺める窓。
    /// 出席者（<see cref="ContentDatabase.AllAdmirals"/>＝シナリオ提督＋<see cref="GalaxyView.CommanderRoster"/>＝戦略の武官）を
    /// 異名・勢力・階級・能力・所持特技（<see cref="TalentCatalog"/>）つきで一覧する。人事（<see cref="PersonObserverOverlay"/>）が
    /// 職分・決裁履歴の台帳なのに対し、こちらは「誰が居て何が得意か」の顔見せ＝特技に焦点。観測専用＝状態は変えない。
    /// Strategy/Battle へ自動生成（`HelpOverlay`/`PersonObserverOverlay` と同型）。
    /// </summary>
    public class SocialVenueOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1094;
        public float dimAlpha = 0.92f;
        public float bodyFontSize = 18f;
        [Tooltip("一覧に出す最大出席者数（超過分は『他N名』と表示）")]
        public int maxAttendees = 40;

        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);

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
            if (UnityEngine.Object.FindAnyObjectByType<SocialVenueOverlay>() != null) return;
            new GameObject("SocialVenueOverlay").AddComponent<SocialVenueOverlay>();
        }

        private object escWindowToken; // UIWindowStack 登録トークン（#ウィンドウESC）

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            EnsureEventSystem();
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => root != null && root.activeSelf, () => SetVisible(false), canvasSortingOrder, "社交場");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (root != null && root.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() { SetVisible(root != null && !root.activeSelf); }
        public void SetVisible(bool v) { if (root != null) root.SetActive(v); }

        // ===== 集約＋整形 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            sb.Append("<b>社交場</b>　各勢力の提督・要人が集う　(× で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            var gv = UnityEngine.Object.FindAnyObjectByType<GalaxyView>();
            bool any = false;

            // 戦略の武官ロスター（在席の人物）。
            if (gv != null && gv.CommanderRoster != null && gv.CommanderRoster.Count > 0)
            {
                sb.Append("\n<color=#5b6b7a>── 在席の武官（戦略ロスター）──</color>\n");
                int shown = 0;
                for (int i = 0; i < gv.CommanderRoster.Count && shown < maxAttendees; i++)
                {
                    Person p = gv.CommanderRoster[i];
                    if (p == null) continue;
                    AppendPerson(sb, p); shown++; any = true;
                }
                if (gv.CommanderRoster.Count > shown) sb.Append($"\n<color=#8aa0b0>…他 {gv.CommanderRoster.Count - shown} 名</color>\n");
            }

            // シナリオ提督（特技つきの顔見せ）。
            var admirals = ContentDatabase.AllAdmirals();
            if (admirals != null && admirals.Count > 0)
            {
                sb.Append("\n<color=#5b6b7a>── 提督衆（顔ぶれと特技）──</color>\n");
                int shown = Mathf.Min(admirals.Count, maxAttendees);
                for (int i = 0; i < shown; i++) AppendAdmiral(sb, admirals[i]);
                if (admirals.Count > shown) sb.Append($"\n<color=#8aa0b0>…他 {admirals.Count - shown} 名</color>");
                any = true;
            }

            if (!any) sb.Append("\n<color=#ffcc66>まだ誰も集っていません（戦役を始めると顔ぶれが揃います）。</color>");
            return sb.ToString();
        }

        private void AppendPerson(StringBuilder sb, Person p)
        {
            string rank = RankSystem.ResolveRankNameOrDefault(null, p.rankTier);
            string rankPart = string.IsNullOrEmpty(rank) ? "" : rank + " ";
            sb.Append($"\n<color=#bfe9c0>◆ {rankPart}{p.name}</color>　<color=#9fb0c0>[{p.faction}]</color>　<color=#8aa0b0>{p.serviceStatus}</color>\n");
            sb.Append($"  統率 {p.leadership} ／ 攻撃 {p.attack} ／ 防御 {p.defense} ／ 機動 {p.mobility}\n");
        }

        private void AppendAdmiral(StringBuilder sb, AdmiralData a)
        {
            if (a == null) return;
            string rank = RankSystem.ResolveRankNameOrDefault(null, a.rankTier);
            string rankPart = string.IsNullOrEmpty(rank) ? "" : rank + " ";
            string proto = a.isProtagonist ? "　<color=#ffd54a>★主人公</color>" : "";
            sb.Append($"\n<color=#bfe9c0>◆ {rankPart}{a.EpithetName}</color>　<color=#9fb0c0>[{a.faction}]</color>{proto}\n");
            sb.Append($"  統率 {a.EffectiveLeadership} ／ 攻撃 {a.EffectiveAttack} ／ 防御 {a.EffectiveDefense} ／ 機動 {a.EffectiveMobility}\n");
            AppendTalents(sb, a);
        }

        /// <summary>提督の所持特技を名称・格つきで横並び表示（社交場の主役＝得意の披露）。無ければ出さない。</summary>
        private void AppendTalents(StringBuilder sb, AdmiralData a)
        {
            if (a.talents == null || a.talents.Count == 0) return;
            sb.Append("  <color=#8aa0b0>特技:</color> ");
            int n = 0;
            for (int i = 0; i < a.talents.Count; i++)
            {
                Talent t = a.talents[i];
                TalentDef def = TalentCatalog.Get(t);
                if (def == null) continue;
                if (n > 0) sb.Append("　");
                sb.Append($"<color=#ffd54a>{def.talentName}</color><color=#9aa7b3>〔{t.grade}〕</color>");
                n++;
            }
            sb.Append('\n');
        }

        // ===== UI =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("SocialVenueCanvas");
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
            var dimImage = root.AddComponent<Image>();
            dimImage.color = new Color(0.02f, 0.03f, 0.06f, dimAlpha);
            WindowChrome.MakeNonModal(dimImage);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.06f, 0.06f); prt.anchorMax = new Vector2(0.94f, 0.94f);
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.96f);
            panel.AddComponent<RectMask2D>();

            WindowChrome.AddTitleBarAnchored(prt, "社交場", () => SetVisible(false));

            float topReserve = WindowChrome.TitleBarHeight;

            var labelGo = new GameObject("Body");
            labelGo.transform.SetParent(panel.transform, false);
            var lrt = labelGo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(20f, 20f); lrt.offsetMax = new Vector2(-20f, -(12f + topReserve));
            bodyLabel = labelGo.AddComponent<TextMeshProUGUI>();
            bodyLabel.fontSize = bodyFontSize;
            bodyLabel.color = new Color(0.92f, 0.94f, 0.97f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.enableWordWrapping = true;
            bodyLabel.raycastTarget = false;
            if (jpFont != null) bodyLabel.font = jpFont;

            root.SetActive(false);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
