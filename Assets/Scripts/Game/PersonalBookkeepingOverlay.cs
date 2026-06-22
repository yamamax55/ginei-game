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
    /// 家計簿（複式簿記）ウィンドウ。上メニュー「家計簿」で開閉し、<b>プレイヤー（主人公）の私有財産の移動</b>を
    /// 複式簿記で<b>月次</b>に観測する（統計情報・read-only）。仕訳帳 <see cref="PersonalLedgerStore"/>（私兵購入・慰労が記帳）から、
    /// 勘定残高（現金/私兵/借入金/自己資本）と月ごとの仕訳（借方/貸方）＋月次の借方=貸方（貸借一致）を表示。
    /// 集計は <see cref="BookkeepingRules"/> を読むだけ＝<b>観測専用・状態は変えない</b>。`PersonObserverOverlay` と同型の自動生成。
    /// </summary>
    public class PersonalBookkeepingOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1120;
        public float dimAlpha = 0.92f;
        public float bodyFontSize = 18f;
        [Tooltip("表示する月数（新しい順・超過は省略）")]
        public int maxMonths = 12;
        [Tooltip("1月あたりに出す仕訳の最大数")]
        public int maxEntriesPerMonth = 12;

        private GameObject root;
        private TextMeshProUGUI bodyLabel;
        private TMP_FontAsset jpFont;
        private readonly List<int> monthScratch = new List<int>();

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
            if (UnityEngine.Object.FindAnyObjectByType<PersonalBookkeepingOverlay>() != null) return;
            new GameObject("PersonalBookkeepingOverlay").AddComponent<PersonalBookkeepingOverlay>();
        }

        private object escWindowToken;

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            EnsureEventSystem();
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => root != null && root.activeSelf, () => SetVisible(false), canvasSortingOrder, "家計簿");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (root != null && root.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() { SetVisible(root != null && !root.activeSelf); }
        public void SetVisible(bool v) { if (root != null) root.SetActive(v); }

        // ===== 集計＋整形 =====

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            sb.Append("<b>家計簿（複式簿記）</b>　主人公の私有財産の移動・月次　(× で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            PersonalLedger l = PersonalLedgerStore.Ledger;
            if (l == null || l.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>まだ私財の取引がありません。</color>\n");
                sb.Append("艦隊管理ウィンドウで「私兵を購入」「私財で慰労」を行うと、ここに複式簿記で記帳されます。");
                return sb.ToString();
            }

            // 月の一覧（新しい順）。
            monthScratch.Clear();
            int maxMonth = int.MinValue;
            for (int i = 0; i < l.entries.Count; i++)
            {
                int m = l.entries[i].month;
                if (m > maxMonth) maxMonth = m;
                if (!monthScratch.Contains(m)) monthScratch.Add(m);
            }
            monthScratch.Sort((a, b) => b - a); // 降順

            // ── 現在の残高（貸借対照表の要点） ──
            sb.Append("\n<color=#cfe8ff>◤ 現在の残高</color>\n");
            long cash = BookkeepingRules.Balance(l, LedgerAccount.現金, maxMonth);
            long force = BookkeepingRules.Balance(l, LedgerAccount.私兵, maxMonth);
            long debt = BookkeepingRules.Balance(l, LedgerAccount.借入金, maxMonth);
            long expense = BookkeepingRules.Balance(l, LedgerAccount.慰労費, maxMonth) + BookkeepingRules.Balance(l, LedgerAccount.商人口銭, maxMonth);
            long income = BookkeepingRules.Balance(l, LedgerAccount.俸給, maxMonth);
            long equity = cash + force - debt; // 自己資本＝資産−負債
            sb.Append($"  現金 <color=#ffe08a>{cash:#,0}</color>　私兵 <color=#ffe08a>{force:#,0}</color>　借入金 <color=#e6a0a0>{debt:#,0}</color>\n");
            sb.Append($"  自己資本（資産−負債） <color=#a0e0a0>{equity:#,0}</color>　累計費用 {expense:#,0}　累計収益 {income:#,0}\n");

            // ── 月次の仕訳 ──
            int shownMonths = Mathf.Min(maxMonths, monthScratch.Count);
            for (int mi = 0; mi < shownMonths; mi++)
            {
                int month = monthScratch[mi];
                long dr = BookkeepingRules.MonthlyDebitTotal(l, month);
                long cr = BookkeepingRules.MonthlyCreditTotal(l, month);
                string okMark = dr == cr ? "<color=#8aa0b0>貸借一致</color>" : "<color=#ff8080>不一致!</color>";
                sb.Append($"\n<color=#cfe8ff>◤ 第 {month} 月</color>　借方 {dr:#,0} ／ 貸方 {cr:#,0}　{okMark}\n");

                int shown = 0;
                for (int i = 0; i < l.entries.Count; i++)
                {
                    LedgerEntry e = l.entries[i];
                    if (e == null || e.month != month) continue;
                    if (shown >= maxEntriesPerMonth) { sb.Append("  <color=#8aa0b0>…（以下省略）</color>\n"); break; }
                    AppendEntry(sb, e);
                    shown++;
                }
            }
            if (monthScratch.Count > shownMonths)
                sb.Append($"\n<color=#8aa0b0>…他 {monthScratch.Count - shownMonths} ヶ月ぶんを省略</color>");

            sb.Append("\n\n<color=#6f8a9a>※ 記帳元＝艦隊管理の「私兵購入」「私財で慰労」。複式簿記ゆえ各月の借方＝貸方で必ず一致する。</color>");
            return sb.ToString();
        }

        private void AppendEntry(StringBuilder sb, LedgerEntry e)
        {
            sb.Append("  <color=#bfe9c0>◆ ").Append(string.IsNullOrEmpty(e.memo) ? "（無題）" : e.memo).Append("</color>\n");
            sb.Append("    借) ");
            AppendLegs(sb, e, true);
            sb.Append("　／　貸) ");
            AppendLegs(sb, e, false);
            sb.Append('\n');
        }

        private void AppendLegs(StringBuilder sb, LedgerEntry e, bool debit)
        {
            int n = 0;
            for (int i = 0; i < e.legs.Count; i++)
            {
                JournalLeg leg = e.legs[i];
                if (leg.isDebit != debit) continue;
                if (n > 0) sb.Append("・");
                sb.Append(leg.account).Append(' ').Append(leg.amount.ToString("#,0"));
                n++;
            }
            if (n == 0) sb.Append("—");
        }

        // ===== UI =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("PersonalBookkeepingCanvas");
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
            prt.anchorMin = new Vector2(0.07f, 0.06f); prt.anchorMax = new Vector2(0.93f, 0.94f);
            prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.96f);
            panel.AddComponent<RectMask2D>();

            WindowChrome.AddTitleBarAnchored(prt, "家計簿（複式簿記）", () => SetVisible(false));

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
