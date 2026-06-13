using UnityEngine;

namespace Ginei
{
    /// <summary>戦場回収の調整係数。</summary>
    public readonly struct SalvageParams
    {
        /// <summary>撃破された戦力のうち残骸として戦場に残る割合。</summary>
        public readonly float wreckRatio;
        /// <summary>残骸から戦力（修理再役）として回収できる上限割合。</summary>
        public readonly float strengthRecoveryRatio;
        /// <summary>残骸から資源（解体スクラップ）として回収できる上限割合。</summary>
        public readonly float resourceRecoveryRatio;

        public SalvageParams(float wreckRatio, float strengthRecoveryRatio, float resourceRecoveryRatio)
        {
            this.wreckRatio = Mathf.Clamp01(wreckRatio);
            this.strengthRecoveryRatio = Mathf.Clamp01(strengthRecoveryRatio);
            this.resourceRecoveryRatio = Mathf.Clamp01(resourceRecoveryRatio);
        }

        /// <summary>既定＝残骸化60%・戦力回収20%・資源回収50%。</summary>
        public static SalvageParams Default => new SalvageParams(0.6f, 0.2f, 0.5f);
    }

    /// <summary>
    /// 戦場回収の純ロジック（勝者が戦場を制する利得）。会戦で失われた戦力の一部は残骸として戦場に残り、
    /// 戦場の支配度（battlefield control 0..1＝勝者がほぼ1）に応じて回収できる＝同じ損害でも戦場を保持した側は
    /// 一部を取り戻し、放棄した側は全損になる。回収は「修理して再役（戦力）」と「解体（資源）」の二系統で、
    /// 同じ残骸を両方には使えない（配分 repairShare で割る）。生きた敵艦の拿捕は
    /// <see cref="BoardingRules.PrizeValue"/>（白兵戦）が担い、ここは戦闘後の残骸処理のみ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SalvageRules
    {
        /// <summary>戦場に残る残骸プール＝双方の喪失戦力合計×残骸化率。</summary>
        public static float WreckPool(float sideALosses, float sideBLosses, SalvageParams p)
        {
            return (Mathf.Max(0f, sideALosses) + Mathf.Max(0f, sideBLosses)) * p.wreckRatio;
        }

        public static float WreckPool(float sideALosses, float sideBLosses)
            => WreckPool(sideALosses, sideBLosses, SalvageParams.Default);

        /// <summary>自陣営が回収権を持つ残骸量＝残骸プール×戦場支配度(0..1)。支配しない戦場からは拾えない。</summary>
        public static float ClaimableWrecks(float wreckPool, float control)
        {
            return Mathf.Max(0f, wreckPool) * Mathf.Clamp01(control);
        }

        /// <summary>
        /// 修理再役で取り戻す戦力＝回収残骸×修理配分 repairShare(0..1)×戦力回収率。
        /// </summary>
        public static float RecoveredStrength(float claimableWrecks, float repairShare, SalvageParams p)
        {
            return Mathf.Max(0f, claimableWrecks) * Mathf.Clamp01(repairShare) * p.strengthRecoveryRatio;
        }

        public static float RecoveredStrength(float claimableWrecks, float repairShare)
            => RecoveredStrength(claimableWrecks, repairShare, SalvageParams.Default);

        /// <summary>
        /// 解体で得る資源＝回収残骸×(1−修理配分)×資源回収率。修理に回した残骸は溶かせない（排他配分）。
        /// </summary>
        public static float RecoveredResources(float claimableWrecks, float repairShare, SalvageParams p)
        {
            return Mathf.Max(0f, claimableWrecks) * (1f - Mathf.Clamp01(repairShare)) * p.resourceRecoveryRatio;
        }

        public static float RecoveredResources(float claimableWrecks, float repairShare)
            => RecoveredResources(claimableWrecks, repairShare, SalvageParams.Default);

        /// <summary>
        /// 戦場保持の利得差＝同じ損害で「支配した場合−放棄した場合」の戦力回収差。
        /// 撤退判断の比較材料（戦場に留まる価値の見える化）。
        /// </summary>
        public static float ControlPremium(float wreckPool, float repairShare, SalvageParams p)
        {
            float held = RecoveredStrength(ClaimableWrecks(wreckPool, 1f), repairShare, p);
            float abandoned = RecoveredStrength(ClaimableWrecks(wreckPool, 0f), repairShare, p);
            return held - abandoned;
        }

        public static float ControlPremium(float wreckPool, float repairShare)
            => ControlPremium(wreckPool, repairShare, SalvageParams.Default);
    }
}
