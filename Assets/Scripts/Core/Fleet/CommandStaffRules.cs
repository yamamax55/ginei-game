using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊指揮班（提督・副提督・参謀の3ネームド・#885）の純ロジック・唯一の窓口。配置の階級ゲート（CMD-2 #14）／
    /// 能力反映（CMD-3＝副提督の補佐＝二重指揮の冗長性で統率・防御↑、参謀の底上げ＝運営・情報↑・実効値パターン）／
    /// 継承（CMD-4＝提督喪失で副提督が昇格し新副提督を補充）を扱う。基準値（`AdmiralData` の能力）は非破壊。
    /// 3スロット未設定なら提督単独の従来動作（後方互換）。`AdmiralData.staffOfficers` は提督自身の幕僚として併存。test-first。
    /// </summary>
    public static class CommandStaffRules
    {
        /// <summary>指揮班の能力反映の調整値。</summary>
        public readonly struct CommandParams
        {
            /// <summary>副提督が司令系能力（統率・防御）へ寄与する割合。</summary>
            public readonly float viceAssistRatio;
            /// <summary>参謀が幕僚系能力（運営・情報）へ寄与する割合。</summary>
            public readonly float chiefAssistRatio;

            public CommandParams(float viceAssistRatio, float chiefAssistRatio)
            {
                this.viceAssistRatio = Mathf.Max(0f, viceAssistRatio);
                this.chiefAssistRatio = Mathf.Max(0f, chiefAssistRatio);
            }

            /// <summary>既定＝副提督0.25・参謀0.20。</summary>
            public static CommandParams Default => new CommandParams(0.25f, 0.20f);
        }

        // ===== CMD-2 配置・任免（階級ゲート #14） =====

        /// <summary>提督（司令）を配属できる階級か（requiredTier 以上。0＝ゲート無し）。</summary>
        public static bool CanAssignCommander(AdmiralData admiral, int requiredTier = 0)
            => admiral != null && admiral.rankTier >= requiredTier;

        /// <summary>副提督を配属できるか（提督が居て、提督以下の階級で、提督と別人）。</summary>
        public static bool CanAssignVice(FleetUnitData unit, AdmiralData vice)
        {
            if (unit == null || vice == null || unit.assignedAdmiral == null) return false;
            if (vice == unit.assignedAdmiral) return false;             // 提督との兼任不可
            return vice.rankTier <= unit.assignedAdmiral.rankTier;      // 副提督は提督以下
        }

        /// <summary>参謀を配属できるか（提督・副提督と別人。幕僚は階級ゲート緩め）。</summary>
        public static bool CanAssignChief(FleetUnitData unit, AdmiralData chief)
        {
            if (unit == null || chief == null) return false;
            return chief != unit.assignedAdmiral && chief != unit.viceCommander;
        }

        /// <summary>副提督を配属する（資格を満たさなければ false で現状維持）。</summary>
        public static bool AssignVice(FleetUnitData unit, AdmiralData vice)
        {
            if (!CanAssignVice(unit, vice)) return false;
            unit.viceCommander = vice;
            return true;
        }

        /// <summary>参謀を配属する（資格を満たさなければ false で現状維持）。</summary>
        public static bool AssignChief(FleetUnitData unit, AdmiralData chief)
        {
            if (!CanAssignChief(unit, chief)) return false;
            unit.chiefOfStaff = chief;
            return true;
        }

        // ===== CMD-3 能力反映（実効値パターン・基準値非破壊） =====

        private static int Combine(int baseValue, AdmiralData assistant, System.Func<AdmiralData, int> selector, float ratio)
        {
            if (assistant == null) return baseValue;
            int bonus = Mathf.RoundToInt(selector(assistant) * ratio);
            return Mathf.Clamp(baseValue + bonus, 0, AdmiralData.MaxStatValue);
        }

        /// <summary>実効統率＝提督の実効統率＋副提督の補佐（二重指揮の冗長性）。提督が居なければ0。</summary>
        public static int EffectiveLeadership(FleetUnitData unit, CommandParams prm)
        {
            if (unit == null || unit.assignedAdmiral == null) return 0;
            return Combine(unit.assignedAdmiral.EffectiveLeadership, unit.viceCommander, a => a.EffectiveLeadership, prm.viceAssistRatio);
        }

        /// <summary>実効防御＝提督の実効防御＋副提督の補佐（冗長性）。</summary>
        public static int EffectiveDefense(FleetUnitData unit, CommandParams prm)
        {
            if (unit == null || unit.assignedAdmiral == null) return 0;
            return Combine(unit.assignedAdmiral.EffectiveDefense, unit.viceCommander, a => a.EffectiveDefense, prm.viceAssistRatio);
        }

        /// <summary>実効運営＝提督の実効運営＋参謀の底上げ。</summary>
        public static int EffectiveOperation(FleetUnitData unit, CommandParams prm)
        {
            if (unit == null || unit.assignedAdmiral == null) return 0;
            return Combine(unit.assignedAdmiral.EffectiveOperation, unit.chiefOfStaff, a => a.EffectiveOperation, prm.chiefAssistRatio);
        }

        /// <summary>実効情報＝提督の実効情報＋参謀の底上げ。</summary>
        public static int EffectiveIntelligence(FleetUnitData unit, CommandParams prm)
        {
            if (unit == null || unit.assignedAdmiral == null) return 0;
            return Combine(unit.assignedAdmiral.EffectiveIntelligence, unit.chiefOfStaff, a => a.EffectiveIntelligence, prm.chiefAssistRatio);
        }

        // ===== CMD-4 継承（提督喪失→副提督が昇格） =====

        /// <summary>提督が空席かつ副提督が居る＝継承が必要か。</summary>
        public static bool NeedsSuccession(FleetUnitData unit)
            => unit != null && unit.assignedAdmiral == null && unit.viceCommander != null;

        /// <summary>副提督を提督へ昇格させる（提督席が空席のときのみ）。昇格したら true、副提督席は空く。</summary>
        public static bool PromoteVice(FleetUnitData unit)
        {
            if (unit == null || unit.assignedAdmiral != null || unit.viceCommander == null) return false;
            unit.assignedAdmiral = unit.viceCommander;
            unit.viceCommander = null;
            return true;
        }

        /// <summary>
        /// 継承の1手：提督が空席なら副提督を昇格させ、空いた副提督席を候補プールから補充する（適任不在は空席）。
        /// 後任の副提督は新提督以下の階級で、提督/参謀と別人。提督席が埋まったか（昇格 or 元から在席）を返す。
        /// </summary>
        public static bool Succeed(FleetUnitData unit, IEnumerable<AdmiralData> pool)
        {
            if (unit == null) return false;
            if (unit.assignedAdmiral == null)
            {
                if (!PromoteVice(unit)) return false; // 副提督も居なければ空席のまま
            }
            // 空いた副提督席を補充（適任不在なら空席を許容）
            if (unit.viceCommander == null && pool != null)
            {
                AdmiralData best = null;
                foreach (AdmiralData cand in pool)
                {
                    if (cand == null) continue;
                    if (cand == unit.assignedAdmiral || cand == unit.chiefOfStaff) continue;
                    if (cand.rankTier > unit.assignedAdmiral.rankTier) continue; // 提督以下
                    if (best == null || cand.rankTier > best.rankTier) best = cand;
                }
                if (best != null) unit.viceCommander = best;
            }
            return unit.assignedAdmiral != null;
        }
    }
}
