using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 部隊の継戦（兵站自立）状態（ORBAT-4 #1720 の会戦配線・観測＋opt-in）。
    /// 戦術単位（戦隊/分艦隊）は上級梯団へ未編入だと補給を賄えず<b>継戦不可</b>＝戦闘力が落ちる（<see cref="OrgClassRules"/>）。
    /// <b>既定は echelon=艦隊（作戦単位＝自己完結）かつ applyPenalty=false ＝従来動作（後方互換）</b>。
    /// 数値ロジックは持たず <see cref="OrgClassRules"/>（純ロジック）を読むだけ＝実効値パターン（基準値は書き換えない）。
    /// 観測は <see cref="FleetHUDManager"/> が表示、実効倍率は <see cref="FleetMorale.GetMoraleFactor"/> が掛ける。
    /// 旗艦に <see cref="Squadron"/> が Awake で自動付与（既定値ゆえ挙動不変）。戦術分遣隊は echelon を分艦隊/戦隊にし applyPenalty を立てる。
    /// </summary>
    [RequireComponent(typeof(FleetStrength))]
    public class FleetSustainment : MonoBehaviour
    {
        [Header("継戦（ORBAT-4 #1720）")]
        [Tooltip("この部隊の梯団種別。既定=艦隊（作戦単位＝自己完結）。戦隊/分艦隊にすると戦術単位＝上級未編入で継戦不可")]
        public EchelonType echelon = EchelonType.艦隊;

        [Tooltip("継戦ペナルティを実際に会戦へ効かせる（既定OFF＝観測のみ・後方互換）。ONで孤立した戦術単位の戦闘力が落ちる")]
        public bool applyPenalty = false;

        private FleetStrength strength;

        private void Awake() => strength = GetComponent<FleetStrength>();

        /// <summary>上級梯団へ編入済みか（軍団/軍集団名が入っていれば親あり＝補給を受けられる）。</summary>
        public bool HasParentFormation => strength != null && strength.HasEchelon;

        /// <summary>戦略/作戦/戦術の区分（<see cref="OrgClassRules.ClassOf"/>）。</summary>
        public UnitEchelonClass OrgClass => OrgClassRules.ClassOf(echelon);

        /// <summary>継戦できているか（自己完結 or 上級編入）。</summary>
        public bool IsSustained => OrgClassRules.CanSustain(echelon, HasParentFormation);

        /// <summary>継戦の実効倍率（孤立した戦術単位＝<see cref="OrgClassRules.UnsustainedPenaltyFactor"/>・それ以外1.0）。</summary>
        public float Factor => OrgClassRules.SustainmentFactor(echelon, HasParentFormation);

        /// <summary>実際に会戦へ効かせる倍率（applyPenalty=OFF や継戦OKなら 1.0＝挙動不変）。</summary>
        public float EffectiveFactor => applyPenalty ? Factor : 1f;
    }
}
