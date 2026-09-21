using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 会戦のミニマップ（俯瞰・クリックジャンプ #84・RTS-3）。
    /// 第2カメラを使わず、uGUI 上に <see cref="FleetRegistry.AllFlagships"/> を陣営色ドットで
    /// 間引きプロットし、ブラックホール（<see cref="BlackHole.All"/>）もアイコン表示する。
    /// 現在のカメラ視界を矩形で重畳し、ミニマップのクリック／ドラッグで <see cref="CameraController.JumpTo"/>。
    /// 戦場ワールド矩形は <see cref="CameraController.MinBounds"/>/<see cref="CameraController.MaxBounds"/> を写像。
    /// Battle シーンに RuntimeInitializeOnLoadMethod で自動生成（手置き不要）。表示/非表示は C キー（#107）。
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        [Header("レイアウト")]
        [Tooltip("ミニマップ枠の一辺サイズ（px）")]
        public float panelSize = 220f;
        [Tooltip("画面端からの余白（px）")]
        public float screenMargin = 12f;
        [Tooltip("枠内の内側余白（px）")]
        public float contentPadding = 8f;

        [Header("ドット")]
        [Tooltip("艦隊ドットの一辺（px）")]
        public float dotSize = 5f;
        [Tooltip("ブラックホールアイコンの一辺（px）")]
        public float blackHoleSize = 8f;

        [Header("更新")]
        [Tooltip("ドット再配置の間隔（秒・unscaled）。視界矩形は毎フレーム更新")]
        public float refreshInterval = 0.1f;

        // 帝国/同盟の既定色（FactionData 未設定時のフォールバック・FactionColor と同値）。
        private static readonly Color ImperialColor = new Color(0.9f, 0.2f, 0.2f);
        private static readonly Color AllianceColor = new Color(0.2f, 0.5f, 0.9f);
        private static readonly Color BlackHoleColor = new Color(0.6f, 0.3f, 0.9f);
        private static readonly Color ViewRectColor = new Color(1f, 1f, 1f, 0.18f);

        private Canvas canvas;
        private GameObject frameRoot;      // 枠ルート（表示/非表示トグル・窓内へ親替えする対象・WIN-4）
        private RectTransform content;     // ドットを配置する内側領域（pivot 中央）
        private RectTransform viewRect;    // 現在のカメラ視界を示す矩形
        private Image frameImage;          // クリック判定の対象（raycastTarget）
        private bool windowAttachDone;     // 窓内へ枠を親替え済みか（WIN-4・ウィンドウ化会戦のみ）
        private readonly List<Image> dotPool = new List<Image>();
        private readonly List<Image> blackHolePool = new List<Image>();

        private Camera cam;
        private CameraController camController;
        private FleetCommander commander;      // 選択の窓口（自会戦シーンのもの）
        private float refreshTimer;
        private bool visible = true;

        // 回廊要塞の地形（#A：要塞・回廊境界・制圧線を実戦場と一致させて出す）
        private RectTransform arenaField;         // 水路（航行できる範囲）
        private RectTransform arenaStandoff;      // 封鎖線（守備が健在なあいだ敵が越えられない線）
        private RectTransform arenaBreakthrough;  // 制圧線（ここまで到達で占領）
        private RectTransform arenaFortress;      // 要塞の実体
        private TMPro.TextMeshProUGUI selectionCaption;   // 選択中の艦隊だけを説明する1行（#C）

        // ===== 自動生成エントリーポイント（BattleAllegianceManager と同型）=====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle") return;
            // 会戦シーンごとに1つ（WIN-4）：ウィンドウ化会戦は複数の Battle シーンが同時にロードされるので、
            // グローバル重複ガードでなく「このシーンに既に在るか」で判定する。窓ごとに自分のミニマップを持ち、
            // 自シーンのカメラ・旗艦だけを映す。フルスクリーン会戦（単一 Battle シーン）では従来どおり1つ。
            if (FindInScene(scene) != null) return;
            GameObject go = new GameObject("Minimap");
            // additive ロードされた会戦シーンに帰属させる（new GameObject は既定でアクティブシーンに入るため移す）。
            if (scene.IsValid() && scene != SceneManager.GetActiveScene())
                SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<Minimap>();
        }

        /// <summary>指定シーンに属する Minimap を返す（無ければ null）。</summary>
        private static Minimap FindInScene(Scene scene)
        {
            Minimap[] all = Object.FindObjectsByType<Minimap>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.scene == scene) return all[i];
            return null;
        }

        /// <summary>
        /// 指定シーンに属するコンポーネントを返す（無ければ <b>null</b>）。
        /// <see cref="BattleWindowUI.FindInSceneOrAny{T}"/> と違い<b>他シーンへ落ちない</b>
        /// ＝複数会戦で別の戦場のカメラや境界を掴まない。
        /// </summary>
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            T[] all = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].gameObject.scene == scene) return all[i];
            return null;
        }

        // ===== 本体 =====

        private void Start()
        {
            // 自分の会戦シーンのカメラ／カメラ制御を掴む（複数会戦が同時でも他会戦を映さない・WIN-4）。
            cam = ResolveSceneCamera();
            // ★カメラ制御は<b>自分の会戦シーンのものだけ</b>を掴む。FindInSceneOrAny は見つからないと
            // 他シーンの1つを返すため、ウィンドウ化会戦で別の会戦の境界を掴み、別の戦場の艦を
            // 自分のミニマップへ写してしまう（複数会戦の混線）。見つからなければ null のままにする。
            camController = FindInScene<CameraController>(gameObject.scene);
            EnsureEventSystem();
            Build();
        }

        /// <summary>自分の会戦シーンに属するカメラを返す（無ければ Camera.main・フルスクリーン後方互換）。</summary>
        private Camera ResolveSceneCamera()
        {
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cams.Length; i++)
                if (cams[i] != null && cams[i].gameObject.scene == gameObject.scene) return cams[i];
            return Camera.main;
        }

        private void Update()
        {
            TryWindowAttach();
            bool acceptsInput = BattleWindowUI.AcceptsInput(gameObject.scene);

            // 表示/非表示トグル（C・GameInput #107）
            if (acceptsInput && GameInput.WasPressed(GameAction.ミニマップ切替))
            {
                visible = !visible;
                if (frameRoot != null) frameRoot.SetActive(visible); // 枠を直接トグル（窓内へ親替え後も効く・WIN-4）
            }
            if (!visible || canvas == null) return;

            // 視界矩形は毎フレーム、ドットは間引いて更新（unscaled＝ポーズ中も追従）。
            UpdateViewRect();
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer >= refreshInterval)
            {
                refreshTimer = 0f;
                PlotArena();      // 地形（要塞・回廊境界・制圧線）を先に敷いてから艦を上に置く
                PlotFleets();
                PlotBlackHoles();
            }

            HandleClickJump();
        }

        /// <summary>
        /// ウィンドウ化会戦（WIN-4）ではミニマップ枠を自分の窓内 UI 親へ親替えする（全画面に出さない・重なり解消）。
        /// フルスクリーン会戦（会戦シーン＝アクティブシーン）では何もしない＝従来どおり右下に全画面表示（後方互換）。
        /// </summary>
        private void TryWindowAttach()
        {
            if (windowAttachDone || frameRoot == null) return;
            if (gameObject.scene == SceneManager.GetActiveScene()) { windowAttachDone = true; return; }
            if (BattleWindowUI.TryAttach(gameObject.scene, frameRoot.GetComponent<RectTransform>())) windowAttachDone = true;
        }

        public void AttachForTest() => TryWindowAttach();
        public bool WindowAttachedForTest => windowAttachDone;
        public Transform WindowParentForTest => frameRoot != null ? frameRoot.transform.parent : null;

        /// <summary>
        /// 戦場ワールド矩形（<b>この会戦の原点を含んだ絶対座標</b>）。
        ///
        /// ★ウィンドウ化会戦は戦場ごとにワールド原点がずれる（<see cref="BattleField.OriginFor"/>）。
        /// <see cref="CameraController.MinBounds"/>／<see cref="CameraController.MaxBounds"/> は<b>原点を含まない</b>
        /// 素の境界で、<see cref="CameraController.ClampPosition"/> だけが原点を足している。
        /// 従来はここで素の境界をそのまま使い、艦やカメラの座標（原点込み）と突き合わせていたため、
        /// 遠方の戦場では全ドットと視界枠がミニマップの端へ貼り付き、クリックしてもカメラが動かなかった。
        /// <b>ここで必ず原点を足して同じ座標系に揃える</b>のが修正の要点。
        ///
        /// 回廊要塞戦（<see cref="CorridorFortressArena"/>）があるときは、カメラ境界ではなく
        /// <b>実際の戦場（水路）の矩形</b>を使う＝ミニマップの形が実戦場と一致する。
        /// </summary>
        private void GetWorldBounds(out Vector2 min, out Vector2 max)
        {
            var arena = CorridorFortressArena.For(gameObject.scene);
            if (arena != null)
            {
                var b = arena.ArenaBounds;
                min = new Vector2(-b.channelHalfLength, -b.channelHalfWidth);
                max = new Vector2(b.channelHalfLength, b.channelHalfWidth);
                // アリーナの原点は transform（BattleSetup が戦場ごと平行移動しても地形と一致する）。
                MinimapProjectionRules.Offset(ref min, ref max, arena.ArenaOrigin);
                return;
            }

            if (camController != null)
            {
                min = camController.MinBounds;
                max = camController.MaxBounds;
            }
            else
            {
                min = new Vector2(-200f, -200f);
                max = new Vector2(200f, 200f);
            }
            // 戦場の原点（ウィンドウ化会戦の遠方オフセット）を足して、艦・カメラと同じ座標系にする。
            MinimapProjectionRules.Offset(ref min, ref max, BattleField.OriginFor(gameObject.scene));
        }

        /// <summary>
        /// 戦場の縦横比を保った描画領域（レターボックス）。
        /// 従来は x と y をパネルの幅・高さへ<b>独立に</b>伸ばしていたため、細長い回廊が正方形に歪んでいた。
        /// </summary>
        private MinimapProjectionRules.Fit CurrentFit()
        {
            GetWorldBounds(out Vector2 min, out Vector2 max);
            return MinimapProjectionRules.FitPreserveAspect(min, max, content.rect.width, content.rect.height);
        }

        /// <summary>ワールド座標→ミニマップ内のローカル座標（content 中央原点・アスペクト保持）。</summary>
        private Vector2 WorldToLocal(Vector2 world)
        {
            GetWorldBounds(out Vector2 min, out Vector2 max);
            return MinimapProjectionRules.WorldToLocal(world, min, max, CurrentFit());
        }

        private void PlotFleets()
        {
            // 自分の会戦シーンの旗艦だけを映す（複数会戦が同時でも他会戦の艦を出さない・WIN-4）。
            // フルスクリーン会戦では当該シーン＝全艦なので従来と同一。
            IReadOnlyList<FleetStrength> flags = FleetRegistry.FlagshipsIn(gameObject.scene);
            int used = 0;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength fs = flags[i];
                if (fs == null || !fs.IsAlive) continue;

                // 選択中の艦隊だけ大きく明るく出す（#C：個々の艦隊と所属軍団を識別できるように）。
                // 全艦に名前を重ねると密集が悪化するので、名前は下の1行キャプションで<b>選択中だけ</b>出す。
                bool selected = IsSelected(fs);
                Image dot = GetPooledDot(dotPool, used, selected ? dotSize * 1.8f : dotSize, "Dot");
                Color c = ResolveColor(fs);
                dot.color = selected ? Color.Lerp(c, Color.white, 0.55f) : c;
                dot.rectTransform.anchoredPosition = WorldToLocal(fs.transform.position);
                dot.enabled = true;
                used++;
            }
            // 余ったドットは隠す
            for (int i = used; i < dotPool.Count; i++) dotPool[i].enabled = false;

            UpdateSelectionCaption();
        }

        /// <summary>
        /// その旗艦が選択中か。選択の唯一の窓口は <see cref="FleetCommander.SelectedFleets"/>
        /// （<see cref="Selectable"/> のリスト）＝ここに選択管理を新設しない。
        /// </summary>
        private bool IsSelected(FleetStrength fs)
        {
            List<Selectable> sel = SelectedInThisScene();
            if (sel == null || fs == null) return false;
            for (int i = 0; i < sel.Count; i++)
                if (sel[i] != null && sel[i].GetComponent<FleetStrength>() == fs) return true;
            return false;
        }

        /// <summary>この会戦シーンの選択（他会戦の選択を混ぜない）。</summary>
        private List<Selectable> SelectedInThisScene()
        {
            if (commander == null) commander = FindInScene<FleetCommander>(gameObject.scene);
            return commander != null ? commander.SelectedFleets : null;
        }

        /// <summary>
        /// ミニマップ下端の1行キャプション（#C）。<b>選択中の艦隊だけ</b>「艦隊名／所属軍団」を出す。
        /// 常時すべての名前を重ねると密集表示が悪化するので、選んだものだけを説明する。
        /// </summary>
        private void UpdateSelectionCaption()
        {
            if (selectionCaption == null) return;

            List<Selectable> sel = SelectedInThisScene();
            FleetStrength one = null;
            int count = 0;
            if (sel != null)
                for (int i = 0; i < sel.Count; i++)
                {
                    if (sel[i] == null || sel[i].gameObject.scene != gameObject.scene) continue;
                    FleetStrength fs = sel[i].GetComponent<FleetStrength>();
                    if (fs == null) continue;
                    count++;
                    if (one == null) one = fs;
                }

            if (one == null) { selectionCaption.text = ""; return; }

            string corps = !string.IsNullOrEmpty(one.corpsName) ? one.corpsName : "独立";
            // 軍団長が乗っている艦隊＝軍団の指揮を担う艦隊（旗艦の有無ではない）。
            if (one.IsCorpsFlagship) corps += "・" + FleetCommandLabelRules.CorpsFlagship;
            string who = one.admiralData != null ? one.admiralData.ShortName : "";
            selectionCaption.text = count > 1
                ? $"{who}（{corps}）ほか {count - 1} 隊"
                : $"{who}（{corps}）";
        }

        /// <summary>地形の部品を1つ作る（作り置きして毎フレーム位置と大きさだけ更新する）。</summary>
        private RectTransform MakeArenaPiece(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(content, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            go.SetActive(false);
            return rt;
        }

        /// <summary>
        /// 回廊要塞の地形をミニマップへ描く（#A）。水路・要塞の実体・封鎖線・制圧線を、
        /// <b>実戦場と同じ矩形・同じ原点</b>で写す＝ミニマップの絵と実際に止まる位置が一致する。
        ///
        /// 要塞は<b>実体半径</b>で描く（封鎖半径ではない）。封鎖半径は「守備が健在なあいだ敵が越えられない線」
        /// というルール上の距離なので、赤い線として別に出す＝大きさと通れなさを混同させない。
        /// アリーナが無い会戦（通常の艦隊戦・攻城戦）では何も出さない。
        /// </summary>
        private void PlotArena()
        {
            var arena = CorridorFortressArena.For(gameObject.scene);
            if (arena == null)
            {
                if (arenaField != null) arenaField.gameObject.SetActive(false);
                if (arenaStandoff != null) arenaStandoff.gameObject.SetActive(false);
                if (arenaBreakthrough != null) arenaBreakthrough.gameObject.SetActive(false);
                if (arenaFortress != null) arenaFortress.gameObject.SetActive(false);
                return;
            }

            GetWorldBounds(out Vector2 min, out Vector2 max);
            MinimapProjectionRules.Fit fit = CurrentFit();
            float px = MinimapProjectionRules.PixelsPerWorld(min, max, fit);
            var b = arena.ArenaBounds;
            Vector2 o = arena.ArenaOrigin;

            // 水路そのもの（アリーナ矩形＝ミニマップの描画領域と一致する）。
            Place(arenaField, Vector2.zero, new Vector2(fit.width, fit.height));

            // 封鎖線・制圧線は縦の細い帯。
            float lineW = Mathf.Max(1.5f, px * 0.6f);
            Place(arenaStandoff,
                  MinimapProjectionRules.WorldToLocal(new Vector2(o.x + arena.StandoffX, o.y), min, max, fit),
                  new Vector2(lineW, fit.height));
            Place(arenaBreakthrough,
                  MinimapProjectionRules.WorldToLocal(new Vector2(o.x + b.breakthroughX, o.y), min, max, fit),
                  new Vector2(lineW, fit.height));

            // 要塞の実体。守備が健在なら明るく、落ちたら暗くする（通れるようになったことが分かる）。
            float d = Mathf.Max(3f, arena.FortressBodyRadius * 2f * px);
            Place(arenaFortress,
                  MinimapProjectionRules.WorldToLocal(new Vector2(o.x + b.fortressX, o.y), min, max, fit),
                  new Vector2(d, d));
            if (arenaFortress != null)
            {
                Image img = arenaFortress.GetComponent<Image>();
                if (img != null)
                    img.color = arena.FortressHolds
                        ? new Color(1f, 0.85f, 0.45f, 0.95f)
                        : new Color(0.55f, 0.58f, 0.62f, 0.8f);
            }
            // 封鎖が解けたら封鎖線も消す（もう止まらないので）。
            if (arenaStandoff != null) arenaStandoff.gameObject.SetActive(arena.FortressHolds);
        }

        private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            if (rt == null) return;
            if (!rt.gameObject.activeSelf) rt.gameObject.SetActive(true);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private void PlotBlackHoles()
        {
            IReadOnlyList<BlackHole> holes = BlackHole.All;
            int used = 0;
            for (int i = 0; i < holes.Count; i++)
            {
                BlackHole bh = holes[i];
                if (bh == null) continue;
                if (bh.gameObject.scene != gameObject.scene) continue; // 自会戦シーンのブラックホールのみ（WIN-4）

                Image icon = GetPooledDot(blackHolePool, used, blackHoleSize, "BlackHole");
                icon.color = BlackHoleColor;
                icon.rectTransform.anchoredPosition = WorldToLocal(bh.transform.position);
                icon.enabled = true;
                used++;
            }
            for (int i = used; i < blackHolePool.Count; i++) blackHolePool[i].enabled = false;
        }

        /// <summary>
        /// 視界枠＝<b>カメラの視界と戦場の交差</b>を描く（見えている範囲だけ）。
        /// 従来は中心だけを戦場内へ丸め、大きさはカメラの視界そのままだったため、端に寄ると枠が
        /// 戦場の外へはみ出し、引くと枠が全面を覆って動かなくなっていた。
        /// </summary>
        private void UpdateViewRect()
        {
            if (cam == null || viewRect == null) return;
            GetWorldBounds(out Vector2 min, out Vector2 max);

            Vector2 camCenter = cam.transform.position;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            if (!MinimapProjectionRules.TryViewportIntersection(camCenter, halfW, halfH, min, max,
                                                                out Vector2 vMin, out Vector2 vMax))
            {
                viewRect.gameObject.SetActive(false);   // 戦場を全く見ていない＝枠を出さない
                return;
            }
            if (!viewRect.gameObject.activeSelf) viewRect.gameObject.SetActive(true);

            MinimapProjectionRules.Fit fit = CurrentFit();
            Vector2 a = MinimapProjectionRules.WorldToLocal(vMin, min, max, fit);
            Vector2 b = MinimapProjectionRules.WorldToLocal(vMax, min, max, fit);

            viewRect.anchoredPosition = (a + b) * 0.5f;
            viewRect.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        }

        /// <summary>
        /// ミニマップ上の左クリック／ドラッグでカメラをその現場へジャンプさせる。
        ///
        /// 逆変換は <see cref="MinimapProjectionRules.LocalToWorld"/>＝描画と<b>同じ矩形・同じ原点</b>を使うので、
        /// クリックした点にカメラが行く（従来は原点なしの座標を渡し、<see cref="CameraController.JumpTo"/> の
        /// クランプ〔原点あり〕に潰されて端へ張り付いていた）。
        /// 複数会戦では<b>手前の窓だけ</b>が反応する（<see cref="BattleViewport.IsFocused"/>）。
        /// </summary>
        private void HandleClickJump()
        {
            if (camController == null || Mouse.current == null) return;
            if (!Mouse.current.leftButton.isPressed) return; // 押下中＝ドラッグ追従
            // 背面の窓のミニマップにも当たって両方の会戦カメラが飛ぶのを防ぐ（ズームと同じゲート）。
            if (!BattleViewport.IsFocused(gameObject.scene)) return;

            Vector2 screen = Mouse.current.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint(content, screen, null)) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, null, out Vector2 local))
                return;

            GetWorldBounds(out Vector2 min, out Vector2 max);
            MinimapProjectionRules.Fit fit = CurrentFit();
            // レターボックスの外（絵の無い余白）は無視する＝戦場の外へ飛ばさない。
            if (Mathf.Abs(local.x) > fit.width * 0.5f || Mathf.Abs(local.y) > fit.height * 0.5f) return;

            camController.JumpTo(MinimapProjectionRules.LocalToWorld(local, min, max, fit));
        }

        /// <summary>艦隊の陣営色（FactionData があればその色・無ければ enum 既定色）。FactionColor と同方針。</summary>
        private static Color ResolveColor(FleetStrength fs)
        {
            if (fs.factionData != null) return fs.factionData.color;
            return fs.faction == Faction.帝国 ? ImperialColor : AllianceColor;
        }

        private Image GetPooledDot(List<Image> pool, int index, float size, string label)
        {
            if (index < pool.Count) return pool[index];

            GameObject obj = new GameObject(label);
            obj.transform.SetParent(content, false);
            Image img = obj.AddComponent<Image>();
            img.raycastTarget = false; // クリック判定は枠（frameImage）だけが担う
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            pool.Add(img);
            return img;
        }

        // ===== UI 構築 =====

        private void Build()
        {
            // Canvas（最前面手前・ポーズメニュー sortingOrder 1000 より下）
            GameObject canvasObj = new GameObject("MinimapCanvas");
            canvasObj.transform.SetParent(transform);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasObj.AddComponent<GraphicRaycaster>(); // IsPointerOverGameObject 用（FleetCommander の誤選択回避）

            // 枠（右下アンカー）＝クリック判定の対象。表示トグル・窓内親替えはこの枠を対象にする（WIN-4）。
            GameObject frame = new GameObject("MinimapFrame");
            frame.transform.SetParent(canvasObj.transform, false);
            frameRoot = frame;
            frameImage = frame.AddComponent<Image>();
            frameImage.color = new Color(0.05f, 0.07f, 0.1f, 0.78f);
            frameImage.raycastTarget = true;
            RectTransform frameRT = frameImage.rectTransform;
            frameRT.anchorMin = frameRT.anchorMax = new Vector2(1f, 0f); // 右下
            frameRT.pivot = new Vector2(1f, 0f);
            frameRT.sizeDelta = new Vector2(panelSize, panelSize);
            frameRT.anchoredPosition = new Vector2(-screenMargin, screenMargin);

            // 内側のマップ領域（padding ぶん縮めた中央 pivot の領域）
            GameObject contentObj = new GameObject("MinimapContent");
            contentObj.transform.SetParent(frame.transform, false);
            content = contentObj.AddComponent<RectTransform>();
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 0.5f);
            content.offsetMin = new Vector2(contentPadding, contentPadding);
            content.offsetMax = new Vector2(-contentPadding, -contentPadding);
            // マスク：はみ出したドットを枠内に収める
            Image maskImg = contentObj.AddComponent<Image>();
            maskImg.color = new Color(0.1f, 0.13f, 0.16f, 0.5f);
            maskImg.raycastTarget = false;
            contentObj.AddComponent<RectMask2D>();

            // 地形（回廊要塞の水路・要塞・封鎖線・制圧線）。艦より奥に置きたいので視界矩形より先に作る。
            arenaField = MakeArenaPiece("ArenaField", new Color(0.16f, 0.20f, 0.26f, 0.85f));
            arenaStandoff = MakeArenaPiece("ArenaStandoff", new Color(1f, 0.45f, 0.35f, 0.75f));
            arenaBreakthrough = MakeArenaPiece("ArenaBreakthrough", new Color(0.55f, 0.95f, 0.65f, 0.75f));
            arenaFortress = MakeArenaPiece("ArenaFortress", new Color(1f, 0.85f, 0.45f, 0.95f));

            // 視界矩形（白の半透明・最後に作って手前へ）
            GameObject vr = new GameObject("ViewRect");
            vr.transform.SetParent(content, false);
            Image vrImg = vr.AddComponent<Image>();
            vrImg.color = ViewRectColor;
            vrImg.raycastTarget = false;
            viewRect = vrImg.rectTransform;
            viewRect.anchorMin = viewRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewRect.pivot = new Vector2(0.5f, 0.5f);
            viewRect.sizeDelta = new Vector2(20f, 20f);

            // 選択中の艦隊の説明（枠の下端に1行だけ）。常時全名を出さないための逃げ道（#C）。
            var capGo = new GameObject("SelectionCaption");
            capGo.transform.SetParent(frame.transform, false);
            selectionCaption = capGo.AddComponent<TMPro.TextMeshProUGUI>();
            selectionCaption.text = "";
            selectionCaption.fontSize = 12f;
            selectionCaption.color = new Color(1f, 0.92f, 0.7f, 0.95f);
            selectionCaption.alignment = TMPro.TextAlignmentOptions.Center;
            selectionCaption.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            selectionCaption.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            selectionCaption.raycastTarget = false;
            var jp = Resources.Load<TMPro.TMP_FontAsset>("JapaneseFont_TMP");
            if (jp != null) selectionCaption.font = jp;
            RectTransform capRT = selectionCaption.rectTransform;
            capRT.anchorMin = new Vector2(0f, 0f);
            capRT.anchorMax = new Vector2(1f, 0f);
            capRT.pivot = new Vector2(0.5f, 0f);
            capRT.sizeDelta = new Vector2(-6f, 16f);
            capRT.anchoredPosition = new Vector2(0f, 2f);
        }

        /// <summary>EventSystem が無ければ生成（IsPointerOverGameObject／クリック判定に必要）。</summary>
        private void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
