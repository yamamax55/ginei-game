using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 複数会戦を同時に潜行・進行させる司令塔（WIN-3 #2570）。各会戦を別シーンへ additive ロードし、
    /// <see cref="BattleWindow"/> を1つずつ生成する。会戦ごとに**固有の遠方オフセット**（<see cref="BattleField"/>）を
    /// 割り当てて空間的に隔離し、<see cref="BattleHandoff"/> はロードを**直列化**しつつスナップショットで保護する
    /// （潜行が重なっても各会戦が自分の受け渡し内容で設定される）。カーソルが乗る窓を**フォーカス**として
    /// <see cref="BattleViewport"/> に流し、その会戦だけがプレイヤー入力を受ける。決着・離脱は各 BattleManager が
    /// <see cref="NotifyBattleEnded"/> を呼び、結果は <see cref="BattleResultQueue"/> 経由で戦略へ直列反映される。
    /// フルスクリーン会戦（<c>windowedBattles=false</c>）はこの経路を通らない（従来どおり）。
    /// </summary>
    public class BattleDirector : MonoBehaviour
    {
        [Tooltip("同時に開ける会戦ウィンドウの上限（超過分の潜行は無視）")]
        public int maxConcurrent = 4;
        [Tooltip("会戦ごとの戦場オフセット間隔（ワールド単位・十分離して相互の映り込み/干渉を防ぐ）")]
        public float offsetSpacing = 60000f;
        [Tooltip("戦場オフセットの基準（戦略マップ原点から十分離す）")]
        public Vector2 offsetBase = new Vector2(0f, 100000f);

        private static BattleDirector instance;

        private static BattleDirector Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("BattleDirector");
                    instance = go.AddComponent<BattleDirector>();
                }
                return instance;
            }
        }

        // 開いている窓と、それが使うオフセットスロット。
        private readonly List<BattleWindow> windows = new List<BattleWindow>();
        private readonly Dictionary<BattleWindow, int> slotOf = new Dictionary<BattleWindow, int>();
        private readonly HashSet<int> usedSlots = new HashSet<int>();

        // ロード直列化（同時に1会戦ずつロードして BattleHandoff の取り合いを防ぐ）。
        private readonly Queue<BattleHandoff.State> loadQueue = new Queue<BattleHandoff.State>();
        private bool loading;
        private BattleHandoff.State currentLoadSnapshot;

        /// <summary>会戦へ潜行する（ウィンドウを開く）。BattleHandoff は呼び出し側が事前に設定済みであること。</summary>
        public static void Open() => Instance.OpenInternal();

        /// <summary>指定シーンの会戦ウィンドウを閉じる（決着・離脱時に BattleManager が呼ぶ）。</summary>
        public static void NotifyBattleEnded(Scene scene)
        {
            if (instance == null) return;
            for (int i = 0; i < instance.windows.Count; i++)
            {
                if (instance.windows[i] != null && instance.windows[i].BattleScene == scene)
                {
                    instance.windows[i].CloseWindow(); // → OnWindowClosed
                    return;
                }
            }
        }

        private void OpenInternal()
        {
            if (windows.Count + loadQueue.Count >= maxConcurrent)
            {
                Debug.LogWarning($"BattleDirector: 同時会戦の上限（{maxConcurrent}）に達したため潜行を見送りました。");
                return;
            }

            // 呼び出し側（GalaxyView の潜行）が今 global へ積んだ受け渡しをスナップショットして退避。
            BattleHandoff.State snap = BattleHandoff.Capture();
            loadQueue.Enqueue(snap);
            Debug.Log($"[WIN3] Open: label={snap.battleLabel} pending={snap.Pending} fleets={snap.fleets.Count} windows={windows.Count} queue={loadQueue.Count} loading={loading}");

            // ロード中なら、その潜行で global を上書きしてしまった分を「進行中の会戦」の内容へ戻す（取り合い防止）。
            if (loading && currentLoadSnapshot != null) BattleHandoff.Restore(currentLoadSnapshot);

            Pump();
        }

        private void Pump()
        {
            if (loading) return;
            if (loadQueue.Count == 0) return;

            currentLoadSnapshot = loadQueue.Dequeue();
            loading = true;
            BattleHandoff.Restore(currentLoadSnapshot); // この会戦の受け渡しを global へ（BattleSetup が読む）

            int slot = AcquireSlot();
            Vector2 worldOffset = offsetBase + new Vector2(slot * offsetSpacing, 0f);
            Vector2 anchoredPos = new Vector2(-140f + (slot % 4) * 48f, 90f - (slot % 4) * 36f);

            GameObject go = new GameObject("BattleWindow");
            go.transform.SetParent(transform, false);
            BattleWindow win = go.AddComponent<BattleWindow>();
            slotOf[win] = slot;
            string title = !string.IsNullOrEmpty(currentLoadSnapshot.battleLabel) ? currentLoadSnapshot.battleLabel : "会戦";
            Debug.Log($"[WIN3] Pump→load: slot={slot} offset={worldOffset} title={title}");
            win.BeginOpen(worldOffset, anchoredPos, title, OnWindowLoaded, OnWindowClosed);
        }

        private void OnWindowLoaded(BattleWindow win)
        {
            windows.Add(win);

            // この会戦の BattleManager に専用の受け渡しスナップショットを注入（global を奪い合わない）。
            BattleManager bm = win.FindBattleManager();
            if (bm != null) bm.SetHandoffContext(currentLoadSnapshot);
            Debug.Log($"[WIN3] OnWindowLoaded: scene={win.BattleScene.name}#{win.BattleScene.handle} cam={(win.Cam != null)} bm={(bm != null)} windowsNow={windows.Count}");

            loading = false;
            currentLoadSnapshot = null;

            if (loadQueue.Count > 0) Pump();
            else BattleHandoff.Clear(); // 全ロード完了＝global を空けて結果キューの反映を通す
        }

        private void OnWindowClosed(BattleWindow win)
        {
            Debug.Log($"[WIN3] OnWindowClosed: scene={win.BattleScene.name}#{win.BattleScene.handle} windowsBefore={windows.Count}\n{System.Environment.StackTrace}");
            windows.Remove(win);
            if (slotOf.TryGetValue(win, out int slot)) { usedSlots.Remove(slot); slotOf.Remove(win); }
            if (BattleViewport.IsFocused(win.BattleScene)) BattleViewport.Clear();
            if (win != null) Destroy(win.gameObject);

            if (windows.Count == 0)
            {
                BattleViewport.Clear();
                GameInput.SetContext(InputContext.戦略); // 会戦が無くなったら戦略入力へ戻す
                Time.timeScale = 1f;                     // 会戦が無くなったら通常速度へ戻す
            }
        }

        private int AcquireSlot()
        {
            int i = 0;
            while (usedSlots.Contains(i)) i++;
            usedSlots.Add(i);
            return i;
        }

        private void Update()
        {
            if (windows.Count == 0) return;

            // 会戦は戦略マップと同じ時間で流す（WIN-3）：統一クロックの速度/ポーズを Time.timeScale へ写す。
            // ＝戦略を一時停止/倍速すると全会戦も同様に追従する（会戦ごとに別時間にはしない）。
            GameClock clock = StrategySession.Clock;
            if (clock != null) Time.timeScale = clock.paused ? 0f : Mathf.Max(0f, clock.speed);

            // フォーカス：カーソルが乗っている窓の会戦だけがプレイヤー入力を受ける。
            BattleWindow focus = null;
            for (int i = 0; i < windows.Count; i++)
            {
                BattleWindow w = windows[i];
                if (w != null && w.Ready && w.ContainsPointer) { focus = w; break; }
            }

            if (focus != null)
            {
                BattleViewport.SetActive(focus.BattleScene, focus.Cam, focus.MapRect);
                GameInput.SetContext(InputContext.会戦);
            }
            else
            {
                // どの窓にもカーソルが無い＝戦略マップ操作。会戦は AI で進む（入力は誰も受けない）。
                GameInput.SetContext(InputContext.戦略);
            }
        }

        /// <summary>いずれかの会戦ウィンドウの上にカーソルがあるか（GalaxyView がマップ操作を譲る判定）。</summary>
        public static bool AnyPointerOverWindow()
        {
            if (instance == null) return false;
            for (int i = 0; i < instance.windows.Count; i++)
                if (instance.windows[i] != null && instance.windows[i].ContainsPointer) return true;
            return false;
        }
    }
}
