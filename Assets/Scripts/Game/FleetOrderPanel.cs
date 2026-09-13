using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略画面の「艦隊メニュー」＝<b>移動命令の唯一の入口</b>（MAP は表示専用になった）。
    /// 盤面をクリックしなくても「誰を・どこへ・なぜ動かせるか」を一覧で確かめてから発令できる。
    ///
    /// 3段構成：
    /// ①自軍艦隊の一覧（艦隊名／艦艇数／状態／現在地→行先）＝行のクリックで選択トグル（<see cref="GalaxyView"/> の選択と同期）。
    /// ②操作（移動／援軍を送る／駐留／出撃／選択解除）＝③の内容を切り替える（出撃は宛先が要らない即時操作）。
    /// ③目的地の一覧（星系名／所有勢力／ホップ数／発令可否の理由）、援軍の宛先（交戦中の戦場）、
    /// または駐留先の要塞（要塞名／所属／駐留／可否の理由・#40 駐留艦隊）。
    ///
    /// 判定は Core の <see cref="FleetOrderRules"/>／<see cref="FortressGarrisonRules"/>、発令は
    /// <see cref="GalaxyView.OrderMove"/>／<see cref="GalaxyView.DispatchReinforcement"/>／
    /// <see cref="GalaxyView.OrderGarrison"/>／<see cref="GalaxyView.OrderSortie"/> に委譲する
    /// ＝<b>ここでは移動・駐留の規則を再実装しない</b>。
    ///
    /// 作法は <see cref="FleetClusterListPanel"/> に合わせる：スクロール一覧・実ピクセル下限の文字/行高
    /// （<see cref="StrategyScreenLayoutRules.MinDesignForActual"/>）・解像度と MAP 窓の変化に追従。
    /// タイトルバー（ドラッグ移動＋×閉じる）は <see cref="WindowChrome"/>、Esc は <see cref="UIWindowStack"/>
    /// へ登録するだけ（Escape を直読みしない＝優先順位チェーンを壊さない）。
    /// </summary>
    public class FleetOrderPanel : MonoBehaviour
    {
        /// <summary>③に何を並べるか。</summary>
        private enum PanelMode
        {
            目的地,
            援軍,
            駐留,
        }

        [Header("外観")]
        [Tooltip("Canvas の描画順（艦隊一覧895より手前・観測オーバーレイ1090より後ろ）")]
        public int canvasSortingOrder = 1000;

        [Tooltip("パネルの幅（設計ピクセル・低解像度では自動で下限まで持ち上げる）")]
        // ★列を「所属軍団」「指揮官」の2本ぶん増やしたので広げた（MAP の設計幅 1420px には収まる）。
        // 幅を据え置いて詰めると1列あたりが読めない幅になり、実機QAで桁が落ちた失敗を繰り返す。
        public float panelDesignWidth = 980f;

        [Tooltip("パネルの高さ（設計ピクセル・MAP の高さに収まるよう自動で切り詰める）")]
        public float panelDesignHeight = 760f;

        [Tooltip("パネル背景色")]
        public Color panelColor = new Color(0.05f, 0.07f, 0.11f, 0.97f);

        [Header("更新")]
        [Tooltip("一覧の表示を作り直す間隔（実時間秒・ポーズ中も進む）")]
        public float refreshInterval = 0.5f;

        [Tooltip("③に並べる行の上限（超えたぶんは打ち切り、その旨を明示する）")]
        public int maxTargetRows = 300;

        // ===== 実ピクセルでの下限（縦長/小窓でも読める・押せる大きさ）=====
        private const float BaseFont = 17f;
        private const float MinFontPx = 14f;
        private const float BaseRow = 34f;
        private const float MinRowPx = 30f;
        // 列が6本になったので実ピクセルの下限も上げる（1列あたりが読める幅を残す）。
        private const float MinPanelWidthPx = 620f;
        // 行き先の3Dプレビュー列の幅（行の幅に対する割合）。小さなアイコンに留めて文字の幅を奪わない。
        private const float PreviewColumnWidth = 0.055f;

        private static FleetOrderPanel instance;

        private Canvas canvas;
        private RectTransform frameRT;
        private RectTransform fleetContent;
        private RectTransform targetContent;
        private TMP_FontAsset jpFont;
        private TextMeshProUGUI selectionLabel;
        private TextMeshProUGUI targetHeader;
        private TextMeshProUGUI messageLabel;
        private TMP_InputField filterField;
        private Button moveModeButton;
        private Button reinforceModeButton;
        private Button garrisonModeButton;
        private Button sortieButton;
        private GameObject sortRowGo;                       // ③の並べ替え行（目的地モードだけ出す）
        private Button sortOrderButton;                     // 昇順⇄降順
        private TextMeshProUGUI sortOrderCaption;
        private readonly List<Button> sortKeyButtons = new List<Button>();
        private Button executeButton;
        private TextMeshProUGUI executeCaption;
        private object escWindowToken;

        private PanelMode mode = PanelMode.目的地;
        private int selectedGoalId = -1;         // ③で選んだ星系（-1＝未選択）
        private int selectedBattlefieldKey = -1; // ③で選んだ戦場（min*100000+max・-1＝未選択）
        private int selectedFortressKey = -1;    // ③で選んだ要塞の回廊（min*100000+max・-1＝未選択）
        private DestinationSortKey sortKey = DestinationSortKey.距離;   // ③の並べ替え条件（既定＝近い順）
        private bool sortAscending = true;
        private string filterText = "";
        private string message = "";

        private readonly List<FleetRow> fleetRows = new List<FleetRow>();
        private readonly List<TargetRow> targetRows = new List<TargetRow>();
        private readonly List<StrategicFleet> fleetBuffer = new List<StrategicFleet>();

        private bool fleetsDirty = true;
        private bool targetsDirty = true;
        private float refreshTimer;
        private int lastSelectionSig;
        private int lastFleetSetSig;
        private int lastRouteSig;
        private Vector2Int lastScreen;
        private Rect lastViewport;

        /// <summary>①の1行（艦隊）。作り直さず中身だけ差し替えられるように参照を持っておく。</summary>
        private sealed class FleetRow
        {
            public GameObject go;
            public Image background;
            public StrategicFleet fleet;
            public TextMeshProUGUI nameLabel;
            public TextMeshProUGUI corpsLabel;       // 所属軍団（軍団旗艦かどうかもここで示す）
            public TextMeshProUGUI commanderLabel;   // その艦隊に実際に任命されている司令官
            public TextMeshProUGUI shipsLabel;       // 艦艇数の列（プレイヤー向けの艦隊規模はこれだけ）
            public TextMeshProUGUI stateLabel;
            public TextMeshProUGUI routeLabel;
        }

        /// <summary>③の1行（目的星系 または 戦場）。</summary>
        private sealed class TargetRow
        {
            public GameObject go;
            public Image background;
            public int key;          // 目的地モード＝星系ID／援軍モード＝戦場キー
            public bool selectable;  // 発令できない理由がある行は押せない
        }

        // ===== 自動生成エントリーポイント（FleetObserverOverlay と同型・手置き不要）=====

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
            if (scene.name != "Strategy") return; // 戦略シーン専用（会戦の指揮は CommandMenu が担う）
            if (UnityEngine.Object.FindAnyObjectByType<FleetOrderPanel>() != null) return;
            new GameObject("FleetOrderPanel").AddComponent<FleetOrderPanel>();
        }

        // ===== static の窓口（上メニュー・MAP の右クリックから呼ばれる）=====

        /// <summary>いま開いているか。</summary>
        public static bool IsOpen => instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf;

        /// <summary>艦隊メニューを開く（③＝目的星系の一覧）。</summary>
        public static void Show()
        {
            FleetOrderPanel p = EnsureInstance();
            if (p != null) p.Open(PanelMode.目的地);
        }

        /// <summary>艦隊メニューを「援軍を送る」で開く（③＝交戦中の戦場一覧）。</summary>
        public static void ShowForReinforcement()
        {
            FleetOrderPanel p = EnsureInstance();
            if (p != null) p.Open(PanelMode.援軍);
        }

        /// <summary>
        /// 艦隊メニューを「援軍を送る」で開き、星系 <paramref name="aId"/>–<paramref name="bId"/> の戦場を
        /// あらかじめ選んでおく（MAP でその回廊を指して呼ばれたとき）。戦場は一覧から選び直せる。
        /// </summary>
        public static void ShowForReinforcement(int aId, int bId)
        {
            FleetOrderPanel p = EnsureInstance();
            if (p == null) return;
            p.selectedBattlefieldKey = BattlefieldKeyOf(aId, bId);
            p.Open(PanelMode.援軍);
        }

        /// <summary>艦隊メニューを「駐留」で開く（③＝要塞の一覧・#40 駐留艦隊）。</summary>
        public static void ShowForGarrison()
        {
            FleetOrderPanel p = EnsureInstance();
            if (p != null) p.Open(PanelMode.駐留);
        }

        /// <summary>
        /// 艦隊メニューを「駐留」で開き、星系 <paramref name="aId"/>–<paramref name="bId"/> の回廊要塞を
        /// あらかじめ選んでおく（MAP でその要塞を指して呼ばれたとき）。要塞は一覧から選び直せる。
        /// </summary>
        public static void ShowForGarrison(int aId, int bId)
        {
            FleetOrderPanel p = EnsureInstance();
            if (p == null) return;
            p.selectedFortressKey = BattlefieldKeyOf(aId, bId);
            p.Open(PanelMode.駐留);
        }

        /// <summary>閉じる。</summary>
        public static void Hide()
        {
            if (instance != null) instance.Close();
        }

        /// <summary>開閉を切り替える（上メニューのボタン用）。</summary>
        public static void Toggle()
        {
            if (IsOpen) Hide();
            else Show();
        }

        private static FleetOrderPanel EnsureInstance()
        {
            if (instance != null) return instance;
            instance = UnityEngine.Object.FindAnyObjectByType<FleetOrderPanel>();
            if (instance == null)
                instance = new GameObject("FleetOrderPanel").AddComponent<FleetOrderPanel>();
            return instance;
        }

        // ===== Unity ライフサイクル =====

        private void Awake()
        {
            instance = this;
            BuildUI();
            if (canvas != null) canvas.gameObject.SetActive(false);
            // Esc は UIWindowStack が「手前から1枚」閉じる＝ここでキーを直読みしない。
            escWindowToken = UIWindowStack.Register(
                () => canvas != null && canvas.gameObject.activeSelf, Close, canvasSortingOrder, "艦隊メニュー");
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!IsOpen) return;

            FollowScreenChanges();

            // 外（MAP のクリック等）で選択が変わったら、一覧の見た目と③の可否を作り直す。
            int sig = SelectionSignature();
            if (sig != lastSelectionSig)
            {
                lastSelectionSig = sig;
                fleetsDirty = true;
                targetsDirty = true;
            }

            // 進路が変わった（到着・再指示）ときも③の可否が変わるので作り直す。
            int route = RouteSignature();
            if (route != lastRouteSig)
            {
                lastRouteSig = route;
                targetsDirty = true;
            }

            // 兵力・状態・行先はゲーム内時間で動くので、実時間で間引いて追従させる（毎フレーム作らない）。
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= Mathf.Max(0.1f, refreshInterval))
            {
                refreshTimer = 0f;
                fleetsDirty = true;
            }

            if (fleetsDirty) RefreshFleetRows();
            if (targetsDirty) RebuildTargetRows();
        }

        // ===== 開閉 =====

        private void Open(PanelMode m)
        {
            mode = m;
            message = "";
            fleetsDirty = true;
            targetsDirty = true;
            refreshTimer = 0f;
            lastSelectionSig = SelectionSignature();
            lastRouteSig = RouteSignature();
            if (canvas != null) canvas.gameObject.SetActive(true);
            Layout();
            RefreshFleetRows();
            RebuildTargetRows();
        }

        private void Close()
        {
            if (canvas != null) canvas.gameObject.SetActive(false);
        }

        // ===== 盤面（GalaxyView）への問い合わせ =====

        private static GalaxyView View => GalaxyView.Active;

        private static Faction PlayerFaction
            => GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

        /// <summary>自軍の戦略艦隊（敵軍は発令できないので一覧に出さない）。</summary>
        private List<StrategicFleet> PlayerFleets()
        {
            fleetBuffer.Clear();
            GalaxyView gv = View;
            StrategicFleetRegistry reg = gv != null ? gv.Registry : null;
            if (reg == null || reg.fleets == null) return fleetBuffer;

            Faction player = PlayerFaction;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != player) continue;
                fleetBuffer.Add(f);
            }
            return fleetBuffer;
        }

        /// <summary>選択中の艦隊（GalaxyView が持つ唯一の選択状態を読むだけ）。</summary>
        private static IReadOnlyList<StrategicFleet> Selected()
        {
            GalaxyView gv = View;
            return gv != null ? gv.SelectedFleets : null;
        }

        /// <summary>③の可否を代表して判定する艦隊（選択の先頭）。</summary>
        private static StrategicFleet PrimaryFleet()
        {
            IReadOnlyList<StrategicFleet> sel = Selected();
            if (sel == null) return null;
            for (int i = 0; i < sel.Count; i++)
                if (sel[i] != null) return sel[i];
            return null;
        }

        private static bool IsSelected(StrategicFleet f)
        {
            IReadOnlyList<StrategicFleet> sel = Selected();
            if (sel == null || f == null) return false;
            for (int i = 0; i < sel.Count; i++)
                if (sel[i] == f) return true;
            return false;
        }

        private static string SystemName(int systemId)
        {
            GalaxyView gv = View;
            return gv != null ? gv.SystemNameOf(systemId) : ("#" + systemId);
        }

        private static int BattlefieldKeyOf(int aId, int bId)
            => Mathf.Min(aId, bId) * 100000 + Mathf.Max(aId, bId);

        // 選択・進路の指紋（変化検出用＝毎フレームの作り直しを避ける）。
        private int SelectionSignature()
        {
            IReadOnlyList<StrategicFleet> sel = Selected();
            if (sel == null) return 0;
            int sig = 17 + sel.Count;
            for (int i = 0; i < sel.Count; i++)
                if (sel[i] != null) sig = sig * 31 + sel[i].id;
            return sig;
        }

        private int RouteSignature()
        {
            StrategicFleet f = PrimaryFleet();
            if (f == null) return 0;
            if (!FleetOrderRules.TryDescribeRoute(f, out int from, out int hop, out int final)) return 0;
            return ((from * 397) ^ (hop * 31) ^ final) * 31 + (f.engaged ? 1 : 0) + (f.IsOnCorridor ? 2 : 0);
        }

        // ===== ①艦隊一覧 =====

        /// <summary>
        /// 艦隊の顔ぶれが変わっていなければ<b>行を作り直さず中身だけ差し替える</b>
        /// （更新のたびに行が消えるとクリックを取りこぼすため）。
        /// </summary>
        private void RefreshFleetRows()
        {
            fleetsDirty = false;
            List<StrategicFleet> fleets = PlayerFleets();

            int setSig = 17 + fleets.Count;
            for (int i = 0; i < fleets.Count; i++) setSig = setSig * 31 + fleets[i].id;
            if (setSig != lastFleetSetSig)
            {
                lastFleetSetSig = setSig;
                RebuildFleetRows(fleets);
            }

            float font = FontSize();
            GalaxyView gv = View;
            for (int i = 0; i < fleetRows.Count; i++)
            {
                FleetRow row = fleetRows[i];
                if (row == null || row.fleet == null) continue;
                bool sel = IsSelected(row.fleet);
                row.background.color = sel ? new Color(0.20f, 0.32f, 0.46f, 1f) : new Color(0.11f, 0.15f, 0.22f, 1f);
                row.nameLabel.text = FleetTitle(row.fleet);
                row.nameLabel.color = sel ? new Color(1f, 0.92f, 0.62f) : new Color(0.92f, 0.95f, 1f);
                // 所属軍団と司令官は独立した列（艦隊名や状態に混ぜない）。
                if (row.corpsLabel != null)
                {
                    row.corpsLabel.text = FleetCommandLabelRules.CorpsLabel(row.fleet);
                    row.corpsLabel.fontSize = font;
                }
                if (row.commanderLabel != null)
                {
                    // 名前は盤面の人物データから引く（合成しない・軍団長で代用しない）。未任命はそのまま出す。
                    string who = gv != null ? gv.FleetCommanderLabel(row.fleet) : FleetCommandLabelRules.Unassigned;
                    row.commanderLabel.text = who;
                    row.commanderLabel.color = who == FleetCommandLabelRules.Unassigned
                        ? new Color(0.62f, 0.66f, 0.74f) : new Color(1f, 0.90f, 0.78f);
                    row.commanderLabel.fontSize = font;
                }
                // ★プレイヤー向けの艦隊規模は<b>艦艇数だけ</b>（抽象兵力は内部の戦闘計算専用で画面に出さない）。
                if (row.shipsLabel != null) row.shipsLabel.text = FleetShipCountRules.Label(row.fleet.Ships);
                row.stateLabel.text = StateText(row.fleet);
                row.routeLabel.text = RouteText(row.fleet);
                row.nameLabel.fontSize = font;
                row.stateLabel.fontSize = font;
                row.routeLabel.fontSize = font;
            }

            UpdateSelectionLabel();
        }

        private void RebuildFleetRows(List<StrategicFleet> fleets)
        {
            ClearRows(fleetRows);
            float font = FontSize();
            float rowH = RowHeight();

            if (fleets.Count == 0)
            {
                MakeNotice(fleetContent, "自軍の艦隊がありません。", font, rowH);
                return;
            }

            for (int i = 0; i < fleets.Count; i++)
                fleetRows.Add(MakeFleetRow(fleets[i], font, rowH));
        }

        private FleetRow MakeFleetRow(StrategicFleet fleet, float font, float rowH)
        {
            GameObject go = new GameObject("Fleet_" + fleet.id, typeof(RectTransform));
            go.transform.SetParent(fleetContent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;

            Image img = go.AddComponent<Image>();
            img.color = new Color(0.11f, 0.15f, 0.22f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.targetGraphic = img;
            StrategicFleet captured = fleet;
            // 行のクリック＝選択のトグル（複数選択に対応）。GalaxyView の選択状態が唯一の窓口。
            btn.onClick.AddListener(() =>
            {
                GalaxyView gv = View;
                if (gv == null) return;
                gv.ToggleSelect(captured);
                fleetsDirty = true;
                targetsDirty = true;
                lastSelectionSig = SelectionSignature();
            });

            RectTransform rt = (RectTransform)go.transform;
            var row = new FleetRow
            {
                go = go,
                background = img,
                fleet = fleet,
                // 列の割り付け：艦隊名 / **所属軍団** / **指揮官** / **艦艇数** / 状態 / 現在地→行先。
                // ★プレイヤーに見せる艦隊規模は艦艇数だけ（抽象兵力は内部計算専用で画面に出さない）。
                // 艦艇数は桁が多いので右寄せで広めに取り、省略記号で桁を落とさない。
                // ★「軍団旗艦」は艦隊名でなく軍団の列に出す＝艦隊名に「・旗艦」と付けると
                //   「他の艦隊には旗艦が無い」と読まれる（全艦隊に旗艦はある）。
                nameLabel = MakeCell(rt, "", font, new Color(0.92f, 0.95f, 1f), 0f, 0.13f, TextAlignmentOptions.Left, 10f, 2f),
                corpsLabel = MakeCell(rt, "", font, new Color(0.85f, 0.82f, 1f), 0.13f, 0.32f, TextAlignmentOptions.Left, 4f, 2f),
                commanderLabel = MakeCell(rt, "", font, new Color(1f, 0.90f, 0.78f), 0.32f, 0.51f, TextAlignmentOptions.Left, 4f, 2f),
                shipsLabel = MakeNumberCell(rt, font, new Color(0.86f, 0.93f, 1f), 0.51f, 0.67f),
                stateLabel = MakeCell(rt, "", font, new Color(0.72f, 0.88f, 0.78f), 0.67f, 0.79f, TextAlignmentOptions.Left, 6f, 2f),
                routeLabel = MakeCell(rt, "", font, new Color(0.80f, 0.86f, 0.95f), 0.79f, 1f, TextAlignmentOptions.Left, 4f, 10f),
            };
            return row;
        }

        /// <summary>
        /// 艦隊の呼び名。<b>「・旗艦」を付けない</b>＝軍団旗艦かどうかは「所属軍団」の列で示す。
        /// 艦隊名に混ぜると「他の艦隊には旗艦が無い」と読まれる（全艦隊に旗艦はある）。
        /// </summary>
        private static string FleetTitle(StrategicFleet f) => FleetCommandLabelRules.FleetTitle(f);

        /// <summary>
        /// 状態の1行。基本は Core の <see cref="FleetOrderRules.StateLabel"/> だが、要塞に駐留している艦隊は
        /// 「駐留」と出す（駐留は盤面の状態＝艦隊側のフラグではないので Core からは見えない）。
        /// どの要塞かは所在地の列（<see cref="RouteText"/>）に添える＝狭い状態欄で要塞名が切れないように。
        /// </summary>
        private static string StateText(StrategicFleet f)
            => GarrisonOf(f) != null ? "駐留" : FleetOrderRules.StateLabel(f);

        /// <summary>その艦隊が駐留している要塞（していなければ null）。判定は <see cref="GalaxyView"/> の窓口へ委譲。</summary>
        private static Fortress GarrisonOf(StrategicFleet f)
        {
            GalaxyView gv = View;
            return gv != null ? gv.GarrisonOf(f) : null;
        }

        /// <summary>選択中に駐留している艦隊が1隊でもあるか（「出撃」ボタンの活性）。</summary>
        private static bool AnySelectedGarrisoned()
        {
            IReadOnlyList<StrategicFleet> sel = Selected();
            if (sel == null) return false;
            for (int i = 0; i < sel.Count; i++)
                if (sel[i] != null && GarrisonOf(sel[i]) != null) return true;
            return false;
        }

        /// <summary>現在地→行先（多ホップは最終目的地も添える）。</summary>
        private static string RouteText(StrategicFleet f)
        {
            if (!FleetOrderRules.TryDescribeRoute(f, out int fromId, out int hopToId, out int finalId))
                return "";
            if (fromId == hopToId && hopToId == finalId)
            {
                // 停泊中：駐留していればどの要塞かをここで添える（状態欄は狭いので名前を入れない）。
                Fortress g = GarrisonOf(f);
                string here = SystemName(fromId);
                return g != null ? here + "（" + (string.IsNullOrEmpty(g.fortressName) ? "要塞" : g.fortressName) + "）" : here;
            }

            string text = SystemName(fromId) + " → " + SystemName(hopToId);
            if (FleetOrderRules.IsMultiHop(f) && finalId != hopToId)
                text += "（最終 " + SystemName(finalId) + "）";
            return text;
        }

        private void UpdateSelectionLabel()
        {
            if (selectionLabel == null) return;
            IReadOnlyList<StrategicFleet> sel = Selected();
            int n = sel != null ? sel.Count : 0;
            if (n == 0)
            {
                selectionLabel.text = "<color=#ffcc66>艦隊が未選択です。上の一覧から選んでください（行をクリックで選択／もう一度で解除）。</color>";
                return;
            }
            // 選択した艦隊の規模は上部でも読めるようにする（一覧を目で追わずに確認できる）。
            // ★出すのは<b>艦艇数だけ</b>。合計は各艦隊の実隻数の合計で、兵力の合計ではない。
            int totalShips = 0;
            for (int i = 0; i < sel.Count; i++)
            {
                if (sel[i] == null) continue;
                totalShips += Mathf.Max(0, sel[i].Ships);
            }

            var sb = new StringBuilder(160);
            sb.Append("選択中 ").Append(n).Append(" 隊：");
            for (int i = 0; i < sel.Count && i < 6; i++)
            {
                if (sel[i] == null) continue;
                if (i > 0) sb.Append('、');
                sb.Append(FleetTitle(sel[i]));
                // 1隊だけ選んでいるときは、その艦隊の数値をそのまま添える。
                if (n == 1) sb.Append("（").Append(FleetShipCountRules.Label(sel[i].Ships)).Append("）");
            }
            if (n > 6) sb.Append(" ほか");
            if (n > 1)
                sb.Append("　合計 ").Append(FleetShipCountRules.Label(totalShips));
            selectionLabel.text = sb.ToString();
        }

        // ===== ③目的地／戦場の一覧 =====

        private void RebuildTargetRows()
        {
            targetsDirty = false;
            ClearRows(targetRows);

            float font = FontSize();
            float rowH = RowHeight();

            if (mode == PanelMode.援軍) BuildBattlefieldRows(font, rowH);
            else if (mode == PanelMode.駐留) BuildGarrisonRows(font, rowH);
            else BuildDestinationRows(font, rowH);

            UpdateModeChrome();
        }

        /// <summary>③の1行ぶんの表示データ（並べ替えは Core、表示文字列はこちら）。</summary>
        private sealed class PendingTarget
        {
            public int systemId;
            public string name;
            public string owner;
            public string hopsText;
            public string note;
            public bool selectable;
            public bool warn;
        }

        private readonly List<PendingTarget> pendingTargets = new List<PendingTarget>();
        private readonly List<DestinationRow> sortRows = new List<DestinationRow>();

        private void BuildDestinationRows(float font, float rowH)
        {
            GalaxyView gv = View;
            GalaxyMap map = gv != null ? gv.Map : null;
            if (map == null || map.systems == null)
            {
                MakeNotice(targetContent, "盤面（銀河マップ）がありません。", font, rowH);
                return;
            }

            StrategicFleet primary = PrimaryFleet();
            if (primary == null)
            {
                MakeNotice(targetContent, "艦隊を選ぶと、行ける星系と行けない理由がここに出ます。", font, rowH);
                return;
            }

            Faction player = PlayerFaction;
            string filter = filterText != null ? filterText.Trim() : "";
            int skipped = 0;

            pendingTargets.Clear();
            sortRows.Clear();

            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s == null) continue;

                string name = string.IsNullOrEmpty(s.systemName) ? ("#" + s.id) : s.systemName;
                if (filter.Length > 0 && name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                if (pendingTargets.Count >= Mathf.Max(1, maxTargetRows)) { skipped++; continue; }

                MoveOrderRejection reason = FleetOrderRules.CanMoveTo(map, primary, player, s.id);
                // 要塞封鎖だけは「行けるが力ずくになる」＝警告にとどめ、押せるままにする（発令は通る）。
                bool selectable = reason == MoveOrderRejection.なし || reason == MoveOrderRejection.要塞封鎖;
                bool warn = reason == MoveOrderRejection.要塞封鎖;

                // ★距離は<b>実際に取る航路</b>から数える（要塞回避を優先する PlanRoute と同じ経路）。
                // 表示だけ別の経路で数えると、並べ替えた「近い順」が実際の行程と食い違う。
                int hops = FleetOrderRules.PlannedHops(map, primary, s.id);

                // ★発令前に「実際どこまで行くか」を出す（飛び石禁止で途中の他勢力星系で必ず止まるため）。
                // ここを出さないと「アイガーへ移動」と言いながらセロトーレで止まる、という食い違いになる。
                string note = reason == MoveOrderRejection.なし ? "発令できます" : FleetOrderRules.RejectionText(reason);
                bool stopsShort = false;
                if (selectable)
                {
                    string stopover = FleetOrderRules.StopoverNote(map, primary, player, s.id, SystemName);
                    if (!string.IsNullOrEmpty(stopover))
                    {
                        note = $"{s.systemName ?? name} 方面：{stopover}";
                        warn = true;   // 目的地まで一気に行かないので警告色にする
                        stopsShort = true;
                    }
                }

                pendingTargets.Add(new PendingTarget
                {
                    systemId = s.id,
                    name = name,
                    owner = OwnerName(s),
                    hopsText = HopsText(hops),
                    note = note,
                    selectable = selectable,
                    warn = warn,
                });
                sortRows.Add(new DestinationRow(
                    s.id, name, OwnerRank(s, player), hops,
                    FleetDestinationSortRules.VerdictOf(reason, stopsShort), sortRows.Count));
            }

            // 並べ替えは Core（安定・決定論）。並べ替えても選択は星系IDで保持されるので、
            // 行の位置が変わっても別の星系へ発令されることはない。
            FleetDestinationSortRules.Sort(sortRows, sortKey, sortAscending);

            for (int i = 0; i < sortRows.Count; i++)
            {
                PendingTarget t = pendingTargets[sortRows[i].order];
                // ★プレビューは<b>星系IDから</b>引く（行の位置ではない）＝並べ替え・絞り込み・スクロールの
                // あとでも、その行の星系と絵が必ず一致する。
                targetRows.Add(MakeTargetRow(t.systemId, t.name, t.owner, t.hopsText, t.note,
                                             t.selectable, t.warn, font, rowH,
                                             previewPath: gv.PreviewResourcePathForSystem(t.systemId)));
            }

            if (pendingTargets.Count == 0)
                MakeNotice(targetContent, filter.Length > 0 ? "その名前の星系はありません。" : "星系がありません。", font, rowH);
            else if (skipped > 0)
                MakeNotice(targetContent, "（表示上限 " + maxTargetRows + " 件を超えたため " + skipped + " 件を省略しました。名前で絞り込んでください）", font, rowH);
        }

        /// <summary>ホップ数の表示（到達不能は「－」・現在地は「現在地」）。</summary>
        private static string HopsText(int hops)
        {
            if (hops < 0) return "－";
            if (hops == 0) return "現在地";
            return hops + " ホップ";
        }

        /// <summary>
        /// 所属の並び順（小さいほど手前）＝自勢力→非敵対→敵対→不明。
        /// 敵対の判定は <see cref="FactionRelations"/> へ委譲する（「勢力が違えば敵」と書かない）。
        /// </summary>
        private static int OwnerRank(StarSystem s, Faction player)
        {
            if (s == null) return 3;
            if (s.owner == player) return 0;
            return FactionRelations.IsHostile(null, player, s.ownerData, s.owner) ? 2 : 1;
        }

        private void BuildBattlefieldRows(float font, float rowH)
        {
            GalaxyView gv = View;
            if (gv == null)
            {
                MakeNotice(targetContent, "盤面がありません。", font, rowH);
                return;
            }

            List<int> keys = gv.EngagedBattlefields();
            if (keys == null || keys.Count == 0)
            {
                MakeNotice(targetContent, "いま交戦中の戦場はありません。", font, rowH);
                return;
            }

            StrategicFleet primary = PrimaryFleet();
            MoveOrderRejection pre = primary != null
                ? FleetOrderRules.CanOrder(primary, PlayerFaction)
                : MoveOrderRejection.敵軍艦隊;
            // 援軍は「交戦中でない・増援航行中でない自軍艦隊」だけが送れる（DispatchReinforcement の受理条件と同じ）。
            bool canSend = primary != null && pre == MoveOrderRejection.なし;
            string note = canSend ? "派遣できます" : FleetOrderRules.RejectionText(pre);

            for (int i = 0; i < keys.Count; i++)
            {
                int key = keys[i];
                int aId = key / 100000;
                int bId = key % 100000;
                string name = SystemName(aId) + " － " + SystemName(bId);
                targetRows.Add(MakeTargetRow(key, name, "交戦中", "", note, canSend, false, font, rowH));
            }
        }

        /// <summary>
        /// ③＝要塞の一覧（#40 駐留艦隊）。盤面に据えられている回廊要塞をすべて並べ、選択中の艦隊が
        /// そこへ入港できるか／できない理由を Core（<see cref="FortressGarrisonRules.CanGarrison"/>）から引いて出す。
        /// 表示は「要塞名／所属／守備力と駐留／可否の理由」＝施設の守備力と駐留艦隊を足し合わせない。
        /// </summary>
        private void BuildGarrisonRows(float font, float rowH)
        {
            GalaxyView gv = View;
            if (gv == null)
            {
                MakeNotice(targetContent, "盤面がありません。", font, rowH);
                return;
            }

            List<int> keys = gv.FortressCorridors();
            if (keys == null || keys.Count == 0)
            {
                MakeNotice(targetContent, "盤面に要塞がありません（要塞は新規戦役の回廊に据えられます）。", font, rowH);
                return;
            }

            StrategicFleet primary = PrimaryFleet();
            if (primary == null)
                MakeNotice(targetContent, "艦隊を選ぶと、駐留できる要塞と駐留できない理由がここに出ます。", font, rowH);

            for (int i = 0; i < keys.Count; i++)
            {
                int key = keys[i];
                int aId = key / 100000;
                int bId = key % 100000;
                if (!gv.TryFortressAt(aId, bId, out string fname, out string owner, out string garrison)) continue;

                string note;
                bool selectable;
                if (primary == null)
                {
                    note = "艦隊未選択";
                    selectable = false;
                }
                else
                {
                    selectable = gv.CanGarrisonAt(primary, aId, bId, out string shortReason, out _);
                    note = selectable ? "駐留できます" : shortReason;
                }

                // 列は駐留用に割り直す（要塞名／所属／駐留／可否）＝理由を省略記号で落とさない。
                // 全文の理由は「この要塞へ駐留」を押したときの結果行に出す。
                targetRows.Add(MakeTargetRow(key, fname, owner, garrison, note, selectable, false, font, rowH,
                                             0.30f, 0.42f, 0.72f, TextAlignmentOptions.Left));
            }
        }

        private static string OwnerName(StarSystem s)
        {
            if (s.ownerData != null && !string.IsNullOrEmpty(s.ownerData.factionName)) return s.ownerData.factionName;
            return s.owner.ToString();
        }

        /// <summary>現在地（移動中は到達予定の星系）からのホップ数。</summary>
        private static string HopText(GalaxyMap map, int startId, int goalId)
        {
            if (startId == goalId) return "現在地";
            List<int> path = GalaxyPathfinder.FindPath(map, startId, goalId);
            if (path == null || path.Count < 2) return "－";
            return (path.Count - 1) + " ホップ";
        }

        /// <param name="c1">2列目（所有勢力）の開始位置（0〜1の割合）。</param>
        /// <param name="c2">3列目（ホップ数／駐留）の開始位置。</param>
        /// <param name="c3">4列目（可否の理由）の開始位置。</param>
        /// <param name="thirdAlign">3列目の揃え（ホップ数は右／駐留の文字は左）。</param>
        /// <param name="previewPath">
        /// 行の先頭に出す3Dモデルのプレビュー（Resources パス・空＝出さない）。
        /// 盤面と同じモデルを焼いた絵を <see cref="ModelPreviewLibrary"/> から借りる＝別の絵を作らない。
        /// </param>
        private TargetRow MakeTargetRow(int key, string name, string owner, string hops, string note,
                                        bool selectable, bool warn, float font, float rowH,
                                        float c1 = 0.30f, float c2 = 0.45f, float c3 = 0.58f,
                                        TextAlignmentOptions thirdAlign = TextAlignmentOptions.Right,
                                        string previewPath = null)
        {
            GameObject go = new GameObject("Target_" + key, typeof(RectTransform));
            go.transform.SetParent(targetContent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;

            Image img = go.AddComponent<Image>();
            Button btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.targetGraphic = img;
            btn.interactable = selectable;

            var row = new TargetRow { go = go, background = img, key = key, selectable = selectable };
            int captured = key;
            btn.onClick.AddListener(() =>
            {
                if (mode == PanelMode.援軍) selectedBattlefieldKey = captured;
                else if (mode == PanelMode.駐留) selectedFortressKey = captured;
                else selectedGoalId = captured;
                ApplyTargetHighlight();
                UpdateModeChrome();
            });

            RectTransform rt = (RectTransform)go.transform;
            Color nameColor = selectable ? new Color(0.92f, 0.95f, 1f) : new Color(0.55f, 0.60f, 0.68f);
            Color noteColor = !selectable ? new Color(1f, 0.62f, 0.55f)
                            : warn ? new Color(1f, 0.82f, 0.42f)
                                   : new Color(0.66f, 0.86f, 0.70f);

            // 行の先頭に盤面と同じ3Dモデルの絵を置く（見分けやすさ）。焼けていない／未納品なら何も置かず、
            // 文字だけの従来表示になる＝プレビューが出ないことで一覧が壊れない。
            float nameLeft = 0f;
            if (!string.IsNullOrEmpty(previewPath))
            {
                Texture tex = ModelPreviewLibrary.PreviewFor(previewPath);
                if (tex != null)
                {
                    nameLeft = PreviewColumnWidth;
                    var iconGo = new GameObject("Preview", typeof(RectTransform));
                    iconGo.transform.SetParent(rt, false);
                    var icon = iconGo.AddComponent<RawImage>();
                    icon.texture = tex;
                    icon.raycastTarget = false;   // 行のクリックを塞がない
                    RectTransform irt = icon.rectTransform;
                    irt.anchorMin = new Vector2(0f, 0f);
                    irt.anchorMax = new Vector2(nameLeft, 1f);
                    irt.offsetMin = new Vector2(6f, 3f);
                    irt.offsetMax = new Vector2(-2f, -3f);
                }
            }

            MakeCell(rt, name, font, nameColor, nameLeft, c1, TextAlignmentOptions.Left, nameLeft > 0f ? 4f : 10f, 4f);
            MakeCell(rt, owner, font, new Color(0.78f, 0.84f, 0.94f), c1, c2, TextAlignmentOptions.Left, 4f, 4f);
            MakeCell(rt, hops, font, new Color(1f, 0.9f, 0.66f), c2, c3, thirdAlign, 4f, 6f);
            MakeCell(rt, note, font, noteColor, c3, 1f, TextAlignmentOptions.Left, 6f, 10f);

            row.background.color = RowColor(row);
            return row;
        }

        private Color RowColor(TargetRow row)
        {
            int sel = mode == PanelMode.援軍 ? selectedBattlefieldKey
                    : mode == PanelMode.駐留 ? selectedFortressKey
                                             : selectedGoalId;
            if (row.key == sel) return new Color(0.20f, 0.32f, 0.46f, 1f);
            if (!row.selectable) return new Color(0.09f, 0.10f, 0.13f, 1f);
            return new Color(0.11f, 0.15f, 0.22f, 1f);
        }

        private void ApplyTargetHighlight()
        {
            for (int i = 0; i < targetRows.Count; i++)
            {
                TargetRow row = targetRows[i];
                if (row == null || row.background == null) continue;
                row.background.color = RowColor(row);
            }
        }

        // ===== ②操作 =====

        private void SetMode(PanelMode m)
        {
            mode = m;
            message = "";
            targetsDirty = true;
        }

        private void ClearSelection()
        {
            GalaxyView gv = View;
            if (gv != null) gv.ClearSelection();
            message = "選択を解除しました。";
            fleetsDirty = true;
            targetsDirty = true;
            lastSelectionSig = SelectionSignature();
            UpdateModeChrome();
        }

        /// <summary>③で選んだ宛先へ、選択中の全艦隊に発令する。</summary>
        private void Execute()
        {
            if (mode == PanelMode.援軍) ExecuteReinforcement();
            else if (mode == PanelMode.駐留) ExecuteGarrison();
            else ExecuteMove();
            fleetsDirty = true;
            targetsDirty = true;
        }

        private void ExecuteMove()
        {
            GalaxyView gv = View;
            if (gv == null) { message = "盤面がありません。"; return; }
            if (selectedGoalId < 0) { message = "目的の星系を一覧から選んでください。"; return; }

            IReadOnlyList<StrategicFleet> sel = Selected();
            if (sel == null || sel.Count == 0) { message = "艦隊が未選択です。"; return; }

            // 発令中に選択が動いても崩れないよう、いったん写し取ってから回す。
            var targets = new List<StrategicFleet>(sel);
            var sb = new StringBuilder(160);
            int ok = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                StrategicFleet f = targets[i];
                if (f == null) continue;
                bool done = gv.OrderMove(f, selectedGoalId, out string reason);
                if (done) ok++;
                if (sb.Length > 0) sb.Append("　");
                sb.Append(FleetTitle(f)).Append('：')
                  .Append(done ? "発令" : "不可")
                  .Append(string.IsNullOrEmpty(reason) ? "" : "（" + reason + "）");
            }
            message = SystemName(selectedGoalId) + " へ " + ok + " 隊に発令。 " + sb;
        }

        private void ExecuteReinforcement()
        {
            GalaxyView gv = View;
            if (gv == null) { message = "盤面がありません。"; return; }
            if (selectedBattlefieldKey < 0) { message = "援軍を送る戦場を一覧から選んでください。"; return; }

            IReadOnlyList<StrategicFleet> sel = Selected();
            if (sel == null || sel.Count == 0) { message = "艦隊が未選択です。"; return; }

            int aId = selectedBattlefieldKey / 100000;
            int bId = selectedBattlefieldKey % 100000;
            var targets = new List<StrategicFleet>(sel);
            int ok = 0;
            var sb = new StringBuilder(160);
            for (int i = 0; i < targets.Count; i++)
            {
                StrategicFleet f = targets[i];
                if (f == null) continue;
                bool done = gv.DispatchReinforcement(f, aId, bId);
                if (done) ok++;
                if (sb.Length > 0) sb.Append("　");
                sb.Append(FleetTitle(f)).Append('：').Append(done ? "派遣" : "不可");
            }
            message = SystemName(aId) + " － " + SystemName(bId) + " へ " + ok + " 隊を派遣。 " + sb;
        }

        /// <summary>
        /// ③で選んだ要塞へ、選択中の全艦隊を駐留させる（#40 駐留艦隊）。
        /// 可否と名簿の操作は <see cref="GalaxyView.OrderGarrison"/>（→ Core <see cref="FortressGarrisonRules"/>）＝
        /// ここでは規則を再実装しない。艦艇数・指揮官・所属は<b>一切書き換えない</b>（名簿に載るだけ）。
        /// </summary>
        private void ExecuteGarrison()
        {
            GalaxyView gv = View;
            if (gv == null) { message = "盤面がありません。"; return; }
            if (selectedFortressKey < 0) { message = "駐留する要塞を一覧から選んでください。"; return; }

            IReadOnlyList<StrategicFleet> sel = Selected();
            if (sel == null || sel.Count == 0) { message = "艦隊が未選択です。"; return; }

            int aId = selectedFortressKey / 100000;
            int bId = selectedFortressKey % 100000;
            var targets = new List<StrategicFleet>(sel);
            var sb = new StringBuilder(160);
            int ok = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                StrategicFleet f = targets[i];
                if (f == null) continue;
                bool done = gv.OrderGarrison(f, aId, bId, out string reason);
                if (done) ok++;
                if (sb.Length > 0) sb.Append("　");
                sb.Append(FleetTitle(f)).Append('：')
                  .Append(done ? "駐留" : "不可")
                  .Append(string.IsNullOrEmpty(reason) ? "" : "（" + reason + "）");
            }
            string where = gv.TryFortressAt(aId, bId, out string fname, out _, out _) ? fname : "要塞";
            message = where + " へ " + ok + " 隊が駐留。 " + sb;
        }

        /// <summary>
        /// 選択中の艦隊を駐留先の要塞から<b>出撃</b>させる（名簿から外して再び動けるようにする）。
        /// 宛先を選ぶ必要が無いので、③の選択に関わらず押せる（駐留している艦隊が居るときだけ活性）。
        /// </summary>
        private void SortieSelected()
        {
            GalaxyView gv = View;
            if (gv == null) { message = "盤面がありません。"; return; }

            IReadOnlyList<StrategicFleet> sel = Selected();
            if (sel == null || sel.Count == 0) { message = "艦隊が未選択です。"; return; }

            var targets = new List<StrategicFleet>(sel);
            var sb = new StringBuilder(160);
            int ok = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                StrategicFleet f = targets[i];
                if (f == null) continue;
                bool done = gv.OrderSortie(f, out string reason);
                if (done) ok++;
                if (sb.Length > 0) sb.Append("　");
                sb.Append(FleetTitle(f)).Append('：')
                  .Append(done ? "出撃" : "不可")
                  .Append(string.IsNullOrEmpty(reason) ? "" : "（" + reason + "）");
            }
            message = ok + " 隊が出撃（駐留を解除）。 " + sb;
            fleetsDirty = true;
            targetsDirty = true;
            UpdateModeChrome();
        }

        /// <summary>モード（③の中身）に合わせて見出し・ボタンの文言と活性を合わせる。</summary>
        private void UpdateModeChrome()
        {
            bool reinforce = mode == PanelMode.援軍;
            bool garrison = mode == PanelMode.駐留;

            if (targetHeader != null)
                targetHeader.text = reinforce
                    ? "③ 援軍の宛先（交戦中の戦場）"
                    : garrison
                        ? "③ 駐留先の要塞" + SelectedFortressNote()
                        : "③ 目的の星系（" + FleetDestinationSortRules.HeaderLabel(sortKey, sortAscending)
                          + BasisFleetNote() + "・押せない行は発令できない理由つき）";

            UpdateSortChrome(!reinforce && !garrison);   // 並べ替えは目的地の一覧だけ

            if (executeCaption != null)
                executeCaption.text = reinforce ? "この戦場へ援軍を送る"
                                    : garrison ? "この要塞へ駐留"
                                               : "この星系へ移動";

            if (executeButton != null)
            {
                bool hasFleet = Selected() != null && Selected().Count > 0;
                bool hasTarget = reinforce ? selectedBattlefieldKey >= 0
                               : garrison ? selectedFortressKey >= 0
                                          : selectedGoalId >= 0;
                executeButton.interactable = hasFleet && hasTarget;
            }

            if (moveModeButton != null) moveModeButton.interactable = mode != PanelMode.目的地;
            if (reinforceModeButton != null) reinforceModeButton.interactable = !reinforce;
            if (garrisonModeButton != null) garrisonModeButton.interactable = !garrison;
            // 出撃は宛先が要らない＝駐留中の艦隊を選んでいるときだけ押せる（モードに依らない）。
            if (sortieButton != null) sortieButton.interactable = AnySelectedGarrisoned();

            if (filterField != null)
                filterField.gameObject.SetActive(!reinforce && !garrison); // 戦場・要塞は数が少ないので絞り込みは要らない

            if (messageLabel != null) messageLabel.text = message;
            UpdateSelectionLabel();
        }

        /// <summary>
        /// ③の距離・可否が<b>どの艦隊を基準にしているか</b>。複数選んでいるときは
        /// <see cref="PrimaryFleet"/>（選択の先頭）が基準＝従来の表示と同じ定義。
        /// これを出さないと「2隊選んだのに距離が片方のもの」と分からない。
        /// </summary>
        private static string BasisFleetNote()
        {
            IReadOnlyList<StrategicFleet> sel = Selected();
            int n = sel != null ? sel.Count : 0;
            StrategicFleet primary = PrimaryFleet();
            if (primary == null) return "";
            return n > 1 ? $"・{FleetTitle(primary)} 基準" : "";
        }

        /// <summary>
        /// ③の見出しに添える、選んだ要塞の状態（施設の守備力＋駐留艦隊）。
        /// 行の狭い列には短縮形しか出せないので、選んだ要塞の全文はここで読ませる。
        /// </summary>
        private string SelectedFortressNote()
        {
            if (selectedFortressKey < 0) return "（行をクリックで選択・押せない行は駐留できない理由つき）";
            GalaxyView gv = View;
            if (gv == null) return "";
            int aId = selectedFortressKey / 100000;
            int bId = selectedFortressKey % 100000;
            string summary = gv.FortressSummary(aId, bId);
            if (string.IsNullOrEmpty(summary)) return "";
            return "　選択：" + summary + "（" + SystemName(aId) + " － " + SystemName(bId) + " 回廊）";
        }

        // ===== 大きさ・位置（解像度と MAP 窓に追従）=====

        /// <summary>実ピクセルで下限を割らない文字サイズ（設計ピクセル）。</summary>
        private static float FontSize()
            => StrategyScreenLayoutRules.MinDesignForActual(BaseFont, MinFontPx, Screen.width);

        /// <summary>実ピクセルで下限を割らない行の高さ（＝クリック領域）。</summary>
        private static float RowHeight()
            => StrategyScreenLayoutRules.MinDesignForActual(BaseRow, MinRowPx, Screen.width);

        private void FollowScreenChanges()
        {
            var now = new Vector2Int(Screen.width, Screen.height);
            Camera mapCam = Camera.main;
            Rect cur = mapCam != null ? mapCam.rect : lastViewport;
            if (now == lastScreen && cur == lastViewport) return;

            lastScreen = now;
            lastViewport = cur;
            Layout();
            // 文字/行の下限は画面幅で変わるので、行そのものを作り直す。
            lastFleetSetSig = 0;
            fleetsDirty = true;
            targetsDirty = true;
        }

        /// <summary>MAP の左上に寄せて置き、MAP の高さに収まるよう切り詰める（右カラムの観測窓と重ならない）。</summary>
        private void Layout()
        {
            if (frameRT == null) return;

            lastScreen = new Vector2Int(Screen.width, Screen.height);
            Camera mapCam = Camera.main;
            lastViewport = mapCam != null ? mapCam.rect : lastViewport;

            float uiScale = Screen.width > 0 ? Screen.width / StrategyScreenLayoutRules.ReferenceWidth : 1f;
            if (uiScale <= 0.0001f) uiScale = 1f;

            StrategyScreenLayout l = StrategyMapWindow.Layout;

            // 幅：低解像度でも読める下限まで持ち上げつつ、MAP の幅は超えない。
            float width = StrategyScreenLayoutRules.MinDesignForActual(panelDesignWidth, MinPanelWidthPx, Screen.width);
            float mapDesignWidth = StrategyScreenLayoutRules.ToDesignWidth(l.mapWidth);
            width = Mathf.Min(width, Mathf.Max(MinPanelWidthPx, mapDesignWidth - 24f));

            // 高さ：MAP の縦幅（実ピクセル）を設計ピクセルへ戻して、その中へ収める。
            float mapHeightDesign = (l.MapHeight * Screen.height) / uiScale;
            float height = Mathf.Clamp(panelDesignHeight, RowHeight() * 8f, Mathf.Max(RowHeight() * 8f, mapHeightDesign - 24f));

            frameRT.sizeDelta = new Vector2(width, height);

            float leftPx = l.mapLeft * Screen.width + 14f;
            float topPx = l.mapTop * Screen.height - 14f;
            frameRT.anchoredPosition = new Vector2(leftPx / uiScale, topPx / uiScale);
        }

        // ===== UI 構築 =====

        private void BuildUI()
        {
            EnsureEventSystem();
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");

            GameObject canvasObj = new GameObject("FleetOrderCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(StrategyScreenLayoutRules.ReferenceWidth,
                                                     StrategyScreenLayoutRules.ReferenceHeight);
            scaler.matchWidthOrHeight = 0f; // 幅基準（MinDesignForActual の前提と揃える）
            canvasObj.AddComponent<GraphicRaycaster>();

            // 枠（ディマーを敷かない＝そもそも非モーダル・盤面を塞がない）。
            GameObject frame = new GameObject("Frame", typeof(RectTransform));
            frame.transform.SetParent(canvasObj.transform, false);
            frameRT = frame.GetComponent<RectTransform>();
            frameRT.anchorMin = frameRT.anchorMax = Vector2.zero;
            frameRT.pivot = new Vector2(0f, 1f);
            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = panelColor;
            Outline outline = frame.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.84f, 0.36f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 10);
            vlg.spacing = 5f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // タイトルバー（ドラッグ移動＋×閉じる）は共通クローム＝二重実装しない。
            WindowChrome.AddTitleBarLayout(frameRT, "艦隊メニュー（移動命令）", Close);

            float font = FontSize();
            float rowH = RowHeight();

            selectionLabel = MakeSectionLabel(frameRT, "", font, new Color(1f, 0.88f, 0.55f), rowH);
            MakeSectionLabel(frameRT, "① 自軍の艦隊（行をクリックで選択／もう一度で解除・複数選べます）",
                             font, new Color(0.72f, 0.82f, 0.92f), rowH * 0.9f);
            fleetContent = MakeScrollArea(frameRT, "FleetList", 1f, rowH * 3f);

            BuildActionRow(frameRT, font, rowH);

            targetHeader = MakeSectionLabel(frameRT, "", font, new Color(0.72f, 0.82f, 0.92f), rowH * 0.9f);
            BuildSortRow(frameRT, font, rowH);
            BuildFilterRow(frameRT, font, rowH);
            targetContent = MakeScrollArea(frameRT, "TargetList", 1.5f, rowH * 3f);

            BuildExecuteRow(frameRT, font, rowH);

            messageLabel = MakeSectionLabel(frameRT, "", font, new Color(0.86f, 0.92f, 1f), rowH * 1.5f);
            messageLabel.textWrappingMode = TextWrappingModes.Normal;
            messageLabel.overflowMode = TextOverflowModes.Truncate;

            Layout();
        }

        /// <summary>
        /// ②の操作ボタン。5つを1行に詰めると1つあたりが読めない幅になるので<b>2行</b>に分ける
        /// （文字を小さくして詰め込まない＝実機QAで狭い列が読めなかった反省）。
        /// 上段＝③の中身を切り替えるモード、下段＝宛先の要らない即時の操作。
        /// </summary>
        private void BuildActionRow(RectTransform parent, float font, float rowH)
        {
            RectTransform top = MakeButtonRow(parent, "Actions", rowH);
            moveModeButton = MakeButton(top, "② 移動", font, () => { SetMode(PanelMode.目的地); RebuildTargetRows(); }, out _);
            reinforceModeButton = MakeButton(top, "② 援軍を送る", font, () => { SetMode(PanelMode.援軍); RebuildTargetRows(); }, out _);
            garrisonModeButton = MakeButton(top, "② 駐留", font, () => { SetMode(PanelMode.駐留); RebuildTargetRows(); }, out _);

            RectTransform bottom = MakeButtonRow(parent, "Actions2", rowH);
            // 出撃は宛先を選ばない即時操作＝駐留中の艦隊を選んでいるときだけ押せる。
            sortieButton = MakeButton(bottom, "② 出撃（駐留解除）", font, SortieSelected, out _);
            MakeButton(bottom, "② 選択解除", font, ClearSelection, out _);
        }

        /// <summary>
        /// ③の並べ替え行（距離／星系名／所属／可否 ＋ 昇順⇄降順）。
        /// <b>押しても発令しない</b>＝一覧を作り直すだけ。選択中の目的地は星系IDで持っているので、
        /// 並びが変わっても選択は同じ星系のまま（行番号で覚えない）。
        /// </summary>
        private void BuildSortRow(RectTransform parent, float font, float rowH)
        {
            RectTransform row = MakeButtonRow(parent, "SortRow", rowH);
            sortRowGo = row.gameObject;

            sortKeyButtons.Clear();
            for (int i = 0; i < FleetDestinationSortRules.AllKeys.Length; i++)
            {
                DestinationSortKey key = FleetDestinationSortRules.AllKeys[i];
                string caption = key + "（" + FleetDestinationSortRules.AscendingMeaning(key) + "）";
                Button b = MakeButton(row, caption, font, () => SetSortKey(key), out _);
                sortKeyButtons.Add(b);
            }

            sortOrderButton = MakeButton(row, "昇順", font, ToggleSortOrder, out sortOrderCaption);
        }

        /// <summary>並べ替えの条件を変える。同じ条件をもう一度押したら昇降順を反転する（一般的な作法）。</summary>
        private void SetSortKey(DestinationSortKey key)
        {
            if (sortKey == key) sortAscending = !sortAscending;
            else { sortKey = key; sortAscending = true; }
            targetsDirty = true;
            RebuildTargetRows();
        }

        private void ToggleSortOrder()
        {
            sortAscending = !sortAscending;
            targetsDirty = true;
            RebuildTargetRows();
        }

        /// <summary>並べ替え行の見た目（いまの条件のボタンだけ押せない＝選択中を示す）。</summary>
        private void UpdateSortChrome(bool visible)
        {
            if (sortRowGo != null && sortRowGo.activeSelf != visible) sortRowGo.SetActive(visible);
            if (!visible) return;

            for (int i = 0; i < sortKeyButtons.Count && i < FleetDestinationSortRules.AllKeys.Length; i++)
            {
                Button b = sortKeyButtons[i];
                if (b != null) b.interactable = FleetDestinationSortRules.AllKeys[i] != sortKey;
            }
            if (sortOrderCaption != null) sortOrderCaption.text = sortAscending ? "昇順" : "降順";
        }

        /// <summary>横並びのボタン置き場を1行作る。</summary>
        private RectTransform MakeButtonRow(RectTransform parent, string name, float rowH)
        {
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.minHeight = rowH * 1.1f; le.preferredHeight = rowH * 1.1f; le.flexibleHeight = 0f;
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;
            return (RectTransform)row.transform;
        }

        private void BuildFilterRow(RectTransform parent, float font, float rowH)
        {
            GameObject go = new GameObject("Filter", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // ★TMP_InputField は OnEnable で textComponent/textViewport を要求する。
            // 組み立て終わるまで無効にしておき、参照を入れてから有効化する（起動時のエラーを避ける）。
            go.SetActive(false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH; le.flexibleHeight = 0f;

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.13f, 0.19f, 1f);

            RectTransform rt = (RectTransform)go.transform;

            GameObject area = new GameObject("TextArea", typeof(RectTransform));
            area.transform.SetParent(rt, false);
            RectTransform areaRT = area.GetComponent<RectTransform>();
            areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one;
            areaRT.offsetMin = new Vector2(10f, 2f); areaRT.offsetMax = new Vector2(-10f, -2f);
            area.AddComponent<RectMask2D>();

            TextMeshProUGUI placeholder = MakeText(areaRT, "星系名で絞り込む（空欄＝すべて）", font, new Color(0.55f, 0.62f, 0.72f));
            StretchFull(placeholder.rectTransform);
            TextMeshProUGUI text = MakeText(areaRT, "", font, new Color(0.95f, 0.97f, 1f));
            StretchFull(text.rectTransform);
            text.richText = false;

            filterField = go.AddComponent<TMP_InputField>();
            filterField.textViewport = areaRT;
            filterField.textComponent = text;
            filterField.placeholder = placeholder;
            filterField.targetGraphic = bg;
            filterField.lineType = TMP_InputField.LineType.SingleLine;
            filterField.richText = false;
            if (jpFont != null) filterField.fontAsset = jpFont;
            filterField.pointSize = font;
            filterField.onValueChanged.AddListener(v => { filterText = v; targetsDirty = true; });
            go.SetActive(true);
        }

        private void BuildExecuteRow(RectTransform parent, float font, float rowH)
        {
            GameObject row = new GameObject("Execute", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            LayoutElement le = row.AddComponent<LayoutElement>();
            le.minHeight = rowH * 1.2f; le.preferredHeight = rowH * 1.2f; le.flexibleHeight = 0f;
            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6f;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true; hlg.childForceExpandHeight = true;

            executeButton = MakeButton(row.transform, "この星系へ移動", font, Execute, out executeCaption);
        }

        /// <summary>スクロールできる一覧の中身（Content）を作って返す。</summary>
        private RectTransform MakeScrollArea(RectTransform parent, string name, float flexibleHeight, float minHeight)
        {
            GameObject scrollGo = new GameObject(name, typeof(RectTransform));
            scrollGo.transform.SetParent(parent, false);
            LayoutElement le = scrollGo.AddComponent<LayoutElement>();
            le.flexibleHeight = flexibleHeight;
            le.minHeight = minHeight;

            Image frameBg = scrollGo.AddComponent<Image>();
            frameBg.color = new Color(0.03f, 0.04f, 0.06f, 0.9f);

            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 28f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollGo.transform, false);
            RectTransform vrt = viewport.GetComponent<RectTransform>();
            vrt.anchorMin = Vector2.zero; vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero; vrt.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            scroll.viewport = vrt;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            // ★横に張ったら sizeDelta.x を 0 にする（既定 (100,100) のままだと左右へ 50px はみ出して切れる）。
            crt.sizeDelta = Vector2.zero;
            crt.anchoredPosition = Vector2.zero;

            VerticalLayoutGroup cvlg = content.AddComponent<VerticalLayoutGroup>();
            cvlg.padding = new RectOffset(6, 6, 5, 5);
            cvlg.spacing = 3f;
            cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = crt;
            UiScrollbars.Attach(scroll);   // #H スクロールできることを画面で示す（見えて掴めるバー）
            return crt;
        }

        private Button MakeButton(Transform parent, string caption, float font, UnityEngine.Events.UnityAction onClick,
                                  out TextMeshProUGUI captionLabel)
        {
            GameObject go = new GameObject("Btn_" + caption, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.22f, 0.32f, 1f);
            Button btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(onClick);

            captionLabel = MakeText((RectTransform)go.transform, caption, font, new Color(0.95f, 0.97f, 1f));
            captionLabel.alignment = TextAlignmentOptions.Center;
            captionLabel.raycastTarget = false;
            StretchFull(captionLabel.rectTransform);
            return btn;
        }

        private TextMeshProUGUI MakeSectionLabel(RectTransform parent, string text, float font, Color color, float height)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = height; le.preferredHeight = height; le.flexibleHeight = 0f;

            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = font;
            t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.margin = new Vector4(4f, 0f, 4f, 0f);
            if (jpFont != null) t.font = jpFont;
            return t;
        }

        /// <summary>一覧に出す注記（行が無いとき・打ち切ったときの説明）。</summary>
        private void MakeNotice(RectTransform parent, string text, float font, float rowH)
        {
            if (parent == null) return;
            GameObject go = new GameObject("Notice", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = rowH; le.preferredHeight = rowH;

            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = font;
            t.color = new Color(1f, 0.82f, 0.42f);
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.margin = new Vector4(8f, 0f, 8f, 0f);
            if (jpFont != null) t.font = jpFont;
        }

        /// <summary>
        /// 行の中の1欄（横方向の割合で位置を決める）。名前が長くて省略されても、兵力・状態・理由は
        /// 別の欄なので必ず読める（<see cref="FleetClusterListPanel"/> と同じ作法）。
        /// </summary>
        private TextMeshProUGUI MakeCell(RectTransform parent, string text, float font, Color color,
                                         float x0, float x1, TextAlignmentOptions align, float leftMargin, float rightMargin)
        {
            TextMeshProUGUI t = MakeText(parent, text, font, color);
            t.alignment = align;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = new Vector2(x0, 0f); rt.anchorMax = new Vector2(x1, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            t.margin = new Vector4(leftMargin, 0f, rightMargin, 0f);
            return t;
        }

        /// <summary>
        /// 数値の列（兵力・艦艇数）。<b>省略記号で桁を落とさない</b>のが要点で、
        /// 幅が足りないときは文字を少しだけ詰めて全桁と単位を必ず見せる（実機QA：「兵力249 …」で読めなかった）。
        /// 最小サイズは実ピクセル下限を割らない範囲に留め、極端に小さくはしない。
        /// </summary>
        private TextMeshProUGUI MakeNumberCell(RectTransform parent, float font, Color color, float x0, float x1)
        {
            TextMeshProUGUI t = MakeCell(parent, "", font, color, x0, x1, TextAlignmentOptions.Right, 4f, 8f);
            t.overflowMode = TextOverflowModes.Overflow;   // 省略しない（… を出さない）
            t.enableAutoSizing = true;
            t.fontSizeMax = font;
            t.fontSizeMin = font * 0.82f;                  // 詰めても2割弱まで＝極端に小さくしない
            return t;
        }

        private TextMeshProUGUI MakeText(RectTransform parent, string text, float size, Color color)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Left;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            if (jpFont != null) t.font = jpFont;
            return t;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void ClearRows(List<FleetRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null) Discard(rows[i].go);
            rows.Clear();
            DestroyNotices(fleetContent);
        }

        private void ClearRows(List<TargetRow> rows)
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null) Discard(rows[i].go);
            rows.Clear();
            DestroyNotices(targetContent);
        }

        /// <summary>行以外に置いた注記も消す（残ると「艦隊がありません」が出っぱなしになる）。</summary>
        private void DestroyNotices(RectTransform content)
        {
            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (child != null && child.name == "Notice") Discard(child.gameObject);
            }
        }

        /// <summary>
        /// 行を捨てる。<see cref="Object.Destroy"/> はフレーム終わりまで残るので、<b>先に親から外す</b>
        /// ＝作り直した直後の1フレームだけ古い行と新しい行が二重に並ぶのを防ぐ。
        /// </summary>
        private void Discard(GameObject go)
        {
            if (go == null) return;
            go.transform.SetParent(null, false);
            Destroy(go);
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
