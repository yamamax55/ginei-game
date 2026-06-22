using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// イージング関数の単一窓口（DEV効率化①・無料ジュース基盤）。t∈[0,1] を eased 値へ写す純関数群。
    /// 各所に散在する SmoothStep/Lerp カーブをここへ集約する（DOTween 等の外部依存なし・test-first）。
    /// Back/Elastic/Bounce は途中で [0,1] を超える（オーバーシュート＝ポップの気持ちよさ）。
    /// 消費は <see cref="Juice"/>（Game）や各UIの補間。新しいトゥイーン曲線はここへ足す（二重実装しない）。
    /// </summary>
    public static class Easing
    {
        /// <summary>OutBack の標準オーバーシュート量（行き過ぎの強さ）。</summary>
        public const float DefaultOvershoot = 1.70158f;

        public static float Linear(float t) => Mathf.Clamp01(t);

        /// <summary>3t²-2t³。最も無難な加減速（既存の手書き SmoothStep の置換先）。</summary>
        public static float SmoothStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        public static float InQuad(float t) { t = Mathf.Clamp01(t); return t * t; }
        public static float OutQuad(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u; }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); float u = 1f - t; return 1f - u * u * u; }

        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>終端でわずかに行き過ぎて戻る＝ポップイン向き（中間で1を超える）。</summary>
        public static float OutBack(float t, float overshoot = DefaultOvershoot)
        {
            t = Mathf.Clamp01(t);
            float c1 = overshoot;
            float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>ばね的に揺れて収束（強いポップ/通知の出現）。</summary>
        public static float OutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            const float c4 = (2f * Mathf.PI) / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        /// <summary>跳ねて着地（落下物・スタンプ表現）。</summary>
        public static float OutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1; return n1 * t * t + 0.984375f;
        }

        /// <summary>0→1→0 の往復パルス（パンチ/シェイク強度の包絡線＝0と1で0）。</summary>
        public static float Pulse(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
    }
}
