using UnityEngine;

namespace Ginei
{
    /// <summary>弾薬補給（継戦＝弾切れ）の調整係数。</summary>
    public readonly struct AmmunitionLogisticsParams
    {
        /// <summary>無交戦でも撃てば最低限消える基礎消費（fireRate=1・intensity=0 のときの消費率）。</summary>
        public readonly float baseConsumption;
        /// <summary>交戦強度が消費をどれだけ押し上げるかの重み（激戦ほど早く弾切れ）。</summary>
        public readonly float intensityWeight;
        /// <summary>弾薬残量比がこれ以下で火力が崩れ始める閾値（0..1）。これ未満で線形に激減。</summary>
        public readonly float firepowerFloorState;
        /// <summary>弾薬残量比がこれ以下で撃ち惜しみ（手数減）が始まる閾値（0..1）。</summary>
        public readonly float disciplineThreshold;
        /// <summary>完全な撃ち惜しみ時にも残る最低手数係数（0..1）。これを下回る手数にはしない。</summary>
        public readonly float minDiscipline;

        public AmmunitionLogisticsParams(float baseConsumption, float intensityWeight, float firepowerFloorState, float disciplineThreshold, float minDiscipline)
        {
            this.baseConsumption = Mathf.Max(0f, baseConsumption);
            this.intensityWeight = Mathf.Max(0f, intensityWeight);
            this.firepowerFloorState = Mathf.Clamp(firepowerFloorState, 0.01f, 1f);
            this.disciplineThreshold = Mathf.Clamp(disciplineThreshold, 0.01f, 1f);
            this.minDiscipline = Mathf.Clamp01(minDiscipline);
        }

        /// <summary>既定＝基礎消費1.0・強度重み1.0・火力崩壊閾値0.2・撃ち惜しみ閾値0.3・最低手数0.4。</summary>
        public static AmmunitionLogisticsParams Default => new AmmunitionLogisticsParams(1f, 1f, 0.2f, 0.3f, 0.4f);
    }

    /// <summary>
    /// 弾薬補給（AMMO・継戦の弾切れ）の純ロジック。戦闘で弾薬（ビーム充填・実体弾・ミサイル）を
    /// 消費し、尽きると火力が止まる。激しい交戦ほど早く弾切れし（消費は発射レート×交戦強度）、
    /// 補給艦・備蓄が継戦を支える。弾薬残量が乏しくなると火力が崩れ（FirepowerFromAmmo）、
    /// 指揮官は撃ち惜しんで手数を落とす（FireDisciplinePenalty）。完全に尽きれば撃てない
    /// （WinchesterState）。補給艦の搬送能力で弾薬は回復する（ResupplyRate）。
    /// 盤面非依存の plain 引数・乱数なし・決定論・実効値パターン（基準値非破壊）。
    /// 分担：MilitarySupplyTickRules（軍需補給の備蓄消費・払底損耗の汎用Tick）とは別＝こちらは
    /// 弾薬に特化した「継戦＝撃ち続けられる時間／弾切れ火力崩壊」のモデル／FuelLogisticsRules
    /// （燃料＝機動の航続）とは別＝こちらは弾薬＝火力の持続。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AmmunitionLogisticsRules
    {
        /// <summary>
        /// 弾薬消費率＝発射レート×(基礎消費＋強度重み×交戦強度)。
        /// 激戦（強度1.0）ほど、撃ちまくる（高レート）ほど早く弾が減る。
        /// </summary>
        public static float AmmoConsumption(float fireRate, float engagementIntensity, AmmunitionLogisticsParams p)
        {
            float rate = Mathf.Max(0f, fireRate);
            float intensity = Mathf.Clamp01(engagementIntensity);
            return rate * (p.baseConsumption + p.intensityWeight * intensity);
        }

        public static float AmmoConsumption(float fireRate, float engagementIntensity)
            => AmmoConsumption(fireRate, engagementIntensity, AmmunitionLogisticsParams.Default);

        /// <summary>弾薬残量比（0..1）＝残弾/搭載定数。定数0以下は弾倉なし＝0。</summary>
        public static float AmmoState(float ammoReserve, float ammoCapacity)
        {
            float cap = Mathf.Max(0f, ammoCapacity);
            if (cap <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Max(0f, ammoReserve) / cap);
        }

        /// <summary>
        /// 今の交戦のままで撃ち続けられる時間＝残弾/消費率。消費0以下は弾が減らない＝無限（PositiveInfinity）。
        /// </summary>
        public static float SustainableFireTime(float ammoReserve, float ammoConsumption)
        {
            float consume = ammoConsumption;
            if (consume <= 0f) return float.PositiveInfinity;
            return Mathf.Max(0f, ammoReserve) / consume;
        }

        /// <summary>
        /// 弾薬残量による火力の維持（0..1）＝崩壊閾値以上は満火力(1.0)、未満は残量比で線形に激減。
        /// 弾が潤沢なうちは衰えず、底をつくと急落する（尽きると撃てない）。
        /// </summary>
        public static float FirepowerFromAmmo(float ammoState, AmmunitionLogisticsParams p)
        {
            float state = Mathf.Clamp01(ammoState);
            if (state >= p.firepowerFloorState) return 1f;
            return Mathf.Clamp01(state / p.firepowerFloorState);
        }

        public static float FirepowerFromAmmo(float ammoState)
            => FirepowerFromAmmo(ammoState, AmmunitionLogisticsParams.Default);

        /// <summary>
        /// 撃ち惜しみによる手数係数（0..1・1.0=制限なし）＝撃ち惜しみ閾値以上は1.0、
        /// 未満は最低手数→1.0へ線形回復。弾が乏しいと無駄撃ちを避けて手数が落ちる。
        /// </summary>
        public static float FireDisciplinePenalty(float ammoState, AmmunitionLogisticsParams p)
        {
            float state = Mathf.Clamp01(ammoState);
            if (state >= p.disciplineThreshold) return 1f;
            float t = state / p.disciplineThreshold; // 0..1
            return Mathf.Clamp01(Mathf.Lerp(p.minDiscipline, 1f, t));
        }

        public static float FireDisciplinePenalty(float ammoState)
            => FireDisciplinePenalty(ammoState, AmmunitionLogisticsParams.Default);

        /// <summary>補給による弾薬回復率＝補給艦数×1隻あたり搬送能力（負はクランプ）。</summary>
        public static float ResupplyRate(int resupplyShips, float ammoTransferCapacity)
            => Mathf.Max(0, resupplyShips) * Mathf.Max(0f, ammoTransferCapacity);

        /// <summary>完全な弾切れ（撃てない）＝残弾0以下。</summary>
        public static bool WinchesterState(float ammoReserve)
            => ammoReserve <= 0f;

        /// <summary>弾薬残少か＝残量比が閾値以下（閾値は0..1にクランプ）。</summary>
        public static bool IsAmmoLow(float ammoState, float threshold)
            => Mathf.Clamp01(ammoState) <= Mathf.Clamp01(threshold);
    }
}
