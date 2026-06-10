using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>艦の設計枠（スロット）に装填する技術モジュールの種別（#1066）。</summary>
    public enum ModuleType
    {
        主砲,
        装甲,
        機関,
        シールド,
        電子機器,
        格納庫
    }

    /// <summary>
    /// 艦に装填する技術モジュール（#1066・純データ）。設計枠1スロットを占める1個の装備。
    /// 重量 <see cref="weight"/>（搭載量を食う）・電力消費 <see cref="powerDraw"/>（機関はマイナス＝供給）・
    /// コスト <see cref="cost"/>・性能値 <see cref="rating"/>（種別に応じ攻撃/防御/機動/索敵などの寄与）を持つ。
    /// 解決は <see cref="ArmamentDesignRules"/> が唯一の窓口。
    /// </summary>
    [System.Serializable]
    public class ShipModule
    {
        /// <summary>モジュール名（任意）。</summary>
        public string moduleName;
        /// <summary>種別（主砲/装甲/機関/…）。</summary>
        public ModuleType type;
        /// <summary>重量（搭載量を消費・負はクランプ）。</summary>
        public float weight;
        /// <summary>電力収支への寄与＝消費（正）／供給（負・主に機関）。</summary>
        public float powerDraw;
        /// <summary>建造コスト（負はクランプ）。</summary>
        public float cost;
        /// <summary>性能値（種別に応じた寄与の大きさ・負はクランプ）。</summary>
        public float rating;

        public ShipModule() { }

        public ShipModule(ModuleType type, float weight, float powerDraw, float cost, float rating, string moduleName = null)
        {
            this.type = type;
            this.weight = weight;
            this.powerDraw = powerDraw;
            this.cost = cost;
            this.rating = rating;
            this.moduleName = moduleName;
        }
    }

    /// <summary>
    /// 艦体仕様（#1066・純データ）。設計の<b>器</b>＝総搭載量 <see cref="maxWeight"/>・総電力 <see cref="maxPower"/>・
    /// スロット数 <see cref="slots"/> の制約枠。ここに <see cref="ShipModule"/> を装填して設計する。
    /// </summary>
    [System.Serializable]
    public class HullSpec
    {
        /// <summary>艦体名（任意）。</summary>
        public string hullName;
        /// <summary>総搭載量（装填モジュール重量の合計上限・負はクランプ）。</summary>
        public float maxWeight;
        /// <summary>総電力供給の上限基準（機関供給とは別の艦体固有電力・負はクランプ）。</summary>
        public float maxPower;
        /// <summary>設計枠（スロット数・装填できるモジュール個数の上限・負はクランプ）。</summary>
        public int slots;

        public HullSpec() { }

        public HullSpec(float maxWeight, float maxPower, int slots, string hullName = null)
        {
            this.maxWeight = Mathf.Max(0f, maxWeight);
            this.maxPower = Mathf.Max(0f, maxPower);
            this.slots = Mathf.Max(0, slots);
            this.hullName = hullName;
        }
    }

    /// <summary>艦艇再設計の調整係数（#1066）。</summary>
    public readonly struct ArmamentDesignParams
    {
        /// <summary>戦闘力の攻撃寄与の重み。</summary>
        public readonly float offenseWeight;
        /// <summary>戦闘力の防御寄与（装甲＋シールド）の重み。</summary>
        public readonly float defenseWeight;
        /// <summary>戦闘力の支援寄与（電子機器＋格納庫）の重み。</summary>
        public readonly float supportWeight;
        /// <summary>特化ボーナス係数（同種集中の相乗の強さ）。</summary>
        public readonly float specializationFactor;

        public ArmamentDesignParams(float offenseWeight, float defenseWeight, float supportWeight, float specializationFactor)
        {
            this.offenseWeight = Mathf.Max(0f, offenseWeight);
            this.defenseWeight = Mathf.Max(0f, defenseWeight);
            this.supportWeight = Mathf.Max(0f, supportWeight);
            this.specializationFactor = Mathf.Max(0f, specializationFactor);
        }

        /// <summary>既定＝攻撃1.0/防御0.8/支援0.5/特化係数0.5。</summary>
        public static ArmamentDesignParams Default => new ArmamentDesignParams(1f, 0.8f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 艦艇再設計の純ロジック（#1066・唯一の窓口）。艦体（<see cref="HullSpec"/>）の設計枠に技術モジュール
    /// （<see cref="ShipModule"/>）を装填し、<b>総搭載量・電力・スロット数の制約下で性能を最適化する組合せ問題</b>を扱う。
    /// 積みたいものは全部積めない＝特化（砲艦/空母）か汎用かのトレードオフを式に出す（<see cref="SpecializationBonus"/>＝
    /// 同種集中の相乗）。<see cref="ShipClass"/>（戦艦/巡航/駆逐の<b>固定枠</b>）とは別＝こちらは<b>設計の自由度</b>。
    /// <see cref="ShipyardRules"/>（建造＝完成した設計を生産力で作る）・<see cref="ShipClassStats"/>（固定艦種の性能倍率）とも別。
    /// 乱数なし・決定論・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ArmamentDesignRules
    {
        /// <summary>装填モジュールの総重量（搭載量の消費・負係数はクランプ）。</summary>
        public static float TotalWeight(ShipModule[] modules)
        {
            if (modules == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] == null) continue;
                sum += Mathf.Max(0f, modules[i].weight);
            }
            return sum;
        }

        /// <summary>
        /// 総電力消費＝モジュール電力寄与の純消費の合計（機関の供給＝負は含めず、消費＝正のみを足す）。
        /// 供給と消費の収支は <see cref="PowerBalance"/> で見る。
        /// </summary>
        public static float TotalPowerDraw(ShipModule[] modules)
        {
            if (modules == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] == null) continue;
                float d = modules[i].powerDraw;
                if (d > 0f) sum += d;
            }
            return sum;
        }

        /// <summary>装填モジュールの総コスト（建造コストの合計・負はクランプ）。</summary>
        public static float TotalCost(ShipModule[] modules)
        {
            if (modules == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] == null) continue;
                sum += Mathf.Max(0f, modules[i].cost);
            }
            return sum;
        }

        /// <summary>艦体固有電力＋機関モジュールの供給（負 powerDraw の絶対値）の合計＝利用可能な総電力供給。</summary>
        public static float TotalPowerSupply(HullSpec hull, ShipModule[] modules)
        {
            float supply = hull == null ? 0f : Mathf.Max(0f, hull.maxPower);
            if (modules != null)
            {
                for (int i = 0; i < modules.Length; i++)
                {
                    if (modules[i] == null) continue;
                    float d = modules[i].powerDraw;
                    if (d < 0f) supply += -d; // 機関は供給
                }
            }
            return supply;
        }

        /// <summary>装填モジュール個数（null を除く）。</summary>
        public static int ModuleCount(ShipModule[] modules)
        {
            if (modules == null) return 0;
            int n = 0;
            for (int i = 0; i < modules.Length; i++)
                if (modules[i] != null) n++;
            return n;
        }

        /// <summary>
        /// 設計の妥当性＝搭載量・電力・スロット数の制約を<b>すべて</b>満たすか（過積載・過電・枠超過は不可）。
        /// 搭載量：総重量≤<see cref="HullSpec.maxWeight"/>／スロット：個数≤<see cref="HullSpec.slots"/>／
        /// 電力：総消費≤総供給（<see cref="TotalPowerSupply"/>）。
        /// </summary>
        public static bool IsValidDesign(HullSpec hull, ShipModule[] modules)
        {
            if (hull == null) return false;
            if (ModuleCount(modules) > hull.slots) return false;
            if (TotalWeight(modules) > Mathf.Max(0f, hull.maxWeight)) return false;
            if (TotalPowerDraw(modules) > TotalPowerSupply(hull, modules)) return false;
            return true;
        }

        /// <summary>
        /// 搭載量の利用率（0..1）＝総重量÷総搭載量。余裕を残すか限界まで積むかの指標（過積載は1.0頭打ち）。
        /// maxWeight≤0 は0。
        /// </summary>
        public static float WeightUtilization(HullSpec hull, ShipModule[] modules)
        {
            if (hull == null) return 0f;
            float max = Mathf.Max(0f, hull.maxWeight);
            if (max <= 0f) return 0f;
            return Mathf.Clamp01(TotalWeight(modules) / max);
        }

        /// <summary>
        /// 電力収支＝総供給−総消費（負なら電力不足＝一部モジュールが動かない＝過電設計の罰）。
        /// </summary>
        public static float PowerBalance(HullSpec hull, ShipModule[] modules)
            => TotalPowerSupply(hull, modules) - TotalPowerDraw(modules);

        /// <summary>種別ごとの性能値合計（rating の和・負はクランプ）。</summary>
        private static float RatingOf(ShipModule[] modules, ModuleType type)
        {
            if (modules == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < modules.Length; i++)
            {
                if (modules[i] == null || modules[i].type != type) continue;
                sum += Mathf.Max(0f, modules[i].rating);
            }
            return sum;
        }

        /// <summary>
        /// 総合戦闘力（#1066）。攻撃（主砲）・防御（装甲＋シールド）・支援（電子機器＋格納庫）を重み付き合成し、
        /// 特化ボーナス（<see cref="SpecializationBonus"/>）を乗じる。バランス設計か専門艦かの差が出る。
        /// </summary>
        public static float CombatRating(ShipModule[] modules, ArmamentDesignParams p)
        {
            float offense = RatingOf(modules, ModuleType.主砲);
            float defense = RatingOf(modules, ModuleType.装甲) + RatingOf(modules, ModuleType.シールド);
            float support = RatingOf(modules, ModuleType.電子機器) + RatingOf(modules, ModuleType.格納庫);

            float baseRating = offense * p.offenseWeight + defense * p.defenseWeight + support * p.supportWeight;
            return baseRating * SpecializationBonus(modules, p);
        }

        public static float CombatRating(ShipModule[] modules)
            => CombatRating(modules, ArmamentDesignParams.Default);

        /// <summary>
        /// 特化ボーナス倍率（≥1.0）＝同種モジュールを集中させると相乗（砲艦/空母のような専門艦）。
        /// 装填モジュールに占める最大種別の割合 share（0..1）が高いほど大きく、
        /// 倍率 = 1 + specializationFactor × max(0, share − 1/種別数)（汎用＝均等分散はボーナスほぼ0）。
        /// モジュール無しは1.0。
        /// </summary>
        public static float SpecializationBonus(ShipModule[] modules, ArmamentDesignParams p)
        {
            int total = ModuleCount(modules);
            if (total <= 0) return 1f;

            int typeCount = Enum.GetValues(typeof(ModuleType)).Length;
            int maxOfType = 0;
            for (int t = 0; t < typeCount; t++)
            {
                int c = 0;
                for (int i = 0; i < modules.Length; i++)
                    if (modules[i] != null && (int)modules[i].type == t) c++;
                if (c > maxOfType) maxOfType = c;
            }

            float share = (float)maxOfType / total;
            float even = 1f / typeCount; // 完全均等＝汎用の基準
            float concentration = Mathf.Max(0f, share - even);
            return 1f + p.specializationFactor * concentration;
        }

        public static float SpecializationBonus(ShipModule[] modules)
            => SpecializationBonus(modules, ArmamentDesignParams.Default);
    }
}
