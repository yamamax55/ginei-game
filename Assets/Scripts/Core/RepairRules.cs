using UnityEngine;

namespace Ginei
{
    /// <summary>修理・回復の調整係数。</summary>
    public readonly struct RepairParams
    {
        /// <summary>応急修理（ドック外）で戻せる上限割合（最大戦力の何割まで自力で戻せるか）。</summary>
        public readonly float fieldRepairCap;
        /// <summary>応急修理の速度（戦力/時間・ドック外）。</summary>
        public readonly float fieldRate;
        /// <summary>ドック修理の速度係数（設備力 facilityPower に掛ける）。</summary>
        public readonly float dockRateScale;

        public RepairParams(float fieldRepairCap, float fieldRate, float dockRateScale)
        {
            this.fieldRepairCap = Mathf.Clamp01(fieldRepairCap);
            this.fieldRate = Mathf.Max(0f, fieldRate);
            this.dockRateScale = Mathf.Max(0f, dockRateScale);
        }

        /// <summary>既定＝応急上限70%・応急速度1/時間・ドック係数1.0。</summary>
        public static RepairParams Default => new RepairParams(0.7f, 1f, 1f);
    }

    /// <summary>
    /// 修理・回復の純ロジック。損傷した戦力は応急修理（ドック外）である程度まで自力で戻るが、
    /// 上限（fieldRepairCap）を超える完全回復はドック（造船所）が要る＝前線に居続ける艦隊は摩耗したまま。
    /// 建艦（新造＝<see cref="ShipyardRules"/>）とは別系統で、既存戦力の回復のみを扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RepairRules
    {
        /// <summary>応急修理で到達できる戦力上限＝最大戦力×fieldRepairCap。</summary>
        public static float FieldRepairCeiling(float maxStrength, RepairParams p)
        {
            return Mathf.Max(0f, maxStrength) * p.fieldRepairCap;
        }

        public static float FieldRepairCeiling(float maxStrength) => FieldRepairCeiling(maxStrength, RepairParams.Default);

        /// <summary>
        /// 応急修理の1tick後の戦力。fieldRate×dt で回復するが、応急上限を超えない（既に上限以上なら据え置き）。
        /// </summary>
        public static float FieldRepairTick(float current, float maxStrength, float dt, RepairParams p)
        {
            float cur = Mathf.Clamp(current, 0f, Mathf.Max(0f, maxStrength));
            float ceiling = FieldRepairCeiling(maxStrength, p);
            if (cur >= ceiling) return cur;
            return Mathf.Min(ceiling, cur + p.fieldRate * Mathf.Max(0f, dt));
        }

        public static float FieldRepairTick(float current, float maxStrength, float dt)
            => FieldRepairTick(current, maxStrength, dt, RepairParams.Default);

        /// <summary>
        /// ドック修理の1tick後の戦力。設備力 facilityPower×係数×dt で回復し、最大戦力まで戻せる
        /// （応急上限の制約なし＝ドックだけが完全回復できる）。
        /// </summary>
        public static float DockRepairTick(float current, float maxStrength, float facilityPower, float dt, RepairParams p)
        {
            float cur = Mathf.Clamp(current, 0f, Mathf.Max(0f, maxStrength));
            float rate = Mathf.Max(0f, facilityPower) * p.dockRateScale;
            return Mathf.Min(Mathf.Max(0f, maxStrength), cur + rate * Mathf.Max(0f, dt));
        }

        public static float DockRepairTick(float current, float maxStrength, float facilityPower, float dt)
            => DockRepairTick(current, maxStrength, facilityPower, dt, RepairParams.Default);

        /// <summary>完全回復までの所要時間（ドック修理）。速度0なら無限大。</summary>
        public static float TimeToFull(float current, float maxStrength, float facilityPower, RepairParams p)
        {
            float deficit = Mathf.Max(0f, Mathf.Max(0f, maxStrength) - Mathf.Clamp(current, 0f, Mathf.Max(0f, maxStrength)));
            if (deficit <= 0f) return 0f;
            float rate = Mathf.Max(0f, facilityPower) * p.dockRateScale;
            if (rate <= 0f) return float.PositiveInfinity;
            return deficit / rate;
        }

        public static float TimeToFull(float current, float maxStrength, float facilityPower)
            => TimeToFull(current, maxStrength, facilityPower, RepairParams.Default);

        /// <summary>ドック修理が必要か＝応急上限では戻りきらない損傷（current が応急上限未満ではなく、最大戦力未満かつ応急上限以上）。</summary>
        public static bool NeedsDock(float current, float maxStrength, RepairParams p)
        {
            float max = Mathf.Max(0f, maxStrength);
            float cur = Mathf.Clamp(current, 0f, max);
            return cur < max && cur >= FieldRepairCeiling(max, p);
        }

        public static bool NeedsDock(float current, float maxStrength) => NeedsDock(current, maxStrength, RepairParams.Default);
    }
}
