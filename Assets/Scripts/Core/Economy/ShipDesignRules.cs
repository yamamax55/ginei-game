using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>登録済みの艦艇設計（#1066）。艦種ごとに一件だけ現役設計を持てる。</summary>
    [Serializable]
    public class ShipDesign
    {
        public int id;
        public string designName;
        public ShipClass shipClass;
        public HullSpec hull = new HullSpec();
        public ShipModule[] modules = Array.Empty<ShipModule>();
        public bool active;
    }

    /// <summary>勢力ごとの設計台帳。廃止設計も履歴として残す。</summary>
    [Serializable]
    public class ShipDesignState
    {
        public List<ShipDesign> designs = new List<ShipDesign>();
        public int nextDesignId = 1;
    }

    /// <summary>固定艦種性能へ掛ける、設計由来のクラス非依存倍率。</summary>
    public readonly struct ShipDesignPerformance
    {
        public readonly float firepower;
        public readonly float durability;
        public readonly float mobility;
        public readonly float support;

        public ShipDesignPerformance(float firepower, float durability, float mobility, float support)
        {
            this.firepower = Mathf.Max(0.1f, firepower);
            this.durability = Mathf.Max(0.1f, durability);
            this.mobility = Mathf.Max(0.1f, mobility);
            this.support = Mathf.Max(0.1f, support);
        }

        public static ShipDesignPerformance Baseline => new ShipDesignPerformance(1f, 1f, 1f, 1f);
    }

    /// <summary>設計の登録・改名・現役化と性能換算の唯一の窓口（#1066）。</summary>
    public static class ShipDesignRules
    {
        public static void NormalizeLoaded(ShipDesignState state)
        {
            if (state == null) return;
            if (state.designs == null) state.designs = new List<ShipDesign>();

            int maxId = 0;
            for (int i = 0; i < state.designs.Count; i++)
                if (state.designs[i] != null) maxId = Math.Max(maxId, state.designs[i].id);
            var activeClasses = new HashSet<ShipClass>();
            for (int i = 0; i < state.designs.Count; i++)
            {
                ShipDesign d = state.designs[i];
                if (d == null) continue;
                if (d.id <= 0) d.id = ++maxId;
                if (string.IsNullOrWhiteSpace(d.designName)) d.designName = $"{d.shipClass}設計 {d.id}";
                if (d.hull == null) d.hull = new HullSpec();
                if (d.modules == null) d.modules = Array.Empty<ShipModule>();
                // 壊れた旧データに同一艦種の現役が複数あっても、先頭一件だけを採用する。
                if (d.active && !activeClasses.Add(d.shipClass)) d.active = false;
            }
            state.nextDesignId = Math.Max(Math.Max(1, state.nextDesignId), maxId + 1);
        }

        public static bool TryRegister(ShipDesignState state, string name, ShipClass shipClass,
            HullSpec hull, ShipModule[] modules, out ShipDesign registered, out string failureReason)
        {
            registered = null;
            failureReason = null;
            if (state == null) { failureReason = "設計台帳がありません"; return false; }
            if (string.IsNullOrWhiteSpace(name)) { failureReason = "設計名を入力してください"; return false; }
            if (!ArmamentDesignRules.IsValidDesign(hull, modules))
            {
                failureReason = DesignFailureReason(hull, modules);
                return false;
            }

            NormalizeLoaded(state);
            for (int i = 0; i < state.designs.Count; i++)
                if (state.designs[i] != null && string.Equals(state.designs[i].designName, name.Trim(), StringComparison.OrdinalIgnoreCase))
                { failureReason = "同名の設計が既にあります"; return false; }

            registered = new ShipDesign
            {
                id = state.nextDesignId++,
                designName = name.Trim(),
                shipClass = shipClass,
                // UIの編集中ドラフトを後から変更しても、登録済み設計が書き換わらないよう値を複製する。
                hull = CloneHull(hull),
                modules = CloneModules(modules),
                active = true
            };
            state.designs.Add(registered);
            SetActive(state, registered);
            return true;
        }

        public static bool TryRename(ShipDesignState state, int designId, string newName, out string failureReason)
        {
            failureReason = null;
            if (state == null || string.IsNullOrWhiteSpace(newName)) { failureReason = "新しい設計名を入力してください"; return false; }
            NormalizeLoaded(state);
            string trimmed = newName.Trim();
            ShipDesign target = null;
            for (int i = 0; i < state.designs.Count; i++)
            {
                ShipDesign d = state.designs[i];
                if (d == null) continue;
                if (d.id == designId) target = d;
                else if (string.Equals(d.designName, trimmed, StringComparison.OrdinalIgnoreCase))
                { failureReason = "同名の設計が既にあります"; return false; }
            }
            if (target == null) { failureReason = "設計が見つかりません"; return false; }
            target.designName = trimmed;
            return true;
        }

        public static ShipDesign ActiveFor(ShipDesignState state, ShipClass shipClass)
        {
            if (state?.designs == null) return null;
            for (int i = 0; i < state.designs.Count; i++)
            {
                ShipDesign d = state.designs[i];
                if (d != null && d.active && d.shipClass == shipClass) return d;
            }
            return null;
        }

        public static bool SetActive(ShipDesignState state, ShipDesign target)
        {
            if (state?.designs == null || target == null) return false;
            bool found = state.designs.Contains(target);
            if (!found && target.id > 0)
                found = state.designs.Exists(d => d != null && d.id == target.id);
            if (!found) return false;
            for (int i = 0; i < state.designs.Count; i++)
            {
                ShipDesign d = state.designs[i];
                if (d != null && d.shipClass == target.shipClass) d.active = d.id == target.id;
            }
            return true;
        }

        public static ShipDesignPerformance PerformanceOf(ShipDesign design)
        {
            if (design == null || !ArmamentDesignRules.IsValidDesign(design.hull, design.modules))
                return ShipDesignPerformance.Baseline;
            float guns = Rating(design.modules, ModuleType.主砲);
            float armor = Rating(design.modules, ModuleType.装甲);
            float shields = Rating(design.modules, ModuleType.シールド);
            float engines = Rating(design.modules, ModuleType.機関);
            float electronics = Rating(design.modules, ModuleType.電子機器);
            float hangars = Rating(design.modules, ModuleType.格納庫);
            float specialization = ArmamentDesignRules.SpecializationBonus(design.modules);
            return new ShipDesignPerformance(
                1f + Mathf.Min(0.75f, guns / 100f) * specialization,
                1f + Mathf.Min(0.75f, (armor + shields) / 120f),
                1f + Mathf.Min(0.5f, engines / 100f),
                1f + Mathf.Min(0.5f, (electronics + hangars) / 100f));
        }

        public static string DesignFailureReason(HullSpec hull, ShipModule[] modules)
        {
            if (hull == null) return "艦体が選択されていません";
            if (ArmamentDesignRules.ModuleCount(modules) > hull.slots) return "技術スロット数を超えています";
            if (ArmamentDesignRules.TotalWeight(modules) > Mathf.Max(0f, hull.maxWeight)) return "搭載量を超えています";
            if (ArmamentDesignRules.TotalPowerDraw(modules) > ArmamentDesignRules.TotalPowerSupply(hull, modules)) return "電力が不足しています";
            return null;
        }

        private static float Rating(ShipModule[] modules, ModuleType type)
        {
            float total = 0f;
            if (modules == null) return total;
            for (int i = 0; i < modules.Length; i++)
                if (modules[i] != null && modules[i].type == type) total += Mathf.Max(0f, modules[i].rating);
            return total;
        }

        private static HullSpec CloneHull(HullSpec hull)
            => new HullSpec(hull.maxWeight, hull.maxPower, hull.slots, hull.hullName);

        private static ShipModule[] CloneModules(ShipModule[] modules)
        {
            if (modules == null || modules.Length == 0) return Array.Empty<ShipModule>();
            var copy = new ShipModule[modules.Length];
            for (int i = 0; i < modules.Length; i++)
            {
                ShipModule m = modules[i];
                if (m != null) copy[i] = new ShipModule(m.type, m.weight, m.powerDraw, m.cost, m.rating, m.moduleName);
            }
            return copy;
        }
    }
}
