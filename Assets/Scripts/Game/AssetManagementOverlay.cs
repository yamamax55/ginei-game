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
    /// 資産運用（アセットマネジメント）ウィンドウ。上メニュー「資産運用」で開閉し、<b>プレイヤー勢力</b>の
    /// 財務ポートフォリオを一望する＝資産クラス別（現金/金融資産/土地/事業/その他）の額と配分比率、純資産（負債控除後）、
    /// レバレッジ、分散度（<see cref="AssetPortfolioRules"/>）＋財政の概況。財産オブザーバ（<see cref="WealthObserverOverlay"/>＝
    /// 全勢力・人物・企業の富の分布）に対し、こちらは<b>自勢力の資産配分</b>に特化した運用ダッシュボード。
    /// 集計は <see cref="NetWorthRules"/>／<see cref="GalaxyView"/> のアクセサを読むだけ＝<b>観測専用・状態は変えない</b>
    /// （売買・配分操作は後段の操作化で）。`PersonObserverOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class AssetManagementOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1116;
        public float dimAlpha = 0.92f;
        public float bodyFontSize = 18f;

        public Color accentColor = new Color(0.55f, 0.85f, 1f, 1f);

        private static readonly string[] ClassNames = { "現金", "金融資産", "土地", "事業", "その他" };

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
            if (UnityEngine.Object.FindAnyObjectByType<AssetManagementOverlay>() != null) return;
            new GameObject("AssetManagementOverlay").AddComponent<AssetManagementOverlay>();
        }

        private object escWindowToken;

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            EnsureEventSystem();
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => root != null && root.activeSelf, () => SetVisible(false), canvasSortingOrder, "資産運用");
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

        private static Faction PlayerFaction()
            => GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            Faction fac = PlayerFaction();
            sb.Append("<b>資産運用</b>　プレイヤー勢力：<color=#9fe0ff>").Append(fac).Append("</color>　(× で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            CampaignState c = StrategySession.Campaign;
            GalaxyView gv = GalaxyView.Active;
            FactionState s = FindState(c, fac);
            if (s == null)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。戦役を始めると資産が表示されます。</color>");
                return sb.ToString();
            }

            OwnerRef stateRef = OwnerRef.State(fac);
            float cash = Mathf.Max(0f, s.treasury);
            float financial = NetWorthRules.HoldingsValue(stateRef);
            float land = NetWorthRules.LandValue(stateRef);
            float other = NetWorthRules.NamedAssetValue(stateRef);
            float business = BusinessAssets(gv, fac, out int stateFirms, out float thEquity, out float reValue);

            float[] values = { cash, financial, land, business, other };
            float gross = AssetPortfolioRules.Gross(values);
            float debt = Mathf.Max(0f, s.fiscal != null ? s.fiscal.debt : 0f);
            float net = AssetPortfolioRules.NetWorth(gross, debt);
            float lev = AssetPortfolioRules.LeverageRatio(debt, gross);
            float effClasses = AssetPortfolioRules.EffectiveClasses(values);

            // ── ポートフォリオ配分 ──
            sb.Append("\n<color=#cfe8ff>◤ ポートフォリオ配分</color>\n");
            for (int i = 0; i < values.Length; i++)
            {
                float frac = AssetPortfolioRules.Fraction(values[i], gross);
                sb.Append("  ").Append(ClassNames[i].PadRight(0)).Append("　")
                  .Append(Bar(frac)).Append("　")
                  .Append("<color=#ffe08a>").Append(values[i].ToString("#,0")).Append("</color>")
                  .Append("　<color=#9fb0c0>").Append((frac * 100f).ToString("0.0")).Append("%</color>\n");
            }

            // ── 総括 ──
            sb.Append("\n<color=#cfe8ff>◤ 総括</color>\n");
            sb.Append("  総資産 <color=#a0e0a0>").Append(gross.ToString("#,0")).Append("</color>");
            sb.Append("　負債(国債) <color=#e6a0a0>").Append(debt.ToString("#,0")).Append("</color>");
            sb.Append("　<b>純資産</b> ").Append(net >= 0f ? "<color=#a0e0a0>" : "<color=#ff8080>").Append(net.ToString("#,0")).Append("</color>\n");
            sb.Append("  レバレッジ(負債/資産) ").Append(LevColor(lev)).Append((lev * 100f).ToString("0.0")).Append("%</color>");
            sb.Append("　分散度 ").Append("<color=#bfe9c0>").Append(effClasses.ToString("0.00")).Append("</color> / ").Append(values.Length).Append(" クラス\n");

            // ── 内訳（事業・財政） ──
            sb.Append("\n<color=#cfe8ff>◤ 内訳</color>\n");
            sb.Append("  <color=#9fb0c0>事業</color>　国有企業 ").Append(stateFirms).Append(" 社")
              .Append("／商社持分 ").Append(thEquity.ToString("#,0"))
              .Append("／不動産 ").Append(reValue.ToString("#,0")).Append('\n');
            if (s.fiscal != null)
            {
                sb.Append("  <color=#9fb0c0>財政</color>　歳入 ").Append(s.fiscal.revenue.ToString("#,0"))
                  .Append("／歳出 ").Append(s.fiscal.baseExpenditure.ToString("#,0"))
                  .Append("／税率 ").Append((s.fiscal.taxRate * 100f).ToString("0.#")).Append("%\n");
            }

            sb.Append("\n<color=#6f8a9a>※ 観測専用＝現状は配分の可視化のみ（売買・再配分の操作化は後段）。財産分布の全体は財産(Alt+W)へ。</color>");
            return sb.ToString();
        }

        /// <summary>自勢力の事業資産＝国有企業の資本＋商社の自己資本＋不動産会社の物件価値。</summary>
        private static float BusinessAssets(GalaxyView gv, Faction fac, out int stateFirms, out float thEquity, out float reValue)
        {
            stateFirms = 0; thEquity = 0f; reValue = 0f;
            if (gv == null) return 0f;

            float biz = 0f;
            var firms = gv.GetEnterprises(fac);
            if (firms != null)
            {
                for (int i = 0; i < firms.Count; i++)
                {
                    Enterprise f = firms[i];
                    if (f == null || f.ownership != Ownership.国有) continue; // プレイヤー（国家）の事業＝国有のみ
                    biz += Mathf.Max(0f, f.capital);
                    stateFirms++;
                }
            }

            TradingHouse th = gv.GetTradingHouse(fac);
            if (th != null) { thEquity = Mathf.Max(0f, th.capital); biz += thEquity; }

            RealEstateCompany re = gv.GetRealEstateCompany(fac);
            if (re != null) { reValue = Mathf.Max(0f, re.propertyValue); biz += reValue; }

            return biz;
        }

        private static FactionState FindState(CampaignState c, Faction fac)
        {
            if (c == null || c.states == null) return null;
            for (int i = 0; i < c.states.Count; i++)
                if (c.states[i] != null && c.states[i].faction == fac) return c.states[i];
            return null;
        }

        /// <summary>テキストの配分バー（10目盛）。</summary>
        private static string Bar(float frac01)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(frac01 * 10f), 0, 10);
            var sb = new StringBuilder(24);
            sb.Append("<color=#5aa0e0>");
            for (int i = 0; i < filled; i++) sb.Append('█');
            sb.Append("</color><color=#33414f>");
            for (int i = filled; i < 10; i++) sb.Append('█');
            sb.Append("</color>");
            return sb.ToString();
        }

        private static string LevColor(float lev)
            => lev >= 1f ? "<color=#ff8080>" : (lev >= 0.6f ? "<color=#ffcc66>" : "<color=#bfe9c0>");

        // ===== UI =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("AssetManagementCanvas");
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
            prt.anchorMin = new Vector2(0.08f, 0.08f); prt.anchorMax = new Vector2(0.92f, 0.92f);
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.96f);
            panel.AddComponent<RectMask2D>();

            WindowChrome.AddTitleBarAnchored(prt, "資産運用", () => SetVisible(false));

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
