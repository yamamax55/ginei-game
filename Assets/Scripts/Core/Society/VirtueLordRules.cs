using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 徳望の主の純ロジック（#徳望・三国志演義＝劉備玄徳）。徳をもって人を得る主君を再現する：
    /// ①<b>徳が限界突破</b>（実効徳が100を超える＝人がこぞって慕う）、
    /// ②<b>漢室の末裔自称</b>（自称でも大義名分となり正統性を高める）、
    /// ③<b>人物審美眼が限界突破</b>（三顧の礼・水魚の交わりで在野の臥龍を見抜き得る＝登用#2313の精度）、
    /// ④<b>固い絆</b>（桃園の誓い・水魚の交わり＝麾下は生死を共にし離反しない＝高い忠誠の下限）、
    /// ⑤<b>徳ゆえの陥穽</b>（義兄弟〔関羽〕を失うと諫言〔孔明〕を無視して出陣し大敗＝夷陵の戦い・陸遜の火計）。
    /// 数式は係数を返すだけで `RenownRules`#2304・`RecruitmentRules`#2313・`AllegianceDriftRules`#2312・`DynastyRules`#867・
    /// `CombatModifiers`#106 等の既存窓口へ橋渡しする。実効値パターン（基準非破壊）・決定論・test-first。
    /// </summary>
    public static class VirtueLordRules
    {
        /// <summary>徳望の主の徳の限界突破倍率。</summary>
        public const float VirtueTranscendMultiplier = 1.3f;
        /// <summary>限界突破した徳の絶対上限（並は100）。</summary>
        public const int VirtueCeiling = 130;
        /// <summary>漢室末裔自称が与える正統性ボーナス（自称でも大義名分）。</summary>
        public const float HanDescentLegitimacy = 0.3f;
        /// <summary>桃園/水魚で結ばれた麾下の忠誠の下限（離反しない）。</summary>
        public const float BondedLoyaltyFloor = 0.9f;
        /// <summary>諫言を無視した出陣の与効果倍率（夷陵の大敗）。</summary>
        public const float RecklessCampaignPenalty = 0.6f;

        /// <summary>
        /// 実効徳。徳望の主は限界突破し100超（<see cref="VirtueCeiling"/> まで）になる。並は100で頭打ち。
        /// </summary>
        public static int EffectiveVirtue(int virtue, bool isVirtuousLord)
        {
            int v = Mathf.Clamp(virtue, 0, 100);
            if (!isVirtuousLord) return v;
            return Mathf.Min(Mathf.RoundToInt(v * VirtueTranscendMultiplier), VirtueCeiling);
        }

        /// <summary>徳が生む人の慕い（忠誠・登用の引力。実効徳/100＝130で1.3）。`RenownRules`/`RecruitmentRules` に乗る。</summary>
        public static float VirtueLoyaltyPull(int effectiveVirtue)
            => Mathf.Max(0, effectiveVirtue) / 100f;

        /// <summary>漢室末裔自称による正統性ボーナス（自称でも大義名分＝人が集う）。`DynastyRules`#867 等へ。</summary>
        public static float HanLegitimacyBonus(bool claimsImperialDescent)
            => claimsImperialDescent ? HanDescentLegitimacy : 0f;

        /// <summary>
        /// 人物審美眼（限界突破）。徳望の主は徳ゆえ人を見抜き在野の才を得る＝実効徳/100の判定精度（&gt;1で臥龍を発掘＝三顧の礼）。
        /// 並は1.0（標準）。`RecruitmentRules`#2313 の見極め・説得に乗る。
        /// </summary>
        public static float TalentJudgmentFactor(bool isVirtuousLord, int effectiveVirtue)
            => isVirtuousLord ? Mathf.Max(0, effectiveVirtue) / 100f : 1f;

        /// <summary>桃園/水魚で結ばれた麾下の忠誠の下限（徳望の主のみ＝0.9・並は0＝特別な絆なし）。`AllegianceDriftRules`#2312 が参照。</summary>
        public static float LoyaltyFloorForBonded(bool isVirtuousLord)
            => isVirtuousLord ? BondedLoyaltyFloor : 0f;

        /// <summary>義兄弟を失い諫言を無視するか（夷陵＝徳望の主が義兄弟〔関羽〕喪失で激発し孔明の諫言を退ける）。</summary>
        public static bool IgnoresCounsel(bool isVirtuousLord, bool swornAllyLost)
            => isVirtuousLord && swornAllyLost;

        /// <summary>諫言を無視した出陣の与効果倍率（無視時＝大敗ペナルティ0.6・通常1.0）。陸遜の火計。</summary>
        public static float RecklessCampaignFactor(bool ignoringCounsel)
            => ignoringCounsel ? RecklessCampaignPenalty : 1f;
    }
}
