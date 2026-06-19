using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 開発投資の最適配分（内政最適化・純ロジック test-first）。
    /// 勢力の内政予算を、配下の惑星 Province 群へ「安定が低く（=助けが要る）・潜在力が高い（=投資対効果が大きい）」順に
    /// 優先度比で配分する唯一の窓口。各惑星には収穫逓減（per-province cap）を設け、1拠点へ際限なく注ぎ込めないようにする。
    /// <para>
    /// 設計（タイクン/ラグ回避・集約粒度を守る）：
    /// ・<see cref="DevelopmentPriority"/> ＝惑星の優先度スコア（低安定×高潜在）。<see cref="GovernanceRules.RebelPressure"/> 等を読むだけ。
    /// ・<see cref="AllocateBudget"/> ＝優先度シェアで比例配分（ウォーターフィリング＝cap で頭打ちした余りを残りへ再配分）。
    /// ・<see cref="ProjectedStabilityGain"/> ＝投資1単位あたりの安定上昇（凹＝収穫逓減）。
    /// </para>
    /// 安定度・産出の数値は <see cref="GovernanceRules"/>（OutputFactor/RebelPressure/MaxStability）へ委譲し二重実装しない。
    /// <b>Province にフィールドを足さず・編集せず</b>、既存フィールド（stability/population/産出余地 OutputFactor 等）の読み取りのみ。
    /// 配分は決定論的（同じ入力→同じ出力）。null/空は安全に空辞書を返す。
    /// </summary>
    public static class DevelopmentAllocationRules
    {
        // --- 調整値（マジックナンバー禁止＝const に集約） ---

        /// <summary>優先度における「不安定さ（要救済度）」の重み。低安定ほど高い。</summary>
        public const float InstabilityWeight = 1.0f;

        /// <summary>優先度における「潜在力（投資対効果）」の重み。人口×産出余地が大きいほど高い。</summary>
        public const float PotentialWeight = 1.0f;

        /// <summary>反乱圧（0..1）への上乗せ重み。反乱リスク域は最優先で梃入れする。</summary>
        public const float UnrestWeight = 1.5f;

        /// <summary>潜在力スコアの正規化基準人口（この人口で潜在力 ≈ 1.0）。集約粒度＝惑星単位。</summary>
        public const float PotentialPopulationScale = 100f;

        /// <summary>1惑星あたりの投資上限（予算に対する割合＝収穫逓減の頭打ち）。1拠点へ注ぎ込みすぎない。</summary>
        public const float PerProvinceCapRatio = 0.4f;

        /// <summary>収穫逓減（凹関数）の半飽和投資量。投資=この値で最大安定上昇の約半分に達する。</summary>
        public const float GainHalfSaturation = 100f;

        /// <summary>投資が無限大に近づいたときの安定上昇の上限（1拠点が1回の配分で得られる最大の伸び）。</summary>
        public const float MaxStabilityGainPerProvince = 30f;

        /// <summary>ウォーターフィリング再配分の最大反復回数（収束保証＝終盤ラグ回避の有界ループ）。</summary>
        public const int MaxFillIterations = 16;

        /// <summary>
        /// 惑星の開発優先度スコア（純関数・大きいほど投資すべき）。
        /// 低安定（<see cref="GovernanceRules.RebelPressure"/> も加味＝要救済）×高潜在（人口×産出余地）で算出。
        /// 安定が満杯の惑星は伸びしろが無く優先度が下がる。Province は読み取りのみ（基準非破壊）。
        /// </summary>
        public static float DevelopmentPriority(Province p)
        {
            if (p == null) return 0f;

            // 要救済度：安定が低いほど大（0..1）。さらに反乱リスク域は UnrestWeight で強調。
            float stab01 = Mathf.Clamp01(p.stability / GovernanceRules.MaxStability);
            float needScore = (1f - stab01) * InstabilityWeight
                            + GovernanceRules.RebelPressure(p) * UnrestWeight;

            // 潜在力：人口規模（投資が効く母数）×産出の伸びしろ（1−現状産出倍率）。
            float pop = Mathf.Max(0f, p.population) / Mathf.Max(1f, PotentialPopulationScale);
            float headroom = 1f - GovernanceRules.OutputFactor(p); // 0（満杯）..0.7（最低）
            float potentialScore = pop * (0.25f + headroom) * PotentialWeight; // 伸びしろ0でも母数ぶんは残す

            float score = needScore * (1f + potentialScore);
            return Mathf.Max(0f, score);
        }

        /// <summary>
        /// 予算を優先度シェアで惑星へ配分する（ウォーターフィリング＝決定論）。
        /// 戻り値＝systemId→配分額の辞書。Σ(配分) ≤ budget・各惑星 ≤ <see cref="PerProvinceCapRatio"/>×budget。
        /// 優先度0の惑星は配分されない。null/空・budget≤0 は空辞書。同 systemId が複数あれば後勝ちで上書き（集約済み前提）。
        /// </summary>
        public static Dictionary<int, float> AllocateBudget(IReadOnlyList<Province> provinces, float budget)
        {
            var result = new Dictionary<int, float>();
            if (provinces == null || provinces.Count == 0 || budget <= 0f) return result;

            // 優先度を収集（>0 のみが配分対象）。
            int n = provinces.Count;
            var ids = new int[n];
            var prio = new float[n];
            var alloc = new float[n];
            var capped = new bool[n];
            float cap = PerProvinceCapRatio * budget;
            int active = 0;
            float totalPrio = 0f;

            for (int i = 0; i < n; i++)
            {
                Province p = provinces[i];
                if (p == null) { prio[i] = 0f; continue; }
                ids[i] = p.systemId;
                float s = DevelopmentPriority(p);
                prio[i] = s;
                if (s > 0f) { active++; totalPrio += s; }
            }
            if (active == 0 || totalPrio <= 0f) return result;

            // ウォーターフィリング：cap で頭打ちした余りを未飽和の惑星へ優先度比で再配分。
            float remaining = budget;
            for (int iter = 0; iter < MaxFillIterations && remaining > 1e-4f; iter++)
            {
                // 未飽和（capped=false かつ prio>0）の優先度合計。
                float poolPrio = 0f;
                for (int i = 0; i < n; i++)
                    if (!capped[i] && prio[i] > 0f) poolPrio += prio[i];
                if (poolPrio <= 0f) break;

                bool newlyCapped = false;
                float distributed = 0f;
                for (int i = 0; i < n; i++)
                {
                    if (capped[i] || prio[i] <= 0f) continue;
                    float share = remaining * (prio[i] / poolPrio);
                    float room = cap - alloc[i];
                    if (share >= room)
                    {
                        // cap に到達＝頭打ち（収穫逓減）。
                        distributed += room;
                        alloc[i] = cap;
                        capped[i] = true;
                        newlyCapped = true;
                    }
                    else
                    {
                        alloc[i] += share;
                        distributed += share;
                    }
                }
                remaining -= distributed;
                if (!newlyCapped) break; // 全員 cap 未満で配り切った＝収束
            }

            for (int i = 0; i < n; i++)
                if (prio[i] > 0f && alloc[i] > 0f)
                    result[ids[i]] = alloc[i];

            return result;
        }

        /// <summary>
        /// 投資 <paramref name="invest"/> による安定度の上昇見込み（純関数・凹＝収穫逓減）。
        /// gain = MaxGain × invest/(invest + GainHalfSaturation)（飽和曲線）。残りの伸びしろ（MaxStability−現安定）でクランプ。
        /// 投資0→0、投資が大→MaxStabilityGainPerProvince に漸近。1単位あたりの上昇は投資が増えるほど減る。
        /// </summary>
        public static float ProjectedStabilityGain(Province p, float invest)
        {
            if (p == null || invest <= 0f) return 0f;
            float gain = MaxStabilityGainPerProvince * (invest / (invest + GainHalfSaturation));
            float headroom = Mathf.Max(0f, GovernanceRules.MaxStability - p.stability);
            return Mathf.Min(gain, headroom);
        }
    }
}
