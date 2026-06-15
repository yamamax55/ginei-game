using UnityEngine;

namespace Ginei
{
    /// <summary>悪の凡庸性の純データ＝無思考・服従・道徳的主体性。</summary>
    public struct BanalityState
    {
        /// <summary>無思考（thoughtlessness 0..1）＝自分の頭で考えず命令を受け流す度合い。</summary>
        public float thoughtlessness;
        /// <summary>服従（obedience 0..1）＝上意への無批判な従順さ。</summary>
        public float obedience;
        /// <summary>道徳的主体性（moralAgency 0..1）＝自ら考え責任を引き受ける力。</summary>
        public float moralAgency;

        public BanalityState(float thoughtlessness, float obedience, float moralAgency)
        {
            this.thoughtlessness = Mathf.Clamp01(thoughtlessness);
            this.obedience = Mathf.Clamp01(obedience);
            this.moralAgency = Mathf.Clamp01(moralAgency);
        }
    }

    /// <summary>悪の凡庸性の調整係数。</summary>
    public readonly struct ThoughtlessnessParams
    {
        /// <summary>階層の深さ・服従規範が道徳的主体性を奪う強さ（深い階層×強い服従ほど主体性が下がる）。</summary>
        public readonly float agencyErosion;
        /// <summary>道徳的主体性の低下が加担リスクへ寄与する強さ（思考停止が巨悪を可能にする）。</summary>
        public readonly float atrocityScale;
        /// <summary>官僚化・日常化が無思考を深める速さ（per dt・ルーティン化された残虐）。</summary>
        public readonly float routinizationRate;
        /// <summary>立ち止まって考える瞬間が道徳的主体性を呼び戻す強さ（思考が悪を止める）。</summary>
        public readonly float conscienceGain;

        public ThoughtlessnessParams(float agencyErosion, float atrocityScale,
                                     float routinizationRate, float conscienceGain)
        {
            this.agencyErosion = Mathf.Clamp01(agencyErosion);
            this.atrocityScale = Mathf.Clamp01(atrocityScale);
            this.routinizationRate = Mathf.Max(0f, routinizationRate);
            this.conscienceGain = Mathf.Clamp01(conscienceGain);
        }

        /// <summary>既定＝主体性侵食0.7・加担係数0.8・日常化速度0.05・良心回復0.4。</summary>
        public static ThoughtlessnessParams Default => new ThoughtlessnessParams(0.7f, 0.8f, 0.05f, 0.4f);
    }

    /// <summary>
    /// 悪の凡庸性の純ロジック（#1530・アーレント『エルサレムのアイヒマン』参考）。アイヒマンは怪物ではなく、
    /// 思考停止した平凡な官僚だった＝命令への無批判な服従と思考の欠如が巨悪に加担する（banality of evil）。
    /// 階層の深さ（責任の希薄化）×服従規範の強さが道徳的主体性（自分の頭で考え責任を負う力）を奪い、
    /// 有害な命令と相まって虐殺への加担リスクを高める。官僚的な抽象化は被害者を「机上の数字」に変えて見えなくし、
    /// 立ち止まって考える瞬間（良心）だけがそれを覆す。<see cref="AtrocityRules"/>（虐殺の実行と恐怖効果）とは別＝
    /// こちらは「無思考による加担」（道徳的主体性の喪失と加担リスク）を扱う。同 EPIC BNAL の
    /// <see cref="PluralityRules"/>（複数性＝他者と語り合う公共性）・服従の規律本体 <see cref="DisciplineRules"/>
    /// （軍紀・査問）とも分担。乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ThoughtlessnessRules
    {
        /// <summary>
        /// 道徳的主体性の倍率（0..1）。階層が深く（責任が希薄）服従規範が強いほど自分で考えなくなり主体性が下がる。
        /// ＝1−(深さ×服従規範)×侵食係数。深い階層と強い服従が掛け合わさるほど主体性が削られる（悪の凡庸性の核）。
        /// </summary>
        public static float MoralAgencyFactor(float hierarchyDepth, float complianceNorm, ThoughtlessnessParams p)
        {
            float pressure = Mathf.Clamp01(hierarchyDepth) * Mathf.Clamp01(complianceNorm);
            return Mathf.Clamp01(1f - pressure * p.agencyErosion);
        }

        public static float MoralAgencyFactor(float hierarchyDepth, float complianceNorm)
            => MoralAgencyFactor(hierarchyDepth, complianceNorm, ThoughtlessnessParams.Default);

        /// <summary>
        /// 責任の拡散（0..1）＝階層が深いほど「自分はただ命令に従っただけ」と責任が薄まり、誰も責任を感じなくなる。
        /// 深さに単調に比例（hierarchyDepth をそのまま拡散度とする）。
        /// </summary>
        public static float ResponsibilityDiffusion(float hierarchyDepth)
        {
            return Mathf.Clamp01(hierarchyDepth);
        }

        /// <summary>
        /// 加担リスク（0..1）＝道徳的主体性が低く、有害な命令 orderToHarm(0..1) が強いほど高い。
        /// ＝(1−道徳的主体性倍率)×有害命令×加担係数。思考停止と有害な命令が揃ったとき巨悪が可能になる。
        /// </summary>
        public static float AtrocityRisk(float moralAgencyFactor, float orderToHarm, ThoughtlessnessParams p)
        {
            float thoughtless = 1f - Mathf.Clamp01(moralAgencyFactor);
            return Mathf.Clamp01(thoughtless * Mathf.Clamp01(orderToHarm) * p.atrocityScale);
        }

        public static float AtrocityRisk(float moralAgencyFactor, float orderToHarm)
            => AtrocityRisk(moralAgencyFactor, orderToHarm, ThoughtlessnessParams.Default);

        /// <summary>
        /// 悪の日常化＝無思考の1tick後の値（0..1）。官僚化・ルーティン化 routinization(0..1) が無思考を深める
        /// （routinizationRate×routinization×dt ずつ上昇）。残虐が日課になるほど何も考えなくなる。
        /// </summary>
        public static float ThoughtlessnessTick(float thoughtlessness, float routinization, float dt, ThoughtlessnessParams p)
        {
            float delta = p.routinizationRate * Mathf.Clamp01(routinization) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(thoughtlessness) + delta);
        }

        public static float ThoughtlessnessTick(float thoughtlessness, float routinization, float dt)
            => ThoughtlessnessTick(thoughtlessness, routinization, dt, ThoughtlessnessParams.Default);

        /// <summary>
        /// 官僚的距離（0..1）＝階層の深さと書類仕事の抽象化 abstraction(0..1) が被害者を見えなくする度合い。
        /// ＝深さ×抽象化。机上の数字として人を扱うほど距離が開き、加担への心理的障壁が消える。
        /// </summary>
        public static float BureaucraticDistance(float hierarchyDepth, float abstraction)
        {
            return Mathf.Clamp01(hierarchyDepth) * Mathf.Clamp01(abstraction);
        }

        /// <summary>
        /// 良心の覚醒＝立ち止まって考える瞬間 momentOfReflection(0..1) が道徳的主体性を呼び戻した後の値（0..1）。
        /// ＝現主体性＋反省の瞬間×良心回復係数。思考が悪を止める＝凡庸性からの脱出口。
        /// </summary>
        public static float ConscienceActivation(float moralAgency, float momentOfReflection, ThoughtlessnessParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(moralAgency) + Mathf.Clamp01(momentOfReflection) * p.conscienceGain);
        }

        public static float ConscienceActivation(float moralAgency, float momentOfReflection)
            => ConscienceActivation(moralAgency, momentOfReflection, ThoughtlessnessParams.Default);

        /// <summary>
        /// 服従と良心の綱引き（−1..1）＝conscience−obedience。正なら良心が勝つ（命令に抗う）、
        /// 負なら服従が勝つ（無批判に従う）、0なら拮抗。どちらが勝つかが加担と抵抗の分かれ目。
        /// </summary>
        public static float ObedienceVsConscience(float obedience, float conscience)
        {
            return Mathf.Clamp01(conscience) - Mathf.Clamp01(obedience);
        }

        /// <summary>
        /// 悪の凡庸性（無思考の加担）に陥った判定。道徳的主体性倍率が低く（しきい値未満）、
        /// かつ加担リスクが threshold 以上のとき true。思考停止した平凡な官僚が巨悪に加担した状態。
        /// </summary>
        public static bool IsBanalEvil(float moralAgencyFactor, float atrocityRisk, float threshold)
        {
            return Mathf.Clamp01(moralAgencyFactor) < (1f - Mathf.Clamp01(threshold))
                   && Mathf.Clamp01(atrocityRisk) >= Mathf.Clamp01(threshold);
        }
    }
}
