using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 射撃目標優先度のパラメータ（集中砲火＝ランチェスター payoff の調整値）。基準値は ctor で全クランプ。
    /// </summary>
    public readonly struct TargetPriorityParams
    {
        /// <summary>距離正規化の基準射程（この距離で近接度0・0で近接度1）。</summary>
        public readonly float maxRange;
        /// <summary>旗艦（斬首＝指揮中枢撃破）の加点。</summary>
        public readonly float flagshipWeight;
        /// <summary>弱った敵を仕留める加点（残存割合が低いほど大）。</summary>
        public readonly float finishWeight;
        /// <summary>側背面を取れる標的の加点（被弾側の背面ほど大）。</summary>
        public readonly float flankWeight;
        /// <summary>集中砲火が有効な攻撃数の上限（ここまでは集中＝二乗則が効く）。</summary>
        public readonly float focusFireCap;
        /// <summary>集中砲火の最大加点（攻撃数が cap に近づくほどこの値へ）。</summary>
        public readonly float focusBonus;
        /// <summary>cap 超過の攻撃集中＝オーバーキル1単位あたりの減点。</summary>
        public readonly float overkillPenalty;

        public TargetPriorityParams(
            float maxRange,
            float flagshipWeight,
            float finishWeight,
            float flankWeight,
            float focusFireCap,
            float focusBonus,
            float overkillPenalty)
        {
            this.maxRange = Mathf.Max(maxRange, 0.01f);
            this.flagshipWeight = Mathf.Max(0f, flagshipWeight);
            this.finishWeight = Mathf.Max(0f, finishWeight);
            this.flankWeight = Mathf.Max(0f, flankWeight);
            this.focusFireCap = Mathf.Max(1f, focusFireCap);
            this.focusBonus = Mathf.Max(0f, focusBonus);
            this.overkillPenalty = Mathf.Max(0f, overkillPenalty);
        }

        /// <summary>既定値（射程10・旗艦+0.5・仕留め+0.8・側背面+0.4・集中3隻まで・集中+0.3・過剰-0.25）。</summary>
        public static TargetPriorityParams Default =>
            new TargetPriorityParams(10f, 0.5f, 0.8f, 0.4f, 3f, 0.3f, 0.25f);
    }

    /// <summary>
    /// 射撃目標優先度の純ロジック（盤面非依存・plain引数）。現状の標的選定（最寄り旗艦→最寄り配下艦の
    /// 純距離ベース＝<c>ShipCombat.FindPrioritizedEnemyInArc</c>）に、集中砲火・弱った敵の止め・斬首・側背面の
    /// 価値を畳み込んだ「狙う価値」スコアを与える。これにより味方が報酬の高い1艦へ火力を集中できる
    /// ＝局所優勢が二乗で効く（<c>LanchesterRules</c>）手応えを標的選定の側から作る。
    ///
    /// 分担：
    /// - 距離→近接度・旗艦/側背面/残存割合→加点をここに集約（インライン heuristic を散らさない）。
    /// - 側背面倍率の公式そのものは <see cref="CombatModifiers.FlankFactor"/>（ダメージ側）。こちらは標的選定側。
    /// - 集中砲火の二乗則そのものは <see cref="LanchesterRules"/>（ダメージ倍率）。こちらは「どの艦へ集中するか」。
    /// すべて Mathf のみ・LINQ/乱数なし・入力非破壊（実効値パターン）・決定論。test-first。
    /// </summary>
    public static class TargetPriorityRules
    {
        private const float Eps = 0.0001f;

        /// <summary>
        /// 近接度 0..1（射程内で近いほど1・射程で0・射程外も0でクランプ）。
        /// </summary>
        public static float Proximity(float distance, TargetPriorityParams p)
        {
            float d = Mathf.Max(distance, 0f);
            return Mathf.Clamp01(1f - d / p.maxRange);
        }

        /// <summary>
        /// 仕留め価値 0..1（残存割合が低いほど大）。<paramref name="currentStrength"/>/<paramref name="maxStrength"/>
        /// が小さい瀕死の敵を優先＝集中して落とし切る（各個撃破）。
        /// </summary>
        public static float FinishValue(float currentStrength, float maxStrength)
        {
            // 兵力バーの無い縮退入力（max<=0）は仕留め価値0（幻の標的に加点しない）。
            if (maxStrength <= Eps) return 0f;
            float frac = Mathf.Clamp01(Mathf.Max(currentStrength, 0f) / maxStrength);
            return 1f - frac;
        }

        /// <summary>
        /// 集中砲火の補正（cap までは集中加点・cap 超過はオーバーキル減点）。
        /// <paramref name="attackerCount"/>＝この標的を既に狙っている味方艦数。
        /// </summary>
        public static float FocusModifier(float attackerCount, TargetPriorityParams p)
        {
            float n = Mathf.Max(attackerCount, 0f);
            if (n <= p.focusFireCap)
            {
                // 0→cap で focusBonus へ線形に立ち上がる（集中ほど有利）。
                return p.focusBonus * (n / p.focusFireCap);
            }
            // cap 超過はオーバーキル＝過剰集中を減点（マイナスもありうる）。
            return p.focusBonus - p.overkillPenalty * (n - p.focusFireCap);
        }

        /// <summary>cap を超えて狙われている＝オーバーキル（火力を別標的へ回すべき）か。</summary>
        public static bool IsOverkill(float attackerCount, TargetPriorityParams p) =>
            Mathf.Max(attackerCount, 0f) > p.focusFireCap;

        /// <summary>
        /// 標的の「狙う価値」スコア（高いほど優先・0以上）。
        /// 近接度＋旗艦加点＋仕留め加点＋側背面加点＋集中補正の合成。
        /// </summary>
        /// <param name="distance">自艦→標的の距離。</param>
        /// <param name="currentStrength">標的の残存兵力。</param>
        /// <param name="maxStrength">標的の最大兵力（残存割合の分母）。</param>
        /// <param name="isFlagship">標的が旗艦か（斬首価値）。</param>
        /// <param name="flankExposure">標的の被弾面 0..1（1=完全な背面＝側背面ボーナス最大）。</param>
        /// <param name="attackerCount">この標的を既に狙っている味方艦数（集中砲火）。</param>
        public static float PriorityScore(
            float distance,
            float currentStrength,
            float maxStrength,
            bool isFlagship,
            float flankExposure,
            float attackerCount,
            TargetPriorityParams p)
        {
            float score = Proximity(distance, p);
            if (isFlagship) score += p.flagshipWeight;
            score += p.finishWeight * FinishValue(currentStrength, maxStrength);
            score += p.flankWeight * Mathf.Clamp01(flankExposure);
            score += FocusModifier(attackerCount, p);
            return Mathf.Max(0f, score);
        }

        /// <summary>狙う価値スコア（既定Params）。</summary>
        public static float PriorityScore(
            float distance, float currentStrength, float maxStrength,
            bool isFlagship, float flankExposure, float attackerCount) =>
            PriorityScore(distance, currentStrength, maxStrength, isFlagship, flankExposure, attackerCount,
                TargetPriorityParams.Default);

        /// <summary>
        /// 2標的の優先比較（-1=A優先 / 1=B優先 / 0=同等）。スコア高優先・同点は近い方優先＝決定論的タイブレーク。
        /// </summary>
        public static int Prefer(float scoreA, float distA, float scoreB, float distB)
        {
            if (scoreA > scoreB + Eps) return -1;
            if (scoreB > scoreA + Eps) return 1;
            // 同点：近い方を優先。
            if (distA < distB - Eps) return -1;
            if (distB < distA - Eps) return 1;
            return 0;
        }
    }
}
