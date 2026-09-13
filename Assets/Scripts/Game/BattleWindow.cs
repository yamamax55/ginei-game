using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 1会戦ぶんのウィンドウ（WIN-1 #2568 / WIN-3 #2570 で複数同時対応）。戦略マップを背後に残したまま Battle シーンを
    /// additive ロード（独立 2D 物理）し、その専用カメラを <see cref="RenderTexture"/> に描いて RawImage 窓へ映す。
    /// タイトルバーでドラッグ移動・× で離脱（BattleManager 経由）・決着でも閉じてシーンをアンロードする。
    /// 生成・直列ロード・オフセット割当・フォーカス・結果反映は <see cref="BattleDirector"/> が司る。
    /// 入力は <see cref="BattleViewport"/>（フォーカス窓のみ）が画面→会戦ワールドへ変換する。
    /// 既存のフルスクリーン会戦は <c>GameSettings.windowedBattles=false</c> で従来どおり（後方互換）。
    /// 会戦 UI の窓内帰属（WIN-4 #2571）：HUD/コマンドメニュー/ミニマップは <see cref="BattleWindowUI"/> 経由で
    /// この窓の <c>BattleUIRoot</c>（RawImage 矩形・RectMask2D クリップ）へ親替えされ、複数窓で重ならない。
    /// 通知は <see cref="NotificationFeed"/> が単一（戦略シーンに1つ）なので重複なし。一時停止/倍速は統一クロック
    /// （<see cref="BattleDirector"/>）が全会戦を駆動し、会戦ごとの <see cref="PauseManager"/> は窓モードで抑止される。
    /// </summary>
    public class BattleWindow : MonoBehaviour
    {
        [Header("ウィンドウ")]
        [Tooltip("窓の希望サイズ(px)。実画面（上メニュー帯を除く）に収まるよう起動時にクランプする")]
        public Vector2 windowSize = new Vector2(1040f, 660f);
        [Tooltip("RenderTexture の初期サイズ（実表示サイズが取れないときのフォールバック）")]
        public int rtWidth = 1040;
        public int rtHeight = 630;

        private const float TitleBarHeight = 30f;
        /// <summary>窓の最小/最大サイズ（px）。</summary>
        private const float MinWindowWidth = 420f, MinWindowHeight = 280f;
        private const float MaxWindowWidth = 1920f, MaxWindowHeight = 1080f;
        /// <summary>画面端に残す余白（px）。窓が端に貼り付いて掴めなくなるのを防ぐ。</summary>
        private const float ScreenMargin = 8f;
        /// <summary>右下リサイズグリップの一辺（px）。小さすぎると掴めないので実機で狙える大きさにする。</summary>
        private const float GripSize = 28f;
        /// <summary>拡縮が落ち着いてから RenderTexture を作り直すまでの待ち（実時間秒）。</summary>
        private const float RtSettleSeconds = 0.18f;
        /// <summary>タイトルバー右端に置く時計/速度の幅（px）。</summary>
        private const float ClockWidth = 250f;

        private bool isOpen;
        private GameObject root;
        private RectTransform windowRT;
        private RawImage mapImage;
        private RectTransform mapRT;
        private RectTransform battleUIRoot; // 窓内の会戦UI親（RawImage矩形に重なる・RectMask2Dでクリップ・WIN-4）
        private RenderTexture rt;
        private Camera battleCam;
        private Scene battleScene;
        private bool sceneLoaded;
        private object escWindowToken;
        private TextMeshProUGUI titleCap;
        private TextMeshProUGUI clockCap;   // タイトルバー右端の時計/速度（戦略の上メニューと同じ作り分け）
        private System.Action<BattleWindow> onLoaded;
        private System.Action<BattleWindow> onClosed;
        private string title = "会戦";

        // 窓枠の追従用。
        private bool handleGrabbed;         // 自窓のタイトルバー/グリップを掴んでいる（背後へ入力を渡さない印）
        private bool rtDirty;               // 表示サイズが変わり RenderTexture を作り直したい
        private float rtSettleTimer;        // 拡縮が落ち着くまでの待ち（毎フレーム作り直さない）
        private Vector2Int lastScreenSize;  // 解像度変化の検出

        /// <summary>この窓が開いているか。</summary>
        public bool IsOpen => isOpen;
        /// <summary>この窓の会戦シーン。</summary>
        public Scene BattleScene => battleScene;
        /// <summary>この窓の会戦カメラ。</summary>
        public Camera Cam => battleCam;
        /// <summary>この窓のマップ領域 RectTransform（入力変換用）。</summary>
        public RectTransform MapRect => mapRT;
        /// <summary>シーンのロードと描画束ねが完了したか。</summary>
        public bool Ready => sceneLoaded && battleCam != null;

        /// <summary>カーソルがこの窓（枠全体）の上にあるか。</summary>
        public bool ContainsPointer
        {
            get
            {
                if (!isOpen || windowRT == null || Mouse.current == null) return false;
                return RectTransformUtility.RectangleContainsScreenPoint(windowRT, Mouse.current.position.ReadValue(), null);
            }
        }

        /// <summary>
        /// この窓のタイトルバー/リサイズグリップを掴んでいる最中か。掴んでいる間はカーソルが枠外へ出ても
        /// 背後（戦略マップ・会戦盤面）へ入力を渡さない＝窓を動かしながらマップがスクロールしない。
        /// </summary>
        public bool IsGrabbingHandle => isOpen && handleGrabbed && MapWindowDrag.AnyGrabbing;

        // ===== 公開ライフサイクル（BattleDirector が呼ぶ）=====

        /// <summary>
        /// 会戦シーンを additive ロードして窓に表示する。<paramref name="worldOffset"/> は戦場の遠方オフセット
        /// （会戦ごとに固有）。<paramref name="anchoredPos"/> は窓の初期位置。完了で <paramref name="loadedCb"/>。
        /// 呼び出し前に BattleDirector が BattleHandoff を当該会戦のスナップショットへ復元済みであること。
        /// </summary>
        public void BeginOpen(Vector2 worldOffset, Vector2 anchoredPos, string windowTitle,
            System.Action<BattleWindow> loadedCb, System.Action<BattleWindow> closedCb)
        {
            onLoaded = loadedCb;
            onClosed = closedCb;
            if (!string.IsNullOrEmpty(windowTitle)) title = windowTitle;
            EnsureEventSystem();
            HookSceneGuard();

            // 実画面（上メニュー帯を除く）に収まるサイズで開く。既定の 1040x660 は 720p 級の画面では
            // 上メニュー帯を引いた高さに入りきらず、枠が画面下へはみ出して**右下のリサイズグリップが
            // 画面外＝掴めない**／毎フレームの上端合わせで**縦ドラッグが打ち消される**（実機報告の原因）。
            windowSize = ClampWindowSize(windowSize);
            Build(anchoredPos);
            ClampWindowPosition();

            // 実表示サイズが決まってから RenderTexture を作る（引き伸ばし歪みを出さない）。
            LayoutRebuilder.ForceRebuildLayoutImmediate(windowRT);
            RebuildRenderTexture();
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            isOpen = true;
            if (root != null) root.SetActive(true);

            // 戦場を会戦ごとの遠方オフセットへ置く（戦略・他会戦と同一ワールド空間でも映り込まないよう隔離）。
            // BattleSetup.Awake が additive ロード中にこの値を読んで自シーンへ確定登録する＝ロード前に設定する。
            BattleField.PendingOrigin = worldOffset;

            sceneLoaded = false;
            SceneLoader.Instance.LoadSceneAdditive("Battle", true, OnBattleLoaded);
        }

        /// <summary>窓を閉じて会戦シーンをアンロードし、描画資源を解放する（BattleDirector が呼ぶ）。</summary>
        public void CloseWindow()
        {
            if (!isOpen) return;
            isOpen = false;

            if (battleScene.IsValid()) BattleWindowUI.Unregister(battleScene); // 窓 UI 親矩形の登録を外す（WIN-4）
            if (battleCam != null) { battleCam.targetTexture = null; battleCam = null; }
            if (sceneLoaded && battleScene.IsValid())
                SceneLoader.Instance.UnloadSceneAdditive(battleScene);
            if (battleScene.IsValid()) BattleField.ClearScene(battleScene);
            sceneLoaded = false;

            Cleanup();
            UIWindowStack.Unregister(escWindowToken);
            escWindowToken = null;
            onClosed?.Invoke(this);
        }

        // ===== ロード完了 =====

        private void OnBattleLoaded(Scene scene)
        {
            battleScene = scene;
            sceneLoaded = scene.IsValid() && scene.isLoaded;
            battleCam = FindBattleCamera(scene);
            if (rt == null) RebuildRenderTexture(); // 保険：RT 未生成のまま画面へ描かせない
            if (battleCam != null)
            {
                battleCam.targetTexture = rt; // 画面でなくウィンドウ（RT）へ描く
                // 会戦カメラの AudioListener は無効化（戦略シーンの1つだけに保つ＝「2 audio listeners」警告/競合回避）。
                var al = battleCam.GetComponent<AudioListener>();
                if (al != null) al.enabled = false;
            }
            else
            {
                Debug.LogWarning("BattleWindow: 会戦カメラが見つかりませんでした（additive ロード後）。");
            }

            // 会戦シーンに含まれる EventSystem を無効化（戦略シーンの1つに統一）。
            // 通常は下の sceneLoaded フック（HookSceneGuard）が一足先に畳んでいるので、ここは取りこぼしの保険。
            DisableSceneEventSystems(scene);

            // 窓モードで自分を無効化した PauseManager の全画面 UI を畳む（枠外へ残らないように）。
            HideSuppressedPauseUI(scene);

            // この会戦シーンの UI（HUD/コマンド/ミニマップ）が自分の窓内へ親替えできるよう、窓 UI 親矩形を登録（WIN-4）。
            if (sceneLoaded && battleUIRoot != null) BattleWindowUI.Register(battleScene, battleUIRoot);

            onLoaded?.Invoke(this);
        }

        /// <summary>
        /// additive ロードされた会戦シーンの EventSystem を「シーンが載った瞬間」に畳む保険を張る（症状1・#2 event systems）。
        /// <see cref="OnBattleLoaded"/> は <see cref="SceneLoader"/> のコルーチンが <c>op.isDone</c> を見てから呼ぶため
        /// <b>シーン活性化より最低1フレーム遅い</b>。その間 uGUI の <c>EventSystem.Update()</c> が
        /// 「There are 2 event systems in the scene」を<b>毎フレーム</b>吐く（uGUI パッケージの Update 内 UNITY_EDITOR ブロック）。
        /// <c>sceneLoaded</c> は Awake/OnEnable の直後・最初の Update より前に来るので、ここで畳めば警告は出ない。
        /// 購読は解除→再購読で冪等（多重購読しない）。
        /// </summary>
        private static void HookSceneGuard()
        {
            SceneManager.sceneLoaded -= OnAnySceneLoaded;
            SceneManager.sceneLoaded += OnAnySceneLoaded;
        }

        private static void OnAnySceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive) return;
            if (scene.name != "Battle") return;
            // 全画面会戦（会戦＝アクティブシーン）には触らない＝従来動作（後方互換）。
            if (scene == SceneManager.GetActiveScene()) return;
            DisableSceneEventSystems(scene);
        }

        /// <summary>
        /// 指定シーン内の EventSystem を<b>取り除く</b>（複数 EventSystem の競合を防ぐ）。
        /// 以前は GameObject を SetActive(false) していたが、①同じ GameObject に載る他のコンポーネントまで
        /// 止めてしまう ②無効な EventSystem は <c>FindAnyObjectByType</c> に引っかからないため、各オーバーレイの
        /// <c>EnsureEventSystem()</c> が3つ目を新規生成しうる、の2点で危うい。コンポーネントだけ破棄して
        /// 「有効な EventSystem は戦略シーンの1つだけ」を保つ（実機で出た "There are 2 event systems" 対策）。
        /// <b>enabled=false を Destroy より先に立てる</b>のが要点：<c>Destroy</c> はフレーム末まで遅延するのに対し、
        /// <c>enabled=false</c> は <c>OnDisable</c> を<b>同期で</b>走らせ uGUI の静的リストから即座に外すため、
        /// 「その1フレームぶんの警告」も出ない。
        /// </summary>
        private static void DisableSceneEventSystems(Scene scene)
        {
            if (!scene.IsValid()) return;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                EventSystem[] systems = roots[i].GetComponentsInChildren<EventSystem>(true);
                for (int j = 0; j < systems.Length; j++)
                {
                    if (systems[j] == null) continue;
                    GameObject host = systems[j].gameObject;
                    // 入力モジュールも一緒に取り除く（残すと EventSystem 不在で警告を出し続ける）。
                    var modules = host.GetComponents<UnityEngine.EventSystems.BaseInputModule>();
                    for (int k = 0; k < modules.Length; k++)
                    {
                        if (modules[k] == null) continue;
                        modules[k].enabled = false;
                        Destroy(modules[k]);
                    }
                    systems[j].enabled = false; // ← 同期で静的リストから外れる（警告が即止まる）
                    Destroy(systems[j]);
                }
            }
        }

        /// <summary>
        /// 窓モードでは会戦シーンの <see cref="PauseManager"/> が自分を無効化する（時間制御は統一クロックが担う）。
        /// ところが無効化された PauseManager がシーンで握っている全画面 UI（ポーズメニュー/設定/「SPEED」ラベル）は
        /// 誰も面倒を見ないまま<b>窓の外＝画面いっぱい</b>に残り、背後の戦略 HUD と重なる（実機報告の症状3）。
        /// 窓の側で畳む（PauseManager 自身は触らない＝フルスクリーン会戦は enabled のままなので従来どおり）。
        /// </summary>
        private static void HideSuppressedPauseUI(Scene scene)
        {
            PauseManager pm = FindInScene<PauseManager>(scene);
            if (pm == null || pm.enabled) return; // 有効＝フルスクリーン会戦＝触らない
            if (pm.pauseMenuRoot != null) pm.pauseMenuRoot.SetActive(false);
            if (pm.settingsPanel != null) pm.settingsPanel.SetActive(false);
            if (pm.timeScaleText != null) pm.timeScaleText.gameObject.SetActive(false);
        }

        /// <summary>指定シーンのルート配下から最初の <typeparamref name="T"/> を返す（無効なコンポーネントも含む）。</summary>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid()) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static Camera FindBattleCamera(Scene scene)
        {
            if (!scene.IsValid()) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Camera c = roots[i].GetComponentInChildren<Camera>(true);
                if (c != null) return c;
            }
            return null;
        }

        /// <summary>この会戦の BattleManager を返す（スナップショット注入・離脱に使う）。</summary>
        public BattleManager FindBattleManager()
        {
            if (!battleScene.IsValid()) return null;
            GameObject[] roots = battleScene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                BattleManager bm = roots[i].GetComponentInChildren<BattleManager>(true);
                if (bm != null) return bm;
            }
            return null;
        }

        /// <summary>× / Esc：現状の優勢側を勝者として書き戻して離脱する（BattleManager 経由）。</summary>
        private void RequestLeave()
        {
            BattleManager bm = Ready ? FindBattleManager() : null;
            if (bm != null) bm.LeaveToStrategy();
            else BattleDirector.NotifyBattleEnded(battleScene); // 見つからなければ単に閉じる
        }

        // ===== UI 構築 =====

        private void Build(Vector2 anchoredPos)
        {
            GameObject canvasObj = new GameObject("BattleWindowCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 940; // 通知(880)/星系図(950)近辺・観測窓(1090)より後ろ
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            GameObject win = new GameObject("Window", typeof(RectTransform));
            win.transform.SetParent(root.transform, false);
            windowRT = win.GetComponent<RectTransform>();
            windowRT.anchorMin = windowRT.anchorMax = windowRT.pivot = new Vector2(0.5f, 0.5f);
            windowRT.sizeDelta = windowSize;
            windowRT.anchoredPosition = anchoredPos;
            Image winImg = win.AddComponent<Image>();
            winImg.color = new Color(0.03f, 0.04f, 0.07f, 0.98f);
            Outline border = win.AddComponent<Outline>();
            border.effectColor = new Color(1f, 0.84f, 0.36f, 0.5f);
            border.effectDistance = new Vector2(2f, -2f);

            VerticalLayoutGroup vlg = win.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 0f;
            vlg.childControlWidth = true; vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true; vlg.childForceExpandHeight = false;

            BuildTitleBar(win.transform);

            // マップ領域（RawImage＝RenderTexture を映す。raycastTarget=false＝会戦クリックは FleetCommander が直接処理）
            // flexibleHeight=1＝タイトルバー以外の残り高さを埋める＝ウィンドウのサイズ変更に追従する。
            GameObject mapGo = new GameObject("Map", typeof(RectTransform));
            mapGo.transform.SetParent(win.transform, false);
            mapRT = mapGo.GetComponent<RectTransform>();
            LayoutElement le = mapGo.AddComponent<LayoutElement>();
            le.minHeight = 120f;
            le.flexibleHeight = 1f;
            mapImage = mapGo.AddComponent<RawImage>();
            mapImage.color = Color.white;
            mapImage.raycastTarget = false;

            // 窓内の会戦UI親（RawImage と同矩形・はみ出しは RectMask2D で窓内にクリップ）。
            // 会戦シーンの HUD/コマンドメニュー/ミニマップ（フルスクリーンに描く Canvas）はここへ親替えされ、
            // 窓に追従して移動・拡縮し、複数窓で重ならない（WIN-4 #2571）。BattleUIRoot は RawImage の子＝RT の上に描く。
            GameObject uiRootGo = new GameObject("BattleUIRoot", typeof(RectTransform));
            uiRootGo.transform.SetParent(mapGo.transform, false);
            battleUIRoot = uiRootGo.GetComponent<RectTransform>();
            StretchFull(battleUIRoot);
            uiRootGo.AddComponent<RectMask2D>();

            BuildResizeGrip(win.transform);

            escWindowToken = UIWindowStack.Register(() => isOpen, RequestLeave, 940, "会戦");
        }

        /// <summary>右下のサイズ変更グリップ（つかんでドラッグでウィンドウを拡縮）。</summary>
        private void BuildResizeGrip(Transform winParent)
        {
            GameObject grip = new GameObject("ResizeGrip", typeof(RectTransform));
            grip.transform.SetParent(winParent, false);
            RectTransform g = grip.GetComponent<RectTransform>();
            g.anchorMin = g.anchorMax = new Vector2(1f, 0f); // 右下
            g.pivot = new Vector2(1f, 0f);
            g.sizeDelta = new Vector2(GripSize, GripSize);
            g.anchoredPosition = Vector2.zero;
            LayoutElement gle = grip.AddComponent<LayoutElement>();
            gle.ignoreLayout = true; // VerticalLayoutGroup の行にせず右下に浮かせる
            Image gi = grip.AddComponent<Image>();
            gi.color = new Color(1f, 0.84f, 0.36f, 0.5f); // 金色のつまみ
            // 見た目より広い当たり判定（実機で 20px 四方は狙えなかった＝掴めない原因の一つ）。
            // raycastPadding は「正で内側へ縮む」ので、広げるには負値を入れる（左/下/右/上）。
            gi.raycastPadding = new Vector4(-12f, -12f, -4f, -4f);
            MapWindowDrag drag = grip.AddComponent<MapWindowDrag>();
            drag.onDragDelta = OnResizeDrag;
        }

        private void BuildTitleBar(Transform parent)
        {
            GameObject bar = new GameObject("TitleBar", typeof(RectTransform));
            bar.transform.SetParent(parent, false);
            Image img = bar.AddComponent<Image>();
            img.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            LayoutElement le = bar.AddComponent<LayoutElement>();
            le.minHeight = TitleBarHeight; le.preferredHeight = TitleBarHeight;

            // ドラッグ移動は EventSystem の drag イベントではなく MapWindowDrag（押下だけ EventSystem・以後は
            // 毎フレーム実ポインタの差分を自前計算）で行う。戦略マップ窓と同じ作法＝人の手でも自動入力でも動き、
            // 掴んでいる間は MapWindowDrag.AnyGrabbing が立って背後の盤面へ入力を渡さない。
            // 旧 UIDragMove は①drag イベント依存で無反応になりうる②LateUpdate で毎フレーム画面内へ是正するため
            // 「窓が利用高より高い」状況では縦ドラッグを毎フレーム打ち消す、の2点で窓が動かない原因だった。
            MapWindowDrag drag = bar.AddComponent<MapWindowDrag>();
            drag.onDragDelta = OnTitleDrag;

            titleCap = CreateText(bar.transform, $"≡ {title} の戦い　（上部ドラッグで移動／右下で拡縮／× 離脱）", 15f, new Color(1f, 0.84f, 0.36f), TextAlignmentOptions.Left);
            RectTransform crt = titleCap.rectTransform;
            crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(12f, 0f); crt.offsetMax = new Vector2(-(42f + ClockWidth), 0f);

            // 時計/速度はタイトルバーへ（戦略が浮きHUDを作らず上メニューに出すのと同じ作り分け）。
            // 整形は TimeDisplay.TryFormatNow の単一窓口を再利用＝二重実装しない。
            clockCap = CreateText(bar.transform, "", 13f, new Color(0.95f, 0.92f, 0.7f), TextAlignmentOptions.Right);
            RectTransform krt = clockCap.rectTransform;
            krt.anchorMin = new Vector2(1f, 0f); krt.anchorMax = new Vector2(1f, 1f);
            krt.pivot = new Vector2(1f, 0.5f);
            krt.sizeDelta = new Vector2(ClockWidth, 0f);
            krt.anchoredPosition = new Vector2(-42f, 0f);

            GameObject cb = new GameObject("Close", typeof(RectTransform));
            cb.transform.SetParent(bar.transform, false);
            RectTransform cbrt = cb.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(1f, 0f); cbrt.anchorMax = new Vector2(1f, 1f);
            cbrt.pivot = new Vector2(1f, 0.5f); cbrt.sizeDelta = new Vector2(34f, 0f);
            cbrt.anchoredPosition = new Vector2(-3f, 0f);
            Image cimg = cb.AddComponent<Image>();
            cimg.color = new Color(0.13f, 0.18f, 0.26f, 1f);
            Button cbtn = cb.AddComponent<Button>();
            cbtn.transition = UnityEngine.UI.Selectable.Transition.None;
            cbtn.onClick.AddListener(RequestLeave);
            TextMeshProUGUI glyph = CreateText(cb.transform, "×", 18f, Color.white, TextAlignmentOptions.Center);
            StretchFull(glyph.rectTransform);
        }

        /// <summary>タイトルバーのドラッグで窓を動かす（画面内・上メニュー帯より下にクランプ）。</summary>
        private void OnTitleDrag(Vector2 delta)
        {
            if (windowRT == null) return;
            handleGrabbed = true;
            Vector2 before = windowRT.anchoredPosition;
            windowRT.anchoredPosition += delta; // スクリーン px と anchoredPosition は同じ向き（上が＋）
            ClampWindowPosition();
            // 診断ON時だけ：受け取った移動量と、クランプ後に実際どれだけ動いたかを出す
            //（「移動量は来ているのに窓側で打ち消されている」を切り分けるため）。
            if (MapWindowDrag.LogEvents)
                Debug.Log($"[窓入力診断] タイトルドラッグ delta={delta} " +
                          $"anchored {before} → {windowRT.anchoredPosition}（クランプ後の実移動={windowRT.anchoredPosition - before}）");
        }

        /// <summary>
        /// 右下グリップのドラッグでウィンドウサイズを変える（最小/最大＋実画面でクランプ）。
        /// ピボットが中央なので、サイズ変化ぶんだけ中心もずらして<b>左上角を固定</b>する
        /// ＝右下のグリップがカーソルに1:1で追従する（従来は中央基準で半分しか動かず「効いていない」ように見えた）。
        /// </summary>
        private void OnResizeDrag(Vector2 delta)
        {
            if (windowRT == null) return;
            handleGrabbed = true;
            Vector2 before = windowRT.sizeDelta;
            // 画面の下方向ドラッグ＝高さ増。右方向ドラッグ＝幅増。
            Vector2 after = ClampWindowSize(before + new Vector2(delta.x, -delta.y));
            Vector2 grew = after - before;
            if (MapWindowDrag.LogEvents)
                Debug.Log($"[窓入力診断] グリップドラッグ delta={delta} size {before} → {after}" +
                          $"（クランプ後の実変化={grew}）");
            if (grew == Vector2.zero) return;
            windowRT.sizeDelta = after;
            windowRT.anchoredPosition += new Vector2(grew.x * 0.5f, -grew.y * 0.5f); // 左上を固定
            ClampWindowPosition();
            rtDirty = true;
            rtSettleTimer = 0f;
        }

        // ===== 窓枠の追従（クランプ・解像度変化・RenderTexture の作り直し）=====

        /// <summary>窓を置いてよい画面領域の高さ（上メニュー帯 <see cref="UIDragMove.TopReservedPx"/> を除く）。</summary>
        private static float AvailableHeight()
            => Mathf.Max(160f, Screen.height - Mathf.Max(0f, UIDragMove.TopReservedPx));

        /// <summary>窓サイズを最小/最大＋実画面（上メニュー帯を除く）に収める。</summary>
        private static Vector2 ClampWindowSize(Vector2 size)
        {
            float maxW = Mathf.Min(MaxWindowWidth, Mathf.Max(MinWindowWidth, Screen.width - ScreenMargin * 2f));
            float maxH = Mathf.Min(MaxWindowHeight, Mathf.Max(MinWindowHeight, AvailableHeight() - ScreenMargin * 2f));
            size.x = Mathf.Clamp(size.x, MinWindowWidth, maxW);
            size.y = Mathf.Clamp(size.y, MinWindowHeight, maxH);
            return size;
        }

        /// <summary>
        /// 窓が画面内（上メニュー帯より下）に収まるよう anchoredPosition を<b>必要なぶんだけ</b>補正する。
        /// アンカー/ピボットとも中央なので anchoredPosition は「画面中心からのずれ」。
        /// </summary>
        private void ClampWindowPosition()
        {
            if (windowRT == null) return;
            Vector2 half = windowRT.sizeDelta * 0.5f;
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 c = screenCenter + windowRT.anchoredPosition;
            float topLimit = Screen.height - Mathf.Max(0f, UIDragMove.TopReservedPx);

            float minX = half.x, maxX = Screen.width - half.x;
            c.x = (minX > maxX) ? minX : Mathf.Clamp(c.x, minX, maxX); // 画面より広い＝左端合わせ
            float minY = half.y, maxY = topLimit - half.y;
            c.y = (minY > maxY) ? maxY : Mathf.Clamp(c.y, minY, maxY); // 帯下に入らない＝上端合わせ

            windowRT.anchoredPosition = c - screenCenter;
        }

        /// <summary>
        /// RenderTexture を現在のマップ表示サイズで作り直す（引き伸ばしによる歪みを出さない）。
        /// 拡縮の最中は呼ばず、<see cref="RtSettleSeconds"/> 落ち着いてから1回だけ実行する。
        /// </summary>
        private void RebuildRenderTexture()
        {
            if (mapRT == null || mapImage == null) return;
            Rect r = mapRT.rect;
            int w = (r.width >= 16f) ? Mathf.RoundToInt(r.width) : Mathf.Max(16, rtWidth);
            int h = (r.height >= 16f) ? Mathf.RoundToInt(r.height) : Mathf.Max(16, rtHeight);
            w = Mathf.Clamp(w, 128, 4096);
            h = Mathf.Clamp(h, 96, 4096);
            if (rt != null && rt.width == w && rt.height == h) return;

            if (battleCam != null) battleCam.targetTexture = null;
            if (rt != null)
            {
                mapImage.texture = null;
                rt.Release();
                Destroy(rt);
            }
            rt = new RenderTexture(w, h, 16);
            rt.Create();
            mapImage.texture = rt;
            if (battleCam != null) battleCam.targetTexture = rt;
        }

        private void Update()
        {
            if (!isOpen) return;

            // 掴みの解除は MapWindowDrag が握っている（離した/無効化された瞬間に降りる）。
            if (!MapWindowDrag.AnyGrabbing) handleGrabbed = false;

            // 解像度・画面サイズが変わったときだけ枠を是正する（毎フレームの是正はドラッグと綱引きする）。
            Vector2Int now = new Vector2Int(Screen.width, Screen.height);
            if (now != lastScreenSize)
            {
                lastScreenSize = now;
                if (windowRT != null) windowRT.sizeDelta = ClampWindowSize(windowRT.sizeDelta);
                ClampWindowPosition();
                rtDirty = true;
                rtSettleTimer = 0f;
            }

            if (rtDirty && !MapWindowDrag.AnyGrabbing)
            {
                rtSettleTimer += Time.unscaledDeltaTime;
                if (rtSettleTimer >= RtSettleSeconds)
                {
                    rtDirty = false;
                    rtSettleTimer = 0f;
                    if (windowRT != null) LayoutRebuilder.ForceRebuildLayoutImmediate(windowRT);
                    RebuildRenderTexture();
                }
            }

            UpdateTitleClock();
        }

        /// <summary>タイトルバーの時計/速度を更新する（整形は TimeDisplay の単一窓口）。</summary>
        private void UpdateTitleClock()
        {
            if (clockCap == null) return;
            if (TimeDisplay.TryFormatNow(out string text, out Color color))
            {
                clockCap.text = text.Replace("\n", "　"); // タイトルバーは1行
                clockCap.color = color;
            }
            else clockCap.text = "";
        }

        private void Cleanup()
        {
            if (rt != null)
            {
                if (mapImage != null) mapImage.texture = null;
                rt.Release();
                Destroy(rt);
                rt = null;
            }
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            Cleanup();
        }

        // ===== ヘルパ =====

        private static TextMeshProUGUI CreateText(Transform parent, string text, float size, Color color, TextAlignmentOptions align)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) t.font = ja;
            return t;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
