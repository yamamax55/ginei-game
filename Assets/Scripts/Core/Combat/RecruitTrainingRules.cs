using UnityEngine;

namespace Ginei
{
    /// <summary>新兵教育の段階（米軍モデル）。募集＝accession／基礎訓練＝Basic Combat Training／専門訓練＝Advanced Individual Training（兵科別）。</summary>
    public enum RecruitStage { 募集, 基礎訓練, 専門訓練 }

    /// <summary>
    /// 新兵教育の純ロジック（RECRUIT・米軍の accession→BCT→AIT パイプラインを集約モデル化・test-first・唯一の窓口）。
    /// 徴募源（軍属POP＝<see cref="OccupationRules.RecruitablePool"/> #96）から訓練枠の範囲で兵を受け入れ、
    /// 教官の質・選抜基準・総力戦動員（<see cref="MobilizationRules"/> #2032）に応じて<b>修了者数</b>と<b>練度</b>を出す。
    /// 練度→軍の質は既存 <see cref="SkillEffectRules.MilitaryQuality"/>（#2034/#106）へ委譲＝二重実装しない。
    /// <b>個体粒度へ降りない＝訓練所×集約</b>（兵1人ずつのシミュはしない＝スカラビリティ規律）。状態は変えない（read-only）。
    /// 中核トレードオフ＝<b>質 vs 量</b>：基準を上げれば少数精鋭（受入↓練度↑）、総力戦で動員すれば頭数は増えるが練度は落ちる。
    /// </summary>
    public static class RecruitTrainingRules
    {
        /// <summary>平時に軍属プールから受け入れる割合の基準（全志願制 AVF 想定）。</summary>
        public const float BaseAccessionFraction = 0.20f;
        /// <summary>基準が受入率を絞る係数（standards=1 で受入率を半減）。</summary>
        public const float StandardsAccessionSquelch = 0.5f;

        /// <summary>基礎訓練の脱落率の基準（washout）。</summary>
        public const float BaseWashout = 0.15f;
        public const float CadreWashoutRelief = 0.10f;   // 教官の質で脱落↓
        public const float StandardsWashoutRelief = 0.10f; // 厳選（事前選抜）で脱落↓
        public const float MobilizationWashoutRise = 0.15f; // 動員サージ（基準緩和）で脱落↑

        /// <summary>練度（proficiency 0..1）の各駆動因。</summary>
        public const float ProficiencyFloor = 0.35f;       // 修了すれば最低限の練度
        public const float CadreProficiencyGain = 0.40f;   // 教官の質
        public const float StandardsProficiencyGain = 0.25f; // 厳選＝素材の質
        public const float MobilizationProficiencyDrop = 0.30f; // 訓練短縮で低下

        /// <summary>訓練所要（game-月）。BCT+AIT 相当。動員で短縮（下限あり）。</summary>
        public const float PeacetimeMonths = 6f;
        public const float MinMonths = 2f;
        public const float MobilizationMonthsCut = 0.6f;

        /// <summary>
        /// 募集（accession）数＝徴募源(軍属)から訓練枠の範囲で受け入れる。基準が高いほど絞り（厳選）、
        /// 総力戦動員（mobilizationRate 0..1）で受入率が上がる（徴兵/サージ）。capacity がスループット上限。
        /// </summary>
        public static int Accessions(RecruitDepot depot, float recruitablePool, float mobilizationRate)
        {
            if (depot == null || recruitablePool <= 0f) return 0;
            float mob = Mathf.Clamp01(mobilizationRate);
            float std = Mathf.Clamp01(depot.standards);
            float fraction = BaseAccessionFraction * (1f - StandardsAccessionSquelch * std) * (1f + mob);
            float desired = recruitablePool * Mathf.Max(0f, fraction);
            int cap = Mathf.Max(0, depot.capacity);
            return Mathf.Clamp(Mathf.FloorToInt(desired), 0, cap);
        }

        /// <summary>基礎訓練の脱落率 0..1（教官の質↑/厳選↑で低下、動員サージで上昇）。</summary>
        public static float WashoutFraction(RecruitDepot depot, float mobilizationRate)
        {
            if (depot == null) return 0f;
            float mob = Mathf.Clamp01(mobilizationRate);
            float w = BaseWashout
                      - CadreWashoutRelief * Mathf.Clamp01(depot.cadreQuality)
                      - StandardsWashoutRelief * Mathf.Clamp01(depot.standards)
                      + MobilizationWashoutRise * mob;
            return Mathf.Clamp01(w);
        }

        /// <summary>修了者数（trained manpower）＝募集×(1−脱落率)。</summary>
        public static int Graduates(int accessions, float washoutFraction)
        {
            if (accessions <= 0) return 0;
            float w = Mathf.Clamp01(washoutFraction);
            return Mathf.Max(0, Mathf.RoundToInt(accessions * (1f - w)));
        }

        /// <summary>修了者数（一括）＝募集→脱落を解いて返す。</summary>
        public static int Graduates(RecruitDepot depot, float recruitablePool, float mobilizationRate)
            => Graduates(Accessions(depot, recruitablePool, mobilizationRate), WashoutFraction(depot, mobilizationRate));

        /// <summary>
        /// 練度（proficiency 0..1）＝教官の質×厳選で上がり、総力戦動員（訓練短縮）で下がる。
        /// これを <see cref="MilitaryQuality"/> 経由で軍の質（戦闘力 #106）へ流す。
        /// </summary>
        public static float Proficiency(RecruitDepot depot, float mobilizationRate)
        {
            if (depot == null) return 0f;
            float mob = Mathf.Clamp01(mobilizationRate);
            float p = ProficiencyFloor
                      + CadreProficiencyGain * Mathf.Clamp01(depot.cadreQuality)
                      + StandardsProficiencyGain * Mathf.Clamp01(depot.standards)
                      - MobilizationProficiencyDrop * mob;
            return Mathf.Clamp01(p);
        }

        /// <summary>訓練所要（game-月）＝平時 BCT+AIT、動員で短縮（<see cref="MinMonths"/> 下限）。</summary>
        public static float TrainingMonths(float mobilizationRate)
        {
            float mob = Mathf.Clamp01(mobilizationRate);
            return Mathf.Max(MinMonths, PeacetimeMonths * (1f - MobilizationMonthsCut * mob));
        }

        /// <summary>
        /// 軍の質への寄与＝練度を既存 <see cref="SkillEffectRules.MilitaryQuality"/> に流す（#2034 SKILL-7・#106 へ合流）。
        /// 二重実装せず委譲する（練度＝militarySkill 入力）。
        /// </summary>
        public static float MilitaryQuality(RecruitDepot depot, float mobilizationRate, float baseline)
            => SkillEffectRules.MilitaryQuality(Proficiency(depot, mobilizationRate), baseline);
    }
}
