using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ginei
{
    /// <summary>
    /// 「窓のドラッグが効かない」の原因を<b>実機で特定する</b>ための一時診断（Play 中のみ・何も保存しない）。
    ///
    /// 推測で直し続けないために、押した瞬間に<b>実際に誰が入力を受け取ったか</b>を出す：
    /// ①その座標のレイキャスト結果（手前から順に全部・Canvas名と sortingOrder つき）
    /// ②各ヒットが <see cref="MapWindowDrag"/>／<see cref="Button"/> を持つか
    /// ③会戦ウィンドウの各パーツ（窓・タイトルバー・グリップ）の<b>実スクリーン矩形</b>
    /// ④押している間の実ポインタ移動量と <see cref="MapWindowDrag.AnyGrabbing"/>
    /// ⑤離したときに、押下時と同じオブジェクトの上にいるか（＝クリックがどこへ入るか）
    ///
    /// これで「イベントが来ていない／来ているが移動量0／移動量はあるが窓側で打ち消されている」を切り分ける。
    /// <see cref="MapWindowDrag.LogEvents"/> と併用する前提。Editor の QA メニューが生成・破棄する。
    /// </summary>
    public class WindowInputDiagnostics : MonoBehaviour
    {
        /// <summary>押している間に移動量を出す間隔（フレーム）。毎フレームだと Console が流れる。</summary>
        public int sampleEveryFrames = 10;

        private static WindowInputDiagnostics instance;
        private static readonly List<RaycastResult> hits = new List<RaycastResult>();

        private bool pressed;
        private Vector2 pressPos;
        private Vector2 lastPos;
        private int frame;
        private string pressedTopTarget = "";

        /// <summary>診断を開始する（既にあれば何もしない）。</summary>
        public static void Enable()
        {
            if (instance != null) return;
            var go = new GameObject("WindowInputDiagnostics") { hideFlags = HideFlags.DontSave };
            instance = go.AddComponent<WindowInputDiagnostics>();
            MapWindowDrag.LogEvents = true;
            Debug.Log("[窓入力診断] 開始しました。会戦ウィンドウのタイトルバー／右下グリップを掴んでみてください。");
            instance.DumpBattleWindowRects();
        }

        /// <summary>診断を止めて片付ける。</summary>
        public static void Disable()
        {
            MapWindowDrag.LogEvents = false;
            if (instance == null) { Debug.Log("[窓入力診断] 動いていません。"); return; }
            Destroy(instance.gameObject);
            instance = null;
            Debug.Log("[窓入力診断] 停止しました。");
        }

        /// <summary>いま診断中か。</summary>
        public static bool IsRunning => instance != null;

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 pos = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressed = true;
                pressPos = pos;
                lastPos = pos;
                frame = 0;
                pressedTopTarget = TopTargetName(pos);
                DumpRaycast("押下", pos);
                DumpBattleWindowRects();
            }
            else if (pressed && mouse.leftButton.isPressed)
            {
                frame++;
                if (frame % Mathf.Max(1, sampleEveryFrames) == 0)
                {
                    Debug.Log($"[窓入力診断] ドラッグ中 pos={pos} 押下からの総移動={(pos - pressPos)} " +
                              $"直近フレーム移動={(pos - lastPos)} AnyGrabbing={MapWindowDrag.AnyGrabbing}");
                    DumpBattleWindowRects();
                }
                lastPos = pos;
            }
            else if (pressed && mouse.leftButton.wasReleasedThisFrame)
            {
                pressed = false;
                string nowTop = TopTargetName(pos);
                Debug.Log($"[窓入力診断] 離した pos={pos} 総移動={(pos - pressPos)} " +
                          $"押下時の最前面=[{pressedTopTarget}] 離した時の最前面=[{nowTop}] " +
                          $"→ 同じなら click はそこへ入る／違うなら別の UI へ抜けている");
                DumpRaycast("離した", pos);
            }
        }

        /// <summary>その座標のレイキャスト結果を手前から全部出す（誰が入力を取ったかの一次情報）。</summary>
        private static void DumpRaycast(string tag, Vector2 screen)
        {
            EventSystem es = EventSystem.current;
            if (es == null) { Debug.LogWarning($"[窓入力診断] {tag}：EventSystem がありません。"); return; }

            var ped = new PointerEventData(es) { position = screen };
            hits.Clear();
            es.RaycastAll(ped, hits);

            var sb = new StringBuilder(256);
            sb.AppendLine($"[窓入力診断] {tag} at {screen}　ヒット {hits.Count} 件（手前から）");
            sb.AppendLine($"  EventSystem={es.name}（シーン {es.gameObject.scene.name}）");
            for (int i = 0; i < hits.Count; i++)
            {
                GameObject go = hits[i].gameObject;
                if (go == null) continue;
                Canvas cv = go.GetComponentInParent<Canvas>();
                sb.Append("  ").Append(i).Append(": ").Append(Path(go));
                sb.Append("　canvas=").Append(cv != null ? cv.name : "なし");
                sb.Append(" order=").Append(cv != null ? cv.sortingOrder.ToString() : "-");
                sb.Append(" scene=").Append(go.scene.name);
                if (go.GetComponentInParent<MapWindowDrag>() != null) sb.Append("　[MapWindowDrag あり]");
                if (go.GetComponentInParent<Button>() != null) sb.Append("　[Button あり]");
                var g = go.GetComponent<Graphic>();
                if (g != null) sb.Append("　raycastTarget=").Append(g.raycastTarget);
                sb.AppendLine();
            }
            if (hits.Count == 0) sb.AppendLine("  （何にも当たっていません＝UI が入力を受け取っていない）");
            Debug.Log(sb.ToString());
        }

        private static string TopTargetName(Vector2 screen)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return "EventSystemなし";
            var ped = new PointerEventData(es) { position = screen };
            hits.Clear();
            es.RaycastAll(ped, hits);
            return hits.Count > 0 && hits[0].gameObject != null ? Path(hits[0].gameObject) : "なし";
        }

        /// <summary>会戦ウィンドウの各パーツの実スクリーン矩形（掴めるはずの場所を実測で出す）。</summary>
        private void DumpBattleWindowRects()
        {
            var win = Object.FindAnyObjectByType<BattleWindow>();
            if (win == null) { Debug.Log("[窓入力診断] BattleWindow が見つかりません（会戦ウィンドウが開いていない）。"); return; }

            var sb = new StringBuilder(256);
            sb.AppendLine("[窓入力診断] 会戦ウィンドウの実スクリーン矩形");
            RectTransform[] rts = win.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rts.Length; i++)
            {
                RectTransform rt = rts[i];
                if (rt == null) continue;
                string n = rt.name;
                // 掴む対象と窓本体だけに絞る（全部出すと読めない）。
                if (n != "Window" && n != "TitleBar" && n != "ResizeGrip" && n != "Root") continue;
                Rect r = ScreenRectOf(rt);
                sb.Append("  ").Append(n)
                  .Append("：x ").Append(Mathf.RoundToInt(r.xMin)).Append("〜").Append(Mathf.RoundToInt(r.xMax))
                  .Append("　y ").Append(Mathf.RoundToInt(r.yMin)).Append("〜").Append(Mathf.RoundToInt(r.yMax))
                  .Append("　active=").Append(rt.gameObject.activeInHierarchy);
                var g = rt.GetComponent<Graphic>();
                if (g != null) sb.Append("　raycastTarget=").Append(g.raycastTarget);
                if (rt.GetComponent<MapWindowDrag>() != null) sb.Append("　[MapWindowDrag]");
                sb.AppendLine();
            }
            sb.Append("  AnyGrabbing=").Append(MapWindowDrag.AnyGrabbing);
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 会戦HUD（艦隊HUD・ミニマップ・メッセージ）の親・実矩形・実文字サイズを出す。
        /// 「HUDが極小」の原因が①親のスケールが小さい ②文字サイズ自体が小さい ③矩形が潰れている、
        /// のどれかを実機で切り分けるためのもの。
        /// </summary>
        public static void DumpBattleHud()
        {
            var hud = Object.FindAnyObjectByType<FleetHUDManager>();
            if (hud == null) { Debug.Log("[窓入力診断] FleetHUDManager が見つかりません。"); return; }

            var sb = new StringBuilder(512);
            sb.AppendLine("[窓入力診断] 会戦HUDの実測");

            Canvas[] canvases = hud.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas c = canvases[i];
                var cs = c.GetComponent<CanvasScaler>();
                sb.Append("  Canvas ").Append(c.name)
                  .Append("　order=").Append(c.sortingOrder)
                  .Append("　scene=").Append(c.gameObject.scene.name)
                  .Append("　scaleFactor=").Append(c.scaleFactor.ToString("0.###"));
                if (cs != null)
                    sb.Append("　scaler=").Append(cs.uiScaleMode)
                      .Append(" ref=").Append(cs.referenceResolution)
                      .Append(" match=").Append(cs.matchWidthOrHeight.ToString("0.##"));
                sb.AppendLine();
            }

            // 会戦ウィンドウとミニマップも同じ物差しで出す（HUDだけ見ても原因が絞れないため）。
            var extraRoots = new List<Transform> { hud.transform };
            var win = Object.FindAnyObjectByType<BattleWindow>();
            if (win != null) extraRoots.Add(win.transform);
            var mini = Object.FindAnyObjectByType<Minimap>();
            if (mini != null) extraRoots.Add(mini.transform);

            var rtList = new List<RectTransform>();
            for (int e = 0; e < extraRoots.Count; e++)
                rtList.AddRange(extraRoots[e].GetComponentsInChildren<RectTransform>(true));
            RectTransform[] rts = rtList.ToArray();

            int hiddenCount = 0;
            for (int i = 0; i < rts.Length; i++)
            {
                RectTransform rt = rts[i];
                if (rt == null) continue;
                Rect r = ScreenRectOf(rt);
                // 潰れている/極小のものだけ拾うと見落とすので、主要な親と文字を出す。
                var txt = rt.GetComponent<TMPro.TMP_Text>();
                bool interesting = txt != null || rt.name.Contains("Panel") || rt.name.Contains("HUD")
                                   || rt.name.Contains("Message") || rt.name.Contains("Minimap")
                                   || rt.name.Contains("Row") || rt.name.Contains("Bar");
                if (!interesting) continue;
                // 非表示は件数だけ数える（前回はこれが混ざって現役と読み違えた）。
                if (!rt.gameObject.activeInHierarchy) { hiddenCount++; continue; }

                // ★表示中かどうかを必ず出す。これが無いと、シーンに手置きされた「隠してある旧HUD」を
                // 現役だと読み違える（前回の実測でその取り違えが起きた）。
                sb.Append("  [表示] ").Append(rt.name)
                  .Append("　親=").Append(rt.parent != null ? rt.parent.name : "なし")
                  .Append("　scale=").Append(rt.lossyScale.x.ToString("0.###"))
                  .Append("　幅x高=").Append(Mathf.RoundToInt(r.width)).Append("x").Append(Mathf.RoundToInt(r.height))
                  .Append("　x ").Append(Mathf.RoundToInt(r.xMin)).Append("〜").Append(Mathf.RoundToInt(r.xMax))
                  .Append("　y ").Append(Mathf.RoundToInt(r.yMin)).Append("〜").Append(Mathf.RoundToInt(r.yMax));
                if (txt != null)
                    sb.Append("　fontSize=").Append(txt.fontSize.ToString("0.#"))
                      .Append("　実px≒").Append((txt.fontSize * rt.lossyScale.y).ToString("0.#"))
                      .Append("　auto=").Append(txt.enableAutoSizing);
                sb.AppendLine();
            }
            sb.Append("  （非表示のため省いた要素 ").Append(hiddenCount).Append(" 件＝旧シーンHUD等）");
            Debug.Log(sb.ToString());
        }

        /// <summary>ScreenSpaceOverlay の RectTransform の実スクリーン矩形（world 座標＝スクリーン座標）。</summary>
        private static Rect ScreenRectOf(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = Mathf.Min(c[0].x, c[2].x), xMax = Mathf.Max(c[0].x, c[2].x);
            float yMin = Mathf.Min(c[0].y, c[2].y), yMax = Mathf.Max(c[0].y, c[2].y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static string Path(GameObject go)
        {
            var sb = new StringBuilder(64);
            Transform t = go.transform;
            sb.Append(t.name);
            int guard = 0;
            while (t.parent != null && guard++ < 12)
            {
                t = t.parent;
                sb.Insert(0, t.name + "/");
            }
            return sb.ToString();
        }
    }
}
