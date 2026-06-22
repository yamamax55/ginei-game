namespace Ginei
{
    /// <summary>
    /// 具申の難度（純・決定論）。区分ごとに採用率へ掛ける補正＝高位の案件（政治/人事/作戦）ほど通りにくく、
    /// 身近な案件（個人/予算/海賊狩り）は通りやすい。さらに effectKey から区分を引く（採用判定が件数一律でなく
    /// 案件ごとに変わる）。<see cref="ProtagonistCareerDirector"/> の採用判定が使う。test-first。
    /// </summary>
    public static class PetitionDifficultyRules
    {
        /// <summary>区分の採用率補正（1.0＝基準）。高位ほど低い。</summary>
        public static float AdoptMultiplier(PetitionCategory category)
        {
            switch (category)
            {
                case PetitionCategory.海賊狩り: return 1.1f; // 若手の腕試し＝通りやすい
                case PetitionCategory.建白: return 1.0f;
                case PetitionCategory.予算: return 1.0f;
                case PetitionCategory.編制: return 0.9f;
                case PetitionCategory.作戦: return 0.8f;
                case PetitionCategory.人事: return 0.7f;
                default: return 0.6f;                        // 政治＝最も通りにくい
            }
        }

        /// <summary>effectKey から具申の区分を引く（不明＝建白）。各 *PetitionRules の判別を再利用。</summary>
        public static PetitionCategory CategoryOf(string effectKey)
        {
            if (FleetBudgetPetitionRules.IsBudgetPetition(effectKey)) return PetitionCategory.予算;
            if (FleetBasePetitionRules.IsBasePetition(effectKey)) return PetitionCategory.作戦;
            if (PirateHuntPetitionRules.IsPiratePetition(effectKey)) return PetitionCategory.海賊狩り;
            if (CareerPetitionRules.IsReinforce(effectKey)) return PetitionCategory.予算;
            if (CareerPetitionRules.IsHonor(effectKey)) return PetitionCategory.人事;
            if (CareerPetitionRules.IsTaxCut(effectKey) || CareerPetitionRules.IsTaxHike(effectKey)) return PetitionCategory.政治;
            return PetitionCategory.建白; // 名誉回復(self.clear)・汎用建白(career.petition)
        }
    }
}
