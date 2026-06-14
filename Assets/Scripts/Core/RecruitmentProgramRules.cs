using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 徴兵・動員・練度の薄いオーケストレータ（純ロジック・read-only・test-first）。
    /// 既存の未配線 Core を合成して「徴募源（軍属POP＝<see cref="OccupationRules.RecruitablePool"/> #96）×動員率 →
    /// 新兵数 → 練度反映の戦力（<see cref="FleetPool"/> へ加える兵力の素）」を一本の流れにまとめる。
    /// 数式は一切持たず既存窓口へ委譲する＝<b>合成のみ</b>：動員プールは <see cref="MobilizationRules.MobilizedPool"/>、
    /// 練度→軍の質は <see cref="SkillEffectRules.MilitaryQuality"/>（#2034/#106・<see cref="RecruitTrainingRules"/> と同じ経路）、
    /// 練度（経験値）の蓄積は <see cref="VeterancyRules.GainFromBattle"/> に流す。状態は引数で受け、戻り値は
    /// <see cref="FleetPool"/> へ加える兵力。乱数なし・決定論。純ロジック（非 MonoBehaviour）。
    /// </summary>
    public static class RecruitmentProgramRules
    {
        /// <summary>
        /// 年間の新兵数＝徴募源（軍属POP）×動員率×経過年 dt。<see cref="MobilizationRules.MobilizedPool"/> へ委譲し
        /// （徴募源を労働力プールとみなして動員率ぶん引き出す）、暦の経過 dt（game-year 比・既定1年）で時間積分する。
        /// プール0・動員率0・dt0 ならいずれも0。負値は0へクランプ。
        /// </summary>
        public static float AnnualRecruits(float recruitablePool, float mobilizationRate, float dt)
        {
            float perYear = MobilizationRules.MobilizedPool(recruitablePool, mobilizationRate);
            return Mathf.Max(0f, perYear) * Mathf.Max(0f, dt);
        }

        /// <summary>dt=1（1年ぶん）の簡易窓口。</summary>
        public static float AnnualRecruits(float recruitablePool, float mobilizationRate)
            => AnnualRecruits(recruitablePool, mobilizationRate, 1f);

        /// <summary>
        /// 練度反映の戦力＝新兵数 recruits を訓練の質 trainingQuality(0..1) で目減りさせた、<see cref="FleetPool"/> へ
        /// 加える兵力。質→係数は <see cref="SkillEffectRules.MilitaryQuality"/>（baseline=1）へ委譲＝
        /// <see cref="RecruitTrainingRules.MilitaryQuality"/> と同じ「練度→軍の質」経路を使い二重実装しない
        /// （質0でも 0.6 倍の最低戦力・質1で 1.0 倍＝錬度が高いほど頭数が戦力になる）。recruits≤0 なら0。
        /// </summary>
        public static float TrainedStrength(float recruits, float trainingQuality)
        {
            if (recruits <= 0f) return 0f;
            return recruits * SkillEffectRules.MilitaryQuality(trainingQuality, 1f);
        }

        /// <summary>
        /// 訓練所（<see cref="RecruitDepot"/>）を持つ呼び出し向けの合成版。募集→脱落を経た修了者数
        /// （<see cref="RecruitTrainingRules.Graduates"/>）に、訓練所の練度（<see cref="RecruitTrainingRules.Proficiency"/>）を
        /// <see cref="TrainedStrength"/> で反映した、<see cref="FleetPool"/> へ加える兵力。depot=null や徴募源0なら0。
        /// </summary>
        public static float TrainedStrengthFromDepot(RecruitDepot depot, float recruitablePool, float mobilizationRate)
        {
            if (depot == null || recruitablePool <= 0f) return 0f;
            int graduates = RecruitTrainingRules.Graduates(depot, recruitablePool, mobilizationRate);
            float proficiency = RecruitTrainingRules.Proficiency(depot, mobilizationRate);
            return TrainedStrength(graduates, proficiency);
        }

        /// <summary>
        /// 会戦後の練度（経験値）蓄積＝<see cref="VeterancyRules.GainFromBattle"/> へ委譲（激戦ほど積む）。
        /// 補充による練度希釈は <see cref="VeterancyRules.DiluteOnReinforce"/> が別途担う（ここは合成の入口のみ）。
        /// </summary>
        public static float VeterancyGain(float xp, float intensity)
            => VeterancyRules.GainFromBattle(xp, intensity);
    }
}
