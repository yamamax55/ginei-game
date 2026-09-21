using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ（C-1 #34）の最小ビジュアライズ。C-1 の純ロジック
    /// （GalaxyMap / StrategicFleet / GalaxyPathfinder / StrategicFleetRegistry / StrategyRules）を
    /// 画面につなぐデモ：星系・回廊・艦隊マーカーを描画し、クリックで艦隊選択→星系クリックでワープ指示。
    /// 銀河時間を進め、到着で占領（色変化）、同一回廊の敵対遭遇を会戦トリガーとして表示。
    ///
    /// 操作：左クリック=艦隊選択（Shiftで追加）／星系クリック=選択艦隊をワープ／
    ///       Space=停止・再開／1・2・3=速度（0.5x/1x/2x）。
    /// 実機確認用。`Ginei/戦略マップ デモを開く` でデモシーンに配置 → 再生。
    /// </summary>
    public partial class GalaxyView : MonoBehaviour
    {
        [Header("見た目")]
        [Tooltip("星系（惑星）の円のサイズ。ラベル位置も追従する")]
        public float systemScale = 1.2f;
        public float fleetScale = 0.4f;
        public Color empireColor = new Color(0.85f, 0.3f, 0.25f);
        public Color allianceColor = new Color(0.3f, 0.5f, 0.9f);
        public Color corridorColor = new Color(0.5f, 0.55f, 0.7f, 0.9f);
        public Color chokeColor = new Color(0.9f, 0.8f, 0.3f, 0.95f);
        public Color frontlineColor = new Color(0.9f, 0.25f, 0.2f, 0.95f); // 前線（FTL不可）
        public Color fortressBlockadeColor = new Color(0.85f, 0.35f, 0.95f, 1f); // 要塞封鎖中の回廊（#40）
        public Color selectColor = new Color(1f, 0.95f, 0.4f);

        [Header("艦隊表示（重なり回避・軍団）")]
        [Tooltip("同一星系で艦隊アイコンを散らすときの、軍団内メンバ（or 同一サブクラスタ）の中心間距離（ワールド）。離れすぎないよう小さめ")]
        public float fleetClusterSpread = 0.95f;
        [Tooltip("同一星系での軍団どうし・無所属艦隊（サブクラスタ）の中心間距離（ワールド）。軍団のまとまりを分けるため広め")]
        public float fleetGroupSpread = 2.0f;
        [Tooltip("軍団隷下の艦隊を囲う四角の色（薄め＝盤面を邪魔しない）")]
        public Color corpsBoxColor = new Color(0.95f, 0.85f, 0.5f, 0.4f);
        [Tooltip("軍団隷下の四角と艦隊アイコンの余白（ワールド）")]
        public float corpsBoxPadding = 0.35f;
        [Tooltip("軍集団の外枠の色（軍団が集結したとき軍団枠の外側に描く）")]
        public Color armyBoxColor = new Color(0.7f, 0.85f, 1f, 0.5f);
        [Tooltip("軍団長乗艦マーカー（★）の色")]
        public Color corpsFlagshipColor = new Color(1f, 0.86f, 0.3f);

        [Header("時間")]
        public float galaxySpeed = 1f;

        [Header("二層遷移（戦略↔戦術・#586）")]
        [Tooltip("交戦中の回廊（赤点滅）の色")]
        public Color combatColor = new Color(1f, 0.35f, 0.15f, 1f);
        [Tooltip("交戦を放置したとき自動解決するまでの猶予（銀河時間・秒）。この間にダブルクリックで潜行できる")]
        public float autoResolveDelay = 2.5f;
        [Tooltip("ダブルクリック判定の猶予（実時間・秒）")]
        public float doubleClickWindow = 0.35f;
        [Tooltip("星系の点をクリックしたと見なす半径（ハブ星系で回廊より星系を優先＝惑星へ入れる）")]
        public float systemClickRadius = 0.65f;

        [Header("マップ操作（#2384 戦略マップUX）")]
        [Tooltip("既定のカメラズーム（orthographicSize）。F キーでこの値へリセット")]
        public float defaultZoom = 8f;
        [Tooltip("ホイール1ノッチあたりのズーム率（0.3=30%。回す量に比例して指数加速＝他ゲーム準拠で速め）")]
        [Range(0.05f, 0.6f)] public float zoomPerNotch = 0.3f;
        [Tooltip("ズーム追従の滑らかさ（小さいほどゆっくり滑らかに・大きいほど即時。unscaled 駆動）")]
        public float zoomLerpSpeed = 11f;
        [Tooltip("ズーム下限（近づける限界）。小さいほど寄れる：1.0 で星系ひとつが画面に収まるくらいまで寄れる")]
        public float minZoom = 0.3f;
        [Tooltip("ズーム上限（引きの限界）")]
        public float maxZoom = 16f;
        [Tooltip("左ドラッグをパンと見なすしきい値（ピクセル）。これ未満で離せばクリック（選択/ダブルクリック）")]
        public float dragThresholdPixels = 8f;
        [Tooltip("カメラ中心の移動可能範囲（±このワールド距離でクランプ＝迷子防止）")]
        public float panLimit = 22f;

        [Tooltip("全体表示（FitAll）で銀河の外周に足す余白（ワールド単位）。星名やリングが窓の縁で切れないための逃げ")]
        public float fitMargin = 2.2f;
        [Tooltip("敵対勢力を結ぶ回廊（前線）の長さの下限。配置を締めても開幕の接敵が早くなりすぎないようにする＝旧配置の前線距離（約12）に近い水準を保つ")]
        public float frontCorridorMinLength = 9f;

        [Header("星系の意匠（#戦略MAP刷新・発光核＋細いリング）")]
        [Tooltip("発光核の半径（星系ノード半径に対する割合）。小さいほど点として締まる")]
        [Range(0.05f, 0.6f)] public float coreRadiusFactor = 0.26f;
        [Tooltip("核を白へ寄せる量（0=陣営色のまま／1=白）。中心だけ明るく光って見せる")]
        [Range(0f, 1f)] public float coreWhiten = 0.55f;
        [Tooltip("光暈（にじみ）の半径。リングの外側へ淡く広がる")]
        [Range(0.5f, 3f)] public float glowRadiusFactor = 1.55f;
        [Tooltip("光暈の不透明度。濃紺の航宙図で沈まない程度に薄く")]
        [Range(0f, 0.5f)] public float glowAlpha = 0.13f;
        [Tooltip("リングの線の太さ（半径に対する割合）。細いほど精密な航宙図に見える")]
        [Range(0.02f, 0.3f)] public float ringThickness = 0.085f;
        [Tooltip("通商路の線幅。背景へ沈める最も細い層")]
        public float tradeCorridorWidth = 0.045f;
        [Tooltip("要衝回廊の線幅。金色でわずかに手前・太くして前線と隘路を際立たせる")]
        public float chokeCorridorWidth = 0.085f;

        [Header("要塞モデル（#要所の3D差し替え・描画のみ）")]
        [Tooltip("Resources 配下の要塞モデルのパス。見つからなければ従来表示のまま（素材未納品でも壊れない）")]
        public string fortressResourcePath = "Models/Fortress/SphericalFortress";
        [Tooltip("3Dメッシュへ差し替える要所の数（前線の要衝回廊の端点から選ぶ）。0で無効")]
        public int fortressCount = 2;
        [Tooltip("普通の星（リング直径）の何倍の外接直径で見せるか。外接にはアンテナ/砲塔も含むので、球本体は概ねこの7割")]
        public float fortressDiameterFactor = 2.4f;
        [Tooltip("自動照準が効かないときの向き（度）。通常は主砲の実位置から自動で決まる")]
        public Vector3 fortressEuler = new Vector3(16f, 208f, -6f);
        [Tooltip("主砲を向ける方向（ワールド）。-Z がカメラ側。既定は右上へ振った 3/4 の見え方")]
        public Vector3 fortressAimDirection = new Vector3(-0.42f, 0.30f, -1f);
        [Tooltip("スプライトより手前へ出す量（ワールド単位）。奥行きの競合を避ける")]
        public float fortressDepthOffset = 0.35f;
        [Tooltip("要塞の陰影を作るキーライトの向き（ワールド）。実ライトではなくシェーダー内の擬似光源＝2D Rendererでも効く")]
        public Vector3 fortressKeyLightDir = new Vector3(-0.45f, 0.72f, -0.53f);
        [Tooltip("要塞星系の所有リングを、要塞の実測直径の何倍にするか。1.0で外接と同じ＝アンテナの先が少し出る程度")]
        public float fortressRingScale = 1.06f;
        [Tooltip("要塞星系の星名を、リングの上端からさらに持ち上げる量（ワールド単位）")]
        public float fortressLabelLift = 0.30f;

        [Header("恒星モデル（#星系名ごとの3D恒星・描画のみ）")]
        [Tooltip("星系名ごとの恒星FBX（Models/Stars/Star_NN）へ差し替える。OFF で従来の発光核に戻せる")]
        public bool useStarModels = true;

        [Tooltip("停泊中の艦隊も戦略MAPに描く（#E 既定 off＝航行中だけ描いて混雑を解消。停泊艦は「軍団編成」メニューで見る）")]
        public bool showStationaryFleetsOnMap = false;
        [Tooltip("恒星本体の直径を、普通の星のリング直径の何倍にするか。所有リングの内側に収まる小ささに")]
        public float starDiameterFactor = 0.62f;
        [Tooltip("コロナ（光暈）の直径を恒星本体の何倍にするか。控えめにしてリングと混ざらないようにする")]
        public float starCoronaScale = 1.55f;
        [Tooltip("コロナの不透明度。恒星と同色で淡く出す（所有色はリングが担う）")]
        [Range(0f, 0.6f)] public float starCoronaAlpha = 0.20f;
        [Tooltip("スプライトより手前へ出す量（ワールド単位）")]
        public float starDepthOffset = 0.25f;
        [Tooltip("恒星モデルの向き（度）。球なので見た目はほぼ変わらないが、紅炎の出る位置が変わる")]
        public Vector3 starEuler = new Vector3(10f, 200f, 0f);

        [Header("星系名の可読性（#星系名が小さく密集する）")]
        [Tooltip("星系名がちょうど良い大きさに見える orthographicSize。ここを基準に、引くほどラベルを大きくして画面上の文字サイズを一定に保つ")]
        public float labelReferenceZoom = 6f;
        [Tooltip("星系名の重なり判定に使う見かけの幅（スクリーンピクセル）。近すぎる名前は後着を隠す")]
        public float starLabelScreenWidth = 108f;
        [Tooltip("同・高さ（スクリーンピクセル）")]
        public float starLabelScreenHeight = 26f;
        [Tooltip("星系名の実ピクセル高さの下限。密集時はここまで縮めてから重なりを間引く（#縦長で名前が全消え）")]
        public float starLabelMinScreenHeight = 15f;

        [Header("回廊要塞（#40 C-7）")]
        [Tooltip("要塞を制圧した艦隊が守備として要塞へ残す兵力の割合。0＝残さない＝すぐ落とし返される")]
        public float captureGarrisonShare = 0.2f;
        [Tooltip("制圧直後の要塞シールド健全度（0..1）。落とした直後は傷んでいる＝奪還されやすい")]
        public float capturedFortressShield = 0.5f;
        [Tooltip("ラベル拡大の上限倍率（引きすぎたときに文字が巨大化して盤面を埋めるのを防ぐ）")]
        public float labelMaxScale = 1.8f;
        [Tooltip("これより寄っていれば艦隊の兵力/ETA を出す。引くと消えて盤面が澄む（選択中と交戦中は常に表示）")]
        public float fleetDetailZoom = 7f;
        [Tooltip("これより寄っていれば「第N艦隊」を出す。兵力より先に消える（選択中と交戦中は常に表示）")]
        public float fleetNameZoom = 5f;

        [Header("艦隊マーカーの集約（#戦略MAPの艦艇表示）")]
        [Tooltip("この距離以内の同陣営・同じ航行状態の艦隊を1つのマーカーへまとめる（ワールド単位）")]
        public float clusterMergeRadius = 0.55f;
        [Tooltip("これより寄ったらまとまりを個別表示へ展開する。引いている間は集約表示のまま")]
        public float clusterExpandZoom = 4.0f;
        [Tooltip("集約マーカー（代表の駒）を何倍に見せるか。1隊でないことを形で示す")]
        public float clusterMarkerScale = 1.45f;
        [Tooltip("集約マーカーをクリックしたと見なす半径（ワールド単位）。低解像度でも押せる大きさに")]
        public float clusterClickRadius = 0.85f;
        [Tooltip("同一星系の艦隊を散らす輪の半径の上限（ワールド単位）。艦隊数が増えても銀河全域へ広がらない")]
        public float maxFleetSpreadRadius = 0.55f;
        [Tooltip("畳んだまとまりの中で選択中の艦隊を強調表示する位置（星系中心からの下方向オフセット・ワールド単位）")]
        public float selectedHighlightOffset = 0.45f;
        [Header("ナビ（キーパン）")]
        [Tooltip("キーボード（WASD/矢印）パンの速度")]
        public float keyPanSpeed = 26f;
        [Tooltip("パン（ドラッグ/端/キー）の追従の滑らかさ（小さいほど即時・大きいほどヌルッと慣性的）")]
        public float panSmoothTime = 0.10f;
        [Tooltip("背景星雲（galaxy_backdrop）の不透明度（0=出さない）。視野に追従して常に覆う")]
        [Range(0f, 1f)] public float backdropAlpha = 0.55f;
        [Tooltip("背景星雲の明るさ（0=黒〜1=原画。盤面の星系/回廊を読みやすくするため暗めに落とす）")]
        [Range(0f, 1f)] public float backdropBrightness = 0.4f;
        [Tooltip("背景星雲が視野からはみ出す余裕倍率（端の隙間防止）")]
        public float backdropCover = 1.15f;

        [Header("惑星攻城（#131）")]
        [Tooltip("S-AV戦力あたりの制空権抑制速度")]
        public float siegeSuppressRate = 0.05f;
        [Tooltip("ドメイン・ダウン後のS-AV戦力あたり侵略値蓄積速度")]
        public float siegeInvadeRate = 0.05f;
        [Tooltip("非交戦時の制空権再建速度")]
        public float siegeDefenseRegen = 0f;
        [Tooltip("デモ：帝国星系に置く惑星の制空権/侵略閾値")]
        public float demoPlanetDefense = 100f;
        public Color defenseColor = new Color(0.9f, 0.55f, 0.25f);
        public Color invadeColor = new Color(0.95f, 0.3f, 0.3f);

        private GalaxyMap map;
        private StrategicFleetRegistry reg;

        /// <summary>盤面の銀河グラフ（読み取り専用の窓口＝Editor の QA メニューや観測層が引く）。</summary>
        public GalaxyMap Map => map;
        /// <summary>盤面の艦隊レジストリ（同上・読み取り用）。</summary>
        public StrategicFleetRegistry Registry => reg;
        private Camera cam;
        private Sprite disc;
        private Material lineMat;
        private bool paused;
        private float occupyTimer;
        private float engagedElapsed;      // 交戦中が継続している時間（自動解決の猶予計測）
        private double currentAutoResolveSeconds; // TIME-4：現交戦の自動解決所要時間（AutoBattleSim 算出・game-seconds）
        // 接敵通知：通知済みの交戦回廊キー（min,max を合成）。交戦が解けたら外して再接敵で再通知。
        private readonly HashSet<long> notifiedEngagements = new HashSet<long>();
        private readonly HashSet<long> engagedKeyScratch = new HashSet<long>(); // 毎フレームの現交戦キー集計（使い回し）
        private readonly List<EncounterOutcome> resolvedOutcomes = new List<EncounterOutcome>(); // 自動解決の結末収集（使い回し）

        [Header("会戦結果ピン（控えめ）")]
        [Tooltip("会戦が起きた回廊に残す結果ピンの最大数（古いものから消す）")]
        public int maxBattlePins = 12;
        // 会戦記録ピン（控えめなX印）。1年で消える＋マウスオーバーであらましをポップアップ。
        private sealed class BattlePinRecord
        {
            public GameObject go;
            public Vector2 worldPos;
            public string summary;          // ホバー時のあらまし（複数行）
            public double bornGameSeconds;  // 生成時の game-秒（経過1年で消す）
        }
        private readonly List<BattlePinRecord> battlePins = new List<BattlePinRecord>();
        private GameObject battleTooltip;   // ホバー用ポップアップ（遅延生成）
        private TextMesh battleTooltipText;
        private float lastClickTime = -1f; // ダブルクリック判定用（実時間）
        private Vector2 lastClickWorld;
        private bool leftPressIsDouble;    // 今回の左押下がダブルクリックの2打目か（押下時に判定＝ダブルクリック＋ドラッグで矩形選択）
        private SpriteRenderer backdrop;    // 背景星雲（galaxy_backdrop・視野追従）
        private bool leftDragging;          // 左ドラッグ中（クリック判定は離した時＝誤選択防止。星系は動かさない）
        private Vector2 leftPressScreen;    // 左押下時のスクリーン座標（ドラッグ判定の起点）
        private bool midPanning;            // 中ボタンドラッグでスクロール中
        private bool rightClickConsumedThisFrame; // UIの「戻る」で処理済みなら盤面の艦隊メニューを開かない
        private bool leftPressOverUI;       // 左押下が UI 上で始まったか（その間マップを動かさない）
        private LineRenderer marqueeLine;   // 矩形選択の枠（左ドラッグ中のみ表示）
        [Tooltip("矩形選択（左ドラッグ）の枠の色")]
        public Color marqueeColor = new Color(0.4f, 1f, 0.55f, 0.9f);
        private bool midPressOverUI;        // 中押下が UI 上で始まったか
        private int pendingFitFrames;       // マップ窓の camera.rect 確定後に全体表示を詰め直す残りフレーム数
        private bool anchoredZoom;          // ホイールズーム中だけ true＝カーソル下のワールド点を固定する補正を掛ける
        private float zoomTarget;           // ホイールズームの目標 orthographicSize（滑らかに追従）
        private bool zoomInit;              // zoomTarget 初期化済みか
        private Vector2 zoomAnchorScreen;   // ズーム中心に保つスクリーン点（最後のホイール時のカーソル）
        private Vector3 panTarget;          // カメラ中心の目標位置（パン入力はここを動かし、cam は滑らかに追従）
        private Vector3 panVelocity;        // SmoothDamp 用の速度キャッシュ
        private bool panInit;               // panTarget 初期化済みか

        private readonly List<StrategicFleet> selectedFleets = new List<StrategicFleet>();
        private readonly Dictionary<int, SpriteRenderer> systemDots = new Dictionary<int, SpriteRenderer>();
        private readonly Dictionary<int, SpriteRenderer> systemGlows = new Dictionary<int, SpriteRenderer>(); // 光暈（所有色の淡いにじみ）
        private readonly Dictionary<int, SpriteRenderer> systemCores = new Dictionary<int, SpriteRenderer>(); // 発光核（中心の明るい点）
        private Sprite ringSprite;          // 星系の細いリング（#戦略MAP刷新）
        private readonly List<int> fortressSystems = new List<int>(); // 3D要塞へ差し替えた星系id
        private float fortressHitRadius;    // 要塞星系の当たり判定半径（見た目に合わせて広げる）
        private readonly Dictionary<int, Transform> systemNameLabelById = new Dictionary<int, Transform>(); // 星名ラベル（id引き）
        // FBX のマテリアル名→専用マテリアル（URP Lit で作り直す＝黒/マゼンタ化を防ぐ）。要塞は1〜2個なので使い回す。
        private readonly Dictionary<string, Material> fortressMaterials = new Dictionary<string, Material>();
        private readonly List<int> starSystems = new List<int>();   // 恒星3Dモデルへ差し替えた星系id
        // 艦隊マーカーの集約（#戦略MAPの艦艇表示）。毎フレーム作り直すので再利用バッファで GC を避ける。
        private readonly List<FleetMarkerInput> clusterInputs = new List<FleetMarkerInput>();
        private readonly List<FleetCluster> fleetClusters = new List<FleetCluster>();
        private readonly Dictionary<int, FleetCluster> clusterOfFleet = new Dictionary<int, FleetCluster>();
        // #40：要塞に駐留している艦隊id（集約から除いて二重計上を防ぐ／駒のラベルを「駐留」にする）。
        // 毎艦隊ごとに盤面を走査しないよう、集約の作り直しのときに1回だけ集める。
        private readonly HashSet<int> garrisonedFleetIds = new HashSet<int>();
        private FleetCluster openedCluster;  // 一覧を開いているまとまり（展開表示にする）
        private FleetMarkerBadgeLayer badgeLayer;                       // 集約バッジ（画面空間・描画＝当たり判定）
        private readonly List<FleetCluster> badgeClusters = new List<FleetCluster>(); // バッジを出す対象
        private readonly List<Rect> labelScreenRects = new List<Rect>();              // 星系名の重なり判定（画面px）
        private readonly List<Vector2> labelScreenPoints = new List<Vector2>();      // 画面内の星系名の位置（混み具合の測定用）
        private readonly List<int> labelVisibleIndex = new List<int>();              // 上と同じ並びの systemNameLabels 添字
        private readonly List<float> labelNeighborDist = new List<float>();          // 最近傍距離（中央値で縮小率を決める）
        private readonly List<LineRenderer> corridorLines = new List<LineRenderer>();
        private readonly Dictionary<StrategicFleet, SpriteRenderer> fleetMarks = new Dictionary<StrategicFleet, SpriteRenderer>();
        // 勢力別の艦隊スプライト（帝国/同盟）。未登録の勢力はマル（disc）のまま。Start で Resources から読み込む。
        private readonly Dictionary<Faction, Sprite> fleetSprites = new Dictionary<Faction, Sprite>();
        private readonly Dictionary<StrategicFleet, SpriteRenderer> fleetRings = new Dictionary<StrategicFleet, SpriteRenderer>();
        private readonly Dictionary<StrategicFleet, TextMesh> fleetEta = new Dictionary<StrategicFleet, TextMesh>();
        private readonly List<LineRenderer> routeLines = new List<LineRenderer>();
        // 軍団長乗艦マーカー（艦隊ごとの★ラベル・既定オフ）と、軍団隷下を囲う四角のプール、重なり回避オフセット（fleet id→offset）。
        private readonly Dictionary<StrategicFleet, TextMesh> fleetCorpsMarks = new Dictionary<StrategicFleet, TextMesh>();
        private readonly Dictionary<StrategicFleet, TextMesh> fleetNumLabels = new Dictionary<StrategicFleet, TextMesh>(); // 艦隊番号ラベル（艦隊の上）
        private readonly List<LineRenderer> corpsBoxLines = new List<LineRenderer>();
        private readonly List<TextMesh> corpsBoxLabels = new List<TextMesh>(); // 軍団名ラベル（枠の中・第N軍団）
        private readonly List<LineRenderer> armyBoxLines = new List<LineRenderer>();  // 軍集団の外枠（軍団が集結したとき）
        private readonly List<TextMesh> armyBoxLabels = new List<TextMesh>();        // 軍集団名ラベル（第N軍集団）
        private readonly Dictionary<int, Vector2> fleetClusterOffsets = new Dictionary<int, Vector2>();
        private TextMesh banner;
        private TextMesh helpLine;
        private TextMesh policyLine;                 // S5：プレイヤー勢力の税率/国庫/民心/安定度の読み取り表示
        private readonly Dictionary<int, TextMesh> siegeLabels = new Dictionary<int, TextMesh>();
        private readonly HashSet<int> besiegedSystems = new HashSet<int>(); // 攻城中の星系（開始/完了通知の状態遷移検出）

        // S5/S6（縦スライス）：税率レバー・財政・支持低下イベント
        [Header("内政スライス（S5/S6）")]
        [Tooltip("税率の1操作あたりの増減")]
        public float taxStep = 0.05f;
        [Tooltip("デバッグモード（` キーで切替）。税率レバー [ ] 等のデバッグ専用操作を有効化する。通常プレイでは税率は内政/AIに委ね手動レバーは出さない（タイクン化回避＝高位の決断＋創発的帰結）。")]
        public bool debugMode = false;
        [Tooltip("民心(希望)がこの値を下回ると不満イベントを提示")]
        public float hopeEventThreshold = 0.35f;
        private EventEngine policyEngine;
        private EventContext policyCtx;
        private const int PolicyDecisionIdBand = 93000;
        // TIME-6（#952）：暦の日境界でイベント判定を駆動するディスパッチャ（毎フレームでなく per-day＝倍速で暦比一定・ポーズで停止）。
        private CalendarDispatcher policyCalendar;

        // TIME-6（#952・LIFE-2 #152）：人物の加齢/老衰を暦の年境界で回すデモロスター。提督が老いて死に、HUDで告知する。
        private List<Person> commanders;
        private int campaignYear;

        // 士官学校（#155 LIFE-5）：勢力ごとの学校。暦の年境界で新任士官を輩出しロスターへ供給する。
        private List<Academy> academies;
        private int nextPersonId = 1;       // 卒業生のID採番（手置き提督の次から）
        private const int OfficerRosterCap = 80; // 士官名簿の上限（PERF＝無制限増加を防ぐ）

        // 大学（#156/#157 LIFE-6/7）：文官/技術者を輩出する文民版の学校。文民ロスターへ供給する。
        private List<University> universities;
        private List<Person> civilians;
        private const int CivilRosterCap = 80; // 文民名簿の上限（PERF）

        // 朝廷の権威（律令の形骸化・官僚制基盤）。封建の世＝既に低め（武家政権相当）＝官職は名誉職化方向。
        // 文官ネームドの考課・叙位（五位の壁）はこの権威で効く（BureaucracyCareerRules / RitsuryoFormalizationRules）。
        private CourtAuthority courtAuthority; // 実体は StrategySession.CourtAuthority（SetupPersonnel で共有＝Battle往復/セーブで永続）

        /// <summary>朝廷の権威（観測用・read-only 参照）。</summary>
        public CourtAuthority Court => courtAuthority;
        /// <summary>文民ネームドのロスター（観測用・人物名鑑が読む）。</summary>
        public IReadOnlyList<Person> CivilianRoster => civilians;
        /// <summary>武官ネームドのロスター（観測用・人物名鑑が読む）。</summary>
        public IReadOnlyList<Person> CommanderRoster => commanders;

        /// <summary>現在のキャンペーン年（観測用・人物動態オブザーバが年齢/死亡判定に使う）。</summary>
        public int CampaignYear => campaignYear;
        /// <summary>内政イベントエンジン（観測用・read-only。提示中/保留件数/発火回数を読む）。</summary>
        public EventEngine PolicyEngine => policyEngine;

        // 幼稚園/小学校/中学校/高校（#155-157 の土台）：勢力ごとの就学前〜中等教育。進学率＝候補の母数、質＝候補の素質を左右する（複利）。
        private List<Kindergarten> kindergartens;
        private List<ElementarySchool> elementarySchools;
        private List<HighSchool> highSchools;
        private List<MiddleSchool> middleSchools;
        // 保育園（#153/#110）：教育でなく保育＝労働参加↑・出生率↑（POP の出生/労働に効く）。
        private List<Nursery> nurseries;
        private List<TechnicalCollege> colleges; // 高専（中学校→高専の実務技術者路・#157）
        private List<JuniorCollege> juniorColleges; // 短大（高校卒後2年・行政中堅・#156）
        private List<VocationalSchool> vocationalSchools; // 専門学校（高校卒後2年・実務specialist・#157）

        // #884 造船 → #148 艦隊プール供給：星系ごとの造船所（全勢力＝AIも建艦）。暦の日次で建艦し、完成を所有勢力の FleetPool へ就役。
        // 生産力は内政（Province 安定度比例＝BUILD-2）に連動＝支配が不安定な系は建艦が遅い。損耗（戦略会戦の戦力喪失）でプール減。
        private List<Shipyard> shipyards;
        [Tooltip("各勢力の初期艦隊プール（FleetPool 未設定時にシード）")]
        public int initialFleetPool = 12000;
        [Tooltip("星系造船所の建艦速度（ポイント/戦略秒。生産力係数 BUILD-2 を掛ける）")]
        public float shipyardBuildPower = 1f;

        // TIME-7（#959）：暦の自動スロー（Paradox 風）。平時は暦を圧縮して速く流し、会戦の生起など「観るべき瞬間」は実時間へ減速。
        [Tooltip("平時に暦を実時間の何倍で流すか（自動スロー時は1倍＝実時間へ減速）。TIME-7 #959")]
        public float idleCalendarCompression = 30f;
        [Tooltip("暦流速の減速/再加速のなめらかさ（1秒あたりの倍率変化）")]
        public float calendarEaseRate = 8f;
        [Tooltip("PERF（死のスパイラル防止）：1フレームで進める実時間の上限(秒)。ヒッチ/alt-tab/GCストールで巨大化した deltaTime が暦圧縮(最大idleCalendarCompression倍)で増幅され、年/日境界が一気に大量発火→重い社会シミュが1フレームで数百回走り管理ヒープが数GBへ膨張するのを防ぐ。スケーラビリティ規律#2。")]
        public float maxFrameDt = 0.1f;
        private float calendarCompression = 1f; // 現在の暦流速倍率（1=実時間。起動時は実時間から立ち上げる）

        // 内政（#109・#759）：星系ごとの統治状態。デモは所有勢力の思想で安定度が動く。
        private readonly Dictionary<int, Province> provinces = new Dictionary<int, Province>();
        private readonly Dictionary<int, Faction> prevOwners = new Dictionary<int, Faction>();
        private readonly Dictionary<Faction, FactionData> demoFactions = new Dictionary<Faction, FactionData>();

        private void Start()
        {
            Active = this; // 観測層が国庫（資源備蓄）等のライブ状態を読むための弱参照（OnDestroy で解除）
            // 戦略マップシーンのコンテキストを設定（#107：会戦から戻った後も正しく絞られるよう再セット）
            GameInput.SetContext(InputContext.戦略);
            AudioManager.Instance.PlayBGM(AudioManager.Instance.bgmStrategy); // 戦略マップ BGM（②音楽）

            cam = Camera.main;
            if (cam == null) cam = new GameObject("GalaxyCamera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = defaultZoom;
            zoomTarget = defaultZoom; zoomInit = true; // ホイールズームの追従目標を初期化
            cam.transform.position = new Vector3(0f, 0f, -10f);
            panTarget = cam.transform.position; panVelocity = Vector3.zero; panInit = true; // パン追従目標を初期化
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.03f, 0.07f);

            disc = MakeDiscSprite(256); // 高解像度＋AA縁＝深ズームでも星系ドットが滑らかな円を保つ
            ringSprite = MakeRingSprite(256, ringThickness); // 星系の細いリング（#戦略MAP刷新）
            lineMat = new Material(Shader.Find("Sprites/Default"));
            LoadFleetSprites();

            SetupBackdrop(); // 背景星雲（galaxy_backdrop）を視野追従で敷く（#2384）

            // 接敵通知のアクション（seq→潜行）は破棄済み参照を残さないよう毎シーン初期化する。
            NotificationActionRegistry.Clear();
            notifiedEngagements.Clear();

            BuildDemoGalaxy();
            SetupGovernance();
            SetupEvents(); // S6：支持低下イベント（#116 エンジン）を用意
            DecisionDeck.Resolved -= OnPolicyDecisionResolved;
            DecisionDeck.Resolved += OnPolicyDecisionResolved;
            BuildVisuals();

            // 新規・セーブ読み込みのどちらでも、開幕は銀河全体が入った状態から始める（#戦略MAP刷新）。
            // カメラ矩形はマップ窓（StrategyMapWindow）が1フレーム目に確定させるため、次フレームで詰め直す。
            FitAll(instant: true);
            pendingFitFrames = 2;

            DrainBattleResults();
        }

        /// <summary>
        /// 会戦から戻った結果を戦略へ反映する（#WIN-3／#131／#38／#40）。
        /// ★<b>Start だけでなく毎フレーム</b>呼ぶ。ウィンドウ化会戦（既定 ON）では戦略シーンが
        /// ロードされたまま残るので Start は二度と走らず、Start 限定だと結果が永久に反映されない。
        /// </summary>
        private void DrainBattleResults()
        {
            // WIN-3：複数同時会戦の結果を1フレーム1件ずつ global へ復元して既存の反映処理へ流す
            //（各会戦は自分のスナップショットを BattleResultQueue へ積む＝global を奪い合わない）。
            if (!BattleHandoff.Pending && !BattleHandoff.Resolved && !BattleHandoff.siegeResolved
                && !BattleHandoff.fortressResolved
                && BattleResultQueue.Count > 0 && BattleResultQueue.TryPop(out BattleHandoff.State bres))
            {
                BattleHandoff.Restore(bres);
            }

            // 実会戦（Battleシーン）から戻ってきた結果を戦略へ反映。
            // さらに、潜行中に銀河の時計は止まらない＝観ていなかった他戦線は自動侵攻で決着（#586 ④⑤）。
            if (BattleHandoff.Resolved && BattleHandoff.Pending)
            {
                // 反映前に手動会戦の場所・勝敗を控える（敗者は撤退で進行元へ引き返すが念のため控える）。
                StrategicFleet ma = reg != null ? reg.GetFleet(BattleHandoff.fleetIdA) : null;
                StrategicFleet mb = reg != null ? reg.GetFleet(BattleHandoff.fleetIdB) : null;
                bool haveInfo = ma != null && mb != null;
                int mMin = 0, mMax = 0, mSurv = 0;
                Faction mWin = Faction.帝国, mLose = Faction.同盟;
                if (haveInfo)
                {
                    mMin = Mathf.Min(ma.currentSystemId, ma.destinationSystemId);
                    mMax = Mathf.Max(ma.currentSystemId, ma.destinationSystemId);
                    mWin = BattleHandoff.sideAWon ? ma.faction : mb.faction;
                    mLose = BattleHandoff.sideAWon ? mb.faction : ma.faction;
                    mSurv = BattleHandoff.survivorStrength;
                }

                // 敗軍は全滅でなければ進行元へ撤退（盤面に残す）し敗戦ペナルティを科す（#戦闘ドクトリン Stage4）。
                if (StrategyRules.ApplyHandoffResultWithRetreat(reg))
                {
                    if (haveInfo) AnnounceOutcome(new EncounterOutcome(mMin, mMax, mWin, mLose, mSurv), manual: true);

                    resolvedOutcomes.Clear();
                    int others = StrategyRules.ResolveEncounters(reg, resolvedOutcomes, CombatFactorOf);
                    for (int i = 0; i < resolvedOutcomes.Count; i++) AnnounceOutcome(resolvedOutcomes[i], manual: false);
                    if (others > 0)
                        NotificationCenter.Push(NotificationCategory.戦闘,
                            $"観ていない{others}戦線は自動解決しました");
                }
            }

            // 惑星攻城の戦術マップでの進捗を惑星へ書き戻す（#131）
            if (BattleHandoff.siegeResolved) ApplySiegeResult();

            // 回廊要塞の戦術マップでの結末を回廊へ書き戻す（#40）
            if (BattleHandoff.fortressResolved) ApplyFortressResult();

            // 会戦が終わって差し戻された援軍を盤面へ戻す（#38）＝派遣した艦隊を宙に浮かせない
            ApplyReinforcementReturns();
        }

        private void Update()
        {
            // 会戦から戻った結果を毎フレーム1件ずつ反映する（ウィンドウ化会戦は Strategy が生き続けるため）。
            DrainBattleResults();

            // ESC（#ウィンドウESC）：重ねたウィンドウを最前面から1枚ずつ閉じ、無くなったらシステムメニュー。
            // モーダル窓が盤面入力を塞ぐ前（下の early-return より前）に評価し、閉じた窓自身もここで処理する。
            if (GameInput.WasPressed(GameAction.キャンセル)) HandleStrategyEscape();

            // UI窓上の右クリックは万能撤回。盤面上は艦隊メニュー入口として HandleMouse へ渡す。
            rightClickConsumedThisFrame = HandleStrategyRightClickReturn();

            // イベント提示モーダル／艦隊編成画面／決裁／終了画面／システムメニュー 表示中は戦略マップの入力・進行を止める。
            // SystemDetailPanel は非モーダル窓化したので塞がない（開いたままマップ操作・進行が続く）。
            if (IsBoardModalOpen) return;

            // 盤面が未構築（Start 前・リロード途中）なら何もしない＝reg/map の null 参照を全面ガード。
            if (map == null || reg == null) return;

            HandleKeys();

            // TIME-1（#947）：統一クロックが速度/ポーズの権威。+/-（TimeDisplay）・1/2/3/Space（HandleKeys）が
            // クロックを駆動し、galaxySpeed/paused はそれをミラーする（日付HUD・時間連続性・自動解決の出所）。
            GameClock clock = StrategySession.Clock;
            if (clock != null) { galaxySpeed = (float)clock.speed; paused = clock.paused; }
            // PERF（死のスパイラル防止）：ヒッチ/alt-tab/GCストールで巨大化した Time.deltaTime を上限クランプ。
            // 暦圧縮(calendarCompression・最大 idleCalendarCompression 倍)で増幅されるため、生のまま使うと
            // 1フレームで年/日境界が大量発火→重い社会シミュが数百回走り管理ヒープが数GBへ膨張する（スケーラビリティ規律#2）。
            float frameDt = Mathf.Min(Time.deltaTime, maxFrameDt);
            float dt = paused ? 0f : frameDt * Mathf.Max(0f, galaxySpeed);

            // TIME-7（#959）：暦は自動スロー。平時は暦を圧縮して速く流し（年が分単位）、会戦の生起など「観るべき瞬間」は
            // 実時間へ減速する。実時間アクション（艦隊移動・自動解決・攻城）は dt のまま＝暦だけ伸縮する（観られる速さは不変）。
            var flow = new TimeFlowRules.TimeFlowParams(idleCalendarCompression, 1f, calendarEaseRate);
            calendarCompression = TimeFlowRules.Ease(
                calendarCompression, TimeFlowRules.TargetCompression(IsActionSalient(), flow), flow, frameDt);
            if (clock != null) clock.Advance(frameDt * calendarCompression);
            // #38：援軍の到着は<b>統一クロックの絶対時刻</b>へ同期する（差分加算しない）。
            // 絶対同期なので会戦側と二重に呼んでもずれず、倍速・ポーズでも到着 game-時刻が動かない。
            if (clock != null) StrategySession.Reinforcements?.SyncTo(clock.ElapsedSeconds);
            reg.Tick(dt);

            // 回廊要塞（#40）：要塞で封鎖された回廊上の敵対艦隊を固着（迂回不可）し、一定間隔で力攻めを自動解決する。
            TickFortressBlockades(dt);

            // 回廊で接触した敵対艦隊は「交戦中」として固着（旧：即・実会戦へ強制遷移＝廃止）。
            // プレイヤーはダブルクリックで潜行＝手動指揮へ。放置すれば猶予後に自動解決（#586 ①④）。
            int newEngagements = StrategyRules.BeginEngagements(reg);
            newEngagements += StrategyRules.BeginSystemEngagements(reg); // 惑星上など同一星系での接敵も交戦に（fleet-vs-fleet）
            NotifyNewEngagements(); // 接敵を通知（ダブルクリックで潜行可能なアクション付き）

            // 新たに接敵が生じたら自動解決の観測ウィンドウをリセットする（共有タイマーの積み残し対策）。
            // engagedElapsed/currentAutoResolveSeconds は全交戦で共有のため、別戦線の交戦が既にタイマーを
            // 消化していると、後から起きた接敵（とくに惑星上＝同一星系の接敵）が次の解決Tickで即・自動解決され
            // 「一瞬で会戦が終わる」＝プレイヤーが潜行できない不具合になる。新規接敵のたびに窓を取り直して
            // 必ず観測/介入の猶予（再計算した所要時間ぶん）を確保する。
            if (newEngagements > 0)
            {
                engagedElapsed = 0f;
                currentAutoResolveSeconds = 0.0; // 次の AnyEngaged 分岐で ComputeEngagementDuration を取り直す
            }

            if (AnyEngaged())
            {
                // TIME-4（#950）：自動解決の所要時間を AutoBattleSim（裏の簡易戦術シミュ）で算出＝
                // 観戦会戦と同じ game-time を消費する（固定 autoResolveDelay でなく交戦戦力から決まる）。
                if (currentAutoResolveSeconds <= 0.0) currentAutoResolveSeconds = ComputeEngagementDuration();
                engagedElapsed += dt;
                if (engagedElapsed >= currentAutoResolveSeconds)
                {
                    resolvedOutcomes.Clear();
                    StrategyRules.ResolveEncounters(reg, resolvedOutcomes, CombatFactorOf); // 放置の自動解決（補給×技術の実効戦力で勝敗）。プールは減らさない
                    StrategyRules.ResolveSystemEncounters(reg, resolvedOutcomes, CombatFactorOf); // 星系上（惑星上）の接敵も自動解決
                    for (int i = 0; i < resolvedOutcomes.Count; i++) AnnounceOutcome(resolvedOutcomes[i], manual: false);
                    engagedElapsed = 0f;
                    currentAutoResolveSeconds = 0.0;
                }
            }
            else { engagedElapsed = 0f; currentAutoResolveSeconds = 0.0; }

            // 防衛惑星の攻城（停泊した敵対艦隊が S-AV で制空権制圧→侵略→占領）。銀河時間で進む。
            // 攻城開始/占領完了の通知＋占領後のラベル残存を防ぐため、TickSieges を状態遷移検出でラップする。
            RunPlanetSiegeTick(dt);

            occupyTimer += dt;
            if (occupyTimer >= 0.4f)
            {
                NotifyStationingCaptures(); // 無防備星系の停泊占領を検出して通知（占領完了）
                occupyTimer = 0f;
                // 盤面の所有が動いたら即・制覇判定（占領も攻城#131 の捕獲も拾う＝全星系支配で年境界を待たず勝利イベント）。
                RunCampaignVictoryCheck();
            }

            // 内政（#109）：所有変化で不安定化→時間で統合・安定。情報パネル(#759)が読む。
            TickGovernance(dt);

            // 国家状態（#817 旗幟の出所）：各勢力の腐敗→合意→希望を銀河時間で進める。
            CampaignRules.Tick(StrategySession.Campaign, dt);
            // 財政（S5）＋支持低下イベント（S6）は日境界、人物の加齢/老衰（LIFE-2）は年境界で進める（TIME-6 #952）。
            // いずれも暦駆動＝倍速で暦比一定・ポーズで停止。日次→年次の順に独立発火（CalendarDispatcher）。
            if (clock != null && policyCalendar != null)
                policyCalendar.Advance(clock.ElapsedSeconds, onDay: RunDailyCampaignTick, onMonth: RunMonthlyCampaignTick, onYear: RunAnnualLifecycleTick);


            HandleMouse();
            UpdateBattlePins(); // 会戦記録ピンの寿命（1年で消える）＋ホバーであらまし表示
            Refresh();
        }

        // ===== 内政（#109）＋星系情報パネル（#759） =====

        // --- オンボーディング（目標提示＋初手ガイド） ---
        private static bool objectiveAnnounced;

        /// <summary>現在の難易度（GameSettings）に応じた勝敗しきい値。盤面/勝敗/目標表示の単一窓口。</summary>
        private static CampaignVictoryRules.CampaignVictoryParams ActiveVictoryParams()
            => CampaignDifficultyRules.VictoryParams(
                GameSettings.Instance != null ? GameSettings.Instance.campaignDifficulty : CampaignDifficulty.普通);

        /// <summary>キャンペーン開始時に勝利目標と最初の操作を通知で提示する（セッション一度きり）。勝敗は <see cref="CampaignVictoryRules"/>。</summary>
        private void AnnounceCampaignObjective()
        {
            if (objectiveAnnounced) return;
            objectiveAnnounced = true;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.注意,
                $"【目標】{player} で銀河の全星系（惑星）を占領せよ＝制覇勝利（全星系を失えば敗北）");
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報,
                "操作：星系を右クリックで進軍 → 前線で接触 → 交戦中の回廊をダブルクリックで潜行（会戦へ）。Space/1-3=速度、H=ヘルプ。");
        }

        /// <summary>プレイヤー勢力の国家状態（無ければ null）。</summary>
        private FactionState PlayerState()
        {
            var campaign = StrategySession.Campaign;
            if (campaign == null) return null;
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            return CampaignRules.GetState(campaign, pf);
        }

        /// <summary>支持低下イベント（#116 エンジン経由）を用意する。Start で SetupGovernance の後に呼ぶ。</summary>
        private void SetupEvents()
        {
            policyEngine = new EventEngine();
            policyCtx = new EventContext(
                GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国);

            var unrest = new GameEventDef
            {
                id = "民衆の不満",
                title = "民衆の不満",
                body = "重税に民が苦しんでいる。民心が離れ始めた——どう応える？",
                repeatable = true,
                cooldown = 180f, // TIME-6：game-seconds 基準（=3 game-day・1日60s）。暦時間でのクールダウン。
            };
            // 条件：プレイヤー勢力の民心(希望)がしきい値を下回る
            unrest.condition = ctx =>
            {
                FactionState s = PlayerState();
                return s != null && s.community != null && s.community.hope < hopeEventThreshold;
            };
            // 選択肢＝政治的帰結（盤面の状態を直接動かす）
            unrest.AddChoice("減税して民を宥める（税率↓・民心↑）", ctx =>
            {
                FactionState s = PlayerState();
                if (s == null) return;
                s.taxRate = Mathf.Clamp01(s.taxRate - 0.15f);
                if (s.community != null) s.community.hope = Mathf.Clamp01(s.community.hope + 0.12f);
            });
            unrest.AddChoice("強硬に抑え込む（抑圧↑・短期しのぎ）", ctx =>
            {
                FactionState s = PlayerState();
                if (s == null || s.community == null) return;
                s.community.repression = Mathf.Clamp01(s.community.repression + 0.2f);
                s.community.hope = Mathf.Clamp01(s.community.hope + 0.05f); // 力で一時的に持ち直す
            });
            policyEngine.Register(unrest);

            // TIME-6（#952）：暦の日境界でイベント判定を回す。現在のクロック経過へ同期（初フレームで日跨ぎを一気に発火させない）。
            double startElapsed = StrategySession.Clock != null ? StrategySession.Clock.ElapsedSeconds : 0d;
            policyCalendar = new CalendarDispatcher(GameDate.DateParams.Default, startElapsed);

            SetupPersonnel();
            SetupShipyard();
            SeedDemoMilitary(); // 艦艇/軍事観測層を満たす初期軍備（艦隊台帳・編制ツリー・指揮班）を勢力ごとにシード
            SeedGovernment();   // 政府観測層を満たす初期政府（要職＝司令長官・省庁＝二官八省と配属）を勢力ごとにシード（#158）
        }

        /// <summary>
        /// 暦の日境界ごとに走る盤面の日次処理（TIME-6 #952）：財政を1日ぶん進め（S5）、続いて支持低下イベント判定（S6）。
        /// 連続ドリフト系（艦隊移動・内政・社会連鎖 CampaignRules.Tick）は従来どおり dt で回る（後方互換・段階移行）。
        /// </summary>
        private void RunDailyCampaignTick()
        {
            float secondsPerDay = (float)policyCalendar.Params.secondsPerDay;
            CampaignRules.TickEconomyDay(StrategySession.Campaign, secondsPerDay); // 歳入＝税収を国庫へ
            CampaignRules.TickBudgetDay(StrategySession.Campaign, secondsPerDay); // 歳出＝予算総額を国庫から（国家予算の基盤）
            TickShipyard(secondsPerDay); // 建艦を1日進め、完成を勢力プールへ（#884→#148）
            RunDailyPolicyTick();
            // MEYASU-6：建白候補の評価は毎フレームでなく暦の日境界に相乗り。
            Object.FindAnyObjectByType<RingiDirector>()?.RunCalendarDayTick();
            RunMilitarySupplyTick(); // 軍要求物資（#2049）：補給切れの前線艦隊が干上がる
            RunDailyStockTick();     // 株価は日次で動く（#株価日次）：収益/配当は月次のまま価格だけ細かく収束
        }

        private int monthCounter; // 月次の通し番号（四半期/年次の月割り分散に使う・Tick改善P1/P2）

        /// <summary>
        /// 月次の経済/社会Tick（Tick改善 P1-P4）：市場（毎月）に加え、外交を四半期（P2 応答性）、重い生産連鎖を
        /// 月割り stagger（P1 年次スパイク分散・各年1回）、人物財産の時価評価を毎月（P3 市場整合）で回す。
        /// </summary>
        private void RunMonthlyCampaignTick()
        {
            RunMarketTick();                       // 毎月：市場/企業/株式/交易
            RunFinancialMarkToMarket();            // P3：保有金融資産を毎月 時価評価（月次市場に追従）
            RunMonthlyPersonFinanceTick();         // 人物の俸給は月払い（#2056）：月俸→消費→特性配分→財産

            int m = monthCounter % 12;
            if (monthCounter % 3 == 0) RunDiplomacyTick();   // P2：外交は四半期（宣戦/講和/賠償/制裁/諜報）
            if (m == 6)  RunBomConsumerTick();               // P1：消費財BOM（食品/衣類/医薬/住宅）を年1回・dt不変。住宅は森林チェーン#2091 廃止で本Tickに統合
            // SCM計画（MRP・#2105）は簡略化で凍結＝撤去。供給配分は RunBomConsumerTick 内の不足平滑化に集約
            monthCounter++;
        }

        /// <summary>
        /// 決定論 roll（2種子のハッシュ→[0,1)）。Tick の乱数依存（UnityEngine.Random）を排し、
        /// 同じセーブから同じ歴史が再生されるよう担保する（規律＝決定論）。年×id 等を種子に使う。
        /// </summary>
        internal static float DetRoll(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 2654435761u + (uint)b * 40503u + 0x9E3779B9u;
                h ^= h >> 13; h *= 0x85EBCA6Bu; h ^= h >> 16;
                return (h & 0xFFFFFFu) / (float)0x1000000;
            }
        }

        private int rollSeq; // 決定論 roll 列の進行カウンタ（年内の連続呼び出しに一意な種子を与える）
        /// <summary>次の決定論 roll 種子（連番）。乱数 roll を置き換える `_ => DetRoll(campaignYear, NextRollSeed())` 用。</summary>
        private int NextRollSeed() => unchecked(rollSeq++);

        // 軍の補給を1日ぶん（MILSUP-6・#2049 配線）：補給源（自勢力領）から切れた前線艦隊は補給が枯れて損耗する。
        // 現在/出発星系が自勢力領なら補給線が通る＝補給。敵に後背を取られる/前線で孤立すると干上がる（兵糧攻め）。
        private void RunMilitarySupplyTick()
        {
            if (reg == null || reg.fleets == null || map == null) return;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.strength <= 0) continue;
                StarSystem sys = map.GetSystem(f.currentSystemId);
                bool supplied = sys != null && sys.owner == f.faction; // 後背が自勢力領＝補給線が通る
                int lost = MilitarySupplyTickRules.TickFleet(f, supplied);
                if (lost > 0)
                    NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                        $"{f.faction} 第{f.id}艦隊 補給途絶で損耗（-{lost}・補給{Mathf.RoundToInt(f.supply * 100f)}%）");
            }
        }

        /// <summary>
        /// 「観るべき瞬間」か（TIME-7 #959 自動スロー）：会戦の生起・前線への亜光速侵入など。true の間は暦を実時間へ減速し、
        /// 早送りで会戦や接触を見逃さない・暦が一気に飛ばないようにする。実時間アクションの速さ自体は変えない。
        /// </summary>
        private bool IsActionSalient()
        {
            if (AnyEngaged()) return true; // 会戦が起きている＝観て介入できるよう減速
            if (reg != null && reg.fleets != null)
            {
                for (int i = 0; i < reg.fleets.Count; i++)
                {
                    StrategicFleet f = reg.fleets[i];
                    if (f != null && f.IsSublight) return true; // 前線へ亜光速侵入中＝接触直前の緊張
                }
            }
            return false;
        }

        /// <summary>
        /// 暦の日境界ごとに支持低下イベントの条件を判定し、発火したら決裁デスクへ積む（S6・TIME-6 #952、DESK-6 #1634）。
        /// EventEngine の cooldown 判定は <b>game-time（クロック経過秒）</b>を渡す＝倍速で暦比一定・ポーズで停止。
        /// </summary>
        private void RunDailyPolicyTick()
        {
            if (policyEngine == null || StrategyEventPanel.IsOpen || HasPendingPolicyDecision()) return;
            float nowGameSeconds = StrategySession.Clock != null ? (float)StrategySession.Clock.ElapsedSeconds : 0f;
            GameEventDef fired = policyEngine.Tick(policyCtx, nowGameSeconds, 0.5f);
            if (fired != null) ShowPolicyEvent(fired);
        }

        /// <summary>保存復元後も同じイベント案件を再起案しない。</summary>
        private static bool HasPendingPolicyDecision()
        {
            DecisionQueue queue = DecisionDeck.Queue;
            if (queue?.items == null) return false;
            for (int i = 0; i < queue.items.Count; i++)
            {
                PendingDecision d = queue.items[i];
                if (d != null && !DecisionResolutionRules.IsSettled(d)
                    && EventDecisionRules.TryDecode(d.effectKey, out _)) return true;
            }
            return false;
        }

        /// <summary>発火したイベントを非ブロッキングの決裁デスクへ積む（DESK-6 #1634）。</summary>
        private void ShowPolicyEvent(GameEventDef def)
        {
            if (def == null) return;
            PendingDecision d = EventDecisionRules.Create(def,
                DecisionDeck.NextDecisionId(PolicyDecisionIdBand), def.severity,
                def.choices != null && def.choices.Count > 0 ? def.choices.Count - 1 : 0);
            if (d == null) return;
            DecisionDeck.Enqueue(d);
            if (d.severity != DecisionSeverity.重大) return;

            var choices = new System.Collections.Generic.List<(string, System.Action)>();
            for (int i = 0; i < d.choices.Count; i++)
            {
                int idx = i;
                choices.Add((d.choices[i], () => DecisionDeck.Resolve(d.id, idx)));
            }
            StrategyEventPanel.ShowCritical(d.title, d.body, choices);
        }

        /// <summary>手動裁可・期限切れのどちらも同じ入口からEventEngineの選択肢を一度だけ適用する。</summary>
        private void OnPolicyDecisionResolved(PendingDecision d, int choiceIndex)
        {
            if (d == null || policyEngine == null || !EventDecisionRules.TryDecode(d.effectKey, out string eventId)) return;
            if (!DecisionResolutionRules.ClaimForApply(d)) return;
            bool applied = policyEngine.ResolveById(eventId, choiceIndex, policyCtx);
            DecisionResolutionRules.RecordResult(d, applied
                ? new PetitionActionResult(PetitionActionOutcome.実行, $"イベント「{d.title}」の選択を実行しました")
                : PetitionActionResult.Fail(PetitionActionOutcome.対象外, $"イベント定義 {eventId} を復元できません"));
        }

        /// <summary>背景星雲をカメラ視野に追従させ常に覆う（ズーム/パンに連動）。</summary>
        private void LateUpdate()
        {
            // マップ窓が camera.rect を確定させた後のアスペクトで全体表示を詰め直す（起動直後の1〜2フレーム）。
            if (pendingFitFrames > 0)
            {
                pendingFitFrames--;
                FitAll(instant: true);
            }

            SmoothPan(); // パン目標へカメラを滑らかに追従（ドラッグ/端/キー共通の慣性的な動き）
            UpdateLabelLegibility(); // 星系名を画面上で一定サイズに保ち、引きすぎたら隠す

            if (backdrop == null || cam == null) return;
            Vector3 cp = cam.transform.position;
            backdrop.transform.position = new Vector3(cp.x, cp.y, 0f);
            float worldH = cam.orthographicSize * 2f;
            float worldW = worldH * Mathf.Max(0.01f, cam.aspect);
            Vector3 sprSize = backdrop.sprite.bounds.size; // ワールド単位（pixelsPerUnit=100）
            float sx = sprSize.x > 0f ? (worldW / sprSize.x) * backdropCover : 1f;
            float sy = sprSize.y > 0f ? (worldH / sprSize.y) * backdropCover : 1f;
            backdrop.transform.localScale = new Vector3(sx, sy, 1f);
        }

        /// <summary>
        /// 新たに接敵（交戦開始）した回廊を通知する。<b>プレイヤー勢力が関与する</b>交戦だけを対象にし、
        /// 通知にはダブルクリックでその会戦へ潜行するアクションを紐づける（<see cref="NotificationActionRegistry"/>）。
        /// 交戦が解けた回廊は通知済みから外し、再接敵で再通知する。
        /// </summary>
        private void NotifyNewEngagements()
        {
            if (reg == null || reg.fleets == null) return;
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;

            engagedKeyScratch.Clear();
            // まず現在交戦中で「プレイヤーが関与する」回廊キーを集める
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || !f.engaged || f.faction != player) continue;
                engagedKeyScratch.Add(CorridorKey(f.currentSystemId, f.destinationSystemId));
            }

            // 新規キーを通知（既知のものは飛ばす）
            foreach (long key in engagedKeyScratch)
            {
                if (notifiedEngagements.Contains(key)) continue;
                notifiedEngagements.Add(key);
                DecodeCorridorKey(key, out int sysA, out int sysB);

                // 星系上（惑星上）の接敵＝停泊艦隊どうし（current==dest ＝ sysA==sysB）。会戦へ潜行できる。
                if (sysA == sysB)
                {
                    string slabel = $"敵艦隊と接敵：{SystemName(sysA)}（ダブルクリックで会戦へ）";
                    long sseq = NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.警告, slabel);
                    int sid = sysA; // クロージャ用にキャプチャ
                    NotificationActionRegistry.Register(sseq, () => DescendSystemBattleBySystem(sid));
                    continue;
                }

                double dur = EstimateAutoResolveSeconds(sysA, sysB);
                string when = dur > 0.0 ? $"約{Mathf.CeilToInt((float)dur)}秒で自動解決／" : "";
                string label = $"敵艦隊と接敵：{SystemName(sysA)}〜{SystemName(sysB)}（{when}ダブルクリックで潜行）";
                long seq = NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.警告, label);
                int a = sysA, b = sysB; // クロージャ用にキャプチャ
                NotificationActionRegistry.Register(seq, () => DescendCorridorBySystems(a, b));
            }

            // 交戦が解けた回廊は通知済みから除外（次に接敵したらまた通知する）
            notifiedEngagements.RemoveWhere(k => !engagedKeyScratch.Contains(k));
        }

        /// <summary>無向の回廊キー（小さいID×大きな桁＋大きいID）。星系IDは小さいので衝突しない。</summary>
        private static long CorridorKey(int a, int b)
        {
            int min = Mathf.Min(a, b), max = Mathf.Max(a, b);
            return (long)min * 100000L + max;
        }

        private static void DecodeCorridorKey(long key, out int a, out int b)
        {
            a = (int)(key / 100000L);
            b = (int)(key % 100000L);
        }

        /// <summary>星系名（無ければID）を返す。通知文言用。</summary>
        private string SystemName(int id)
        {
            StarSystem s = map != null ? map.GetSystem(id) : null;
            return (s != null && !string.IsNullOrEmpty(s.systemName)) ? s.systemName : $"星系{id}";
        }

        /// <summary>勢力の表示名（戦略艦隊は legacy enum）。</summary>
        private static string FactionLabel(Faction f) => f.ToString();

        /// <summary>指定回廊の交戦ペアから、放置時の自動解決所要 game-秒を見積もる（AutoBattleSim）。無ければ0。</summary>
        private double EstimateAutoResolveSeconds(int sysA, int sysB)
        {
            if (!StrategyRules.TryGetEngagementOnCorridor(reg, sysA, sysB, out var a, out var b)) return 0.0;
            var r = AutoBattleSim.Resolve(a.strength, b.strength);
            return r.durationSeconds > 0.0 ? r.durationSeconds : autoResolveDelay;
        }

        /// <summary>会戦の決着を通知し、発生回廊に控えめな結果ピンを残す（#接敵通知）。</summary>
        private void AnnounceOutcome(EncounterOutcome o, bool manual)
        {
            // #38：決着した戦場を閉じ、まだ着いていない援軍を差し戻す。
            // 潜行して戻った会戦は BattleManager が閉じるが、**放置して自動解決した会戦もここで必ず閉じる**
            // （閉じ忘れると派遣した艦隊が「増援航行中」のまま宙に浮く）。
            CloseBattlefieldAndReturn(BattlefieldKey.Corridor(o.sysMin, o.sysMax));

            string place = $"{SystemName(o.sysMin)}〜{SystemName(o.sysMax)}";
            string mode = manual ? "・潜行" : "・自動解決";
            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                $"会戦決着：{place}　{FactionLabel(o.winner)}が{FactionLabel(o.loser)}を撃破（残存{o.survivorStrength}）{mode}");

            // ホバー用のあらまし（複数行）
            string summary =
                $"【会戦記録】{place}{mode}\n" +
                $"勝者：{FactionLabel(o.winner)}（残存 {o.survivorStrength}）\n" +
                $"敗者：{FactionLabel(o.loser)}（壊滅）";
            AddBattlePin(o.sysMin, o.sysMax, o.winner, summary);
        }

        /// <summary>
        /// 会戦が起きた回廊の中点に「控えめな」結果ピンを残す（勝者色のごく薄いドット＋小さな ×）。
        /// 星系/艦隊より背面・低アルファで目立たせない。上限 <see cref="maxBattlePins"/> で古いものから消す。
        /// 経過1年で消える＋マウスオーバーで <paramref name="summary"/> をポップアップ表示する。
        /// </summary>
        private void AddBattlePin(int sysMin, int sysMax, Faction winner, string summary)
        {
            StarSystem a = map != null ? map.GetSystem(sysMin) : null;
            StarSystem b = map != null ? map.GetSystem(sysMax) : null;
            if (a == null || b == null) return;

            Vector2 mid = (a.position + b.position) * 0.5f;
            var go = new GameObject($"BattlePin_{sysMin}_{sysMax}");
            go.transform.SetParent(transform, false);
            go.transform.position = (Vector3)mid;
            go.transform.localScale = Vector3.one * (systemScale * 0.5f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = disc;
            Color wc = FactionColor(winner);
            sr.color = new Color(wc.r, wc.g, wc.b, 0.18f); // ごく薄い勝者色
            sr.sortingOrder = 1;                            // 回廊(0)より前・星系(2)/艦隊(4)より背面

            // 小さな × の印（くすんだ金・半透明・背面）。会戦跡を示すが目立たせない。
            var lblGo = MakeLabel(go.transform, "×", Vector3.zero, 0.5f);
            var ltm = lblGo.GetComponent<TextMesh>();
            ltm.color = new Color(0.82f, 0.78f, 0.6f, 0.5f);
            var lmr = lblGo.GetComponent<MeshRenderer>();
            if (lmr != null) lmr.sortingOrder = 1;

            double now = StrategySession.Clock != null ? StrategySession.Clock.ElapsedSeconds : 0d;
            battlePins.Add(new BattlePinRecord { go = go, worldPos = mid, summary = summary, bornGameSeconds = now });
            while (battlePins.Count > Mathf.Max(1, maxBattlePins))
            {
                BattlePinRecord old = battlePins[0];
                battlePins.RemoveAt(0);
                if (old != null && old.go != null) Destroy(old.go);
            }
        }

        /// <summary>暦の1年あたりの game-秒（既定 60秒/日 ×30日 ×12月＝21600）。</summary>
        private static double OneYearSeconds()
        {
            var p = GameDate.DateParams.Default;
            return p.secondsPerDay * p.daysPerMonth * p.monthsPerYear;
        }

        /// <summary>会戦記録ピンを更新：経過1年で消す＋マウスオーバーであらましをポップアップ。</summary>
        private void UpdateBattlePins()
        {
            // 1年で消える（game-時間基準＝ポーズ中は進まない）
            double now = StrategySession.Clock != null ? StrategySession.Clock.ElapsedSeconds : 0d;
            double oneYear = OneYearSeconds();
            for (int i = battlePins.Count - 1; i >= 0; i--)
            {
                BattlePinRecord p = battlePins[i];
                if (p == null) { battlePins.RemoveAt(i); continue; }
                if (oneYear > 0d && now - p.bornGameSeconds >= oneYear)
                {
                    if (p.go != null) Destroy(p.go);
                    battlePins.RemoveAt(i);
                }
            }
            UpdateBattlePinTooltip();
        }

        /// <summary>
        /// マウス直下の会戦記録ピンを探し、あればあらましのポップアップを出す（無ければ隠す）。
        /// ズーム非依存：当たり判定は<b>画面ピクセル</b>で行い（×印を覆うピクセル半径＋下限）、
        /// ポップアップは orthographicSize に追従させて<b>画面上で一定サイズ</b>・×印のすぐ近くに出す（HoI4風）。
        /// </summary>
        private void UpdateBattlePinTooltip()
        {
            if (cam == null || Mouse.current == null || battlePins.Count == 0 || PointerOverUI())
            {
                if (battleTooltip != null) battleTooltip.SetActive(false);
                return;
            }

            // 画面ピクセル基準の当たり判定（ズームに依らず掴みやすい＝×印の画面サイズを覆い、下限18px）。
            float pixelsPerWorld = Screen.height / (2f * Mathf.Max(0.01f, cam.orthographicSize));
            float markScreenR = (systemScale * 0.6f) * pixelsPerWorld;
            float hoverPixels = Mathf.Max(18f, markScreenR);

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            BattlePinRecord hit = null;
            float best = float.MaxValue;
            for (int i = 0; i < battlePins.Count; i++)
            {
                BattlePinRecord p = battlePins[i];
                if (p == null || p.go == null) continue;
                Vector3 sp = cam.WorldToScreenPoint((Vector3)p.worldPos);
                float d = Vector2.Distance(mouseScreen, new Vector2(sp.x, sp.y));
                if (d <= hoverPixels && d < best) { best = d; hit = p; }
            }

            if (hit == null)
            {
                if (battleTooltip != null) battleTooltip.SetActive(false);
                return;
            }

            EnsureBattleTooltip();
            // 文字は他のマップラベル（星系名）と同じく world-fixed＝ズームインで大きく読みやすくなる。
            // 位置は ×印の右上へ「ズームに比例した小オフセット」で、どのズームでも印のすぐ近くに出す。
            float worldPerPixel = (2f * cam.orthographicSize) / Mathf.Max(1f, Screen.height);
            Vector3 off = new Vector3(14f, 14f, 0f) * worldPerPixel;
            battleTooltip.transform.position = (Vector3)(hit.worldPos) + off;
            battleTooltipText.text = hit.summary;
            battleTooltip.SetActive(true);
        }

        /// <summary>ホバーポップアップ（世界座標の TextMesh）を遅延生成する。文字は world-fixed＝ズームインで大きく読める。</summary>
        private void EnsureBattleTooltip()
        {
            if (battleTooltip != null) return;
            var go = MakeLabel(transform, "", Vector3.zero, 0.6f); // 星系名(0.9)よりやや小さい読める大きさ
            go.name = "BattlePinTooltip";
            go.transform.localScale = Vector3.one;               // world-fixed（毎フレーム拡縮しない）
            battleTooltipText = go.GetComponent<TextMesh>();
            battleTooltipText.anchor = TextAnchor.LowerLeft;     // ×印の右上へ伸びる
            battleTooltipText.alignment = TextAlignment.Left;
            battleTooltipText.color = new Color(1f, 0.95f, 0.8f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 12;                 // ピン/星系/艦隊より前面
            battleTooltip = go;
        }

        /// <summary>交戦中（engaged）の艦隊が1隻でも居るか。</summary>
        private bool AnyEngaged()
        {
            if (reg == null || reg.fleets == null) return false; // リロード途中など未初期化時の保険
            foreach (var f in reg.fleets) if (f != null && f.engaged) return true;
            return false;
        }

        /// <summary>
        /// 交戦中の最初の敵対ペアから自動解決の所要時間を <see cref="AutoBattleSim"/>（裏の簡易戦術シミュ）で見積もる
        /// （TIME-4 #950・時間統一）。ペアが取れなければ従来の固定値にフォールバック。返り値は game-seconds。
        /// </summary>
        private double ComputeEngagementDuration()
        {
            StrategicFleet a = null;
            foreach (var f in reg.fleets)
            {
                if (f == null || !f.engaged) continue;
                if (a == null) { a = f; continue; }
                if (FactionRelations.IsHostile(null, a.faction, null, f.faction))
                {
                    var r = AutoBattleSim.Resolve(a.strength, f.strength);
                    return r.durationSeconds > 0.0 ? r.durationSeconds : autoResolveDelay;
                }
            }
            return autoResolveDelay; // フォールバック（従来値）
        }

        /// <summary>回廊 c の上に交戦中の艦隊が居るか（描画の点滅判定用）。</summary>
        private bool IsEngagedCorridor(Corridor c)
        {
            int min = Mathf.Min(c.aId, c.bId), max = Mathf.Max(c.aId, c.bId);
            foreach (var f in reg.fleets)
            {
                // 「戦場である」の判定は EngagedBattlefields と同じ述語を使う＝艦隊同士の交戦だけでなく
                // 要塞に足止めされている回廊も戦場に数える（要塞の固着は engaged を立てないため）。
                if (!IsBattlefieldFleet(f)) continue;
                int fMin = Mathf.Min(f.currentSystemId, f.destinationSystemId);
                int fMax = Mathf.Max(f.currentSystemId, f.destinationSystemId);
                if (fMin == min && fMax == max) return true;
            }
            return false;
        }

        /// <summary>
        /// その艦隊が「進行中の戦場」に居るか（#38 援軍の宛先・回廊の戦闘表示で共通の述語）。
        /// 艦隊同士の交戦（<see cref="StrategicFleet.engaged"/>）に加えて、
        /// 要塞に釘付けにされている艦隊（<see cref="StrategicFleet.IsBlockadedByFortress"/>）も数える。
        /// </summary>
        internal static bool IsBattlefieldFleet(StrategicFleet f)
            => f != null && f.IsOnCorridor && (f.engaged || f.IsBlockadedByFortress);

        // ===== 回廊要塞＝戦略ノード（#40 C-7）=====

        private float fortressAssaultTimer = 0f;
        private const float FortressAssaultInterval = 4f; // 力攻め判定の間隔（game-秒）。毎フレーム解決して瞬殺しない
        private readonly List<StrategicFleet> fortressAttackers = new List<StrategicFleet>();
        private readonly List<Faction> fortressAssaultFactions = new List<Faction>(); // 同じ要塞に取り付いた勢力（合算しない）

        /// <summary>
        /// 回廊要塞（#40）の力攻めを進める。<b>前進の足止めそのものは Core の
        /// <see cref="FortressBlockadeRules"/>／<see cref="StrategicFleet"/> が移動実行の中で行う</b>ので、
        /// ここは「要塞の手前に釘付けになった敵対艦隊」を集めて <see cref="FortressAssaultInterval"/> ごとに
        /// 力攻めを自動解決するだけにする（<see cref="StrategicFleet.engaged"/> は艦隊同士の会戦の印なので触らない）。
        ///
        /// 制圧すると要塞は攻撃側のものになり、生き残りの一部が守備に入って<b>今度は元の持ち主を締め出す</b>
        /// （再占領の整合）。撃退なら攻撃側が消耗して足止めが続く（難攻不落）。
        /// 複数の勢力が同じ要塞に取り付いている場合は<b>勢力ごとに別々に</b>解決する（合算して他勢力の戦果にしない）。
        /// </summary>
        private void TickFortressBlockades(float dt)
        {
            if (map == null || map.corridors == null || reg == null || reg.fleets == null) return;

            fortressAssaultTimer += dt;
            if (fortressAssaultTimer < FortressAssaultInterval) return;
            fortressAssaultTimer = 0f;

            for (int ci = 0; ci < map.corridors.Count; ci++)
            {
                Corridor c = map.corridors[ci];
                if (c == null || c.fortress == null) continue;

                // ★プレイヤーがいま潜行して戦っている要塞は、戦略側で勝手に解決しない。
                // 放っておくと、戦術マップで戦っている最中に戦略が力攻めを片付けて所有まで移し、
                // 戻ったときの書き戻しと二重適用になる（突入中の艦隊の兵力も勝手に削られる）。
                if (IsDescendedFortressCorridor(c)) continue;

                // 取り付いている敵対勢力を洗い出す（艦隊の並び順＝決定論。合算はしない）。
                fortressAssaultFactions.Clear();
                for (int fi = 0; fi < reg.fleets.Count; fi++)
                {
                    StrategicFleet f = reg.fleets[fi];
                    if (!IsPinnedAtFortress(f, c)) continue;
                    if (!fortressAssaultFactions.Contains(f.faction)) fortressAssaultFactions.Add(f.faction);
                }

                NotifyFortressBlockade(c);

                for (int ai = 0; ai < fortressAssaultFactions.Count; ai++)
                {
                    if (c.fortress == null || !FortressRules.BlocksPassage(c.fortress)) break; // 先の勢力が落とした
                    Faction attacker = fortressAssaultFactions[ai];
                    if (!StrategyRules.IsFortressBlocked(c, attacker)) continue;                // 既に味方の要塞

                    fortressAttackers.Clear();
                    int total = 0;
                    for (int fi = 0; fi < reg.fleets.Count; fi++)
                    {
                        StrategicFleet f = reg.fleets[fi];
                        if (!IsPinnedAtFortress(f, c) || f.faction != attacker) continue;
                        fortressAttackers.Add(f);
                        total += Mathf.Max(0, f.strength);
                    }
                    if (fortressAttackers.Count == 0 || total <= 0) continue;

                    Faction defender = c.fortress.owner;
                    string fname = c.fortress.fortressName;
                    FortressAssaultResult r = StrategyRules.AssaultFortress(c.fortress, attacker, total);

                    // 成功でも失敗でも攻撃側は消耗する（制圧をタダにしない）。
                    ScaleAndCullAttackers(fortressAttackers, r.attackerSurvivor, total);

                    if (r.captured)
                    {
                        int garrison = GarrisonFromSurvivors(fortressAttackers);
                        FortressBlockadeRules.Regarrison(c.fortress, attacker, garrison, capturedFortressShield);
                        // 要塞が落ちればその回廊の戦いは終わり＝未到着の援軍を帰投させる（宙に浮かせない）。
                        CloseBattlefieldAndReturn(BattlefieldKey.Corridor(c.aId, c.bId));
                        NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.警告,
                            garrison > 0
                                ? $"{fname} を {attacker} が制圧した（回廊が開通・守備力 {garrison} を残置）"
                                : $"{fname} を {attacker} が制圧した（回廊が開通・守備を残せず無防備）");
                    }
                    else
                    {
                        NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                            $"{fname}（{defender}）の攻略に失敗＝難攻不落（{attacker} 軍が損害）");
                    }
                }
            }
        }

        /// <summary>
        /// 回廊要塞の戦術マップ（#40）の結末を戦略の回廊へ書き戻す。
        /// 突破＝要塞は攻撃側のものになり、突入した艦隊から守備を残置して回廊が開く（戦略の自動解決と同じ扱い）。
        /// 未突破＝要塞は守備側のまま封鎖が続く。守備が尽きていれば無防備な要塞として残る。
        /// </summary>
        private void ApplyFortressResult()
        {
            Corridor c = map != null
                ? map.GetCorridor(BattleHandoff.fortressCorridorA, BattleHandoff.fortressCorridorB)
                : null;

            // ★まず会戦の損害を戦略へ反映する（突入した本隊＋参戦した援軍）。
            // 突破の可否より先に行う＝守備の残置は「戦い抜いた後の生き残り」から取ることになる。
            ApplyFortressAttrition(c);

            if (c != null && c.fortress != null)
            {
                string fname = c.fortress.fortressName;
                if (BattleHandoff.fortressBreached)
                {
                    // 突破：所有が移り、突入した艦隊から守備を残置する（残せなければ無防備）。
                    Faction attacker = BattleHandoff.fortressAttacker;
                    // 徴発は<b>突破した勢力の艦隊だけ</b>から。勢力で絞らないと、同じ要塞に足止めされている
                    // 第三勢力の艦隊からも守備兵力を抜いてしまう。
                    fortressAttackers.Clear();
                    for (int i = 0; i < reg.fleets.Count; i++)
                    {
                        StrategicFleet pf = reg.fleets[i];
                        if (pf == null || pf.faction != attacker) continue;
                        if (IsPinnedAtFortress(pf, c)) fortressAttackers.Add(pf);
                    }
                    int garrison = GarrisonFromSurvivors(fortressAttackers);
                    FortressBlockadeRules.Regarrison(c.fortress, attacker, garrison, capturedFortressShield);
                    NotificationCenter.Push(NotificationCategory.占領, NotificationSeverity.警告,
                        $"{fname} を {attacker} が戦術突破で制圧した（回廊が開通・守備力 {garrison}）");
                }
                else if (!BattleHandoff.fortressStillHolds)
                {
                    // 要塞は撃破したが突破線までは届かなかった＝封鎖は解けたが所有は移らない。
                    c.fortress.garrisonStrength = 0f;
                    c.fortress.controlsCorridor = false;
                    NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                        $"{fname} の守備は壊滅したが突破には至らなかった（封鎖のみ解除）");
                }
                else
                {
                    NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                        $"{fname} は持ちこたえた（回廊の封鎖は続く）");
                }
            }

            BattleHandoff.ClearFortress();
            BattleHandoff.Clear();
        }

        // ===== 援軍（ワープイン・#38 C-5）=====

        private readonly List<WarpReinforcement> reinforcementReturns = new List<WarpReinforcement>();
        private readonly List<WarpReinforcement> reinforcementPeek = new List<WarpReinforcement>();

        /// <summary>
        /// 交戦中の回廊へ自軍の艦隊を援軍として派遣する（#38）。所要時間は回廊の長さと艦隊のワープ速度から出し、
        /// <b>戦場の両端のうち早く着く側＝自陣側の入口</b>へ向かう。受理できたら true。
        ///
        /// 派遣した艦隊は盤面から消さず「増援航行中」にする＝到着しても戦闘が終わっていても、
        /// 同じ艦隊として戻せる（消失しない）。
        /// </summary>
        public bool DispatchReinforcement(StrategicFleet fleet, int sysA, int sysB)
        {
            if (fleet == null || map == null || reg == null) return false;
            if (fleet.warpingAsReinforcement || fleet.engaged || fleet.strength <= 0) return false;

            var ledger = StrategySession.Reinforcements;
            if (ledger == null) return false;

            var key = BattlefieldKey.Corridor(sysA, sysB);
            float travel = WarpReinforcementRules.TravelSecondsToBattlefield(
                map, fleet.currentSystemId, key, fleet.warpSpeed, fleet.sublightFactor, out int entrySystemId);

            long id = ledger.Dispatch(key, fleet.faction, fleet.id, fleet.strength, travel);
            if (id <= 0)
            {
                NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                    $"第{fleet.id}艦隊は {SystemName(sysA)}–{SystemName(sysB)} へ派遣できません（到達不能か戦闘終了）");
                return false;
            }

            fleet.warpingAsReinforcement = true;
            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                $"第{fleet.id}艦隊を {SystemName(sysA)}–{SystemName(sysB)} へ派遣（{SystemName(entrySystemId)} 側から到着予定 " +
                $"{FormatEta(travel)}）");
            return true;
        }

        /// <summary>指定回廊へ到着待ちの自軍援軍を「あと mm:ss」の形で1行にまとめる（UI 表示用）。</summary>
        public string ReinforcementEtaText(int sysA, int sysB, Faction faction)
        {
            var ledger = StrategySession.Reinforcements;
            if (ledger == null) return "";
            reinforcementPeek.Clear();
            if (ledger.PeekPending(BattlefieldKey.Corridor(sysA, sysB), faction, reinforcementPeek) <= 0) return "";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < reinforcementPeek.Count; i++)
            {
                WarpReinforcement o = reinforcementPeek[i];
                if (i > 0) sb.Append("　");
                sb.Append($"第{o.fleetId}艦隊 あと{FormatEta(WarpReinforcementRules.RemainingSeconds(o.arrivalTime, ledger.Elapsed))}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 戦場を閉じ、未到着の援軍をその場で盤面へ戻す（#38・戦略側で決着したとき）。
        /// 会戦シーンを経由しない自動解決でも派遣艦隊が確実に帰ってくるようにする。
        /// </summary>
        private void CloseBattlefieldAndReturn(BattlefieldKey key)
        {
            var ledger = StrategySession.Reinforcements;
            if (ledger == null || reg == null || !key.IsValid) return;

            reinforcementReturns.Clear();
            if (ledger.CloseBattlefield(key, reinforcementReturns) <= 0) return;

            for (int i = 0; i < reinforcementReturns.Count; i++)
            {
                StrategicFleet f = reg.GetFleet(reinforcementReturns[i].fleetId);
                if (f == null) continue;
                f.warpingAsReinforcement = false;
                NotificationCenter.Push(NotificationCategory.戦闘,
                    $"第{f.id}艦隊は戦闘終了により帰投しました");
            }
        }

        /// <summary>秒を mm:ss に整形する（到着予定の表示）。</summary>
        private static string FormatEta(float seconds)
        {
            if (float.IsPositiveInfinity(seconds)) return "到達不能";
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{s / 60:00}:{s % 60:00}";
        }

        /// <summary>
        /// 会戦が終わって差し戻された援軍を盤面へ戻す（#38）。
        /// 「増援航行中」を解いて元の星系に停泊させる＝派遣した艦隊が消えない。
        /// </summary>
        private void ApplyReinforcementReturns()
        {
            if (reg == null) return;

            // ① まだ着いていなかった援軍＝そのまま盤面へ戻す。
            reinforcementReturns.Clear();
            if (ReinforcementReturnQueue.TakeAll(reinforcementReturns))
            {
                for (int i = 0; i < reinforcementReturns.Count; i++)
                {
                    StrategicFleet f = reg.GetFleet(reinforcementReturns[i].fleetId);
                    if (f == null) continue;
                    f.warpingAsReinforcement = false;
                    NotificationCenter.Push(NotificationCategory.戦闘,
                        $"第{f.id}艦隊は戦闘終了により帰投しました");
                }
            }

            // ② 戦場に到着して参戦した援軍＝印を外して操作できる状態に戻す。
            // これをやらないと「増援航行中」のまま盤面に残り、二度と動かせない（実機QAで判明）。
            reinforcementArrivedIds.Clear();
            if (ReinforcementReturnQueue.TakeArrivedIds(reinforcementArrivedIds))
            {
                for (int i = 0; i < reinforcementArrivedIds.Count; i++)
                {
                    StrategicFleet f = reg.GetFleet(reinforcementArrivedIds[i]);
                    if (f == null) continue;
                    f.warpingAsReinforcement = false;
                    NotificationCenter.Push(NotificationCategory.戦闘,
                        $"第{f.id}艦隊は援軍として参戦し、戦線へ復帰しました");
                }
            }
        }

        private readonly List<int> reinforcementArrivedIds = new List<int>();

        // 封鎖の通知を出した回廊（同じ足止めで通知を毎回積まない）。要塞が落ちる/開通したら忘れる。
        private readonly HashSet<int> notifiedBlockades = new HashSet<int>();

        /// <summary>
        /// プレイヤーの艦隊が要塞に足止めされたら、<b>ダブルクリックで要塞戦へ潜行できる通知</b>を1度だけ出す
        /// （#40 の操作導線）。放置すれば <see cref="TickFortressBlockades"/> が自動で力攻めを解決する。
        /// </summary>
        private void NotifyFortressBlockade(Corridor c)
        {
            int key = Mathf.Min(c.aId, c.bId) * 10007 + Mathf.Max(c.aId, c.bId);
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

            if (c.fortress == null || !StrategyRules.IsFortressBlocked(c, player))
            {
                notifiedBlockades.Remove(key);   // 開通/制圧＝次に封鎖されたらまた知らせる
                return;
            }
            if (!fortressAssaultFactions.Contains(player)) return; // 自軍が取り付いていない
            if (!notifiedBlockades.Add(key)) return;               // 同じ足止めでは1度だけ

            long seq = NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.警告,
                $"{c.fortress.fortressName}（{c.fortress.owner}）が回廊を扼している＝迂回不可。" +
                $"ダブルクリックで要塞戦へ");
            int a = c.aId, b = c.bId;
            NotificationActionRegistry.Register(seq, () => DescendFortressBySystems(a, b));
        }

        /// <summary>
        /// その回廊が「いまプレイヤーが潜行して戦っている要塞戦」か（＝戦略側の自動解決から外す）。
        /// 会戦ウィンドウでも全画面でも、受け渡しの戦場キーで判定する。
        /// </summary>
        private static bool IsDescendedFortressCorridor(Corridor c)
        {
            // ★受け渡し（BattleHandoff）ではなく <see cref="ActiveBattlefields"/> を見る。
            // ウィンドウ化会戦では BattleDirector がロード完了時に global の受け渡しを空けるので、
            // 受け渡しで判定すると必ず false になり、戦っている最中に戦略側が横取りしてしまう。
            return c != null && ActiveBattlefields.IsCorridorActive(c.aId, c.bId);
        }

        /// <summary>
        /// 要塞戦（#40）の損害を戦略の艦隊へ反映する。参戦した艦隊＝<b>要塞の手前に釘付けの本隊</b>＋
        /// <b>戦場に到着して参戦した援軍</b>。会戦が返した残存兵力を、参戦時の兵力比で按分する。
        ///
        /// ・生存＝実際の残存兵力（按分）／全滅＝0（盤面から除去）
        /// ・未到着で帰投した援軍は<b>この対象に入れない</b>＝派遣時の兵力を保持する
        /// ・<see cref="ScaleAndCullAttackers"/> と同じ按分の窓口を使い、二重に減らさない
        ///   （放置の自動解決はこのメソッドを通らず、逆にこちらは自動解決を通らない）
        /// </summary>
        private void ApplyFortressAttrition(Corridor c)
        {
            if (reg == null || c == null) return;

            Faction attacker = BattleHandoff.fortressAttacker;

            // 参戦者を集める（重複させない）。本隊＝この回廊で釘付けの自勢力艦隊。
            fortressAttackers.Clear();
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != attacker) continue;
                if (IsPinnedAtFortress(f, c)) fortressAttackers.Add(f);
            }

            // 参戦した援軍（到着済み）。まだ着かずに帰投したぶんは含めない＝派遣時の兵力のまま。
            reinforcementArrivedIds.Clear();
            ReinforcementReturnQueue.PeekArrivedIds(reinforcementArrivedIds);
            for (int i = 0; i < reinforcementArrivedIds.Count; i++)
            {
                StrategicFleet f = reg.GetFleet(reinforcementArrivedIds[i]);
                if (f == null || f.faction != attacker) continue;
                if (!fortressAttackers.Contains(f)) fortressAttackers.Add(f);
            }

            if (fortressAttackers.Count == 0) return;

            int before = 0, shipsBefore = 0;
            for (int i = 0; i < fortressAttackers.Count; i++)
            {
                before += Mathf.Max(0, fortressAttackers[i].strength);
                shipsBefore += Mathf.Max(0, fortressAttackers[i].Ships);   // 損害の通知に使う実隻数
            }
            if (before <= 0) return;

            // ★手動で戦った会戦は<b>艦隊ごとの実残存</b>を使う。無傷の隊と全滅した隊を合計して
            // 按分すると両方が半減してしまうため、明細があるかぎり按分にはしない。
            int survivor;
            if (BattleHandoff.fortressSurvivors.Count > 0)
            {
                survivor = ApplyPerFleetSurvivors(fortressAttackers);
            }
            else
            {
                // 明細が無い＝抽象的な自動解決の結果。従来どおり合計を兵力比で按分する。
                survivor = Mathf.Clamp(BattleHandoff.fortressAttackerSurvivor, 0, before);
                ScaleAndCullAttackers(fortressAttackers, survivor, before);
            }

            // 損害の通知も<b>艦艇数</b>で出す（抽象兵力はプレイヤーに見せない）。
            // 反映後の実隻数を数え直し、差分を失った隻数として出す＝兵力に「隻」を付け替えない。
            int shipsAfter = 0;
            for (int i = 0; i < fortressAttackers.Count; i++)
                if (fortressAttackers[i] != null) shipsAfter += Mathf.Max(0, fortressAttackers[i].Ships);
            int shipsLost = Mathf.Max(0, shipsBefore - shipsAfter);
            if (shipsLost > 0)
                NotificationCenter.Push(NotificationCategory.戦闘,
                    $"要塞戦の損害：{attacker} 軍が {shipsLost:N0}隻 を失いました（残存 {shipsAfter:N0}隻）");
        }

        /// <summary>
        /// 会戦が返した<b>艦隊別の実残存</b>をそのまま各艦隊へ書き込む（#40 手動突入）。
        /// 明細に無い艦隊は戦っていない＝無傷として触らない。0 の艦隊は盤面から除去する。
        /// 艦艇数も<b>その艦隊自身の</b>兵力の減り方だけで決める（他隊の損失を混ぜない）。
        /// 反映後の残存合計を返す。
        /// </summary>
        private int ApplyPerFleetSurvivors(List<StrategicFleet> fleets)
        {
            int survivorTotal = 0;
            for (int i = fleets.Count - 1; i >= 0; i--)
            {
                StrategicFleet f = fleets[i];
                if (f == null) continue;

                bool found = false;
                int after = 0;
                for (int k = 0; k < BattleHandoff.fortressSurvivors.Count; k++)
                {
                    if (BattleHandoff.fortressSurvivors[k].fleetId != f.id) continue;
                    after = Mathf.Max(0, BattleHandoff.fortressSurvivors[k].survivor);
                    found = true;
                    break;
                }
                if (!found) { survivorTotal += Mathf.Max(0, f.strength); continue; } // 参戦していない＝無傷

                int before = Mathf.Max(0, f.strength);
                after = Mathf.Min(after, before);   // 会戦で増えることはない
                FleetShipCountRules.ApplyPhysicalLoss(f, after);
                survivorTotal += after;
                if (f.strength <= 0) reg.Remove(f);
            }
            return survivorTotal;
        }

        /// <summary>この艦隊が当該回廊の要塞に釘付けにされているか（＝力攻めに参加する）。</summary>
        private static bool IsPinnedAtFortress(StrategicFleet f, Corridor c)
        {
            if (f == null || !f.IsOnCorridor) return false;
            int min = Mathf.Min(c.aId, c.bId), max = Mathf.Max(c.aId, c.bId);
            if (Mathf.Min(f.currentSystemId, f.destinationSystemId) != min) return false;
            if (Mathf.Max(f.currentSystemId, f.destinationSystemId) != max) return false;
            return StrategyRules.IsFortressBlocked(c, f.faction);
        }

        /// <summary>
        /// 制圧した艦隊から守備兵力を差し引いて要塞へ残置する（占領のコスト＝落としたぶん野戦戦力が減る）。
        /// これが 0 だと要塞は無防備＝すぐ落とし返される。
        /// </summary>
        private int GarrisonFromSurvivors(List<StrategicFleet> fleets)
        {
            int garrison = 0;
            for (int i = fleets.Count - 1; i >= 0; i--)
            {
                StrategicFleet f = fleets[i];
                if (f == null || f.strength <= 0) continue;
                int take = Mathf.FloorToInt(f.strength * Mathf.Clamp01(captureGarrisonShare));
                if (take <= 0) continue;
                int before = f.strength;
                f.strength -= take;
                // 要塞へ残す兵力ぶんの艦艇もその艦隊から抜ける（隻数と兵力の整合を保つ）。
                f.SetShips(FleetShipCountRules.AfterLosses(f.Ships, before, f.strength));
                garrison += take;
                if (f.strength <= 0) reg.Remove(f);
            }
            return garrison;
        }

        private readonly List<int> attritionBefore = new List<int>();
        private readonly List<int> attritionAfter = new List<int>();

        /// <summary>
        /// 会戦・力攻めの残存兵力を参加艦隊へ按分し、0 になった隊は盤面から除去する。
        /// 按分は Core の <see cref="FleetAttritionRules.Distribute"/> が唯一の窓口＝合計が必ず残存に一致する
        /// （四捨五入だけだと合計がずれて兵力が勝手に増減する）。
        ///
        /// <b>艦艇数（隻）も同時に減らす</b>。減るのは<b>その艦隊自身の</b>兵力比だけで、
        /// 他の艦隊へ損失を均等割りしない（艦隊ごとに独立して持ち歩く数のため）。
        /// </summary>
        private void ScaleAndCullAttackers(List<StrategicFleet> fleets, int survivor, int total)
        {
            if (fleets == null || fleets.Count == 0 || total <= 0) return;

            attritionBefore.Clear();
            for (int i = 0; i < fleets.Count; i++)
                attritionBefore.Add(fleets[i] != null ? Mathf.Max(0, fleets[i].strength) : 0);

            FleetAttritionRules.Distribute(attritionBefore, survivor, attritionAfter);

            for (int i = fleets.Count - 1; i >= 0; i--)
            {
                StrategicFleet f = fleets[i];
                if (f == null) continue;
                int before = attritionBefore[i];
                int after = (i < attritionAfter.Count) ? attritionAfter[i] : 0;

                // 艦艇数はこの艦隊の兵力の減り方だけで決める（他隊の損失を混ぜない）。
                FleetShipCountRules.ApplyPhysicalLoss(f, after);
                if (f.strength <= 0) reg.Remove(f);
            }
        }

        private void OnDestroy()
        {
            DecisionDeck.Resolved -= OnPolicyDecisionResolved;
            if (Active == this) Active = null;
            if (lineMat != null) Destroy(lineMat);
            if (disc != null && disc.texture != null) Destroy(disc.texture);
            if (ringSprite != null && ringSprite.texture != null) Destroy(ringSprite.texture);
            // 実行時生成したマテリアルはリークするので明示的に破棄する（規約）。
            foreach (var kv in fortressMaterials) if (kv.Value != null) Destroy(kv.Value);
            fortressMaterials.Clear();
            // 恒星マテリアルは StarMaterialFactory が持つ（盤面とプレビューで共有）＝そこへ解放を任せる。
            StarMaterialFactory.ReleaseAll();
        }
    }
}
