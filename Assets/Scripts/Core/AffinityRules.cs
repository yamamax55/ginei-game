using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 提督どうしの相性・人間関係の純ロジック（ADM-4 #2305・信長の野望/三国志の相性）。
    /// 参謀補完（能力の足し算・#885）の上に「人の和」を乗せる＝相性が良ければ補佐が活き軍団は結束し、
    /// 反目すれば補佐が空回りし離反を招く。相性は既存フィールド（`humility`/`ambition`/`faction`）から決定論で導く
    /// （別レジストリを増やさない）。野心家どうしは反目しやすい（功名争い）。実効値パターン・test-first。
    /// </summary>
    public static class AffinityRules
    {
        /// <summary>野心家とみなす功名心のしきい値。</summary>
        public const int RivalAmbition = 70;
        /// <summary>野心家どうしの反目ペナルティ。</summary>
        public const float RivalryPenalty = 0.2f;

        /// <summary>
        /// 2提督の相性（0..1）。性向（謙虚さ・功名心）が近いほど高く、敵勢力・野心家どうしは下がる。
        /// </summary>
        public static float Affinity(AdmiralData a, AdmiralData b)
        {
            if (a == null || b == null) return 0.5f; // 不明は中立
            float diff = Mathf.Abs(a.humility - b.humility) + Mathf.Abs(a.ambition - b.ambition);
            float score = 1f - diff / 200f;                 // 性向の近さ
            if (a.faction != b.faction) score -= 0.3f;       // 敵対勢力は相性が悪い
            if (a.ambition >= RivalAmbition && b.ambition >= RivalAmbition)
                score -= RivalryPenalty;                     // 功名を争う者どうしは反目
            return Mathf.Clamp01(score);
        }

        /// <summary>相性→参謀補完の効率倍率（0.5..1.5）。相性が良いほど補佐が活きる。</summary>
        public static float StaffSynergyFactor(float affinity)
            => Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(affinity));

        /// <summary>相性→軍団結束の倍率（0.85..1.15）。反目する軍団は崩れやすい（`CorpsCommandRules` に乗る）。</summary>
        public static float CorpsCohesionFactor(float affinity)
            => Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(affinity));

        /// <summary>相性→寝返り修正（-0.25..+0.25）。低相性ほど離反しやすい（正＝離反増・負＝抑制）。</summary>
        public static float DefectionModifier(float affinity)
            => (0.5f - Mathf.Clamp01(affinity)) * 0.5f;
    }
}
