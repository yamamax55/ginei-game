using UnityEngine;

namespace Ginei
{
    /// <summary>勢力圏（非公式な浸透）の調整係数。</summary>
    public readonly struct InfluenceParams
    {
        /// <summary>経済的紐帯（交易・投資・市場支配）の浸透重み。</summary>
        public readonly float economicWeight;
        /// <summary>軍事顧問（装備供与・訓練・基地化）の浸透重み。</summary>
        public readonly float militaryWeight;
        /// <summary>政治介入（資金供与・親派育成・メディア）の浸透重み。</summary>
        public readonly float politicalWeight;
        /// <summary>浸透の基礎速度（圧力1.0・無抵抗のとき毎秒これだけ深まる）。</summary>
        public readonly float penetrationRate;
        /// <summary>風化速度（相手の抵抗力に比例して影響が剥がれ落ちる毎秒の割合）。</summary>
        public readonly float decayRate;
        /// <summary>民族主義反発が立ち上がる浸透度（これ以下の浸透は世論に気づかれない）。</summary>
        public readonly float backlashThreshold;
        /// <summary>非公式影響が公式属国化へ転換できる浸透度の閾値。</summary>
        public readonly float vassalThreshold;

        public InfluenceParams(float economicWeight, float militaryWeight, float politicalWeight,
            float penetrationRate, float decayRate, float backlashThreshold, float vassalThreshold)
        {
            this.economicWeight = Mathf.Clamp01(economicWeight);
            this.militaryWeight = Mathf.Clamp01(militaryWeight);
            this.politicalWeight = Mathf.Clamp01(politicalWeight);
            this.penetrationRate = Mathf.Clamp01(penetrationRate);
            this.decayRate = Mathf.Clamp01(decayRate);
            this.backlashThreshold = Mathf.Clamp(backlashThreshold, 0f, 0.99f); // 1.0だと反発勾配が消えるため上限0.99
            this.vassalThreshold = Mathf.Clamp01(vassalThreshold);
        }

        /// <summary>既定＝経済0.4/軍事0.35/政治0.25（合計1.0）・浸透速度0.1/s・風化0.05/s・反発閾値0.5・属国化閾値0.8。</summary>
        public static InfluenceParams Default =>
            new InfluenceParams(0.4f, 0.35f, 0.25f, 0.1f, 0.05f, 0.5f, 0.8f);
    }

    /// <summary>
    /// 勢力圏の純ロジック＝旗を立てない帝国。直接領有せず、経済的紐帯・軍事顧問・政治介入の
    /// 三経路で他国を影響下に置く「支配のグラデーション」を式に出す：浸透は蓄積し（<see cref="PenetrationTick"/>）、
    /// 深まるほど相手の政策が追従し（<see cref="PolicyCompliance"/>）、二大国が同じ国を取り合えば
    /// 相殺と代理紛争を生み（<see cref="NetInfluence"/>/<see cref="RivalContestation"/>）、浸透しすぎれば
    /// 民族主義の反発で急進的に離反され（<see cref="BacklashRisk"/>）、十分深ければ公式の属国化へ
    /// 転換できる（<see cref="SoftToHardThreshold"/>）。
    /// <see cref="DiplomacyState"/>（属国＝条約に基づく公式の外交状態）とは別系統＝こちらは条約なき
    /// 非公式な浸透度を扱い、属国化の成立・敵対判定への反映は DiplomacyState へ委譲する。
    /// 経済一経路を債務の罠として深掘りする DebtDiplomacyRules（同Wave並行実装）とも分担し、
    /// ここでは三経路の合成と浸透の力学のみを扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class InfluenceRules
    {
        /// <summary>
        /// 三経路の合成圧力（0..1）＝経済×economicWeight＋軍事×militaryWeight＋政治×politicalWeight。
        /// 既定重みは合計1.0＝全経路全開で圧力1.0。各入力はクランプ。
        /// </summary>
        public static float Pressure(float economicTies, float militaryAdvisors, float politicalFunding, InfluenceParams p)
        {
            return Mathf.Clamp01(
                p.economicWeight * Mathf.Clamp01(economicTies) +
                p.militaryWeight * Mathf.Clamp01(militaryAdvisors) +
                p.politicalWeight * Mathf.Clamp01(politicalFunding));
        }

        /// <summary>
        /// 浸透の1tick（戻り値＝新しい浸透度0..1）。蓄積＝浸透速度×三経路圧力×(1−抵抗力)、
        /// 風化＝風化速度×現在浸透度×抵抗力。抵抗力0の国へは素通りで深まり、抵抗力1の国は
        /// 一切浸透せず既存の影響も剥がれていく＝働きかけを止めれば帝国は風化する。dtは負を0に。
        /// </summary>
        public static float PenetrationTick(float influence, float economicTies, float militaryAdvisors,
            float politicalFunding, float targetResilience, float dt, InfluenceParams p)
        {
            float inf = Mathf.Clamp01(influence);
            float res = Mathf.Clamp01(targetResilience);
            float t = Mathf.Max(0f, dt);
            float gain = p.penetrationRate * Pressure(economicTies, militaryAdvisors, politicalFunding, p) * (1f - res);
            float decay = p.decayRate * inf * res;
            return Mathf.Clamp01(inf + (gain - decay) * t);
        }

        public static float PenetrationTick(float influence, float economicTies, float militaryAdvisors,
            float politicalFunding, float targetResilience, float dt)
            => PenetrationTick(influence, economicTies, militaryAdvisors, politicalFunding, targetResilience, dt, InfluenceParams.Default);

        /// <summary>
        /// 政策追従度（0..1）＝浸透度の二乗。浅い浸透では相手は面従腹背で従わず、深まるほど
        /// 急速に外交投票・基地提供・政策同調が得られる＝支配のグラデーションの非線形性。
        /// </summary>
        public static float PolicyCompliance(float influence)
        {
            float inf = Mathf.Clamp01(influence);
            return inf * inf;
        }

        /// <summary>
        /// 相殺後の純影響（-1..+1）＝influenceA−influenceB。二大国が同じ国へ浸透すると
        /// 互いの工作が打ち消し合い、実効的に政策を動かせるのは差分だけ＝綱引きの正味。
        /// 正でA優位・負でB優位。
        /// </summary>
        public static float NetInfluence(float influenceA, float influenceB)
        {
            return Mathf.Clamp(Mathf.Clamp01(influenceA) - Mathf.Clamp01(influenceB), -1f, 1f);
        }

        /// <summary>
        /// 代理紛争リスク（0..1）＝2×min(A,B)。双方が深く食い込むほど同じ国の内部で
        /// 親A派と親B派が育ち、互いに引かない＝双方0.5以上の浸透で確実に火がつく。
        /// 一方しか食い込んでいなければ綱引き自体が成立せずリスク0。
        /// </summary>
        public static float RivalContestation(float influenceA, float influenceB)
        {
            return Mathf.Clamp01(2f * Mathf.Min(Mathf.Clamp01(influenceA), Mathf.Clamp01(influenceB)));
        }

        /// <summary>
        /// 民族主義反発のリスク（0..1）＝閾値超過分の割合×民族的誇り。浸透が backlashThreshold を
        /// 超えて「属国扱い」が世論に見えた瞬間から立ち上がり、誇り高い国ほど急進的に離反する＝
        /// 旗を立てない帝国の限界。閾値以下の浸透は気づかれずリスク0。
        /// </summary>
        public static float BacklashRisk(float influence, float nationalPride, InfluenceParams p)
        {
            float inf = Mathf.Clamp01(influence);
            if (inf <= p.backlashThreshold) return 0f;
            float exposure = (inf - p.backlashThreshold) / (1f - p.backlashThreshold);
            return Mathf.Clamp01(exposure * Mathf.Clamp01(nationalPride));
        }

        public static float BacklashRisk(float influence, float nationalPride)
            => BacklashRisk(influence, nationalPride, InfluenceParams.Default);

        /// <summary>
        /// 非公式影響→公式属国化の転換可否＝浸透度が vassalThreshold 以上か。成立後の公式な
        /// 属国状態の管理（敵対判定への反映含む）は <see cref="DiplomacyState"/> の属国へ委譲する。
        /// </summary>
        public static bool SoftToHardThreshold(float influence, InfluenceParams p)
        {
            return Mathf.Clamp01(influence) >= p.vassalThreshold;
        }

        public static bool SoftToHardThreshold(float influence)
            => SoftToHardThreshold(influence, InfluenceParams.Default);
    }
}
