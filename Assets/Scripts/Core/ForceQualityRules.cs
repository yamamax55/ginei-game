using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍の「質」を会戦の戦闘力倍率へ合成する純ロジック（C4・#106 係数・test-first・唯一の窓口）。
    /// 会戦ダメージ（<c>ShipCombat.ComputeDamage</c>）は現状 提督攻撃×士気×側背面 のみで、
    /// <b>下士官団（背骨）・新兵練度・補給/弾薬の即応</b>という「質」が届いていない（C4 監査）。
    /// 本窓口はそれらを<b>単一の戦闘力倍率</b>に合成する＝会戦経路がこれを1つ掛けるだけで質が効く設計点。
    /// 各部品は既存窓口に委譲（<see cref="NcoEducationRules.ProficiencyMultiplier"/>＝下士官団、練度＝<see cref="RecruitTrainingRules"/>/
    /// <see cref="SkillEffectRules.MilitaryQuality"/>、即応＝<see cref="MilitaryReadinessRules.FirepowerFactor"/>/予算 <see cref="BudgetRules.MilitaryReadinessFactor"/>）。基準非破壊。
    /// </summary>
    public static class ForceQualityRules
    {
        public const float MinFactor = 0.4f;   // 質が崩壊しても下限（全滅的0倍を防ぐ）
        public const float MaxFactor = 2.0f;    // 精鋭の上限
        public const float RecruitFloor = 0.85f; // 新兵（練度0）の最低戦闘力倍率
        public const float RecruitSwing = 0.30f; // 練度0..1 で +0.30（練度1で1.15）

        /// <summary>新兵練度（0..1）→戦闘力倍率（0.85〜1.15）。素人部隊は当たらず精鋭は冴える。</summary>
        public static float RecruitFactor(float proficiency)
            => RecruitFloor + RecruitSwing * Mathf.Clamp01(proficiency);

        /// <summary>
        /// 軍の質→戦闘力倍率（0.4〜2.0）。下士官団の背骨（練度倍率）×新兵練度×即応（弾薬/予算）を合成。
        /// <paramref name="readinessFactor"/> は <see cref="MilitaryReadinessRules.FirepowerFactor"/> や
        /// <see cref="BudgetRules.MilitaryReadinessFactor"/> の出力（0..2 想定）をそのまま渡す（満額1.0）。
        /// </summary>
        public static float CombatMultiplier(NcoCorps corps, float recruitProficiency, float readinessFactor)
        {
            float backbone = NcoEducationRules.ProficiencyMultiplier(corps); // 1.0〜1.3（下士官団の厚み×質）
            float recruit = RecruitFactor(recruitProficiency);               // 0.85〜1.15
            float ready = Mathf.Clamp(readinessFactor, 0f, MaxFactor);       // 弾薬/予算の即応
            return Mathf.Clamp(backbone * recruit * ready, MinFactor, MaxFactor);
        }

        /// <summary>即応を満額（1.0）とみなす簡易版（補給/予算未配線の会戦用）。</summary>
        public static float CombatMultiplier(NcoCorps corps, float recruitProficiency)
            => CombatMultiplier(corps, recruitProficiency, 1f);

        /// <summary>
        /// 練度（経験値 veterancyXp）を含めた戦闘力倍率。
        /// <see cref="VeterancyRules.CombatFactor"/> を実効値パターンで乗算（基準値非破壊）。
        /// 歴戦艦隊は同じ装備・補給でも新兵より強い手応えを数値で表現する。
        /// </summary>
        public static float CombatMultiplier(NcoCorps corps, float recruitProficiency, float readinessFactor, float veterancyXp)
        {
            float vet = VeterancyRules.CombatFactor(veterancyXp);
            return Mathf.Clamp(CombatMultiplier(corps, recruitProficiency, readinessFactor) * vet, MinFactor, MaxFactor);
        }
    }
}
