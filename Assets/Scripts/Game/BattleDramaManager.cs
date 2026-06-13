using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 決定的瞬間の演出（#2261・ヒットストップ＋カットイン）。
    /// <para>
    /// <c>NotificationCenter</c> の新着（<c>Since</c> による差分）を間引きで監視し、
    /// 戦闘カテゴリかつ重要な通知（重要度 警告/注意 またはキーワード一致）が来たら
    /// 短時間のヒットストップ（<c>Time.timeScale</c> 一時減速）と
    /// 画面カットイン（帯テキストUI + 既存 <c>CameraController.Shake</c>）を発火する。
    /// </para>
    /// 演出のみ＝状態は一切変えない。Battle シーンに自動生成。
    /// </summary>
    public class BattleDramaManager : MonoBehaviour
    {
        // ===== Inspector 調整値 =====

        [Header("通知監視")]
        [Tooltip("NotificationCenter の新着チェック間隔（秒・unscaled）")]
        public float checkInterval = 0.25f;

        [Header("ヒットストップ")]
        [Tooltip("ヒットストップ中の timeScale 倍率（0.2 = 通常の 1/5 の速さ）")]
        public float hitstopScale = 0.2f;

        [Tooltip("ヒットストップの持続時間（unscaled 秒）")]
        public float hitstopDuration = 0.15f;

        [Header("カットイン")]
        [Tooltip("カットインテキストの表示時間（unscaled 秒）")]
        public float cutinDuration = 2.5f;

        [Header("間引き")]
        [Tooltip("演出連発を防ぐ最小クールダウン（unscaled 秒）")]
        public float cooldown = 3.0f;

        // ===== 内部状態 =====

        /// <summary>最後に処理した通知の seq（起動時に LastSeq を代入して既往分をスキップ）。</summary>
        private long lastSeq;

        /// <summary>次に新着チェックできる unscaled 時刻。</summary>
        private float nextCheck;

        /// <summary>ヒットストップのコルーチンが走っているか（重複発火ガード）。</summary>
        private bool isDramaActive;

        /// <summary>最後に演出を発火した unscaled 時刻（クールダウン管理）。</summary>
        private float lastDramaTime = float.NegativeInfinity;

        // カットインUI
        private Canvas cutinCanvas;
        private TextMeshProUGUI cutinText;
        private Image cutinBg;
        private Coroutine cutinRoutine;

        // ===== 自動生成（BattleMomentumHud / BattleEventManager と同パターン）=====

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
            if (FindAnyObjectByType<BattleDramaManager>() != null) return;
            new GameObject("BattleDramaManager").AddComponent<BattleDramaManager>();
        }

        // ===== Unity ライフサイクル =====

        private void Start()
        {
            // 起動時点の既往通知は無視して、ここ以降の新着だけを対象にする。
            lastSeq = NotificationCenter.LastSeq;
            nextCheck = Time.unscaledTime + checkInterval;
            BuildCutinUI();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + Mathf.Max(0.05f, checkInterval);

            // 演出中・クールダウン中は新着の seq だけ進めて演出はスキップ。
            if (isDramaActive || Time.unscaledTime - lastDramaTime < cooldown)
            {
                AdvanceSeq(); // 積みあがった通知を読み飛ばし、次のクールダウン後に最新から再開。
                return;
            }

            CheckNotifications();
        }

        // ===== 通知監視 =====

        /// <summary>演出せずに seq だけ最新まで進める（クールダウン中の読み飛ばし用）。</summary>
        private void AdvanceSeq()
        {
            long current = NotificationCenter.LastSeq;
            if (current > lastSeq) lastSeq = current;
        }

        /// <summary>新着通知を走査し、演出対象があれば発火する（1フレームに1件まで）。</summary>
        private void CheckNotifications()
        {
            var news = NotificationCenter.Since(lastSeq);
            if (news == null || news.Count == 0) return;

            Notification? target = null;

            for (int i = 0; i < news.Count; i++)
            {
                var n = news[i];
                // seq は常に最新まで追いつかせる。
                if (n.seq > lastSeq) lastSeq = n.seq;

                // 演出対象を先着1件だけ記憶（残りは seq を読み進めるだけ）。
                if (target == null && IsDramaWorthy(n)) target = n;
            }

            if (target.HasValue) TriggerDrama(target.Value);
        }

        /// <summary>この通知が演出を発火するべきかを判定する（状態変更なし）。</summary>
        private static bool IsDramaWorthy(Notification n)
        {
            // 戦闘カテゴリのみ対象。
            if (n.category != NotificationCategory.戦闘) return false;

            // 重要度「警告」は常に演出する（旗艦撃墜など）。
            if (n.severity == NotificationSeverity.警告) return true;

            // 重要度「注意」かつドラマキーワードを含む場合のみ演出する。
            if (n.severity == NotificationSeverity.注意)
                return ContainsDramaKeyword(n.message);

            return false;
        }

        /// <summary>
        /// 決定的瞬間のキーワード一覧。
        /// 既存の通知メッセージ（FleetStrength / BattleAllegianceManager / BattleSetup）に合わせた表記。
        /// </summary>
        private static readonly string[] Keywords =
        {
            "撃墜",       // 旗艦撃墜（FleetStrength.DestroyFlagship）
            "捨てがまり", // 島津の捨てがまり（FleetStrength.ResolveFlagshipDown）
            "寝返",       // 寝返り・寝返った（BattleAllegianceManager.ApplyDefection）
            "増援到着",   // 増援部隊の到着（BattleSetup.SpawnReinforcement）
            "旗艦撃破",   // 勝利条件・旗艦撃破イベント
            "旗幟",       // 旗幟変更系
        };

        private static bool ContainsDramaKeyword(string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            for (int i = 0; i < Keywords.Length; i++)
                if (message.IndexOf(Keywords[i], System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        // ===== 演出の発火 =====

        /// <summary>ヒットストップとカットインを同時に発火する。演出のみ・状態変更なし。</summary>
        private void TriggerDrama(Notification n)
        {
            // ポーズ中（timeScale==0）は発火しない（timeScale を復帰させると意図しない解除が起きる）。
            if (Mathf.Approximately(Time.timeScale, 0f)) return;

            isDramaActive = true;
            lastDramaTime = Time.unscaledTime;

            // ヒットストップ。
            StartCoroutine(HitstopRoutine());

            // カットイン表示（メッセージの先頭部を整形して表示）。
            string label = BuildCutinLabel(n.message);
            ShowCutin(label, n.severity);

            // 警告（旗艦撃墜等）のみカメラシェイク（注意は揺らしすぎない）。
            if (n.severity == NotificationSeverity.警告)
            {
                CameraController cam = Object.FindAnyObjectByType<CameraController>();
                if (cam != null) cam.Shake();
            }
        }

        /// <summary>通知メッセージを画面に出す短いラベルへ整形する（最大40文字）。</summary>
        private static string BuildCutinLabel(string message)
        {
            if (string.IsNullOrEmpty(message)) return "決定的瞬間！";

            string trimmed = message;

            // 「：」があれば先頭部分（艦隊名や種別）＋後続の必要最低限だけ残す。
            int colon = trimmed.IndexOf('：');
            if (colon > 0 && colon < trimmed.Length - 1)
            {
                // 「：」の直後から最大12文字追加（詳細を少し見せる）。
                int after = System.Math.Min(trimmed.Length, colon + 13);
                trimmed = trimmed.Substring(0, after);
            }

            if (trimmed.Length > 40) trimmed = trimmed.Substring(0, 40) + "…";
            return trimmed;
        }

        // ===== ヒットストップコルーチン =====

        private IEnumerator HitstopRoutine()
        {
            // 現在の timeScale を記録し、hitstopScale 倍に下げる。
            // PauseManager の savedTimeScale フィールドには触れない（競合を避ける）。
            float original = Time.timeScale;
            Time.timeScale = original * Mathf.Clamp01(hitstopScale);

            // unscaled 時間でカウントするため yield return null を繰り返す（WaitForSeconds は timeScale に従う）。
            float endTime = Time.unscaledTime + Mathf.Max(0.01f, hitstopDuration);
            while (Time.unscaledTime < endTime)
                yield return null;

            // 他のコンポーネントがこの間に timeScale=0 にしていたら timeScale は復帰させない。
            // original が正の値だった場合のみ元に戻す（ポーズと競合させない）。
            if (original > 0f && !Mathf.Approximately(Time.timeScale, 0f))
                Time.timeScale = original;

            isDramaActive = false;
        }

        // ===== カットインUI（uGUI・実行時生成） =====

        /// <summary>カットイン帯テキストを duration 秒間表示する（unscaled 時間）。</summary>
        private void ShowCutin(string label, NotificationSeverity severity)
        {
            if (cutinCanvas == null || cutinText == null) return;

            cutinText.text = label;

            // 重要度で色を変える。
            Color textColor;
            Color bgColor;
            switch (severity)
            {
                case NotificationSeverity.警告:
                    textColor = new Color(1f,  0.42f, 0.30f, 1f);
                    bgColor   = new Color(0.22f, 0.04f, 0.04f, 0.90f);
                    break;
                case NotificationSeverity.注意:
                    textColor = new Color(1f,  0.92f, 0.30f, 1f);
                    bgColor   = new Color(0.15f, 0.12f, 0.02f, 0.90f);
                    break;
                default:
                    textColor = new Color(0.70f, 0.95f, 1f,  1f);
                    bgColor   = new Color(0.03f, 0.10f, 0.20f, 0.90f);
                    break;
            }
            cutinText.color = textColor;
            if (cutinBg != null) cutinBg.color = bgColor;

            cutinCanvas.gameObject.SetActive(true);

            // 既存コルーチンを止めて再起動（連続演出で残像が出ない）。
            if (cutinRoutine != null) StopCoroutine(cutinRoutine);
            cutinRoutine = StartCoroutine(HideCutinAfter(cutinDuration));
        }

        private IEnumerator HideCutinAfter(float duration)
        {
            // WaitForSecondsRealtime = unscaled＝ポーズ中でも消える。
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, duration));
            if (cutinCanvas != null) cutinCanvas.gameObject.SetActive(false);
            cutinRoutine = null;
        }

        /// <summary>
        /// 画面下 1/4 付近に横帯テキストを出すカットインUIを実行時生成する。
        /// sortingOrder=85（モーダル90 の手前・HUD 40 の奥）。
        /// </summary>
        private void BuildCutinUI()
        {
            var canvasGo = new GameObject("BattleDramaCanvas");
            canvasGo.transform.SetParent(transform, false);

            cutinCanvas = canvasGo.AddComponent<Canvas>();
            cutinCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cutinCanvas.sortingOrder = 85;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // ---- 背景帯 ----
            var bgGo = new GameObject("CutinBG");
            bgGo.transform.SetParent(canvasGo.transform, false);
            cutinBg = bgGo.AddComponent<Image>();
            cutinBg.color = new Color(0.15f, 0.12f, 0.02f, 0.90f);

            var bgRt = cutinBg.rectTransform;
            // 画面下 23% の高さに横帯（左右いっぱい・縦 70px）。
            bgRt.anchorMin = new Vector2(0f, 0.23f);
            bgRt.anchorMax = new Vector2(1f, 0.23f);
            bgRt.pivot     = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(0f, 70f); // anchorMin.x=0 / anchorMax.x=1 で横幅は自動。

            // ---- テキスト ----
            var textGo = new GameObject("CutinText");
            textGo.transform.SetParent(canvasGo.transform, false);
            cutinText = textGo.AddComponent<TextMeshProUGUI>();
            cutinText.fontSize  = 30;
            cutinText.fontStyle = FontStyles.Bold;
            cutinText.alignment = TextAlignmentOptions.Center;
            cutinText.color     = new Color(1f, 0.92f, 0.30f, 1f);

            // 日本語フォント（BattleEventManager と同じ解決順）。
            var font = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (font != null) cutinText.font = font;

            var textRt = cutinText.rectTransform;
            textRt.anchorMin = new Vector2(0f, 0.23f);
            textRt.anchorMax = new Vector2(1f, 0.23f);
            textRt.pivot     = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = Vector2.zero;
            textRt.sizeDelta = new Vector2(0f, 70f);

            // 初期は非表示。
            canvasGo.SetActive(false);
        }
    }
}
