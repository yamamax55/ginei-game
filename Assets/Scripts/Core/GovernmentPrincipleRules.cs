using UnityEngine;

namespace Ginei
{
    /// <summary>政体を動かす原理＝情念（モンテスキュー『法の精神』・#1439）。共和政=徳/君主政=名誉/専制政=恐怖。</summary>
    public enum GovernmentPrinciple
    {
        徳,   // vertu：共和政の原理＝公共心・愛国（公益を私益に優先する情念）
        名誉, // honneur：君主政の原理＝栄誉への愛（身分・面目を重んじる情念）
        恐怖  // crainte：専制政の原理＝恐れ（恐怖だけが臣下を従わせる情念）
    }

    /// <summary>政体の原理の調整係数（#1439）。</summary>
    public readonly struct GovernmentPrincipleParams
    {
        /// <summary>原理が強いときの最小服従コスト（原理が市民を自発的に従わせる＝統治が安い下限）。</summary>
        public readonly float minObedienceCost;
        /// <summary>原理が皆無のときの最大服従コスト（原理を欠くと力ずくで従わせる維持費）。</summary>
        public readonly float maxObedienceCost;
        /// <summary>原理が損なわれて腐敗が進む速さ（1秒・侵食最大あたり）。</summary>
        public readonly float corruptionRate;
        /// <summary>恐怖の逓減の強さ（恐怖が高水準で麻痺＝慣れる度合い）。</summary>
        public readonly float fearHabituation;
        /// <summary>政体が腐敗したと判定する原理の強さの既定閾値（これ未満で腐敗）。</summary>
        public readonly float corruptionThreshold;

        public GovernmentPrincipleParams(float minObedienceCost, float maxObedienceCost,
            float corruptionRate, float fearHabituation, float corruptionThreshold)
        {
            this.minObedienceCost = Mathf.Clamp01(minObedienceCost);
            this.maxObedienceCost = Mathf.Clamp01(maxObedienceCost);
            this.corruptionRate = Mathf.Max(0f, corruptionRate);
            this.fearHabituation = Mathf.Clamp01(fearHabituation);
            this.corruptionThreshold = Mathf.Clamp01(corruptionThreshold);
        }

        /// <summary>既定＝最小コスト0.2/最大コスト0.9・腐敗0.2/秒・恐怖の慣れ0.6・腐敗閾値0.3。</summary>
        public static GovernmentPrincipleParams Default
            => new GovernmentPrincipleParams(0.2f, 0.9f, 0.2f, 0.6f, 0.3f);
    }

    /// <summary>
    /// 政体の原動力＝原理の純ロジック（MONT-1 #1439・モンテスキュー『法の精神』参考）。
    /// 各政体にはそれを動かす「原理（principe）」＝情念があり、共和政の原理は<b>徳</b>（公共心・愛国）、
    /// 君主政の原理は<b>名誉</b>（栄誉への愛）、専制政の原理は<b>恐怖</b>である。
    /// <b>この原理が市民を自発的に従わせて服従コストを下げる（統治を容易にする）が、原理が損なわれると
    /// 政体は腐敗し崩壊する</b>＝徳の喪失・名誉の堕落・恐怖の麻痺。原理が強く法が原理に沿うほど政体は活力を持ち、
    /// 政体の形態と実際の社会のエートスがずれると機能不全に陥る（専制に徳を求める等）。
    /// <see cref="WangDaoRules"/>（王道/覇道＝統治の道）・<c>HerrschaftRules</c>（支配の三類型＝正統性の源・生成済み）とは別＝
    /// こちらは<b>政体ごとの駆動原理（徳/名誉/恐怖）→服従コスト</b>を解く。
    /// <see cref="GovernanceRules"/>（per-system 内政の安定度）へは服従コスト・活力を係数で供給し、
    /// <c>PolityCorruptionRules</c>（政体腐化・同EPIC MONT）が原理腐敗の帰結（崩壊）を担う。
    /// 乱数なし・決定論・全入力クランプ。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class GovernmentPrincipleRules
    {
        /// <summary>
        /// 政体の原理に対応する情念の強さ（0..1）。共和政＝徳(civicVirtue)・君主政＝名誉(honorCulture)・
        /// 専制政＝恐怖(fearLevel)を主因に読む＝政体の形態がどの情念を原動力にするかを写す。
        /// 専制の恐怖は麻痺で目減りする（<see cref="FearDiminishingReturns"/> を通す）。
        /// </summary>
        public static float PrincipleStrength(GovernmentPrinciple principle,
            float civicVirtue, float honorCulture, float fearLevel, GovernmentPrincipleParams p)
        {
            switch (principle)
            {
                case GovernmentPrinciple.徳:   return Mathf.Clamp01(civicVirtue);
                case GovernmentPrinciple.名誉: return Mathf.Clamp01(honorCulture);
                case GovernmentPrinciple.恐怖: return FearDiminishingReturns(fearLevel, p);
                default: return 0f;
            }
        }

        public static float PrincipleStrength(GovernmentPrinciple principle,
            float civicVirtue, float honorCulture, float fearLevel)
            => PrincipleStrength(principle, civicVirtue, honorCulture, fearLevel, GovernmentPrincipleParams.Default);

        /// <summary>
        /// 服従のコスト（0..1、低いほど統治が容易）。原理が強いほどコストが低い＝
        /// <b>徳・名誉・恐怖が市民を自発的に従わせるので力ずくの維持費が要らない</b>。
        /// 原理皆無で最大コスト・原理満点で最小コストを線形補間する。
        /// </summary>
        public static float ObedienceCost(float principleStrength, GovernmentPrincipleParams p)
        {
            float s = Mathf.Clamp01(principleStrength);
            return Mathf.Clamp01(Mathf.Lerp(p.maxObedienceCost, p.minObedienceCost, s));
        }

        public static float ObedienceCost(float principleStrength)
            => ObedienceCost(principleStrength, GovernmentPrincipleParams.Default);

        /// <summary>
        /// 原理の腐敗（侵食された後の原理の強さ 0..1 を返す）。原理が損なわれると政体が腐敗する＝
        /// <b>徳の喪失・名誉の堕落・恐怖の麻痺</b>。erosion（侵食圧 0..1）ぶん原理を削る。
        /// 専制の恐怖は麻痺しやすい（慣れ）ため、君主政・共和政よりわずかに速く腐る。
        /// </summary>
        public static float PrincipleCorruption(GovernmentPrinciple principle,
            float principleStrength, float erosion, float dt, GovernmentPrincipleParams p)
        {
            float s = Mathf.Clamp01(principleStrength);
            float er = Mathf.Clamp01(erosion);
            // 恐怖は麻痺で速く損なわれる（慣れで効かなくなる）。
            float modeFactor = principle == GovernmentPrinciple.恐怖 ? (1f + p.fearHabituation) : 1f;
            float loss = er * p.corruptionRate * modeFactor * Mathf.Max(0f, dt);
            return Mathf.Clamp01(s - loss);
        }

        public static float PrincipleCorruption(GovernmentPrinciple principle,
            float principleStrength, float erosion, float dt)
            => PrincipleCorruption(principle, principleStrength, erosion, dt, GovernmentPrincipleParams.Default);

        /// <summary>
        /// 政体の活力（0..1）。原理が強く、かつ法が原理に沿う(lawAlignment)ほど政体は活力を持つ＝
        /// <b>原理を支える法制度がそろってはじめて原動力が回る</b>。法が原理に背けば活力は出ない（積で効く）。
        /// </summary>
        public static float RegimeVitality(float principleStrength, float lawAlignment)
        {
            float s = Mathf.Clamp01(principleStrength);
            float align = Mathf.Clamp01(lawAlignment);
            return Mathf.Clamp01(s * align);
        }

        /// <summary>
        /// 原理のミスマッチ（0..1、大きいほど機能不全）。政体の形態が要求する原理と、
        /// 実際の社会のエートス(actualEthos＝その政体の原理に対応する情念の実勢)がずれると機能不全に陥る＝
        /// <b>専制に徳を求める／共和政に徳がない</b>等。ミスマッチ＝1−実勢エートス（原理が要求水準に届かない差）。
        /// </summary>
        public static float PrincipleMismatch(GovernmentPrinciple principle, float actualEthos)
        {
            // principle は要求される原理の種別（どの情念を要するか）。actualEthos はその情念の実勢。
            float ethos = Mathf.Clamp01(actualEthos);
            return Mathf.Clamp01(1f - ethos);
        }

        /// <summary>
        /// 恐怖の逓減（実効的な恐怖 0..1 を返す）。専制の恐怖は高水準で麻痺すると効かなくなる＝
        /// <b>慣れ</b>。低水準では恐怖はほぼそのまま効くが、高水準ほど慣れで目減りする（凹カーブ）。
        /// 実効恐怖＝fearLevel×(1 − 慣れ×fearLevel)＝二次で逓減する。
        /// </summary>
        public static float FearDiminishingReturns(float fearLevel, GovernmentPrincipleParams p)
        {
            float f = Mathf.Clamp01(fearLevel);
            return Mathf.Clamp01(f * (1f - p.fearHabituation * f));
        }

        public static float FearDiminishingReturns(float fearLevel)
            => FearDiminishingReturns(fearLevel, GovernmentPrincipleParams.Default);

        /// <summary>
        /// 共和政の徳の持続可能性（0..1）。共和政の原理＝徳は平等が崩れると維持しにくい＝
        /// <b>モンテスキューいわく共和政には質素と平等が要る</b>。不平等(inequality 0..1)が徳を蝕む＝
        /// 持続可能性＝徳×(1 − 不平等)＝格差が広がるほど公共心が痩せる。
        /// </summary>
        public static float VirtueSustainability(float civicVirtue, float inequality)
        {
            float v = Mathf.Clamp01(civicVirtue);
            float ineq = Mathf.Clamp01(inequality);
            return Mathf.Clamp01(v * (1f - ineq));
        }

        /// <summary>
        /// 政体が腐敗した判定。原理の強さが閾値(threshold)を下回ると、原理が損なわれ政体が腐敗したとみなす＝
        /// <b>原理を失った政体は崩壊へ向かう</b>。
        /// </summary>
        public static bool IsRegimeCorrupted(float principleStrength, float threshold)
        {
            return Mathf.Clamp01(principleStrength) < Mathf.Clamp01(threshold);
        }

        public static bool IsRegimeCorrupted(float principleStrength)
            => IsRegimeCorrupted(principleStrength, GovernmentPrincipleParams.Default.corruptionThreshold);
    }
}
