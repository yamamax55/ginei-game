using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    public partial class GalaxyView
    {
        // --- 兵器産業（#2020 兵器メーカー／軍産複合体#1389 の供給側）の配線 ---
        // 兵器メーカーは R&D で性能を上げ、平時/戦時の調達数ぶん兵器を生産して勢力の艦隊プール（#148）へ
        // 戦力供給する＝建艦（#884）と別経路の戦力調達。調達コストは国庫から払い、戦時は特需で生産が跳ねる。
        // 数式は WeaponsRules へ委譲（接続のみ）・勢力単位の集約（個社経営 micro へ降りない＝タイクン化回避）。

        /// <summary>勢力ごとの兵器メーカー（1社・#2020）。R&D で性能を上げ FleetPool へ戦力供給する。</summary>
        private readonly Dictionary<Faction, WeaponsManufacturer> weaponsMakers
            = new Dictionary<Faction, WeaponsManufacturer>();

        /// <summary>基準兵器性能（火力/装甲などの基準値・少量で創発＝タイクン化回避）。</summary>
        private const float WeaponBaseSpec = 100f;
        /// <summary>兵器メーカーの生産能力（年間に納入できる兵器数・bounded）。</summary>
        private const float WeaponProductionCapacity = 4f;
        /// <summary>R&D 1段あたりの性能寄与（性能＝基準×(1＋R&D×これ)）。</summary>
        private const float WeaponRdGainPerLevel = 0.1f;
        /// <summary>年あたりの R&D 進捗（小・技術水準で底上げ・上限で頭打ち）。</summary>
        private const float WeaponRdPerYear = 0.05f;
        /// <summary>R&D 水準の上限（際限ない性能インフレを避ける＝bounded）。</summary>
        private const float WeaponRdCap = 10f;
        /// <summary>調達単価（原価 proxy・コストプラスで上乗せされる）。</summary>
        private const float WeaponUnitCost = 200f;
        /// <summary>戦時の需要急増の基準（戦争強度＝1相当・WartimeDemand へ渡す）。</summary>
        private const float WeaponWarIntensity = 1f;

        /// <summary>勢力の兵器メーカー（基準性能/R&D/生産能力・#2020）。観測層専用＝read-only。</summary>
        public WeaponsManufacturer GetWeaponsManufacturer(Faction faction)
            => weaponsMakers.TryGetValue(faction, out var w) ? w : null;

        /// <summary>
        /// 兵器産業の年次（#2020 配線）：勢力ごとに兵器メーカーを用意し、R&D で兵器性能を上げ、平時/戦時の調達数ぶん
        /// 兵器を生産して<b>戦力換算</b>を勢力の艦隊プール（#148・建艦と別経路）へ供給する。調達コストは国庫から払い、戦時は
        /// 特需（<see cref="WeaponsRules.WartimeDemand"/>）で生産が跳ねる。数式は <see cref="WeaponsRules"/> へ委譲・集約近似。
        /// </summary>
        private void RunWeaponsIndustryTick()
        {
            if (DemoFactions == null) return;
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.帝国;
            // 基準性能（R&D 0 の兵器＝戦力換算の分母）。
            float referencePerformance = WeaponsRules.WeaponPerformance(WeaponBaseSpec, 0f, WeaponRdGainPerLevel);

            for (int f = 0; f < DemoFactions.Length; f++)
            {
                Faction fac = DemoFactions[f];

                // 不在ならシード（造船所/企業と同じ per-faction Dictionary パターン）。
                if (!weaponsMakers.TryGetValue(fac, out var maker) || maker == null)
                {
                    maker = new WeaponsManufacturer($"{fac}兵器廠", WeaponBaseSpec, 0f, WeaponProductionCapacity)
                    { faction = fac };
                    weaponsMakers[fac] = maker;
                }

                // R&D を年次で modest に前進（技術水準で底上げ・上限で頭打ち＝性能インフレ回避）。
                float techBoost = 1f + Mathf.Clamp01(TechLevelOf(fac));
                maker.rdLevel = Mathf.Min(WeaponRdCap, maker.rdLevel + WeaponRdPerYear * techBoost);

                // 兵器性能（R&D で火力/装甲が上がる）。
                float performance = WeaponsRules.WeaponPerformance(maker.baseWeaponSpec, maker.rdLevel, WeaponRdGainPerLevel);

                // 調達数：平時は生産能力、戦時は特需で跳ねる（WPN-3）。能力を上限にクランプ＝過剰生産を防ぐ。
                float warIntensity = IsAtWar(fac) ? WeaponWarIntensity : 0f;
                float demand = WeaponsRules.WartimeDemand(maker.productionCapacity, warIntensity,
                    WeaponsRules.DefaultWartimeSurge);
                float units = Mathf.Min(demand, maker.productionCapacity * WeaponsRules.DefaultWartimeSurge);
                if (units <= 0f) continue;

                // 兵器の戦力換算（高性能ほど1機あたりの戦力が高い）→ 勢力プール（#148）へ供給。
                int strength = Mathf.RoundToInt(
                    WeaponsRules.WeaponStrengthYield(units, performance, referencePerformance));
                if (strength > 0)
                    WeaponsRules.CommissionWeapons(fac, strength); // FleetPool.Add を内包＝二重加算しない

                // 調達コスト＝納入数×コストプラス単価。国庫から払う（0 下限）。
                float unitPrice = WeaponsRules.CostPlusPrice(WeaponUnitCost, WeaponsRules.DefaultCostPlusMargin);
                float cost = WeaponsRules.ProcurementRevenue(units, unitPrice);
                FactionState fs = StateOf(fac);
                if (fs != null) fs.treasury = Mathf.Max(0f, fs.treasury - cost);

                // プレイヤー勢力の調達のみ告知（AI の兵器調達は静かに進む）。
                if (fac == pf && strength > 0)
                    NotificationCenter.Push(NotificationCategory.建艦,
                        $"兵器調達：戦力 +{strength}（{maker.name}／性能 {performance:0}・プールへ）");
            }
        }
    }
}
