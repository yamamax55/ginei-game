using UnityEngine;

namespace Ginei
{
    /// <summary>派閥増殖安定則（FED-1 #1473）の調整係数。</summary>
    public readonly struct FactionMultiplicityParams
    {
        /// <summary>多数派暴政リスクの鋭さ（集中度がそのままリスクに乗る基礎係数）。</summary>
        public readonly float tyrannySharpness;
        /// <summary>派閥の数が安定をもたらす逓減の鋭さ（実効派閥数が増えるほど安定が1へ近づく速さ）。</summary>
        public readonly float multiplicityRate;
        /// <summary>会派形成コストの混雑効果（既存派閥が多いほどコストが上がる強さ）。</summary>
        public readonly float crowdingWeight;
        /// <summary>派閥均衡と見なす実効派閥数の既定閾値（これ以上多様なら多数派専制に強い）。</summary>
        public readonly float balancedThreshold;

        public FactionMultiplicityParams(float tyrannySharpness, float multiplicityRate, float crowdingWeight, float balancedThreshold)
        {
            this.tyrannySharpness = Mathf.Clamp01(tyrannySharpness);
            this.multiplicityRate = Mathf.Max(0.01f, multiplicityRate);
            this.crowdingWeight = Mathf.Clamp01(crowdingWeight);
            this.balancedThreshold = Mathf.Max(1f, balancedThreshold);
        }

        /// <summary>既定＝暴政鋭さ1.0・多数性逓減率0.5・会派混雑重み0.6・均衡閾値3.0（実効3派閥以上で多数派専制に強い）。</summary>
        public static FactionMultiplicityParams Default
            => new FactionMultiplicityParams(1f, 0.5f, 0.6f, 3f);
    }

    /// <summary>
    /// 派閥増殖安定則の純ロジック＝『ザ・フェデラリスト』第10篇（マディソン）の拡大共和国論（FED-1 #1473）。
    /// 「派閥（faction）の害は、派閥を無くすのではなく**数を増やす**ことで中和できる」＝多数の多様な派閥が
    /// 並立すれば、どれも単独で多数派の専制を握れず互いに牽制し合う＝大きな共和国ほど派閥が多様化し
    /// 多数派暴政が起きにくい。核心は集中度（HHI＝各派閥シェアの二乗和）とその逆数（実効派閥数）：
    /// 集中するほど多数派専制が起きやすく、分散して実効派閥数が増えるほどシステムは安定する（マディソンの逆説）。
    /// <see cref="CoalitionRules"/>（連立＝単独過半数なき政権がどう持ちこたえるか）とは別＝こちらは
    /// 派閥が多いほど多数派専制が起きにくいという<b>分布の安定性</b>を扱う（分散ゆえに連立が強制される）。
    /// <see cref="PartyRules"/>（党勢から誰が統べるかを決める）とも別＝こちらは個々の党でなく派閥分布そのものの集中度。
    /// 同EPIC FED の <see cref="ExtendedRepublicRules"/>（拡大共和国＝規模が多様性を生む側）とは
    /// 連続するが分担：あちらは「共和国の規模→派閥の多様性」、こちらは「派閥の多様性→多数派専制の抑制」。
    /// 別EPIC の <see cref="MajorityTyrannyRules"/>（多数者の専制そのものの力学）とも別＝こちらは
    /// その専制を「派閥の数を増やす」ことで未然に防ぐ予防側。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FactionMultiplicityRules
    {
        /// <summary>
        /// ハーフィンダール指数 HHI（0..1）＝各派閥シェアの二乗和（集中度）。シェアは負をゼロにクランプし
        /// 合計で正規化してから二乗和を取る（入力が合計1でなくても安全）。1派閥独占で1.0、N派閥が均等なら1/N＝低い。
        /// null/空配列は0（派閥なし＝集中なし）。
        /// </summary>
        public static float HerfindahlIndex(float[] shares)
        {
            if (shares == null || shares.Length == 0) return 0f;
            float total = 0f;
            for (int i = 0; i < shares.Length; i++)
            {
                total += Mathf.Max(0f, shares[i]);
            }
            if (total <= 0.0001f) return 0f;
            float hhi = 0f;
            for (int i = 0; i < shares.Length; i++)
            {
                float s = Mathf.Max(0f, shares[i]) / total;
                hhi += s * s;
            }
            return Mathf.Clamp01(hhi);
        }

        /// <summary>
        /// 実効派閥数＝HHIの逆数（1/HHI＝多様性の指標・マディソンの核）。集中するほど少なく（独占で1）、
        /// 分散するほど多い。HHIが0以下（派閥なし）なら0。多数の小派閥が並立するほどこの値は大きい。
        /// </summary>
        public static float EffectiveFactionCount(float hhi)
        {
            float h = Mathf.Clamp01(hhi);
            if (h <= 0.0001f) return 0f;
            return 1f / h;
        }

        /// <summary>
        /// 多数派暴政リスク（0..1）＝集中度（HHI）が高いほど高い。少数の大派閥は単独で多数派の専制を握りやすい。
        /// factionalIntensity（0..1＝派閥対立の烈しさ）が燃料＝対立が烈しいほどリスクが顕在化する。
        /// HHI×強度×鋭さ。派閥が分散すれば（HHI低）どれだけ対立が烈しくても専制は起きにくい。
        /// </summary>
        public static float MajorityTyrannyRisk(float hhi, float factionalIntensity, FactionMultiplicityParams p)
        {
            float h = Mathf.Clamp01(hhi);
            float intensity = Mathf.Clamp01(factionalIntensity);
            return Mathf.Clamp01(h * intensity * p.tyrannySharpness);
        }

        public static float MajorityTyrannyRisk(float hhi, float factionalIntensity)
            => MajorityTyrannyRisk(hhi, factionalIntensity, FactionMultiplicityParams.Default);

        /// <summary>
        /// 多数性による安定化（0..1）＝実効派閥数が多いほど安定が1へ近づく（互いに牽制＝マディソンの逆説）。
        /// 1派閥なら0（単独で専制可能＝不安定）、派閥が増えるほど飽和的に1へ。
        /// 1−1/(1+(N−1)×率)＝N=1で0、Nが大きいほど1へ漸近。多様性そのものが安定を生む。
        /// </summary>
        public static float MultiplicityStabilization(float effectiveFactionCount, FactionMultiplicityParams p)
        {
            float n = Mathf.Max(0f, effectiveFactionCount);
            if (n <= 1f) return 0f; // 単独・派閥なしは牽制が働かない。
            float excess = (n - 1f) * p.multiplicityRate;
            return Mathf.Clamp01(1f - 1f / (1f + excess));
        }

        public static float MultiplicityStabilization(float effectiveFactionCount)
            => MultiplicityStabilization(effectiveFactionCount, FactionMultiplicityParams.Default);

        /// <summary>
        /// 連立の必要性（0..1）＝派閥が分散するほど単独過半数が取れず連立が要る（妥協の強制）。
        /// 1−HHI＝集中（HHI高）なら単独過半数が容易で必要性低、分散（HHI低）なら高い。
        /// マディソンの「派閥が多いと専制できず妥協が強制される」を <see cref="CoalitionRules"/> へ橋渡しする窓口。
        /// </summary>
        public static float CoalitionNecessity(float hhi)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(hhi));
        }

        /// <summary>
        /// 会派形成のコスト（0..1）＝新派閥を立ち上げる難しさ。制度的障壁（barriers 0..1）が基礎で、
        /// 既存派閥の混雑（existingFactions 0..1＝既に派閥が多い度合い）が混雑重みで上乗せされる。
        /// 既存派閥が多すぎると新派閥は作りにくい（増殖には上限がある）。障壁＋混雑×重み。
        /// </summary>
        public static float FactionFormationCost(float existingFactions, float barriers, FactionMultiplicityParams p)
        {
            float crowd = Mathf.Clamp01(existingFactions);
            float baseCost = Mathf.Clamp01(barriers);
            return Mathf.Clamp01(baseCost + crowd * p.crowdingWeight);
        }

        public static float FactionFormationCost(float existingFactions, float barriers)
            => FactionFormationCost(existingFactions, barriers, FactionMultiplicityParams.Default);

        /// <summary>
        /// 争点の交差（0..1）＝争点が多次元（issueDimensions 0..1）なほど派閥が交差し固定的対立を防ぐ。
        /// 同じ敵味方が固定されず（ある争点での味方が別の争点では敵）、多数派が常に同じ顔ぶれで固まらない＝
        /// 多数派暴政の温床を崩す（クロスカッティング・クリーヴェッジ）。争点次元そのものを写す。
        /// </summary>
        public static float CrossCuttingCleavages(float issueDimensions)
        {
            return Mathf.Clamp01(issueDimensions);
        }

        /// <summary>
        /// 派閥が十分多様で多数派専制に強いか＝実効派閥数が閾値以上なら true（既定3.0＝実効3派閥以上）。
        /// 大きな共和国ほど派閥が多様化し、この均衡条件を満たして安定する（マディソンの拡大共和国論）。
        /// </summary>
        public static bool IsFactionallyBalanced(float effectiveFactionCount, float threshold)
        {
            return Mathf.Max(0f, effectiveFactionCount) >= Mathf.Max(1f, threshold);
        }

        public static bool IsFactionallyBalanced(float effectiveFactionCount)
            => IsFactionallyBalanced(effectiveFactionCount, FactionMultiplicityParams.Default.balancedThreshold);
    }
}
