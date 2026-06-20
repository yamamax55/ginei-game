using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 【テスト/デモ】会戦シーンで<b>帝国軍が攻撃を始めた瞬間</b>、画面右上に枠付きで
    /// 3D戦艦の「砲塔旋回→主砲斉射」アニメ動画を<b>一度だけ</b>再生する。
    /// 非侵襲：<see cref="FleetRegistry"/> を監視するだけで既存の攻撃ロジックには手を入れない。
    /// 動画は <c>Resources/warship_salvo.mp4</c> を <see cref="VideoClip"/> としてインポートして使う
    /// （URL再生より Unity 管理＝再生が安定。Inspector で Transcode を有効にすると更に最適化）。
    /// Battle シーンにのみ自動生成（<see cref="NotificationFeed"/> と同じブートストラップ作法）。
    ///
    /// <para>スムーズ再生の工夫：会戦処理が重いと再生がカクつくため、(1) シーン開始時に裏で
    /// <see cref="VideoPlayer.Prepare"/> して交戦ピーク時の初回デコード停止を避ける、
    /// (2) <see cref="VideoPlayer.skipOnDrop"/>=true ＋音声出力で<b>実時間ペース</b>を保つ（timeScale 非依存）。</para>
    /// </summary>
    public class WarshipDemoOverlay : MonoBehaviour
    {
        [Header("表示")]
        [Tooltip("枠パネルのサイズ（px・16:9＋タイトルバー）")]
        public Vector2 panelSize = new Vector2(426f, 266f);
        [Tooltip("画面右上からのマージン（X, Y）")]
        public Vector2 margin = new Vector2(24f, 24f);
        [Tooltip("Resources 内の動画 VideoClip 名（拡張子なし）")]
        public string videoResourceName = "warship_salvo";

        [Header("配色（ゲーム意匠）")]
        public Color panelColor = new Color(0.05f, 0.07f, 0.12f, 0.96f);
        public Color borderColor = new Color(0.45f, 0.40f, 0.22f, 0.95f);
        public Color titleBarColor = new Color(0.09f, 0.12f, 0.18f, 1f);
        public Color accentColor = new Color(1f, 0.84f, 0.36f, 1f);

        private const float TitleHeight = 26f;

        private bool triggered;            // 一回だけ
        private VideoPlayer player;        // このGameObjectに常駐（プリロード用）
        private AudioSource audioOut;
        private RenderTexture rt;
        private GameObject canvasObj;
        private RawImage videoView;

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
            if (scene.name != "Battle") return;
            if (FindAnyObjectByType<WarshipDemoOverlay>() != null) return;
            new GameObject("WarshipDemoOverlay").AddComponent<WarshipDemoOverlay>();
        }

        private void Awake()
        {
            // 動画は Resources の VideoClip（Unity 管理＝URL再生より安定）。裏でプリロードしておく。
            VideoClip clip = Resources.Load<VideoClip>(videoResourceName);
            if (clip == null)
            {
                Debug.LogWarning($"[WarshipDemoOverlay] VideoClip が見つかりません: Resources/{videoResourceName}");
                enabled = false;
                return;
            }

            rt = new RenderTexture(640, 360, 0) { name = "WarshipDemoRT" };

            audioOut = gameObject.AddComponent<AudioSource>();
            audioOut.playOnAwake = false;

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = rt;
            player.isLooping = false;
            player.waitForFirstFrame = true;      // 黒フレーム飛ばし
            player.skipOnDrop = true;             // 負荷時はフレーム間引きで実時間ペース維持
            player.audioOutputMode = VideoAudioOutputMode.AudioSource; // 音声クロック＝実時間（timeScale非依存）
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, audioOut);
            player.loopPointReached += OnVideoEnd; // 再生終了で枠を閉じる
            player.errorReceived += OnVideoError;
            player.Prepare();                     // 先にデコード準備だけ進めておく（プリロード）
        }

        private void Update()
        {
            if (triggered) return;
            if (!ImperialIsAttacking()) return;
            triggered = true;   // 以降は監視しない＝一回だけ
            ShowDemo();
        }

        /// <summary>帝国軍の旗艦のいずれかが交戦を開始したか（非侵襲の検出）。</summary>
        private static bool ImperialIsAttacking()
        {
            var flags = FleetRegistry.AllFlagships;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength fs = flags[i];
                if (fs == null || !fs.IsAlive) continue;
                if (fs.faction != Faction.帝国) continue;
                FleetWeapon w = fs.GetComponent<FleetWeapon>();
                if (w != null && w.IsInCombat) return true;
            }
            return false;
        }

        private void ShowDemo()
        {
            BuildUI();
            videoView.texture = rt;
            // 事前準備済みなら即時・滑らかに開始。未完了でも Play() がそのまま準備して再生する。
            player.Play();
        }

        private void BuildUI()
        {
            canvasObj = new GameObject("WarshipDemoCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 885; // ゲームUIより前・モーダル(900+)より後ろ
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 枠ウィンドウ（右上）
            var win = new GameObject("Window");
            win.transform.SetParent(canvasObj.transform, false);
            var wrt = win.AddComponent<RectTransform>();
            wrt.anchorMin = new Vector2(1f, 1f);
            wrt.anchorMax = new Vector2(1f, 1f);
            wrt.pivot = new Vector2(1f, 1f);
            wrt.anchoredPosition = new Vector2(-margin.x, -margin.y);
            wrt.sizeDelta = panelSize;
            win.AddComponent<Image>().color = panelColor;
            var border = win.AddComponent<Outline>();
            border.effectColor = borderColor;
            border.effectDistance = new Vector2(2f, -2f);

            // タイトルバー
            var bar = new GameObject("TitleBar");
            bar.transform.SetParent(win.transform, false);
            var brt = bar.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(0f, TitleHeight);
            brt.anchoredPosition = Vector2.zero;
            bar.AddComponent<Image>().color = titleBarColor;

            var cap = new GameObject("Caption").AddComponent<TextMeshProUGUI>();
            cap.transform.SetParent(bar.transform, false);
            var crt = cap.rectTransform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = new Vector2(10f, 0f);
            crt.offsetMax = new Vector2(-8f, 0f);
            cap.text = "帝国軍 砲撃開始";
            cap.fontSize = 14f;
            cap.color = accentColor;
            cap.alignment = TextAlignmentOptions.Left;
            cap.raycastTarget = false;
            var jp = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jp != null) cap.font = jp;

            // 映像領域（RawImage）
            videoView = new GameObject("Video").AddComponent<RawImage>();
            videoView.transform.SetParent(win.transform, false);
            var vrt = videoView.rectTransform;
            vrt.anchorMin = new Vector2(0f, 0f);
            vrt.anchorMax = new Vector2(1f, 1f);
            vrt.offsetMin = new Vector2(4f, 4f);
            vrt.offsetMax = new Vector2(-4f, -(TitleHeight + 2f));
            videoView.color = Color.white;
            videoView.raycastTarget = false;
        }

        private void OnVideoEnd(VideoPlayer source) => Close();

        private void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[WarshipDemoOverlay] 動画再生エラー: {message}");
            Close();
        }

        private void Close()
        {
            if (canvasObj != null) Destroy(canvasObj);
            // 一回だけ：このオブジェクトも破棄（再生成は会戦シーン再ロード時のみ）
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.loopPointReached -= OnVideoEnd;
                player.errorReceived -= OnVideoError;
            }
            if (rt != null)
            {
                rt.Release();
                Destroy(rt);
                rt = null;
            }
        }
    }
}
