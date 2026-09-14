using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略（星系）マップの Windows 風UI（銀英伝の古典UI意匠・#UI統一）。
    /// ①画面上部の<b>固定コマンドメニューバー</b>（国家ステータス・二重暦/速度・各パネルを開くボタン列）と、
    /// ②<b>ドラッグで動かせる星系マップ窓</b>から成る。
    /// <b>整合の要</b>：マップ窓は正規化矩形 <c>mapRect</c>（0〜1・画面全体基準）を唯一の真実とし、
    /// <see cref="Camera.rect"/> と窓UIのアンカーの<b>両方に同じ mapRect を与える</b>＝両者とも画面全体基準なので
    /// 解像度・アスペクトに依らず<b>ピクセル一致</b>する（GetWorldCorners/Screen 依存の逆算をしない）。
    /// タイトルバーのドラッグは mapRect を正規化で平行移動する。<see cref="GalaxyView"/> は <see cref="Camera.ScreenToWorldPoint"/>
    /// でクリックを拾い、これはビューポート rect を尊重するため窓移動後も選択/進軍が正しく動く。
    /// 窓の外は<b>背景カメラ</b>が黒でクリアし残像を防ぐ。浮きHUDは <see cref="GalaxyView.HideWorldHud"/> で抑制し上メニューへ集約。Strategy 専用。
    /// </summary>
    public class StrategyMapWindow : MonoBehaviour
    {
        [Header("上部メニューバー")]
        public float menuBarFrac = 0.10f; // 目標(勝利進捗)行を足したぶん少し高く

        [Header("マップ窓")]
        [Tooltip("窓タイトルバーの高さ（ピクセル）")]
        public float mapTitleHeight = 38f;   // 掴みやすい高さ（#MAPドラッグが効かない）

        [Header("配色（ゲーム意匠）")]
        public Color menuBarColor = new Color(0.11f, 0.15f, 0.22f, 1f);
        public Color titleBarColor = new Color(0.13f, 0.18f, 0.26f, 1f);
        public Color buttonColor = new Color(0.16f, 0.21f, 0.30f, 1f);
        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);
        public Color desktopColor = new Color(0.02f, 0.02f, 0.05f, 1f);

        [Header("リサイズ")]
        [Tooltip("右下のリサイズグリップの一辺（ピクセル）")]
        public float resizeGripSize = 34f;   // 掴みやすい大きさ（#MAPドラッグが効かない）
        [Tooltip("マップ窓の最小幅/高さ（画面比 0〜1）")]
        public float minWindowFrac = 0.2f;

        // マップ窓の正規化矩形（画面全体を 0〜1 とした位置/大きさ）。camera.rect と窓UIの両方に使う＝必ず一致。
        // 初期は左寄せ・幅約63%・上メニュー直下から高さ約55%（右と下に通知/決裁の浮き窓ぶんの余白を残す）。
        // #戦略MAP刷新：MAP を画面の主役にする。上メニュー(menuBarFrac)の直下から下端近くまで取り、
        // 右端に観測/決裁の帯、下端に通知の帯だけを残す。ドラッグ/リサイズは従来どおり効く。
        // ===== 画面レイアウトの取り決め（#戦略MAP刷新・パネルの重なり解消）=====
        // MAP・右カラム（勝敗メーター/決裁デスク）・下の通知が、どの解像度でも同じ割り付けになるよう
        // <b>この定数を唯一の基準</b>にする。割合なので 1920x1080 と 2560x1440（同じ16:9）で一致する。
        // 各パネルはここを読んで自分の初期位置を決める＝個別に数値を持たせない（ずれの再発防止）。

        /// <summary>
        /// 割り付けの実体は Core（<see cref="StrategyScreenLayout"/>）＝TestHarness で不変条件を検証できる。
        /// <b>実画面サイズから作る</b>＝16:9/4:3/21:9/縦長で通知帯の実寸が変わっても重ならない（#画面比率への適応）。
        /// </summary>
        public static StrategyScreenLayout Layout =>
            StrategyScreenLayoutRules.ForScreen(Screen.width > 0 ? Screen.width : 1920f,
                                                Screen.height > 0 ? Screen.height : 1080f);

        /// <summary>右カラム（勝敗メーター・決裁デスク）の左端（画面幅に対する割合）。</summary>
        public static float RightColumnLeftFrac => Layout.RightColumnLeft;
        /// <summary>右カラムの幅（画面幅に対する割合）。</summary>
        public static float RightColumnWidthFrac => Layout.RightColumnWidth;
        /// <summary>右カラムの幅を参照解像度(1920)の設計ピクセルで返す（パネルの preferredWidth 用）。</summary>
        public static float RightColumnDesignWidth => StrategyScreenLayoutRules.ToDesignWidth(Layout.RightColumnWidth);
        /// <summary>右カラムの各パネルが右端を揃える余白（設計ピクセル）。</summary>
        public static float RightColumnDesignMargin => StrategyScreenLayoutRules.ToDesignWidth(Layout.rightMargin);

        private Rect mapRect = new Rect(
            StrategyScreenLayout.Default.mapLeft, StrategyScreenLayout.Default.mapBottom,
            StrategyScreenLayout.Default.mapWidth, StrategyScreenLayout.Default.MapHeight);

        // プレイヤーが窓を動かした/大きさを変えたか。触っていない間は画面サイズの変化に既定配置で追従し、
        // 一度でも触ったら位置は尊重して画面内へのクランプだけ行う（勝手に動かさない）。
        private bool userMovedWindow;
        private Vector2Int lastScreen;

        /// <summary>いまの画面に合わせた既定の窓矩形。</summary>
        private static Rect DefaultMapRect()
        {
            StrategyScreenLayout l = Layout;
            return new Rect(l.mapLeft, l.mapBottom, l.mapWidth, l.MapHeight);
        }

        private Camera cam;
        private Camera bgCam;
        private Rect originalRect;
        private bool rectApplied;

        private RectTransform titleBarRT;
        private RectTransform menuBarRT;      // 上メニューバー（折り返しに応じて縦に伸ばす）
        private WrapLayoutGroup cmdWrap;      // コマンド行（何行になったかを高さで返す）
        private float baseMenuBarFrac = 0.10f;
        private RectTransform contentRT;
        private RectTransform edgeLeft, edgeRight, edgeBottom;
        private RectTransform resizeGripRT;
        private TextMeshProUGUI clockLabel;

        // 最小化（タイトルバーだけ残してマップ表示を畳む）
        private bool minimized;
        private int savedCullingMask;
        private CameraClearFlags savedClearFlags;
        private TextMeshProUGUI minimizeLabel;

        // 目標（勝利進捗＋次の一手）— B：目的可視化（#遊べる縦スライス）
        private RectTransform objectiveFillRT;
        private TextMeshProUGUI objectiveLabel;
        private TextMeshProUGUI hintLabel;
        private float objectiveTimer;
        private const float ObjectiveInterval = 0.5f; // 毎フレーム再計算しない（終盤ラグ規律）
        private float nudgeFontSize = 14f;            // 配置パネルの文字（実ピクセル下限つきで決める）
        private GameObject nudgePanel;                // 配置パネル（開いている間だけ存在＝解像度変更後も組み直す）

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
            if (scene.name != "Strategy") return;
            if (UnityEngine.Object.FindAnyObjectByType<StrategyMapWindow>() != null) return;
            new GameObject("StrategyMapWindow").AddComponent<StrategyMapWindow>();
        }

        private void Awake()
        {
            cam = Camera.main;
            if (cam == null) cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            GalaxyView.HideWorldHud = true;
            SetupBackgroundCamera();
            mapRect = DefaultMapRect();                       // 起動時は実画面に合わせた既定位置
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            BuildUI();
            ApplyLayout();
        }

        private void OnDestroy()
        {
            if (cam != null && rectApplied) cam.rect = originalRect;
            if (cam != null && minimized) { cam.cullingMask = savedCullingMask; cam.clearFlags = savedClearFlags; } // 最小化中の破棄でも復元
            if (bgCam != null) Destroy(bgCam.gameObject);
            GalaxyView.HideWorldHud = false;
            UIDragMove.TopReservedPx = 0f; // 戦略を離れたら確保帯を解除（他シーンに持ち越さない）
        }

        private void Update()
        {
            TimeDisplay.StepSpeedInput();
            if (clockLabel != null && TimeDisplay.TryFormatNow(out string text, out Color color))
            {
                clockLabel.text = text;
                clockLabel.color = color;
            }

            // 目標（勝利進捗＋次の一手）は間引いて更新（毎フレーム再計算しない＝終盤ラグ規律）。
            objectiveTimer += Time.unscaledDeltaTime;
            if (objectiveTimer >= ObjectiveInterval) { objectiveTimer = 0f; UpdateObjective(); }
        }

        private void LateUpdate()
        {
            // ドラッグで動かせる各窓（観測オーバーレイ等）が上メニューより上へ行かないよう、確保帯＝上メニュー高を公開。
            UIDragMove.TopReservedPx = menuBarFrac * Screen.height;

            // 解像度/ウィンドウサイズが変わったら追従する（#画面比率への適応）。
            // まだ触っていない窓は新しい比率の既定へ、触った窓は位置を尊重して画面内へ収め直すだけ。
            var now = new Vector2Int(Screen.width, Screen.height);
            if (now != lastScreen)
            {
                lastScreen = now;
                if (!userMovedWindow) mapRect = DefaultMapRect();
                // 開いている配置パネルは新しい実画面から組み直す（実機QA：縦長で 6px のまま残った）。
                if (nudgePanel != null) { Destroy(nudgePanel); nudgePanel = BuildNudgePanel(); }
                // 上段の文字・ボタンは生成時の画面幅で倍率が決まるので、切替のたびに計算し直す。
                RescaleMenuBar();
                // ★盤面のフィットは<b>次フレーム以降</b>に確定させる（実機QA：切替直後は端の星系が切れた）。
                // このフレームではまだ camera.rect を入れ替えたばかりで cam.aspect が古く、
                // その場で FitAll すると誤った縦横比で縮尺を決めてしまう。
                Galaxy()?.RequestFitAfterLayout();
            }
            FitMenuBarToCommandRow();
            ApplyLayout();
        }

        /// <summary>コマンド行が占める上メニューバーの割合（残りは「≡ メニュー」行と目標行）。</summary>
        private const float CommandRowFrac = 0.32f;

        /// <summary>
        /// コマンド行が<b>折り返して2行以上になったら上メニューバーを縦に伸ばす</b>（#低解像度での可読性）。
        /// 文字を実ピクセル14px以上に保つと狭い画面ではボタン列が1行に収まらないので、
        /// 潰すのでも枠外へ出すのでもなく、バーごと高さを増やして全部押せる状態にする。
        /// マップ窓の上端は <see cref="ApplyLayout"/> が menuBarFrac から引き直すので自動で下がる。
        /// </summary>
        private void FitMenuBarToCommandRow()
        {
            if (cmdWrap == null || menuBarRT == null) return;
            var parent = menuBarRT.parent as RectTransform;
            if (parent == null) return;

            float canvasH = parent.rect.height;
            if (canvasH <= 1f) return;

            float need = cmdWrap.PreferredHeight;   // 設計px（行数×ボタン高＋行間）
            if (need <= 0f) return;

            float frac = Mathf.Clamp(need / CommandRowFrac / canvasH, baseMenuBarFrac, 0.34f);
            if (Mathf.Abs(frac - menuBarFrac) < 0.0015f) return;

            menuBarFrac = frac;
            menuBarRT.anchorMin = new Vector2(0f, 1f - menuBarFrac);
            menuBarRT.anchorMax = new Vector2(1f, 1f);
        }

        // ===== カメラ =====

        private void SetupBackgroundCamera()
        {
            if (cam == null) return;
            originalRect = cam.rect;
            rectApplied = true;

            var go = new GameObject("StrategyDesktopCamera");
            bgCam = go.AddComponent<Camera>();
            bgCam.orthographic = true;
            bgCam.depth = cam.depth - 1f;
            bgCam.clearFlags = CameraClearFlags.SolidColor;
            bgCam.backgroundColor = desktopColor;
            bgCam.cullingMask = 0;
            bgCam.rect = new Rect(0f, 0f, 1f, 1f);
        }

        /// <summary>タイトルバーのドラッグで窓（mapRect）を正規化平行移動する。</summary>
        private void OnTitleDrag(Vector2 deltaPixels)
        {
            float sw = Screen.width > 0 ? Screen.width : 1920f;
            float sh = Screen.height > 0 ? Screen.height : 1080f;
            mapRect.x += deltaPixels.x / sw;
            mapRect.y += deltaPixels.y / sh;
            userMovedWindow = true;
            ApplyLayout();
        }

        /// <summary>窓の位置と大きさを既定へ戻す（掴み損ねて画面外へやってしまったときの復帰口）。</summary>
        public void ResetWindow()
        {
            userMovedWindow = false;
            mapRect = DefaultMapRect();
            minimized = false;
            ApplyLayout();
            // 盤面も全体表示へ戻す＝「迷子になった」状態から一手で復帰できる。
            Galaxy()?.FitAll();
            NotificationCenter.Push(NotificationCategory.システム, NotificationSeverity.情報,
                "マップ窓の位置と大きさを既定に戻しました");
        }

        /// <summary>右下グリップのドラッグで窓（mapRect）をリサイズする（上端＝top は固定し下/右辺を動かす）。</summary>
        private void OnResizeDrag(Vector2 deltaPixels)
        {
            float sw = Screen.width > 0 ? Screen.width : 1920f;
            float sh = Screen.height > 0 ? Screen.height : 1080f;
            userMovedWindow = true;
            float topY = mapRect.yMax;            // 上端を固定（上メニュー側を動かさない）
            mapRect.width = Mathf.Max(minWindowFrac, mapRect.width + deltaPixels.x / sw);
            mapRect.y += deltaPixels.y / sh;       // 下辺をカーソルに追従
            mapRect.height = Mathf.Max(minWindowFrac, topY - mapRect.y);
            mapRect.y = topY - mapRect.height;     // height をクランプしたぶん下辺を整合
            ApplyLayout();
        }

        /// <summary>mapRect を camera.rect と窓UIのアンカーへ反映（両者とも画面全体基準＝一致）。</summary>
        private void ApplyLayout()
        {
            if (cam == null) return;
            float sh = Screen.height > 0 ? Screen.height : 1080f;
            // 上メニューバー＋窓タイトルバーのぶんを差し引いた上限＝窓の上端はここを越えない（#4）。
            float titleFrac = TitleBarActualPx / sh;
            float topLimit = Mathf.Clamp01(1f - menuBarFrac - titleFrac);

            mapRect.width = Mathf.Clamp(mapRect.width, minWindowFrac, 1f);
            mapRect.height = Mathf.Clamp(mapRect.height, minWindowFrac, Mathf.Max(minWindowFrac, topLimit));
            mapRect.x = Mathf.Clamp(mapRect.x, 0f, 1f - mapRect.width);
            mapRect.y = Mathf.Clamp(mapRect.y, 0f, Mathf.Max(0f, topLimit - mapRect.height));
            cam.rect = mapRect;

            float x0 = mapRect.xMin, x1 = mapRect.xMax, y0 = mapRect.yMin, y1 = mapRect.yMax;

            // 最小化中は本体（マップ枠・縁・グリップ）を隠してタイトルバーだけ残す。
            bool body = !minimized;
            if (contentRT != null) contentRT.gameObject.SetActive(body);
            if (edgeLeft != null) edgeLeft.gameObject.SetActive(body);
            if (edgeRight != null) edgeRight.gameObject.SetActive(body);
            if (edgeBottom != null) edgeBottom.gameObject.SetActive(body);
            if (resizeGripRT != null) resizeGripRT.gameObject.SetActive(body);

            if (contentRT != null) { Stretch(contentRT, x0, y0, x1, y1); }
            if (titleBarRT != null)
            {
                titleBarRT.anchorMin = new Vector2(x0, y1);
                titleBarRT.anchorMax = new Vector2(x1, y1);
                titleBarRT.pivot = new Vector2(0.5f, 0f);
                titleBarRT.sizeDelta = new Vector2(0f, TitleBarDesign);
                titleBarRT.anchoredPosition = Vector2.zero;
            }
            // 縁取り（細いバー）
            Edge(edgeLeft, x0, y0, x0, y1, new Vector2(0f, 0.5f), new Vector2(2f, 0f));
            Edge(edgeRight, x1, y0, x1, y1, new Vector2(1f, 0.5f), new Vector2(2f, 0f));
            Edge(edgeBottom, x0, y0, x1, y0, new Vector2(0.5f, 0f), new Vector2(0f, 2f));

            // 右下リサイズグリップ（窓の右下角）。
            if (resizeGripRT != null)
            {
                resizeGripRT.anchorMin = new Vector2(x1, y0);
                resizeGripRT.anchorMax = new Vector2(x1, y0);
                resizeGripRT.pivot = new Vector2(1f, 0f);
                resizeGripRT.sizeDelta = new Vector2(resizeGripSize, resizeGripSize);
                resizeGripRT.anchoredPosition = Vector2.zero;
            }
        }

        private static void Stretch(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Edge(RectTransform rt, float x0, float y0, float x1, float y1, Vector2 pivot, Vector2 sizeDelta)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.pivot = pivot;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
        }

        // ===== UI =====

        private void BuildUI()
        {
            var canvasObj = new GameObject("StrategyMapWindowCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 860;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();
            Transform root = canvasObj.transform;

            BuildMenuBar(root);
            BuildMapWindow(root);
            BuildObserverWindow(root);
        }

        private void BuildMenuBar(Transform root)
        {
            var bar = AddBar(root, "MenuBar", new Vector2(0f, 1f - menuBarFrac), new Vector2(1f, 1f), menuBarColor);

            var top = new GameObject("TopRow").AddComponent<RectTransform>();
            top.transform.SetParent(bar.transform, false);
            top.anchorMin = new Vector2(0f, 0.66f); top.anchorMax = new Vector2(1f, 1f);
            top.offsetMin = Vector2.zero; top.offsetMax = Vector2.zero;

            // 「≡」はメニューの記号なのに従来はただのラベルで、押しても何も起きなかった（実機報告）。
            // クリックでシステムメニュー（再開／セーブ／タイトルへ戻る）を開く＝ESC が効かない環境でも
            // マウスだけでセーブと終了に到達できる導線を確保する。
            var title = AddText(top, "≡ メニュー（セーブ / 終了）", 20f, accentColor, TextAlignmentOptions.Left);
            title.fontStyle = FontStyles.Bold;
            title.raycastTarget = true;
            SetAnchors(title.rectTransform, new Vector2(0f, 0f), new Vector2(0.30f, 1f), new Vector2(20f, 0f), new Vector2(-8f, 0f));
            var titleBtn = title.gameObject.AddComponent<Button>();
            titleBtn.transition = UnityEngine.UI.Selectable.Transition.None;
            titleBtn.targetGraphic = title;
            titleBtn.onClick.AddListener(OpenSystemMenu);

            // 税率/国庫/民心/安定度の常時表示は廃止（じゃまなので削除）。出所は「勢力」(G)／「財政」(E) パネル。
            clockLabel = AddText(top, "", 16f, new Color(0.95f, 0.92f, 0.7f), TextAlignmentOptions.Right);
            SetAnchors(clockLabel.rectTransform, new Vector2(0.74f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-20f, 0f));

            // 目標行（勝利進捗バー＋次の一手）＝プレイ中の行動指針（B：目的可視化）。
            BuildObjectiveRow(bar.transform);

            var cmd = new GameObject("CommandRow").AddComponent<RectTransform>();
            cmd.transform.SetParent(bar.transform, false);
            cmd.anchorMin = new Vector2(0f, 0f); cmd.anchorMax = new Vector2(1f, 0.32f);
            cmd.offsetMin = new Vector2(16f, 4f); cmd.offsetMax = new Vector2(-16f, -2f);
            // ★1行に入らなければ折り返す（実機QA：縦長/1024幅でボタンが潰れて読めなくなった）。
            // GridLayout ではなく Flexible なラッピングが要るので、行が溢れたら次の行へ送る単純な実装にする。
            var hlg = cmd.gameObject.AddComponent<WrapLayoutGroup>();
            hlg.spacing = 4f;
            hlg.lineSpacing = 4f;
            cmdWrap = hlg;
            menuBarRT = (RectTransform)bar.transform;
            baseMenuBarFrac = menuBarFrac;

            // 執務机（なりきり提督の一人称UI）を上メニューへ格上げ＝観測ウィンドウのシステムタブ内項目でなく、
            // コマンドバーの専用ボタンで直接開く（プレイヤーの主画面ゆえ最優先・観測の左に置く）。Alt+J も従来どおり。
            MakeBarButton(cmd.transform, "執務机", 110f,
                () => UnityEngine.Object.FindAnyObjectByType<ProtagonistDeskOverlay>()?.Toggle());

            // 上メニューの集約：25個のボタンを「観測」1個に畳み、タブ化したウィンドウ（内政/経済/軍事/政治/
            // システムの5タブ）から各オブザーバを開く。既存ウィンドウ・単一文字ショートカット（G/J/M/…）は不変。
            MakeBarButton(cmd.transform, "観測", 116f, ToggleObserverWindow);

            // 時間操作をマウスだけで完結させる（#キー入力が効かない）。キーと同じ実装を呼ぶので挙動が分岐しない。
            // 自動入力ではキーが届かないことがあり、キーだけに依存すると操作不能になるため画面上にも置く。
            MakeBarButton(cmd.transform, "停止 / 再開", 132f, TimeDisplay.TogglePause);
            MakeBarButton(cmd.transform, "遅く", 82f, TimeDisplay.SlowDown);
            MakeBarButton(cmd.transform, "速く", 82f, TimeDisplay.SpeedUp);

            // 窓を画面外へやってしまったときの復帰口（掴み直せなくなるのを防ぐ）。
            MakeBarButton(cmd.transform, "窓を戻す", 116f, ResetWindow);
            // 艦隊メニュー：戦略的な移動命令の入口。MAP の右クリック発令は廃止したので、
            // 「誰を・どこへ・出せるか」をここで確認してから出す（#艦隊メニューへ集約）。
            MakeBarButton(cmd.transform, "艦隊", 92f, FleetOrderPanel.Toggle);
            // 軍団編成：MAP から停泊中の艦隊表示を外した代わりに、軍団の枠と配下艦隊をここで見る（#E）。
            MakeBarButton(cmd.transform, "軍団編成", 120f, CorpsOrganizationPanel.Toggle);

            // 位置と大きさをクリックだけで調整するパネル（ドラッグが効かない環境の逃げ道）。
            MakeBarButton(cmd.transform, "配置", 92f, ToggleNudgePanel);

            var rule = AddBar(bar.transform, "Rule", new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Color(accentColor.r, accentColor.g, accentColor.b, 0.6f));
            var rrt = (RectTransform)rule.transform; rrt.pivot = new Vector2(0.5f, 0f); rrt.sizeDelta = new Vector2(0f, 2f);
        }

        /// <summary>上メニューの「≡」からシステムメニュー（再開/セーブ/タイトルへ戻る）を開閉する。</summary>
        private void OpenSystemMenu()
        {
            StrategySystemMenu menu = UnityEngine.Object.FindAnyObjectByType<StrategySystemMenu>();
            if (menu != null) menu.Toggle();
        }

        private void BuildMapWindow(Transform root)
        {
            // タイトルバー（つかんで移動・mapRect を動かす）
            var bar = new GameObject("MapTitleBar").AddComponent<RectTransform>();
            bar.transform.SetParent(root, false);
            titleBarRT = bar;
            var tImg = bar.gameObject.AddComponent<Image>();
            tImg.color = titleBarColor;
            var drag = bar.gameObject.AddComponent<MapWindowDrag>();
            drag.onDragDelta = OnTitleDrag;
            var cap = AddText(bar, "≡ 星系マップ　（ドラッグで移動）", 15f, accentColor, TextAlignmentOptions.Left);
            TrackText(cap, 15f, MenuBarMinFontPx);
            SetAnchors(cap.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-44f, 0f));

            // 最小化／復元ボタン（右上の「—」）。タイトルバーだけ残してマップ表示を畳む。
            BuildMinimizeButton(bar);
            BuildViewButtons(bar);   // 全体表示／ズーム（マウスだけで縮尺を操作できるようにする）
            // 配置パネルは常設せず「配置」ボタンで開閉する（実機QA：右カラムの常設だと決裁デスクに隠れ、
            // 超横長では他パネルと重なって押せなかった）。開いたときに実画面から組み直して常に前面へ出す。

            // 中身領域（透明＝マップを見せる・クリックを塞がない）。アンカーは ApplyLayout で mapRect に合わせる。
            contentRT = new GameObject("MapContent").AddComponent<RectTransform>();
            contentRT.transform.SetParent(root, false);

            BuildLegend(contentRT);  // 色と線の意味を読み取れるようにする（#戦略MAP刷新）

            // 縁取り（細い金色バー・raycast しない）
            Color edge = new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f);
            edgeLeft = MakeEdge(root, edge);
            edgeRight = MakeEdge(root, edge);
            edgeBottom = MakeEdge(root, edge);

            // 右下リサイズグリップ（つかんで窓の大きさを変える）。raycast を受けてドラッグを拾う。
            var gripGo = new GameObject("MapResizeGrip");
            gripGo.transform.SetParent(root, false);
            resizeGripRT = gripGo.AddComponent<RectTransform>();
            var gripImg = gripGo.AddComponent<Image>();
            gripImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.85f);
            gripImg.raycastTarget = true;
            var gripDrag = gripGo.AddComponent<MapWindowDrag>();
            gripDrag.onDragDelta = OnResizeDrag;
        }

        /// <summary>
        /// 配置パネルの開閉（#MAPドラッグが効かない）。開くたびに<b>実画面から組み直す</b>ので、
        /// 解像度を変えたあとでも文字が潰れず、画面内に収まる（実機QA：縦長で 6px になった）。
        /// 常設ではなく前面の一時パネル＝決裁デスクや勝敗メーターと重ならない。
        /// </summary>
        private void ToggleNudgePanel()
        {
            if (nudgePanel != null) { Destroy(nudgePanel); nudgePanel = null; return; }
            nudgePanel = BuildNudgePanel();
        }

        /// <summary>配置パネルを実画面サイズから組む（前面Canvas・画面内クランプ・実ピクセル下限）。</summary>
        private GameObject BuildNudgePanel()
        {
            var canvasObj = new GameObject("WindowNudgeCanvas");
            canvasObj.transform.SetParent(transform, false);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 899;  // 通知/決裁/バッジより前・モーダル(900+)より後ろ
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 実ピクセルの下限を課す（幅基準スケールなので縦長・小窓では設計値が縮む）。
            float font = StrategyScreenLayoutRules.MinDesignForActual(14f, 14f, Screen.width);
            float w = StrategyScreenLayoutRules.MinDesignForActual(300f, 260f, Screen.width);
            float h = StrategyScreenLayoutRules.MinDesignForActual(210f, 190f, Screen.width);
            nudgeFontSize = font;

            var go = new GameObject("WindowNudgePanel", typeof(RectTransform));
            go.transform.SetParent(canvasObj.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(w, h);

            // 画面中央やや上に出し、画面内へクランプ（どの比率でも触れる位置に置く）。
            float uiScale = Screen.width > 0 ? Screen.width / 1920f : 1f;
            float wPx = w * uiScale, hPx = h * uiScale;
            float x = Mathf.Clamp(Screen.width * 0.5f - wPx * 0.5f, 4f, Mathf.Max(4f, Screen.width - wPx - 4f));
            float y = Mathf.Clamp(Screen.height * 0.55f, 4f, Mathf.Max(4f, Screen.height - hPx - 4f));
            rt.anchoredPosition = new Vector2(x / uiScale, y / uiScale);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.11f, 0.17f, 0.97f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.75f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var cap = AddText(rt, "マップ窓の配置", font + 1f, accentColor, TextAlignmentOptions.Center);
            SetAnchors(cap.rectTransform, new Vector2(0f, 0.80f), new Vector2(0.78f, 1f), Vector2.zero, Vector2.zero);
            cap.raycastTarget = false;
            NudgeButton(rt, "閉", 0.80f, 0.80f, 0.18f, 0.18f, ToggleNudgePanel);

            const float step = 0.03f;    // 画面比 3%
            NudgeButton(rt, "左", 0.02f, 0.55f, 0.23f, 0.22f, () => Nudge(-step, 0f));
            NudgeButton(rt, "上", 0.27f, 0.55f, 0.23f, 0.22f, () => Nudge(0f, +step));
            NudgeButton(rt, "下", 0.52f, 0.55f, 0.23f, 0.22f, () => Nudge(0f, -step));
            NudgeButton(rt, "右", 0.77f, 0.55f, 0.21f, 0.22f, () => Nudge(+step, 0f));

            NudgeButton(rt, "幅－", 0.02f, 0.30f, 0.23f, 0.22f, () => Resize(-step, 0f));
            NudgeButton(rt, "幅＋", 0.27f, 0.30f, 0.23f, 0.22f, () => Resize(+step, 0f));
            NudgeButton(rt, "高－", 0.52f, 0.30f, 0.23f, 0.22f, () => Resize(0f, -step));
            NudgeButton(rt, "高＋", 0.77f, 0.30f, 0.21f, 0.22f, () => Resize(0f, +step));

            NudgeButton(rt, "既定に戻す", 0.02f, 0.04f, 0.96f, 0.22f, ResetWindow);
            return canvasObj;
        }

        private void NudgeButton(RectTransform parent, string label, float x, float y, float w, float h, System.Action onClick)
        {
            var go = new GameObject("Nudge_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x, y);
            rt.anchorMax = new Vector2(x + w, y + h);
            rt.offsetMin = new Vector2(1f, 1f); rt.offsetMax = new Vector2(-1f, -1f);
            var img = go.AddComponent<Image>();
            img.color = buttonColor;
            var btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            var cap = AddText(rt, label, nudgeFontSize, new Color(0.9f, 0.94f, 1f), TextAlignmentOptions.Center);
            SetAnchors(cap.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cap.raycastTarget = false;
        }

        /// <summary>窓を平行移動（画面比）。</summary>
        private void Nudge(float dx, float dy)
        {
            mapRect.x += dx; mapRect.y += dy;
            userMovedWindow = true;
            ApplyLayout();
        }

        /// <summary>窓の大きさを変える（上端は固定＝上メニュー側を動かさない）。</summary>
        private void Resize(float dw, float dh)
        {
            float topY = mapRect.yMax;
            mapRect.width = Mathf.Max(minWindowFrac, mapRect.width + dw);
            mapRect.height = Mathf.Max(minWindowFrac, mapRect.height + dh);
            mapRect.y = topY - mapRect.height;
            userMovedWindow = true;
            ApplyLayout();
        }

        /// <summary>
        /// マップ左下に凡例を置く（#戦略MAP刷新）。色と線の意味（陣営・要衝・通商路・選択）を明示して、
        /// 「何を見ているか」を説明なしで読み取れるようにする。<b>クリックは透過</b>させ盤面操作を妨げない。
        /// </summary>
        private void BuildLegend(RectTransform parent)
        {
            var go = new GameObject("MapLegend");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(12f, 12f);
            rt.sizeDelta = new Vector2(250f, 122f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.06f, 0.11f, 0.72f); // 濃紺の航宙図に沈む半透明の板
            bg.raycastTarget = false;                          // 盤面のクリック/ドラッグを塞がない

            var body = AddText(rt,
                "<color=#67A7F2>●</color> 同盟　<color=#EF7567>●</color> 帝国\n" +
                "<color=#FAD26B>━</color> 要衝（隘路・前線）\n" +
                "<color=#8E9BB8>─</color> 通商路\n" +
                "<color=#FAD26B>○</color> 選択中\n" +
                // ★プレイヤーに見せる艦隊規模は艦艇数（隻）だけ＝凡例の例文も実艦艇数で書く
                // （FleetClusterRules.MarkerLabel が出す形と同じ。「兵力」は内部の戦闘計算専用で画面に出さない）。
                "<color=#9FB4CC>まとまり</color> 例「3艦隊 48,000隻」＝押すと一覧",
                14f, new Color(0.86f, 0.9f, 0.98f), TextAlignmentOptions.TopLeft);
            TrackText(body, 14f, MenuBarMinFontPx);
            SetAnchors(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 6f), new Vector2(-8f, -6f));
            body.raycastTarget = false;
        }

        /// <summary>
        /// タイトルバー右端（最小化の左）に「全体表示 / ＋ / −」を作る（#戦略MAP刷新）。
        /// ホイールが使えない環境や、引きすぎて迷子になったときにマウスだけで縮尺を戻せる導線。
        /// </summary>
        private void BuildViewButtons(Transform titleBar)
        {
            // ★実ピクセルで 14px を下回らない文字にし、ボタン幅と並び位置も同じ比率で広げる
            // （実機QA：縦長 900 幅／1024 幅で「全体」が 6〜8px になり読めなかった）。
            // 右端の最小化ボタンから左へ、拡大した幅を足しながら並べる＝重ならない。
            // 位置と幅は<b>設計値のまま</b>渡し、実際の倍率は TrackBox が掛ける（解像度変更にも追従する）。
            float x = -4f - 38f - 4f;              // 最小化ボタン（幅38・右端から4）の左

            MakeTitleBarButton(titleBar, "−", 30f, x, () => Galaxy()?.NudgeZoom(1.25f));
            x -= 30f + 4f;
            MakeTitleBarButton(titleBar, "＋", 30f, x, () => Galaxy()?.NudgeZoom(0.8f));
            x -= 30f + 4f;
            MakeTitleBarButton(titleBar, "全体", 52f, x, () => Galaxy()?.FitAll());
        }

        /// <summary>タイトルバー右寄せの小ボタン（右端から offsetX だけ左へ置く）。</summary>
        private void MakeTitleBarButton(Transform titleBar, string label, float width, float offsetX, System.Action onClick)
        {
            var go = new GameObject("TitleBarButton_" + label);
            go.transform.SetParent(titleBar, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(width, -6f);
            rt.anchoredPosition = new Vector2(offsetX, 0f);
            TrackBox(rt, width, offsetX);   // 幅と並び位置を実画面に合わせて拡大（以後の解像度変更にも追従）
            var img = go.AddComponent<Image>();
            img.color = buttonColor;
            var btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None;
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var cap = AddText(rt, label, 15f, accentColor, TextAlignmentOptions.Center);
            TrackText(cap, 15f, MenuBarMinFontPx);
            SetAnchors(cap.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cap.raycastTarget = false;
        }

        /// <summary>盤面（GalaxyView）を引く。シーンに1つ＝毎回探しても軽い（ボタン押下時のみ）。</summary>
        private static GalaxyView Galaxy() => UnityEngine.Object.FindAnyObjectByType<GalaxyView>();

        /// <summary>タイトルバー右端に最小化／復元ボタン（—／＋）を作る。</summary>
        private void BuildMinimizeButton(Transform titleBar)
        {
            var go = new GameObject("MinimizeButton");
            go.transform.SetParent(titleBar, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            // 「全体／＋／−」と同じ倍率で広げる（BuildViewButtons の並び計算と一致させる）。
            rt.sizeDelta = new Vector2(38f, 0f);
            rt.anchoredPosition = new Vector2(-4f, 0f);
            TrackBox(rt, 38f, -4f);
            var img = go.AddComponent<Image>();
            img.color = buttonColor;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(ToggleMinimize);
            minimizeLabel = AddText(go.transform, "—", 18f, accentColor, TextAlignmentOptions.Center);
            TrackText(minimizeLabel, 18f, MenuBarMinFontPx);
            minimizeLabel.fontStyle = FontStyles.Bold;
            SetAnchors(minimizeLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        /// <summary>最小化／復元を切り替える。最小化中はマップ描画を畳み（カメラ非描画）、タイトルバーだけ残す。</summary>
        public void ToggleMinimize()
        {
            minimized = !minimized;
            if (cam != null)
            {
                if (minimized)
                {
                    // マップを描かず背景デスクトップ（黒）を見せる＝タイトルバーだけ残った最小化状態。
                    savedCullingMask = cam.cullingMask;
                    savedClearFlags = cam.clearFlags;
                    cam.cullingMask = 0;
                    cam.clearFlags = CameraClearFlags.Depth;
                }
                else
                {
                    cam.cullingMask = savedCullingMask;
                    cam.clearFlags = savedClearFlags;
                }
            }
            if (minimizeLabel != null) minimizeLabel.text = minimized ? "＋" : "—";
            ApplyLayout();
        }

        private static RectTransform MakeEdge(Transform parent, Color color)
        {
            var go = new GameObject("Edge");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            img.color = color; img.raycastTarget = false;
            return rt;
        }

        // ===== 目標（勝利進捗＋次の一手）＝B：目的可視化 =====

        /// <summary>現在の難易度（GameSettings）に応じた制覇しきい値（進捗バーのマーカー/着色に使う）。</summary>
        private static float ActiveDominationFraction()
            => CampaignDifficultyRules.VictoryParams(
                GameSettings.Instance != null ? GameSettings.Instance.campaignDifficulty : CampaignDifficulty.普通).dominationFraction;

        private void BuildObjectiveRow(Transform bar)
        {
            var row = new GameObject("ObjectiveRow").AddComponent<RectTransform>();
            row.transform.SetParent(bar, false);
            row.anchorMin = new Vector2(0f, 0.34f); row.anchorMax = new Vector2(1f, 0.64f);
            row.offsetMin = new Vector2(20f, 0f); row.offsetMax = new Vector2(-20f, 0f);

            // 勝利進捗バー（左）：背景＋フィル＋しきい値マーカー。上に進捗テキストを重ねる。
            var barBg = new GameObject("VictoryBarBg").AddComponent<RectTransform>();
            barBg.transform.SetParent(row, false);
            SetAnchors(barBg, new Vector2(0f, 0.12f), new Vector2(0.46f, 0.88f), Vector2.zero, Vector2.zero);
            var bgImg = barBg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.5f); bgImg.raycastTarget = false;

            objectiveFillRT = new GameObject("Fill").AddComponent<RectTransform>();
            objectiveFillRT.transform.SetParent(barBg, false);
            objectiveFillRT.anchorMin = new Vector2(0f, 0f);
            objectiveFillRT.anchorMax = new Vector2(0f, 1f); // 幅は UpdateObjective で支配率に
            objectiveFillRT.offsetMin = Vector2.zero; objectiveFillRT.offsetMax = Vector2.zero;
            var fillImg = objectiveFillRT.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.35f, 0.7f, 0.95f, 0.9f); fillImg.raycastTarget = false;

            // 勝利しきい値（難易度連動）の縦マーカー。
            float winMark = ActiveDominationFraction();
            var mark = new GameObject("WinMark").AddComponent<RectTransform>();
            mark.transform.SetParent(barBg, false);
            mark.anchorMin = new Vector2(winMark, 0f); mark.anchorMax = new Vector2(winMark, 1f);
            mark.pivot = new Vector2(0.5f, 0.5f); mark.sizeDelta = new Vector2(2f, 0f);
            var markImg = mark.gameObject.AddComponent<Image>();
            markImg.color = new Color(1f, 0.84f, 0.36f, 0.9f); markImg.raycastTarget = false;

            objectiveLabel = AddText(barBg, "", 14f, Color.white, TextAlignmentOptions.Center);
            SetAnchors(objectiveLabel.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // 次の一手ヒント（右）。
            hintLabel = AddText(row, "", 15f, new Color(0.95f, 0.9f, 0.6f), TextAlignmentOptions.Left);
            SetAnchors(hintLabel.rectTransform, new Vector2(0.48f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), Vector2.zero);
        }

        /// <summary>勝利進捗バー＋次の一手を更新する（盤面シグナルから・間引き）。</summary>
        private void UpdateObjective()
        {
            if (objectiveFillRT == null) return;
            GalaxyMap map = StrategySession.Map;
            if (map == null) { if (objectiveLabel != null) objectiveLabel.text = ""; if (hintLabel != null) hintLabel.text = ""; return; }

            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            int total = CampaignVictoryRules.TotalSystems(map);
            int owned = CampaignVictoryRules.OwnedCount(map, pf);
            float frac = CampaignVictoryRules.OwnedFraction(map, pf);
            float winFrac = ActiveDominationFraction();
            bool rivalsRemain = CampaignVictoryRules.RivalSystemsRemain(map, pf);

            objectiveFillRT.anchorMax = new Vector2(Mathf.Clamp01(frac), 1f);
            var fillImg = objectiveFillRT.GetComponent<Image>();
            if (fillImg != null)
            {
                // 勝利目前=金／守勢(支配≦15%)=赤／通常=青。
                fillImg.color = frac >= winFrac - 0.1f ? new Color(1f, 0.84f, 0.36f, 0.95f)
                    : frac <= 0.15f ? new Color(0.95f, 0.45f, 0.4f, 0.95f)
                    : new Color(0.35f, 0.7f, 0.95f, 0.9f);
            }
            if (objectiveLabel != null)
                objectiveLabel.text = $"制覇 {Mathf.RoundToInt(frac * 100f)}% / {Mathf.RoundToInt(winFrac * 100f)}%（{pf} {owned} / {total} 星系）";

            // 次の一手（Core が選び、Game が文言＋キーへ）。
            CountFleetSignals(pf, out bool hasEngagement, out int idleFleets);
            CampaignHint hint = CampaignGuidanceRules.NextAction(hasEngagement, idleFleets, rivalsRemain);
            if (hintLabel != null) hintLabel.text = "▶ " + HintText(hint);
        }

        /// <summary>プレイヤー艦隊の交戦中の有無・遊休数を数える（次の一手のシグナル）。</summary>
        private void CountFleetSignals(Faction pf, out bool hasEngagement, out int idleFleets)
        {
            hasEngagement = false; idleFleets = 0;
            StrategicFleetRegistry reg = StrategySession.Reg;
            if (reg == null || reg.fleets == null) return;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != pf) continue;
                if (f.engaged) hasEngagement = true;
                else if (!f.IsMoving && f.strength > 0) idleFleets++;
            }
        }

        private static string HintText(CampaignHint hint)
        {
            switch (hint)
            {
                case CampaignHint.前線へ潜行: return "交戦中の回廊をダブルクリックで潜行（手動指揮）";
                case CampaignHint.任務を発令: return "C: 攻略任務を発令 ／ B: 艦艇観測";
                case CampaignHint.領土を広げよ: return "艦隊を選んで右クリックで敵星系へ進軍";
                default: return "好機を待つ";
            }
        }

        // ===== 部品 =====

        // ===== 観測ウィンドウ（上メニュー集約・タブ化） =====

        private GameObject observerWindow;
        private readonly List<GameObject> tabPages = new List<GameObject>();
        private readonly List<Button> tabButtons = new List<Button>();
        private int activeTab;

        /// <summary>観測カテゴリ（5タブ）と各タブの項目（ラベル→該当オブザーバの Toggle）。集約の単一定義。</summary>
        private (string name, (string label, System.Action action)[] items)[] ObserverCategories()
            => new (string, (string, System.Action)[])[]
        {
            ("内政", new (string, System.Action)[]
            {
                ("勢力",   () => UnityEngine.Object.FindAnyObjectByType<CampaignObserverOverlay>()?.Toggle()),
                ("法令",   () => UnityEngine.Object.FindAnyObjectByType<LawObserverOverlay>()?.Toggle()),
                ("政府",   () => UnityEngine.Object.FindAnyObjectByType<GovernmentObserverOverlay>()?.Toggle()),
                ("官僚",   () => UnityEngine.Object.FindAnyObjectByType<BureaucracyObserverOverlay>()?.Toggle()),
                ("人口",   () => UnityEngine.Object.FindAnyObjectByType<DemographicsObserverOverlay>()?.Toggle()),
                ("労働",   () => UnityEngine.Object.FindAnyObjectByType<LaborObserverOverlay>()?.Toggle()),
                ("教育",   () => UnityEngine.Object.FindAnyObjectByType<EducationObserverOverlay>()?.Toggle()),
                ("事象",   () => UnityEngine.Object.FindAnyObjectByType<ChronicleObserverOverlay>()?.Toggle()),
            }),
            ("経済", new (string, System.Action)[]
            {
                ("財政",   () => UnityEngine.Object.FindAnyObjectByType<EconomyObserverOverlay>()?.Toggle()),
                ("財政詳", () => UnityEngine.Object.FindAnyObjectByType<FiscalObserverOverlay>()?.Toggle()),
                ("生産",   () => UnityEngine.Object.FindAnyObjectByType<ProductionObserverOverlay>()?.Toggle()),
                ("兵站",   () => UnityEngine.Object.FindAnyObjectByType<LogisticsObserverOverlay>()?.Toggle()),
                ("造船",   () => UnityEngine.Object.FindAnyObjectByType<ShipyardObserverOverlay>()?.Toggle()),
                ("研究",   () => UnityEngine.Object.FindAnyObjectByType<ResearchObserverOverlay>()?.Toggle()),
            }),
            ("軍事", new (string, System.Action)[]
            {
                ("軍事",   () => UnityEngine.Object.FindAnyObjectByType<MilitaryObserverOverlay>()?.Toggle()),
                ("艦艇",   () => UnityEngine.Object.FindAnyObjectByType<FleetObserverOverlay>()?.Toggle()),
                ("人物動", () => UnityEngine.Object.FindAnyObjectByType<PersonnelDynamicsObserverOverlay>()?.Toggle()),
            }),
            ("政治", new (string, System.Action)[]
            {
                ("政治",   () => UnityEngine.Object.FindAnyObjectByType<PoliticsObserverOverlay>()?.Toggle()),
                ("外交",   () => UnityEngine.Object.FindAnyObjectByType<DiplomacyObserverOverlay>()?.Toggle()),
                ("人事",   () => UnityEngine.Object.FindAnyObjectByType<PersonObserverOverlay>()?.Toggle()),
                // 内閣人事（#2768）：首相の閣僚任免・大臣の副大臣への委任をクリックで操作する窓
                ("内閣人事", CabinetAppointmentPanel.Toggle),
            }),
            ("システム", new (string, System.Action)[]
            {
                ("決裁",     () => UnityEngine.Object.FindAnyObjectByType<DecisionBoardPanel>()?.Toggle()),
                ("稟議",     () => UnityEngine.Object.FindAnyObjectByType<RingiObserverOverlay>()?.Toggle()),
                // 「執務机」はコマンドバーの専用ボタンへ格上げ（BuildMenuBar）＝ここからは除去（重複回避）。
                ("メーター", () => UnityEngine.Object.FindAnyObjectByType<DecisionCampaignDirector>()?.Toggle()),
                ("情報",     () => UnityEngine.Object.FindAnyObjectByType<CoreStateInspector>()?.Toggle()),
                ("通知",     () => UnityEngine.Object.FindAnyObjectByType<NotificationLogOverlay>()?.Toggle()),
                ("ヘルプ",   () => UnityEngine.Object.FindAnyObjectByType<HelpOverlay>()?.Toggle()),
            }),
        };

        /// <summary>
        /// 観測ウィンドウを組む（上メニュー集約・タブ化）。1枚のドラッグ可能な窓に 5タブ（内政/経済/軍事/政治/
        /// システム）を並べ、タブで中身を切り替えて各オブザーバを開く。独立 Canvas（overrideSorting=872）で
        /// マップより前面・初期は閉。各項目は既存オブザーバの Toggle() を呼ぶだけ（オブザーバ窓は不変）。
        /// </summary>
        private void BuildObserverWindow(Transform root)
        {
            var cats = ObserverCategories();

            var winGo = new GameObject("ObserverWindow");
            winGo.transform.SetParent(root, false);
            var win = winGo.AddComponent<RectTransform>();
            win.anchorMin = win.anchorMax = new Vector2(0.5f, 0.5f);
            win.pivot = new Vector2(0.5f, 0.5f);
            win.sizeDelta = new Vector2(584f, 380f);
            win.anchoredPosition = new Vector2(0f, 40f);
            var winCanvas = winGo.AddComponent<Canvas>();
            winCanvas.overrideSorting = true; winCanvas.sortingOrder = 872;
            winGo.AddComponent<GraphicRaycaster>();
            var winBg = winGo.AddComponent<Image>();
            winBg.color = new Color(menuBarColor.r, menuBarColor.g, menuBarColor.b, 0.99f);

            // タイトルバー（つかんで移動・×で閉じる）
            var titleGo = new GameObject("TitleBar");
            titleGo.transform.SetParent(win, false);
            var titleRT = titleGo.AddComponent<RectTransform>();
            SetAnchors(titleRT, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, 0f));
            var titleImg = titleGo.AddComponent<Image>();
            titleImg.color = titleBarColor;
            var drag = titleGo.AddComponent<UIDragMove>();
            drag.target = win;
            var titleTxt = AddText(titleGo.transform, "≡ 観測ウィンドウ", 15f, accentColor, TextAlignmentOptions.Left);
            SetAnchors(titleTxt.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-40f, 0f));
            var closeBtn = MakeButton(titleGo.transform, "Close", "×", 18f, TextAlignmentOptions.Center,
                () => { if (winGo != null) winGo.SetActive(false); }, out _);
            SetAnchors((RectTransform)closeBtn.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-32f, 3f), new Vector2(-4f, -3f));

            // タブ行
            var tabRowGo = new GameObject("TabRow");
            tabRowGo.transform.SetParent(win, false);
            var tabRT = tabRowGo.AddComponent<RectTransform>();
            SetAnchors(tabRT, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -66f), new Vector2(-6f, -32f));
            var tabHlg = tabRowGo.AddComponent<HorizontalLayoutGroup>();
            tabHlg.spacing = 4f; tabHlg.childAlignment = TextAnchor.MiddleCenter;
            tabHlg.childControlWidth = true; tabHlg.childControlHeight = true;
            tabHlg.childForceExpandWidth = true; tabHlg.childForceExpandHeight = true;

            // コンテンツ領域（各ページを重ね active のみ表示）
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(win, false);
            var contentRT2 = contentGo.AddComponent<RectTransform>();
            SetAnchors(contentRT2, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 6f), new Vector2(-6f, -70f));

            for (int c = 0; c < cats.Length; c++)
            {
                int idx = c;
                var tabBtn = MakeButton(tabRowGo.transform, "Tab_" + cats[c].name, cats[c].name, 15f,
                    TextAlignmentOptions.Center, () => SetActiveTab(idx), out _);
                tabButtons.Add(tabBtn);

                var pageGo = new GameObject("Page_" + cats[c].name);
                pageGo.transform.SetParent(contentGo.transform, false);
                var pageRT = pageGo.AddComponent<RectTransform>();
                SetAnchors(pageRT, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var grid = pageGo.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(130f, 34f);
                grid.spacing = new Vector2(8f, 8f);
                grid.padding = new RectOffset(6, 6, 6, 6);
                grid.childAlignment = TextAnchor.UpperLeft;

                var items = cats[c].items;
                for (int k = 0; k < items.Length; k++)
                {
                    System.Action act = items[k].action; // クロージャ捕捉
                    MakeButton(pageGo.transform, "It_" + items[k].label, items[k].label, 15f,
                        TextAlignmentOptions.Center, () => act?.Invoke(), out _);
                }
                tabPages.Add(pageGo);
            }

            observerWindow = winGo;
            observerWindow.SetActive(false);
            SetActiveTab(0);
        }

        /// <summary>観測ウィンドウの開閉（上メニューの「観測」ボタン）。開くとき最前面へ。</summary>
        private void ToggleObserverWindow()
        {
            if (observerWindow == null) return;
            bool open = !observerWindow.activeSelf;
            observerWindow.SetActive(open);
            if (open) observerWindow.transform.SetAsLastSibling();
        }

        /// <summary>アクティブタブを切り替える（該当ページのみ表示・タブの強調を更新）。</summary>
        private void SetActiveTab(int idx)
        {
            activeTab = idx;
            for (int i = 0; i < tabPages.Count; i++)
                if (tabPages[i] != null) tabPages[i].SetActive(i == idx);
            for (int i = 0; i < tabButtons.Count; i++)
            {
                if (tabButtons[i] == null) continue;
                var cb = tabButtons[i].colors;
                cb.normalColor = (i == idx) ? new Color(0.27f, 0.45f, 0.68f, 1f) : buttonColor;
                cb.selectedColor = cb.normalColor;
                tabButtons[i].colors = cb;
            }
        }

        /// <summary>上メニューバーの固定幅ボタン（HLG 内・LayoutElement で幅を固定）。</summary>
        /// <summary>
        /// 上段の主要操作ボタン。<b>実ピクセルで 14px を下回らない文字</b>にし、幅も同じ比率で広げる
        /// （実機QA：縦長 900 幅や 1024 幅で 6〜8px になり読めなかった）。
        /// Canvas は幅基準スケールなので、画面が狭いほど設計値を積まないと実寸が保てない。
        /// </summary>
        private void MakeBarButton(Transform parent, string label, float width, System.Action onClick)
        {
            float font = StrategyScreenLayoutRules.MinDesignForActual(16f, MenuBarMinFontPx, Screen.width);

            var btn = MakeButton(parent, "Cmd_" + label, label, font, TextAlignmentOptions.Center, onClick, out _);
            TrackText(btn.GetComponentInChildren<TMP_Text>(), 16f, MenuBarMinFontPx);

            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 0f; le.flexibleHeight = 0f;
            // 幅は「文字を大きくしたぶん」と「ラベルの長さ」の大きいほう、
            // 高さは文字に比例（親の行高に依存させると折り返し時に発散する）。ApplyCmd が唯一の式。
            var e = new ScaledCmd { le = le, label = label, baseW = width };
            scaledCmds.Add(e);
            ApplyCmd(e);
        }

        /// <summary>上段の主要操作の実ピクセル最小文字サイズ（可読性の下限）。</summary>
        private const float MenuBarMinFontPx = 14f;

        // ===== 解像度が変わっても実ピクセルを保つための追従（#低解像度での可読性）=====
        // 上段のボタン/見出しは生成時の Screen.width で倍率を決めるので、Play 中に解像度を
        // 切り替えると<b>古い倍率のまま</b>残る（実機QA：縦長へ切替後も 6〜8px のまま）。
        // 作った要素と「元の設計値」を控えておき、切替時に同じ式で計算し直す。

        private sealed class ScaledText { public TMP_Text text; public float baseFont; public float minPx; }
        private sealed class ScaledBox { public RectTransform rt; public float baseW; public float baseOffsetX; }
        private sealed class ScaledCmd { public LayoutElement le; public string label; public float baseW; }

        private readonly List<ScaledText> scaledTexts = new List<ScaledText>();
        private readonly List<ScaledBox> scaledBoxes = new List<ScaledBox>();
        private readonly List<ScaledCmd> scaledCmds = new List<ScaledCmd>();

        /// <summary>タイトルバーの小物（幅・並び位置）を広げる倍率。文字の下限確保と同じ比率にする。</summary>
        private static float BarScale =>
            StrategyScreenLayoutRules.MinDesignForActual(15f, MenuBarMinFontPx, Screen.width) / 15f;

        /// <summary>文字を実ピクセル下限つきで設定し、以後の解像度変更にも追従させる。</summary>
        private TMP_Text TrackText(TMP_Text t, float baseFont, float minPx)
        {
            if (t == null) return null;
            var e = new ScaledText { text = t, baseFont = baseFont, minPx = minPx };
            scaledTexts.Add(e);
            ApplyText(e);
            return t;
        }

        private static void ApplyText(ScaledText e)
        {
            if (e.text == null) return;
            e.text.fontSize = StrategyScreenLayoutRules.MinDesignForActual(e.baseFont, e.minPx, Screen.width);
        }

        /// <summary>タイトルバー小ボタンの幅と右端からの位置を倍率つきで設定する。</summary>
        private void TrackBox(RectTransform rt, float baseW, float baseOffsetX)
        {
            if (rt == null) return;
            var e = new ScaledBox { rt = rt, baseW = baseW, baseOffsetX = baseOffsetX };
            scaledBoxes.Add(e);
            ApplyBox(e);
        }

        private static void ApplyBox(ScaledBox e)
        {
            if (e.rt == null) return;
            float k = BarScale;
            e.rt.sizeDelta = new Vector2(e.baseW * k, e.rt.sizeDelta.y);
            e.rt.anchoredPosition = new Vector2(e.baseOffsetX * k, e.rt.anchoredPosition.y);
        }

        private static void ApplyCmd(ScaledCmd e)
        {
            if (e.le == null) return;
            float font = StrategyScreenLayoutRules.MinDesignForActual(16f, MenuBarMinFontPx, Screen.width);
            float w = Mathf.Max(e.baseW * (font / 16f), e.label.Length * font * 1.15f + 22f);
            float h = font * 1.8f + 6f;
            e.le.minWidth = w; e.le.preferredWidth = w;
            e.le.minHeight = h; e.le.preferredHeight = h;
        }

        /// <summary>解像度が変わったとき、上段の文字とボタンを新しい実画面で計算し直す。</summary>
        private void RescaleMenuBar()
        {
            for (int i = 0; i < scaledTexts.Count; i++) ApplyText(scaledTexts[i]);
            for (int i = 0; i < scaledBoxes.Count; i++) ApplyBox(scaledBoxes[i]);
            for (int i = 0; i < scaledCmds.Count; i++) ApplyCmd(scaledCmds[i]);
            if (cmdWrap != null) LayoutRebuilder.MarkLayoutForRebuild(cmdWrap.transform as RectTransform);
        }

        /// <summary>マップ窓タイトルバーの高さ（設計px）。狭い画面ほど積み増して「全体」等の実寸を確保する。</summary>
        private float TitleBarDesign =>
            StrategyScreenLayoutRules.MinDesignForActual(mapTitleHeight, 26f, Screen.width);

        /// <summary>同・実画面px。<see cref="Camera.rect"/> の割り付けは画面基準なので設計px から換算する。</summary>
        private float TitleBarActualPx =>
            TitleBarDesign * (Screen.width > 1f ? Screen.width / StrategyScreenLayoutRules.ReferenceWidth : 1f);

        /// <summary>汎用ボタン（背景＋ラベル＋色遷移）。<paramref name="bg"/> で背景 Image を受け取る（タブ強調等）。</summary>
        private Button MakeButton(Transform parent, string name, string label, float fontSize,
            TextAlignmentOptions align, System.Action onClick, out Image bg)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            bg = go.AddComponent<Image>();
            bg.color = Color.white;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            var cb = btn.colors;
            cb.normalColor = buttonColor;
            cb.highlightedColor = new Color(0.30f, 0.48f, 0.70f, 1f);
            cb.pressedColor = new Color(0.20f, 0.36f, 0.58f, 1f);
            cb.selectedColor = buttonColor;
            cb.fadeDuration = 0.05f;
            btn.colors = cb;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            float padL = align == TextAlignmentOptions.Left ? 10f : 4f;
            var t = AddText(go.transform, label, fontSize, new Color(0.92f, 0.95f, 1f), align);
            SetAnchors(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(padL, 0f), new Vector2(-4f, 0f));
            return btn;
        }

        private static Image AddBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offMin; rt.offsetMax = offMax;
        }

        private static TextMeshProUGUI AddText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) tmp.font = ja;
            return tmp;
        }
    }
}
