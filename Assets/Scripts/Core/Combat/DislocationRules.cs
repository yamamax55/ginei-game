using UnityEngine;

namespace Ginei
{
    /// <summary>心理的瓦解（ディスロケーション）の調整係数。</summary>
    public readonly struct DislocationParams
    {
        /// <summary>脅威の方向が増えるごとに瓦解が加速する係数（1方向は対処できるが多方向は心理が崩れる）。</summary>
        public readonly float directionWeight;
        /// <summary>不意（surprise）が心理的瓦解に効く重み。</summary>
        public readonly float surpriseWeight;
        /// <summary>退路の遮断が心理的瓦解に効く重み。</summary>
        public readonly float retreatCutWeight;
        /// <summary>瓦解度が士気低下を加速する最大倍率（実効値パターン＝基準drainに掛ける上限）。</summary>
        public readonly float collapseAccelMax;
        /// <summary>指揮の結束が瓦解からの立て直しに効く係数。</summary>
        public readonly float recoveryWeight;
        /// <summary>心理的に瓦解したと判定する既定閾値。</summary>
        public readonly float dislocatedThreshold;

        public DislocationParams(float directionWeight, float surpriseWeight, float retreatCutWeight,
                                 float collapseAccelMax, float recoveryWeight, float dislocatedThreshold)
        {
            this.directionWeight = Mathf.Clamp01(directionWeight);
            this.surpriseWeight = Mathf.Clamp01(surpriseWeight);
            this.retreatCutWeight = Mathf.Clamp01(retreatCutWeight);
            this.collapseAccelMax = Mathf.Max(1f, collapseAccelMax);
            this.recoveryWeight = Mathf.Clamp01(recoveryWeight);
            this.dislocatedThreshold = Mathf.Clamp01(dislocatedThreshold);
        }

        /// <summary>既定＝方向係数0.35・不意0.4・退路遮断0.5・崩壊加速最大2.5・立て直し0.7・瓦解閾値0.5。</summary>
        public static DislocationParams Default
            => new DislocationParams(0.35f, 0.4f, 0.5f, 2.5f, 0.7f, 0.5f);
    }

    /// <summary>
    /// 心理的瓦解＝間接アプローチによるディスロケーション（リデルハート・LDH-3 #1344）の純ロジック。
    /// 複数方向からの脅威・不意・連絡線（退路）の遮断によって敵の心理が崩れ、物理的損害より先に士気・組織が
    /// 崩壊する＝戦わずして敵を無力化する。一方向の脅威は対処できるが、多方向から同時に脅かされると心理が
    /// 崩れ、予期した軸と異なる方向から不意に突かれると動揺が深まる。指揮の結束が高ければ瓦解から立て直せる。
    /// <c>PsychologicalSiegeMoraleRules</c>（四面楚歌＝物理包囲の心理＝包囲された軍の崩壊）とは別＝間接
    /// アプローチによる機動的瓦解（包囲せずとも心理を崩す）。<see cref="EncirclementRules"/>（物理的包囲度・
    /// 降伏確率）とは別＝心理の崩壊に特化（物理損害を伴わない）。<c>FleetMorale</c>（Game層の士気実体）は
    /// read-only相当＝ここは係数算出のみ（返す倍率を士気drainに掛ける消費側がGame層）。
    /// <c>IndirectApproachRules</c>（同EPIC LDH＝間接アプローチ）の心理利得の入力先。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DislocationRules
    {
        /// <summary>
        /// 脅威の方向数による瓦解係数（0..1）。方向0=0、方向が増えるほど飽和的に上がる
        /// （1方向は対処でき、多方向で心理が崩れる）。= 1 − 1/(1 + directionWeight×directions)。
        /// </summary>
        public static float ThreatDirectionFactor(int threatDirections, DislocationParams p)
        {
            int dirs = Mathf.Max(0, threatDirections);
            float x = p.directionWeight * dirs;
            return Mathf.Clamp01(x / (1f + x));
        }

        public static float ThreatDirectionFactor(int threatDirections)
            => ThreatDirectionFactor(threatDirections, DislocationParams.Default);

        /// <summary>
        /// 心理的瓦解度（0..1）＝多方向の脅威 threatFactor を基盤に、不意 surprise と退路遮断 lineOfRetreatCut が
        /// 上乗せする。退路を断たれ・不意を突かれるほど瓦解が深い。重み付き合成（飽和的に1へ近づく）。
        /// </summary>
        public static float PsychologicalDislocation(float threatFactor, float surprise, float lineOfRetreatCut,
                                                     DislocationParams p)
        {
            float threat = Mathf.Clamp01(threatFactor);
            float surp = Mathf.Clamp01(surprise);
            float cut = Mathf.Clamp01(lineOfRetreatCut);
            // 多方向の脅威を土台に、残り余地へ不意・退路遮断を相乗で足し込む。
            float room = 1f - threat;
            float added = room * (surp * p.surpriseWeight + cut * p.retreatCutWeight);
            return Mathf.Clamp01(threat + added);
        }

        public static float PsychologicalDislocation(float threatFactor, float surprise, float lineOfRetreatCut)
            => PsychologicalDislocation(threatFactor, surprise, lineOfRetreatCut, DislocationParams.Default);

        /// <summary>
        /// 士気低下の加速倍率（1..collapseAccelMax・実効値パターン）＝瓦解度が高いほど基準の士気低下 baseMoraleDrain を
        /// 速める倍率を返す。瓦解0で1.0（等倍）、瓦解1で collapseAccelMax。基準drainは破壊せず倍率のみ返す。
        /// </summary>
        public static float MoraleCollapseAcceleration(float dislocation, float baseMoraleDrain, DislocationParams p)
        {
            float d = Mathf.Clamp01(dislocation);
            float multiplier = Mathf.Lerp(1f, p.collapseAccelMax, d);
            // baseMoraleDrain は参考入力（係数算出のみ・破壊しない）＝倍率を返す。
            return multiplier;
        }

        public static float MoraleCollapseAcceleration(float dislocation, float baseMoraleDrain)
            => MoraleCollapseAcceleration(dislocation, baseMoraleDrain, DislocationParams.Default);

        /// <summary>
        /// 物理損害より心理瓦解が先に効く度合い（0..1）。瓦解が物理損害を上回るほど1へ近づく＝心理が先に崩れる。
        /// physicalDamage（既に受けた物理損害0..1）と dislocation（心理瓦解0..1）を比べ、瓦解優位の比を返す。
        /// = dislocation / (dislocation + physicalDamage)（両者0なら0）。
        /// </summary>
        public static float PhysicalVsPsychological(float physicalDamage, float dislocation)
        {
            float phys = Mathf.Clamp01(physicalDamage);
            float psych = Mathf.Clamp01(dislocation);
            float total = phys + psych;
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(psych / total);
        }

        /// <summary>
        /// 予期した軸と実際の軸のズレによる動揺（0..1）。predictedAxis/actualAxis は度（0..360）。
        /// 正面に来ると思った敵が側背面から来たほど（角度差が大きいほど）動揺が深い。
        /// 角度差を [0,180] に正規化し 180度（真逆）で最大。
        /// </summary>
        public static float ExpectationUpset(float predictedAxis, float actualAxis)
        {
            float diff = Mathf.Abs(predictedAxis - actualAxis) % 360f;
            if (diff > 180f) diff = 360f - diff;
            return Mathf.Clamp01(diff / 180f);
        }

        /// <summary>
        /// 瓦解からの立て直し抵抗力（0..1）＝指揮の結束 commandCohesion が高いほど瓦解 dislocation を打ち消す。
        /// 結束が瓦解を上回れば立て直せる（高い値＝立て直せる残存度）。= cohesion×recoveryWeight − dislocation を下限0で。
        /// </summary>
        public static float RecoveryResistance(float commandCohesion, float dislocation, DislocationParams p)
        {
            float cohesion = Mathf.Clamp01(commandCohesion);
            float d = Mathf.Clamp01(dislocation);
            return Mathf.Clamp01(cohesion * p.recoveryWeight - d * (1f - p.recoveryWeight));
        }

        public static float RecoveryResistance(float commandCohesion, float dislocation)
            => RecoveryResistance(commandCohesion, dislocation, DislocationParams.Default);

        /// <summary>
        /// 敗走誘発＝瓦解度 dislocation が士気の床 moraleFloor を割ると true（心理が床を割って崩れる）。
        /// moraleFloor は耐えうる瓦解の上限（0..1・高いほど粘る）。dislocation がそれを超えると敗走。
        /// </summary>
        public static bool RoutTrigger(float dislocation, float moraleFloor)
        {
            return Mathf.Clamp01(dislocation) > Mathf.Clamp01(moraleFloor);
        }

        /// <summary>
        /// 心理的に瓦解したか＝瓦解度が閾値以上（間接アプローチが成立し、戦わずして無力化された状態）。
        /// </summary>
        public static bool IsDislocated(float dislocation, float threshold)
        {
            return Mathf.Clamp01(dislocation) >= Mathf.Clamp01(threshold);
        }

        public static bool IsDislocated(float dislocation)
            => IsDislocated(dislocation, DislocationParams.Default.dislocatedThreshold);
    }
}
