namespace Ginei
{
    /// <summary>
    /// 具申が採用されたときに反映される効果の種類。<see cref="PetitionEffectRules.Apply"/> が effectKey から判定する。
    /// </summary>
    public enum PetitionEffect { なし, 予算増額, 根拠地変更 }

    /// <summary>
    /// 主人公が具申で勝ち取った効果の蓄積（拝領した予算枠・現在の根拠地）。採用された具申がここへ反映され、
    /// 執務机・艦隊管理が読む（単一ソース）。純データ。
    /// </summary>
    public class PetitionGrants
    {
        /// <summary>採用された予算増額の累計（拝領した運用予算の上乗せ）。</summary>
        public long fleetBudgetGrant;

        /// <summary>採用された根拠地（星系ID。-1＝司令部一任/未変更）。</summary>
        public int fleetBaseSystemId = -1;

        public void Clear() { fleetBudgetGrant = 0; fleetBaseSystemId = -1; }
    }

    /// <summary>
    /// 採用された具申の effectKey を decode して効果を <see cref="PetitionGrants"/> へ反映する純ロジック（唯一の窓口）。
    /// 既存の effectKey 直列化（<see cref="FleetBudgetPetitionRules"/>/<see cref="FleetBasePetitionRules"/>）を再利用し、
    /// 「記帳だけ」だった具申を「採用で実際に効く」へつなぐ。未知キー（汎用建白等）は効果なし。test-first。
    /// </summary>
    public static class PetitionEffectRules
    {
        /// <summary>effectKey の効果種別を判定（適用はしない）。</summary>
        public static PetitionEffect Classify(string effectKey)
        {
            if (FleetBudgetPetitionRules.IsBudgetPetition(effectKey)) return PetitionEffect.予算増額;
            if (FleetBasePetitionRules.IsBasePetition(effectKey)) return PetitionEffect.根拠地変更;
            return PetitionEffect.なし;
        }

        /// <summary>
        /// 採用された具申を grants へ反映する。予算増額は枠を加算、根拠地変更は星系を設定。
        /// 反映した種別を返す（未知/null は <see cref="PetitionEffect.なし"/>＝武勲のみ）。
        /// </summary>
        public static PetitionEffect Apply(PetitionGrants grants, string effectKey)
        {
            if (grants == null) return PetitionEffect.なし;
            if (FleetBudgetPetitionRules.TryDecode(effectKey, out _, out int amount))
            {
                grants.fleetBudgetGrant += amount < 0 ? 0 : amount;
                return PetitionEffect.予算増額;
            }
            if (FleetBasePetitionRules.TryDecode(effectKey, out _, out int systemId))
            {
                grants.fleetBaseSystemId = systemId;
                return PetitionEffect.根拠地変更;
            }
            return PetitionEffect.なし;
        }
    }
}
