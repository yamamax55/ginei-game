using System.Collections;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 手触り（ジュース）の単一窓口（DEV効率化①・外部依存なし＝DOTween 不要）。
    /// 曲線は <see cref="Easing"/>（Core・test済）に委譲し、ここは Transform/色/α の補間と宿主管理だけ。
    /// timeScale 規約：UI 演出は <paramref name="unscaled"/>=true（ポーズ/倍速でも一定）、
    /// 盤面（艦の揺れ等）は false（ポーズで止まる）。対象が破棄されたら安全に中断（null チェック）。
    /// 各所に散在する手書き Lerp/Shake の集約先（新しい演出はここへ足す）。
    /// </summary>
    public static class Juice
    {
        // ── UI/オブジェクトのポップ（強調・出現） ──

        /// <summary>一瞬だけ拡大して戻る強調（クリック/更新の手応え）。</summary>
        public static Coroutine ScalePunch(Transform target, float strength = 0.18f, float duration = 0.18f, bool unscaled = true)
        {
            if (target == null) return null;
            return JuiceRunner.Instance.StartCoroutine(ScalePunchRoutine(target, strength, duration, unscaled));
        }

        private static IEnumerator ScalePunchRoutine(Transform t, float strength, float duration, bool unscaled)
        {
            if (t == null) yield break;
            Vector3 baseScale = t.localScale;
            float e = 0f;
            while (e < duration && t != null)
            {
                e += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float p = Easing.Pulse(Mathf.Clamp01(e / Mathf.Max(0.0001f, duration))); // 0→1→0
                t.localScale = baseScale * (1f + strength * p);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        /// <summary>小さい状態から行き過ぎて1へ収まる出現（OutBack）。</summary>
        public static Coroutine PopIn(Transform target, float duration = 0.2f, float from = 0.7f, bool unscaled = true)
        {
            if (target == null) return null;
            return JuiceRunner.Instance.StartCoroutine(PopInRoutine(target, duration, from, unscaled));
        }

        private static IEnumerator PopInRoutine(Transform t, float duration, float from, bool unscaled)
        {
            if (t == null) yield break;
            Vector3 baseScale = t.localScale;
            float e = 0f;
            while (e < duration && t != null)
            {
                e += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float k = Easing.OutBack(Mathf.Clamp01(e / Mathf.Max(0.0001f, duration)));
                t.localScale = baseScale * Mathf.LerpUnclamped(from, 1f, k);
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }

        // ── シェイク（被弾・衝撃） ──

        /// <summary>ローカル位置を揺らして減衰（既定は盤面用＝ポーズで止まる）。</summary>
        public static Coroutine Shake(Transform target, float magnitude = 0.2f, float duration = 0.25f, bool unscaled = false)
        {
            if (target == null) return null;
            return JuiceRunner.Instance.StartCoroutine(ShakeRoutine(target, magnitude, duration, unscaled));
        }

        private static IEnumerator ShakeRoutine(Transform t, float magnitude, float duration, bool unscaled)
        {
            if (t == null) yield break;
            Vector3 basePos = t.localPosition;
            float e = 0f;
            while (e < duration && t != null)
            {
                e += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float env = 1f - Mathf.Clamp01(e / Mathf.Max(0.0001f, duration)); // 線形減衰
                Vector2 off = Random.insideUnitCircle * (magnitude * env);
                t.localPosition = basePos + new Vector3(off.x, off.y, 0f);
                yield return null;
            }
            if (t != null) t.localPosition = basePos;
        }

        // ── 色フラッシュ／フェード ──

        /// <summary>SpriteRenderer の色を一瞬指定色へ振って戻す（被弾の明滅等）。Material 生成なし。</summary>
        public static Coroutine Flash(SpriteRenderer sr, Color flash, float duration = 0.15f, bool unscaled = false)
        {
            if (sr == null) return null;
            return JuiceRunner.Instance.StartCoroutine(FlashRoutine(sr, flash, duration, unscaled));
        }

        private static IEnumerator FlashRoutine(SpriteRenderer sr, Color flash, float duration, bool unscaled)
        {
            if (sr == null) yield break;
            Color baseColor = sr.color;
            float e = 0f;
            while (e < duration && sr != null)
            {
                e += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                float p = Easing.Pulse(Mathf.Clamp01(e / Mathf.Max(0.0001f, duration)));
                sr.color = Color.Lerp(baseColor, flash, p);
                yield return null;
            }
            if (sr != null) sr.color = baseColor;
        }

        /// <summary>CanvasGroup の α を補間（UI のフェードイン/アウト・既定 unscaled）。</summary>
        public static Coroutine Fade(CanvasGroup cg, float to, float duration = 0.2f, bool unscaled = true)
        {
            if (cg == null) return null;
            return JuiceRunner.Instance.StartCoroutine(FadeRoutine(cg, to, duration, unscaled));
        }

        private static IEnumerator FadeRoutine(CanvasGroup cg, float to, float duration, bool unscaled)
        {
            if (cg == null) yield break;
            float from = cg.alpha;
            float e = 0f;
            while (e < duration && cg != null)
            {
                e += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Easing.SmoothStep(Mathf.Clamp01(e / Mathf.Max(0.0001f, duration))));
                yield return null;
            }
            if (cg != null) cg.alpha = to;
        }

        // ── ヒットストップ（一瞬の時間停止＝衝撃の重み） ──

        /// <summary>
        /// ごく短時間だけ timeScale を落として衝撃を強調し、元の速度へ戻す。
        /// ※会戦の決定的瞬間用の極短演出。timeScale の権威は本来 PauseManager だが、
        /// ここは直前値を捕捉して必ず復元する（数十msのみ）。多用しないこと。
        /// </summary>
        public static void HitStop(float seconds = 0.06f, float scale = 0.05f)
        {
            JuiceRunner.Instance.StartCoroutine(HitStopRoutine(seconds, scale));
        }

        private static IEnumerator HitStopRoutine(float seconds, float scale)
        {
            float prev = Time.timeScale;
            Time.timeScale = Mathf.Max(0f, prev * Mathf.Clamp01(scale));
            float e = 0f;
            while (e < seconds) { e += Time.unscaledDeltaTime; yield return null; }
            Time.timeScale = prev;
        }
    }
}
