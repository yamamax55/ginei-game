using UnityEngine;

namespace Ginei
{
    /// <summary>研究プログラム（マクロ研究進行）の調整係数。</summary>
    public readonly struct ResearchProgramParams
    {
        /// <summary>研究予算→研究力の換算（予算1あたりの研究力）。</summary>
        public readonly float fundingToPower;
        /// <summary>技能(0..1)が研究効率へ寄与する重み（残りは技能ゼロでも残る基礎効率）。</summary>
        public readonly float skillWeight;
        /// <summary>後発勢力の格差縮小→技術水準ゲインの換算（拡散で進む段の速さ）。</summary>
        public readonly float diffusionLevelScale;

        public ResearchProgramParams(float fundingToPower, float skillWeight, float diffusionLevelScale)
        {
            this.fundingToPower = Mathf.Max(0f, fundingToPower);
            this.skillWeight = Mathf.Clamp01(skillWeight);
            this.diffusionLevelScale = Mathf.Max(0f, diffusionLevelScale);
        }

        /// <summary>既定＝予算1あたり研究力1・技能寄与0.7（基礎0.3）・拡散段スケール1.0。</summary>
        public static ResearchProgramParams Default => new ResearchProgramParams(1f, 0.7f, 1f);
    }

    /// <summary>
    /// 研究プログラムの薄いオーケストレータ（純ロジック・唯一の合成窓口・test-first）。
    /// 既存の未配線 Core（<see cref="ResearchRules"/>＝研究産出・進捗、<see cref="TechDiffusionRules"/>＝
    /// 勢力間の技術伝播＝後発者利益、<see cref="InnovationWaveRules"/>＝コンドラチェフ長波の景気勢い）を
    /// 勢力レベルの <see cref="ResearchState"/>（集約技術水準）の上で<b>合成するだけ</b>＝数式は全て委譲先が持ち、
    /// ここで二重実装しない（係数の合成のみ）。研究予算×技能で progress を進め、閾値で techLevel↑。
    /// 先進勢力に接触している後発勢力は拡散で追いつきやすい（<see cref="DiffusionGain"/>）。
    /// 個体粒度・通貨経済へ降りない（タイクン回避）。乱数なし・決定論。純ロジック（非 MonoBehaviour）。
    /// </summary>
    public static class ResearchProgramRules
    {
        /// <summary>
        /// 研究効率（0..1）＝技能(0..1)を <see cref="ResearchProgramParams.skillWeight"/> で重み付けし、
        /// 残りを技能ゼロでも残る基礎効率とする＝技能が高いほど研究がはかどるが、技能0でも止まらない。
        /// </summary>
        public static float ResearchEfficiency(float skill, ResearchProgramParams p)
        {
            float s = Mathf.Clamp01(skill);
            return (1f - p.skillWeight) + p.skillWeight * s;
        }

        public static float ResearchEfficiency(float skill)
            => ResearchEfficiency(skill, ResearchProgramParams.Default);

        /// <summary>
        /// 研究プログラムを deltaTime 進める（自前研究＝予算×技能）。
        /// 研究予算 researchFunding を研究力に換算し、研究効率（技能）を生産力代理として
        /// <see cref="ResearchRules.ResearchOutput"/> で研究産出を出し、その分 progress を積む。
        /// progress が levelCost に達するたび techLevel を +1 し、超過分を次段へ繰り越す（離散段）。
        /// null/非正の dt は無視＝決定論。基準値非破壊（rs のみ前進）。
        /// </summary>
        public static void TickYear(ResearchState rs, float researchFunding, float skill, float dt, ResearchProgramParams p)
        {
            if (rs == null || dt <= 0f) return;
            float power = Mathf.Max(0f, researchFunding) * p.fundingToPower;
            float efficiency = ResearchEfficiency(skill, p); // 技能を生産力代理として委譲
            // 研究産出は既存窓口に委譲（研究力×効率×基準産出）。
            float output = ResearchRules.ResearchOutput(power, efficiency);
            rs.progress += Mathf.Max(0f, output) * dt;

            float cost = rs.levelCost > 0f ? rs.levelCost : 1f;
            // 閾値ごとに段を上げ、超過分を繰り越す（複数段の一括前進にも耐える）。
            while (rs.progress >= cost)
            {
                rs.progress -= cost;
                rs.techLevel += 1f;
            }
        }

        public static void TickYear(ResearchState rs, float researchFunding, float skill, float dt)
            => TickYear(rs, researchFunding, skill, dt, ResearchProgramParams.Default);

        /// <summary>
        /// 先進勢力からの技術伝播による1tickの技術水準ゲイン（後発者利益）。
        /// 自勢力技術 ownTech が先進勢力 leaderTech に遅れているほど格差が大きく、開放度(接触) openness が
        /// 高いほど速く漏れる＝<see cref="TechDiffusionRules"/> に格差縮小量を委譲し、段スケールを掛けて
        /// techLevel のゲインに換算する。追い越しはしない（伝播は先進水準まで＝委譲先が保証）。
        /// leaderTech ≤ ownTech（先進でない）や openness 0 ならゲイン0＝独占は接触で時限的に崩れる思想。
        /// </summary>
        public static float DiffusionGain(float ownTech, float leaderTech, float openness, float dt, ResearchProgramParams p)
        {
            if (dt <= 0f) return 0f;
            float lead = leaderTech - ownTech;
            if (lead <= 0f) return 0f; // 先進でなければ漏れてこない
            // 技術格差を 0..1 へ正規化（先進水準を分母に＝相対的な遅れ）。
            float denom = Mathf.Max(1e-4f, Mathf.Max(0f, leaderTech));
            float techGap = Mathf.Clamp01(lead / denom);
            // 拡散速度（格差×接触×封鎖なし）を既存窓口へ委譲。
            float rate = TechDiffusionRules.DiffusionRate(techGap, openness, 0f);
            // 1tick の格差縮小量（0..techGap・後発者利益）を委譲で出す。
            float closing = TechDiffusionRules.CatchUpAcceleration(techGap, rate, dt);
            // 正規化格差→技術水準の段ゲインへ戻す（縮小ぶん×先進水準×スケール）。
            return closing * denom * p.diffusionLevelScale;
        }

        public static float DiffusionGain(float ownTech, float leaderTech, float openness, float dt)
            => DiffusionGain(ownTech, leaderTech, openness, dt, ResearchProgramParams.Default);

        /// <summary>
        /// 革新の波（コンドラチェフ長波）による研究効率の景気倍率。波位置 wavePosition の相での景気の勢い
        /// （<see cref="InnovationWaveRules.EconomicMomentum"/>）を、繁栄期に研究が加速し不況期に淀む倍率へ写す
        /// ＝<see cref="InnovationWaveRules"/> へ委譲する。倍率は floor..floor+span（既定 0.5..1.5）。
        /// 普及度 adoption は相の勢いを修飾する（普及が進むほど上昇/繁栄が勢いづく）。
        /// </summary>
        public static float InnovationWaveFactor(float wavePosition, float adoption, float floor, float span)
        {
            var phase = InnovationWaveRules.PhaseOf(wavePosition);
            float momentum = InnovationWaveRules.EconomicMomentum(phase, adoption); // 0..1
            float f = Mathf.Max(0f, floor);
            float s = Mathf.Max(0f, span);
            return f + s * Mathf.Clamp01(momentum);
        }

        /// <summary>既定＝景気倍率 0.5（不況の底）..1.5（繁栄のピーク）。</summary>
        public static float InnovationWaveFactor(float wavePosition, float adoption)
            => InnovationWaveFactor(wavePosition, adoption, 0.5f, 1f);
    }
}
