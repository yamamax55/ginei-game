using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Ginei
{
    /// <summary>
    /// 入力まわりの<b>読み取り専用</b>診断（Play 中のみ）。
    /// 「ボタンを押しても効かない」が<b>自動入力の制約なのか実装不具合なのか</b>を切り分けるための窓。
    ///
    /// <b>何も変更しない</b>：EventSystem もモジュールもボタンも状態を書き換えず、
    /// いま何がどうなっているかを Console とダイアログへ出すだけ。
    /// 通常の入力経路には一切割り込まない（ここから停止／再開を代行することもしない）。
    /// </summary>
    public static class InputDiagnosticsQaMenu
    {
        [MenuItem("Ginei/QA: 入力診断 EventSystem とボタンの状態を出力（Play中）", false, 360)]
        public static void Dump()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("入力診断", "Play 中に実行してください。", "OK");
                return;
            }

            var sb = new StringBuilder();

            // ===== 1. EventSystem =====
            EventSystem[] all = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.Append("■ EventSystem：").Append(all.Length).Append(" 個");
            if (all.Length > 1) sb.Append("　★複数あります（どれが効くか読めません）");
            sb.Append('\n');

            for (int i = 0; i < all.Length; i++)
            {
                EventSystem es = all[i];
                if (es == null) continue;
                sb.Append("  [").Append(i).Append("] ").Append(es.gameObject.name)
                  .Append("　シーン=").Append(es.gameObject.scene.name)
                  .Append("　アクティブ=").Append(es.gameObject.activeInHierarchy)
                  .Append("　有効=").Append(es.enabled)
                  .Append(es == EventSystem.current ? "　← current" : "")
                  .Append('\n');

                var module = es.GetComponent<InputSystemUIInputModule>();
                if (module == null)
                {
                    BaseInputModule other = es.currentInputModule;
                    sb.Append("      入力モジュール：InputSystemUIInputModule が無い（")
                      .Append(other != null ? other.GetType().Name : "モジュールなし")
                      .Append("）★新 Input System ではボタンが反応しません\n");
                    continue;
                }

                sb.Append("      モジュール有効=").Append(module.enabled)
                  .Append("　actionsAsset=").Append(module.actionsAsset != null ? "あり" : "★なし")
                  .Append("　point=").Append(module.point != null ? "割当" : "★未割当")
                  .Append("　leftClick=").Append(module.leftClick != null ? "割当" : "★未割当")
                  .Append('\n');
                if (module.actionsAsset != null)
                    sb.Append("      actionsAsset 有効=").Append(module.actionsAsset.enabled).Append('\n');
            }

            // ===== 2. 入力デバイスと Input System の設定 =====
            sb.Append("\n■ 入力デバイス\n");
            sb.Append("  Mouse.current=").Append(Mouse.current != null ? "あり" : "★なし")
              .Append("　Keyboard.current=").Append(Keyboard.current != null ? "あり" : "★なし").Append('\n');
            if (Mouse.current != null)
                sb.Append("  マウス座標=").Append(Mouse.current.position.ReadValue())
                  .Append("　左ボタン押下中=").Append(Mouse.current.leftButton.isPressed).Append('\n');
            sb.Append("  updateMode=").Append(InputSystem.settings.updateMode)
              .Append("（FixedUpdate 処理だと timeScale=0 で入力が止まります）\n");
            sb.Append("  backgroundBehavior=").Append(InputSystem.settings.backgroundBehavior).Append('\n');
            sb.Append("  Application.isFocused=").Append(Application.isFocused)
              .Append("（false ならゲームビューにフォーカスが無く、自動入力が届きません）\n");

            // ===== 3. 時間の状態 =====
            sb.Append("\n■ 時間\n");
            sb.Append("  Time.timeScale=").Append(Time.timeScale)
              .Append("　unscaledTime=").Append(Time.unscaledTime.ToString("0.0")).Append('\n');

            PauseManager pm = Object.FindAnyObjectByType<PauseManager>();
            if (pm == null)
            {
                sb.Append("  PauseManager：★ありません（この会戦では停止／再開できません）\n");
            }
            else
            {
                sb.Append("  PauseManager 有効=").Append(pm.isActiveAndEnabled)
                  .Append("　IsPaused=").Append(pm.IsPaused)
                  .Append("　アクティブポーズ入力可=").Append(PauseManager.IsActivePauseInputAllowed).Append('\n');
                sb.Append("  ガード：モーダルへ譲る=").Append(PauseManager.IsTimeInputDeferred)
                  .Append("（艦隊詳細/編制）　システムUI表示中=").Append(pm.IsSystemUiShown)
                  .Append("（メニュー/設定）\n");
                if (PauseManager.IsTimeInputDeferred || pm.IsSystemUiShown)
                    sb.Append("      ★このどちらかが true の間はボタンが隠れ、押しても見送られます\n");
            }

            // ===== 3.5 盤面入力の関門（FleetCommander.Update の早期 return） =====
            // ★ここが今回の診断で欠けていた観測点。
            //   FleetCommander は毎フレーム先頭で2つの関門を通る。どちらかで return すると
            //   艦隊の選択も右クリックも<b>一切届かない</b>（例外もログも出ない）。
            //   停止／再開ボタンはこの関門を通らないので、「ボタンは効くのに盤面だけ無反応」になりうる。
            sb.Append("\n■ 盤面入力の関門（FleetCommander）\n");
            sb.Append("  関門1 timeScale==0 && !アクティブポーズ入力可 → ")
              .Append(Time.timeScale == 0f && !PauseManager.IsActivePauseInputAllowed ? "★遮断" : "通過")
              .Append('\n');
            sb.Append("  BattleViewport.Active=").Append(BattleViewport.Active)
              .Append("　FocusedScene=")
              .Append(BattleViewport.Active ? SceneLabel(BattleViewport.FocusedScene) : "－")
              .Append("　PointerInside=").Append(BattleViewport.PointerInside).Append('\n');

            FleetCommander[] commanders = Object.FindObjectsByType<FleetCommander>(FindObjectsSortMode.None);
            if (commanders.Length == 0) sb.Append("  （FleetCommander がありません）\n");
            for (int i = 0; i < commanders.Length; i++)
            {
                FleetCommander fc = commanders[i];
                if (fc == null) continue;
                bool blocked2 = BattleViewport.Active && !BattleViewport.IsFocused(fc.gameObject.scene);
                sb.Append("  [").Append(i).Append("] シーン=").Append(SceneLabel(fc.gameObject.scene))
                  .Append("　関門2 窓フォーカス → ").Append(blocked2 ? "★遮断" : "通過")
                  .Append("　選択中=").Append(fc.SelectedFleets.Count).Append(" 隊")
                  .Append("　移動先待ち=").Append(fc.IsWaitingForMoveTarget)
                  .Append("　攻撃目標待ち=").Append(fc.IsWaitingForAttackTarget)
                  .Append('\n');
                if (blocked2)
                    sb.Append("      ★このシーンの盤面入力は関門2で捨てられています。\n")
                      .Append("        BattleViewport はウィンドウ化会戦（戦略の窓）が立てる static で、\n")
                      .Append("        自動リセットの経路がありません（BattleDirector に OnDestroy なし・\n")
                      .Append("        Clear は窓を閉じたときだけ）。戦略から窓会戦を開いたまま\n")
                      .Append("        フルスクリーン会戦へ遷移すると、立ったまま残りうる状態です。\n");
            }

            // ===== 4. 停止／再開ボタン =====
            sb.Append("\n■ 停止／再開ボタン\n");
            BattlePauseButton btn = BattlePauseButton.Instance;
            if (btn == null)
            {
                sb.Append("  ★ありません（フルスクリーン会戦でのみ生成します。ウィンドウ化会戦では出しません）\n");
            }
            else
            {
                sb.Append("  表示中=").Append(btn.IsShown)
                  .Append("　押せる=").Append(btn.IsInteractable)
                  .Append("　ポインタが上にある=").Append(BattlePauseButton.IsPointerOverButton()).Append('\n');
                sb.Append("  直近のクリック経路：").Append(BattlePauseButton.LastClickPath).Append('\n');
                sb.Append("  到達回数：EventSystem 経由=").Append(BattlePauseButton.EventSystemClickCount)
                  .Append(" 回　直接判定=").Append(BattlePauseButton.FallbackClickCount).Append(" 回\n");
                sb.Append("  EventSystem 経由が 0 で直接判定が増えていれば、原因は EventSystem 側です。\n");
                sb.Append("  どちらも 0 なら、このボタンが押されていないというだけです。\n");
                sb.Append("  ※これはボタン内部の計数であって、生のマウス入力が来ていない証明にはなりません\n");
                sb.Append("    （ポインタがボタン上に無ければ 0 のままです）。生入力の有無は\n");
                sb.Append("    「QA: 入力診断 生入力の記録」で確かめてください。\n");
            }

            // ===== 5. いまポインタの下にある UI =====
            sb.Append("\n■ ポインタ直下の UI\n");
            EventSystem cur = EventSystem.current;
            if (cur == null || Mouse.current == null)
            {
                sb.Append("  （EventSystem かマウスがありません）\n");
            }
            else
            {
                sb.Append("  IsPointerOverGameObject=").Append(cur.IsPointerOverGameObject()).Append('\n');
                // ★押してから離すまでに この画素数より動くと「ドラッグ」と見なされ、
                //   PointerClick が飛ばない＝押下の変色だけ出て onClick が来ない、という症状になる。
                sb.Append("  pixelDragThreshold=").Append(cur.pixelDragThreshold)
                  .Append("（押下〜離すの間にこれ以上動くとクリックでなくドラッグ扱い）\n");
                var data = new PointerEventData(cur) { position = Mouse.current.position.ReadValue() };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                cur.RaycastAll(data, hits);
                if (hits.Count == 0) sb.Append("  （何も当たっていません）\n");
                for (int i = 0; i < hits.Count && i < 5; i++)
                    sb.Append("  ").Append(i).Append(": ").Append(hits[i].gameObject.name)
                      .Append("　sortingOrder=").Append(hits[i].sortingOrder).Append('\n');
            }

            string msg = sb.ToString();
            Debug.Log("[入力診断]\n" + msg);
            EditorUtility.DisplayDialog("入力診断（読み取り専用）", msg, "OK");
        }

        /// <summary>シーンの表示名（無効なシーンも読めるように）。</summary>
        private static string SceneLabel(UnityEngine.SceneManagement.Scene s)
            => s.IsValid() ? (string.IsNullOrEmpty(s.name) ? "(名前なし)" : s.name) : "(無効なシーン)";

        // ===== 生入力の記録（どこで消えているかを切り分ける） =====

        [MenuItem("Ginei/QA: 入力診断 生入力の記録を開始／停止（Play中）", false, 361)]
        public static void ToggleTrace()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("入力診断", "Play 中に実行してください。", "OK");
                return;
            }

            InputTraceRecorder existing = Object.FindFirstObjectByType<InputTraceRecorder>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
                EditorUtility.DisplayDialog("入力診断", "生入力の記録を停止しました。", "OK");
                return;
            }

            var go = new GameObject("QA_InputTraceRecorder");
            go.AddComponent<InputTraceRecorder>();
            EditorUtility.DisplayDialog("入力診断",
                "生入力の記録を開始しました。\n\n" +
                "毎フレーム（Play のフレーム）マウスの押下／離上を見て、\n" +
                "そのときの座標・UI 判定・盤面の関門・選択数を記録します。\n\n" +
                "問題の操作を数回ためしてから\n" +
                "「QA: 入力診断 生入力の記録を出力」を実行してください。\n\n" +
                "※記録するだけで、入力も状態も変えません。", "OK");
        }

        [MenuItem("Ginei/QA: 入力診断 生入力の記録を出力（Play中）", false, 362)]
        public static void DumpTrace()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("入力診断", "Play 中に実行してください。", "OK");
                return;
            }

            InputTraceRecorder rec = Object.FindFirstObjectByType<InputTraceRecorder>();
            if (rec == null)
            {
                EditorUtility.DisplayDialog("入力診断",
                    "記録していません。先に「生入力の記録を開始」を実行してください。", "OK");
                return;
            }

            string msg = rec.Dump();
            Debug.Log("[入力診断・生入力]\n" + msg);
            EditorUtility.DisplayDialog("入力診断（生入力）",
                rec.EventCount + " 件記録しました。\n\n全文は Console の [入力診断・生入力] に出しています。", "OK");
        }
    }

    /// <summary>
    /// マウスの<b>生入力</b>を Play のフレームごとに見て、有限の履歴へ残す QA 専用の記録係。
    ///
    /// 「ボタンのクリック数が 0」だけでは<b>生の入力が来ていない証明にはならない</b>ため、
    /// 次の3つを切り分けられるようにする：
    /// <list type="number">
    ///   <item>生の押下／離上が<b>そもそも来ていない</b>（＝自動入力がアプリへ届いていない）</item>
    ///   <item>生は来ているが <b>EventSystem が拾っていない</b>（UI の重なり・ドラッグ判定）</item>
    ///   <item>両方来ているが <b>盤面の関門で捨てられている</b>（FleetCommander の早期 return）</item>
    /// </list>
    ///
    /// <b>記録するだけ</b>で、入力にも状態にも触れない。Editor アセンブリにあるので製品には入らない。
    /// </summary>
    public class InputTraceRecorder : MonoBehaviour
    {
        /// <summary>残す履歴の上限（古いものから捨てる）。</summary>
        private const int Capacity = 240;

        private readonly System.Collections.Generic.List<string> lines =
            new System.Collections.Generic.List<string>();

        private int lastSelectionCount = -1;

        /// <summary>記録した件数。</summary>
        public int EventCount => lines.Count;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            bool down = mouse.leftButton.wasPressedThisFrame;
            bool up = mouse.leftButton.wasReleasedThisFrame;
            bool rightDown = mouse.rightButton.wasPressedThisFrame;

            // 選択数の変化も1件として拾う（クリックが届いたのに選択が変わらない、を見分ける）。
            FleetCommander fc = Object.FindFirstObjectByType<FleetCommander>();
            int selection = fc != null ? fc.SelectedFleets.Count : -1;
            bool selectionChanged = selection != lastSelectionCount;
            lastSelectionCount = selection;

            if (!down && !up && !rightDown && !selectionChanged) return;

            var sb = new System.Text.StringBuilder();
            sb.Append("F").Append(Time.frameCount)
              .Append(" t=").Append(Time.unscaledTime.ToString("0.00"))
              .Append(" ts=").Append(Time.timeScale.ToString("0.0"))
              .Append(down ? " [左押下]" : "").Append(up ? " [左離上]" : "")
              .Append(rightDown ? " [右押下]" : "")
              .Append(selectionChanged ? " [選択変化]" : "");

            sb.Append(" pos=").Append(mouse.position.ReadValue().ToString("0"));

            EventSystem es = EventSystem.current;
            sb.Append(" overUI=").Append(es != null ? es.IsPointerOverGameObject().ToString() : "ESなし");

            if (es != null && (down || up))
            {
                var data = new PointerEventData(es) { position = mouse.position.ReadValue() };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                es.RaycastAll(data, hits);
                sb.Append(" 直下=").Append(hits.Count > 0 ? hits[0].gameObject.name : "なし");
            }

            // 盤面の関門（ここで捨てられていれば、生入力が来ていても何も起きない）。
            bool gate1 = Time.timeScale == 0f && !PauseManager.IsActivePauseInputAllowed;
            bool gate2 = fc != null && BattleViewport.Active
                         && !BattleViewport.IsFocused(fc.gameObject.scene);
            sb.Append(" 関門1=").Append(gate1 ? "遮断" : "通過")
              .Append(" 関門2=").Append(gate2 ? "遮断" : "通過")
              .Append(" 選択数=").Append(selection);

            lines.Add(sb.ToString());
            if (lines.Count > Capacity) lines.RemoveAt(0);
        }

        /// <summary>記録の全文と、読み取りの手引き。</summary>
        public string Dump()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("生入力の記録（新しいものが下）。\n");
            sb.Append("読み方：\n");
            sb.Append("  ・[左押下]/[左離上] が1件も無い → この記録では観測できなかった、という意味。\n");
            sb.Append("    ★OSから届いていない証明ではありません。本記録は Update（Play のフレーム）で\n");
            sb.Append("      見ているため、フレーム間で完結した入力や、記録開始前／停止後の入力は写りません。\n");
            sb.Append("      手動クリックで1回出るか試すと切り分けられます。\n");
            sb.Append("  ・押下/離上はあるが overUI=False で 直下=なし → その位置に UI は無かった（盤面の話）\n");
            sb.Append("  ・押下/離上はあるのに [選択変化] が続かない → 関門か選択判定で消えた可能性\n");
            sb.Append("  ・関門2=遮断 → その瞬間は盤面入力が捨てられている\n");
            sb.Append("  ・押下と離上で座標が10px以上動く → ドラッグ扱いになりうる（pixelDragThreshold）。\n");
            sb.Append("    ★フレーム差が大きいだけではドラッグとは限りません（長押しでも座標が動かなければ\n");
            sb.Append("      クリックとして扱われます）。判断は座標の移動量で行ってください。\n\n");
            if (lines.Count == 0) sb.Append("（記録なし＝押下も離上も選択変化も一度も観測していません）\n");
            for (int i = 0; i < lines.Count; i++) sb.Append(lines[i]).Append('\n');
            return sb.ToString();
        }
    }
}
