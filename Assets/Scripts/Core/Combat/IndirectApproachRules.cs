using UnityEngine;

namespace Ginei
{
    /// <summary>間接的アプローチの調整係数。ctor で全値をクランプする。</summary>
    public readonly struct IndirectApproachParams
    {
        /// <summary>予期される度合いにおける「経路の直接性（最短ほど予期される）」の寄与。</summary>
        public readonly float directnessWeight;
        /// <summary>予期される度合いにおける「敵の警戒（その軸に注意が向いている）」の寄与。</summary>
        public readonly float attentionWeight;
        /// <summary>間接アプローチと判定する期待外れ度の閾値（0..1）。これ以上に予期されない経路を間接と呼ぶ。</summary>
        public readonly float indirectThreshold;
        /// <summary>分岐がもたらす惑わし効果の非線形度（冪指数・1以上）。分岐が増えるほど惑わしが加速する。</summary>
        public readonly float flexibilityExponent;
        /// <summary>総合評価における経路コストのペナルティ重み（0..1）。意表を突く道が遠回りでも、その代償。</summary>
        public readonly float costWeight;

        public IndirectApproachParams(float directnessWeight, float attentionWeight,
            float indirectThreshold, float flexibilityExponent, float costWeight)
        {
            this.directnessWeight = Mathf.Max(0f, directnessWeight);
            this.attentionWeight = Mathf.Max(0f, attentionWeight);
            this.indirectThreshold = Mathf.Clamp01(indirectThreshold);
            this.flexibilityExponent = Mathf.Max(1f, flexibilityExponent);
            this.costWeight = Mathf.Clamp01(costWeight);
        }

        /// <summary>既定＝直接性重み0.6・警戒重み0.4／間接閾値0.6／分岐冪0.7→実効1（Clampで1以上）／コスト重み0.5。</summary>
        public static IndirectApproachParams Default =>
            new IndirectApproachParams(0.6f, 0.4f, 0.6f, 1.5f, 0.5f);
    }

    /// <summary>
    /// 間接的アプローチの純ロジック＝リデルハート『戦略論：間接的アプローチ』（LDH-1・#1339）。戦略の核は
    /// <b>最小予期線（line of least expectation）＝敵が最も予期しない経路を突く</b>こと。正面の最短経路は敵に
    /// 予期され抵抗が固いが、迂回・意表を突く経路は警戒が薄く抵抗も薄い＝<b>最短≠最善</b>。経路の「敵にとっての
    /// 期待外れ度」を評価し、最も予期されない道ほど高く評価する。分岐の多い経路は複数の目標を同時に脅かして
    /// 敵を惑わせ（デュアルスレット）、心理的動揺（dislocation）と相まって間接アプローチの利得を生む。
    /// <see cref="GalaxyPathfinder"/>（最短/Dijkstra の経路探索＝物理的に最短の道）とは別＝こちらは経路の
    /// <b>心理的・戦略的評価レイヤー（どの道が最も予期されないか）</b>。<see cref="CenterOfGravityRules"/>
    /// （重心＝叩くべき一点の同定）とも別＝こちらはその一点へ<b>至る最も予期されない道</b>の選定。
    /// <see cref="SunziDoctrineRules"/>（謀攻優先＝手段の優劣）とも別。盤面非依存の plain 引数（pathDirectness
    /// 等の連続値）で評価する。倍率・利得は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class IndirectApproachRules
    {
        /// <summary>
        /// 経路が敵に予期される度合い（0..1）＝pathDirectness（経路の直接性0..1＝最短・正面ほど1）と
        /// enemyAttentionOnAxis（その軸への敵の警戒0..1）を重み付き平均する（重み合計で正規化、合計0なら0）。
        /// 直接的で・かつ敵がその方向を警戒しているほど予期される＝抵抗が固い。
        /// </summary>
        public static float ExpectationLevel(float pathDirectness, float enemyAttentionOnAxis, IndirectApproachParams p)
        {
            float d = Mathf.Clamp01(pathDirectness);
            float a = Mathf.Clamp01(enemyAttentionOnAxis);
            float weightSum = p.directnessWeight + p.attentionWeight;
            if (weightSum <= 0f) return 0f;
            return Mathf.Clamp01((p.directnessWeight * d + p.attentionWeight * a) / weightSum);
        }

        /// <summary>既定係数での予期される度合い（0..1）。</summary>
        public static float ExpectationLevel(float pathDirectness, float enemyAttentionOnAxis)
            => ExpectationLevel(pathDirectness, enemyAttentionOnAxis, IndirectApproachParams.Default);

        /// <summary>
        /// 最小予期スコア（期待外れ度・0..1）＝1−予期される度合い。予期されない経路ほど高い＝最も警戒の薄い
        /// 線（line of least expectation）こそ戦略の本命。
        /// </summary>
        public static float LeastExpectationScore(float expectationLevel)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(expectationLevel));
        }

        /// <summary>
        /// 経路上の抵抗（0..1）＝予期された経路ほど抵抗が固い。baseDefense（基礎防御0..1）に予期される度合いを
        /// 掛ける＝同じ防御力でも、敵が予期している軸では守りが厚く展開され実効抵抗が大きい。予期されない経路は
        /// 守りが手薄＝抵抗が薄い。
        /// </summary>
        public static float ResistanceOnPath(float expectationLevel, float baseDefense)
        {
            float exp = Mathf.Clamp01(expectationLevel);
            float def = Mathf.Clamp01(baseDefense);
            return Mathf.Clamp01(def * exp);
        }

        /// <summary>
        /// 間接アプローチの利得（0..1）＝意表×心理的動揺。leastExpectationScore（期待外れ度0..1）と
        /// dislocationGain（突かれた敵の心理的動揺＝陣形・計画の崩れ0..1）の積＝予期されない打撃が、かつ敵を
        /// 動揺させてこそ間接アプローチの真価。どちらか0なら利得0＝意表を突いても動揺させられなければ意味がない。
        /// </summary>
        public static float IndirectAdvantage(float leastExpectationScore, float dislocationGain)
        {
            float least = Mathf.Clamp01(leastExpectationScore);
            float disloc = Mathf.Clamp01(dislocationGain);
            return Mathf.Clamp01(least * disloc);
        }

        /// <summary>
        /// 経路の柔軟性（0..1・デュアルスレット）＝分岐の多い経路ほど敵を惑わせる。branchCount（その経路から
        /// 同時に脅かせる目標の数・0以下なら0）を冪で非線形に効かせて0..1へ写す＝複数の目標を同時に脅かすほど
        /// 敵は守りを分散させられ的を絞れない。分岐ゼロ・単一なら惑わしは無い。
        /// </summary>
        public static float PathFlexibility(int branchCount, IndirectApproachParams p)
        {
            if (branchCount <= 0) return 0f;
            // 同時に脅かせる目標数 → 飽和する惑わし度。branchCount=1 で 0、増えるほど 1 へ漸近。
            float extra = branchCount - 1; // 単一目標を超えた分岐数
            float raw = extra / (extra + 1f); // 1分岐増ごとに逓減して飽和
            return Mathf.Clamp01(Mathf.Pow(raw, 1f / p.flexibilityExponent));
        }

        /// <summary>既定係数での経路の柔軟性（0..1）。</summary>
        public static float PathFlexibility(int branchCount)
            => PathFlexibility(branchCount, IndirectApproachParams.Default);

        /// <summary>
        /// 正面集中への直撃ペナルティ（0..1・最短≠最善）＝enemyConcentration（正面に集中した敵戦力0..1）が
        /// 高いほど直接アプローチのコストが高い＝固めた正面へ真っ直ぐ突っ込むのは高くつく。集中ゼロなら直撃も
        /// 安く済む。
        /// </summary>
        public static float DirectCostPenalty(float enemyConcentration)
        {
            return Mathf.Clamp01(enemyConcentration);
        }

        /// <summary>
        /// 経路の総合評価（0..1）＝期待外れ度と経路コストのトレードオフ。leastExpectationScore（期待外れ度0..1）
        /// から、pathCost（その経路の遠回りコスト0..1）を costWeight で割り引く＝意表を突く道が高評価でも、遠回り
        /// すぎれば差し引かれる（最も予期されないが到達に時間がかかりすぎる道は本命にならない）。最良の間接
        /// アプローチは「予期されず、かつ過大な遠回りでない」道。
        /// </summary>
        public static float ApproachScore(float leastExpectationScore, float pathCost, IndirectApproachParams p)
        {
            float least = Mathf.Clamp01(leastExpectationScore);
            float cost = Mathf.Clamp01(pathCost);
            return Mathf.Clamp01(least - p.costWeight * cost);
        }

        /// <summary>既定係数での経路の総合評価（0..1）。</summary>
        public static float ApproachScore(float leastExpectationScore, float pathCost)
            => ApproachScore(leastExpectationScore, pathCost, IndirectApproachParams.Default);

        /// <summary>
        /// 間接アプローチか（期待外れ度が閾値超）＝最小予期線に乗っているか。leastExpectationScore が threshold
        /// 以上なら、その経路は十分に予期されない＝間接アプローチと判定する。
        /// </summary>
        public static bool IsIndirectApproach(float leastExpectationScore, float threshold)
        {
            return Mathf.Clamp01(leastExpectationScore) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="IndirectApproachParams.indirectThreshold"/>）での間接アプローチ判定。</summary>
        public static bool IsIndirectApproach(float leastExpectationScore)
            => IsIndirectApproach(leastExpectationScore, IndirectApproachParams.Default.indirectThreshold);
    }
}
