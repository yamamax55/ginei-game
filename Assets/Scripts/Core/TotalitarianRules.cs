using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 全体主義圧力の純データ（BNAL-3 #1535・アーレント『全体主義の起原』型）。
    /// 原子化・恐怖・イデオロギーの掌握の3軸＝全体的支配の構成要素。可変フィールド（時間で動く）。
    /// </summary>
    public struct TotalitarianPressure
    {
        /// <summary>原子化(0..1)＝人々が孤立させられ横の繋がりを失った度合い。</summary>
        public float atomization;
        /// <summary>恐怖(0..1)＝予測不能な暴力で麻痺した度合い。</summary>
        public float terror;
        /// <summary>イデオロギーの掌握(0..1)＝虚構の論理が現実を上書きする度合い。</summary>
        public float ideologyGrip;

        public TotalitarianPressure(float atomization, float terror, float ideologyGrip)
        {
            this.atomization = Mathf.Clamp01(atomization);
            this.terror = Mathf.Clamp01(terror);
            this.ideologyGrip = Mathf.Clamp01(ideologyGrip);
        }
    }

    /// <summary>全体主義の調整係数（アーレント型）。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct TotalitarianParams
    {
        /// <summary>予測不能な弾圧が恐怖を深める速度（per dt・最大強度のとき）。</summary>
        public readonly float terrorDeepenRate;
        /// <summary>恐怖が原子化を介して自己増幅するループ利得（恐怖が更なる恐怖を呼ぶ核）。</summary>
        public readonly float terrorLoopWeight;
        /// <summary>恐怖と監視が孤立を進める速度（per dt・最大のとき）。</summary>
        public readonly float atomizeRate;
        /// <summary>イデオロギーが内的論理で暴走する速度（per dt・掌握1のとき）。</summary>
        public readonly float ideologyMomentumRate;

        public TotalitarianParams(float terrorDeepenRate, float terrorLoopWeight,
                                  float atomizeRate, float ideologyMomentumRate)
        {
            this.terrorDeepenRate = Mathf.Max(0f, terrorDeepenRate);
            this.terrorLoopWeight = Mathf.Max(0f, terrorLoopWeight);
            this.atomizeRate = Mathf.Max(0f, atomizeRate);
            this.ideologyMomentumRate = Mathf.Max(0f, ideologyMomentumRate);
        }

        /// <summary>既定＝恐怖深化0.3・恐怖ループ利得0.5・原子化0.2・イデオロギー暴走0.1。</summary>
        public static TotalitarianParams Default => new TotalitarianParams(0.3f, 0.5f, 0.2f, 0.1f);
    }

    /// <summary>
    /// 全体主義の動態（BNAL-3 #1535・アーレント『全体主義の起原』型）の純ロジック。全体主義は
    /// ①原子化（人々を孤立させ横の連帯を断つ）②恐怖（予測不能な暴力で誰もが標的になりうる麻痺）
    /// ③イデオロギーによる現実の代替（虚構の論理が現実の矛盾を上書きする）の3つで**自己強化**する。
    /// 核は <see cref="TerrorLoopGain"/>＝恐怖が原子化を強め、孤立した者にはさらに恐怖が効くという
    /// ループ。3要素が揃うと個人の全人格を握る全体的支配（<see cref="TotalControl"/>）が固まり、
    /// 誰も互いを信じられず抵抗がほぼ不可能になる（<see cref="ResistanceImpossibility"/>）。
    /// 反体制派の抑圧装置（<see cref="SecurityRules"/>）・公然時限の戒厳令（<see cref="MartialLawRules"/>）
    /// とは別系統＝こちらは原子化×恐怖×イデオロギー代替の**自己強化ループそのもの**。
    /// 複数性の喪失は <see cref="PluralityRules"/>、無思想性は <see cref="ThoughtlessnessRules"/>（同 EPIC BNAL）が担う。
    /// 全入力 0..1 にクランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TotalitarianRules
    {
        /// <summary>
        /// 恐怖の1tick後の値(0..1)。予測不能な弾圧 unpredictability が高いほど（誰が標的か分からない＝
        /// 麻痺）弾圧 repression が恐怖を深める。予測可能な暴力は恐怖を増幅しにくい（避けられるから）。
        /// </summary>
        public static float TerrorTick(float terror, float repression, float unpredictability, float dt, TotalitarianParams p)
        {
            float d = Mathf.Max(0f, dt);
            float deepen = p.terrorDeepenRate * Mathf.Clamp01(repression) * Mathf.Clamp01(unpredictability) * d;
            return Mathf.Clamp01(Mathf.Clamp01(terror) + deepen);
        }

        public static float TerrorTick(float terror, float repression, float unpredictability, float dt)
            => TerrorTick(terror, repression, unpredictability, dt, TotalitarianParams.Default);

        /// <summary>
        /// 恐怖ループの利得(0..1)＝核。恐怖が原子化を強め、孤立した者ほどさらに恐怖が効きやすくなる
        /// 自己増幅の利得＝terror × atomization × loopWeight。両者が揃って初めて回る（どちらか0なら0）。
        /// </summary>
        public static float TerrorLoopGain(float terror, float atomization, TotalitarianParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(terror) * Mathf.Clamp01(atomization) * p.terrorLoopWeight);
        }

        public static float TerrorLoopGain(float terror, float atomization)
            => TerrorLoopGain(terror, atomization, TotalitarianParams.Default);

        /// <summary>
        /// イデオロギーによる現実の代替度(0..1)＝イデオロギーの掌握 ideologyGrip が現実の矛盾
        /// realityContradiction を上書きする度合い。掌握が強いほど、矛盾が大きくても虚構が現実に勝つ。
        /// </summary>
        public static float IdeologySubstitution(float ideologyGrip, float realityContradiction)
        {
            float grip = Mathf.Clamp01(ideologyGrip);
            // 掌握が強いほど矛盾を呑み込む＝矛盾×掌握ぶんを虚構が上書きする。
            return Mathf.Clamp01(grip * Mathf.Clamp01(realityContradiction) + grip * (1f - Mathf.Clamp01(realityContradiction)) * grip);
        }

        /// <summary>
        /// 原子化の1tick後の値(0..1)。恐怖と監視 surveillance が横の繋がりを断ち、人々を孤立させる
        /// ＝誰も互いを信じられない（密告の恐れ）。両者の最大効果ぶんずつ進む。
        /// </summary>
        public static float AtomizationTick(float atomization, float terror, float surveillance, float dt, TotalitarianParams p)
        {
            float d = Mathf.Max(0f, dt);
            float isolate = p.atomizeRate * Mathf.Max(Mathf.Clamp01(terror), Mathf.Clamp01(surveillance)) * d;
            return Mathf.Clamp01(Mathf.Clamp01(atomization) + isolate);
        }

        public static float AtomizationTick(float atomization, float terror, float surveillance, float dt)
            => AtomizationTick(atomization, terror, surveillance, dt, TotalitarianParams.Default);

        /// <summary>
        /// 全体的支配の強さ(0..1)＝原子化×恐怖×イデオロギー掌握の積（幾何的）。3要素が揃って初めて
        /// 個人の全人格を握る＝どれか1つでも欠ければ全体的支配は成立しない（部分的圧政に留まる）。
        /// </summary>
        public static float TotalControl(float atomization, float terror, float ideologyGrip)
        {
            return Mathf.Clamp01(Mathf.Clamp01(atomization) * Mathf.Clamp01(terror) * Mathf.Clamp01(ideologyGrip));
        }

        public static float TotalControl(TotalitarianPressure tp)
            => TotalControl(tp.atomization, tp.terror, tp.ideologyGrip);

        /// <summary>
        /// 抵抗の不可能度(0..1)＝原子化と恐怖が抵抗をほぼ不可能にする。孤立した者は連帯できず、
        /// 恐怖で誰も信じられない＝両者が揃うほど抵抗の余地が消える（積で増幅）。
        /// </summary>
        public static float ResistanceImpossibility(float atomization, float terror)
        {
            float a = Mathf.Clamp01(atomization);
            float t = Mathf.Clamp01(terror);
            // 孤立か恐怖の片方でも抵抗を削るが、両者が揃うと相乗で不可能に近づく。
            return Mathf.Clamp01(a + t - a * t);
        }

        /// <summary>
        /// イデオロギーの1tick後の掌握(0..1)＝内的論理による暴走。掌握が強いほど自己の論理だけで
        /// 加速し、現実から乖離しても止まらない（虚構の慣性）。掌握が強いほど伸びも速い（自乗的）。
        /// </summary>
        public static float IdeologicalMomentum(float ideologyGrip, float dt, TotalitarianParams p)
        {
            float d = Mathf.Max(0f, dt);
            float grip = Mathf.Clamp01(ideologyGrip);
            float momentum = p.ideologyMomentumRate * grip * grip * d;
            return Mathf.Clamp01(grip + momentum);
        }

        public static float IdeologicalMomentum(float ideologyGrip, float dt)
            => IdeologicalMomentum(ideologyGrip, dt, TotalitarianParams.Default);

        /// <summary>全体主義が固まったか＝全体的支配 totalControl が閾値以上（個人の全人格を掌握）。</summary>
        public static bool IsTotalitarianConsolidated(float totalControl, float threshold)
        {
            return Mathf.Clamp01(totalControl) >= Mathf.Clamp01(threshold);
        }
    }
}
