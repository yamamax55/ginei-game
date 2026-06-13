using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 参謀本部基盤の純ロジック（米軍 continental staff 参考・史実準拠）。
    /// <b>部隊参謀</b>（艦隊長〜軍団長に付く幕僚＝G-staff）と<b>大本営参謀本部</b>（勢力の最高統帥幕僚＝J-staff）を扱い、
    /// 各セクション（人事/情報/作戦/兵站/計画/通信）の練度と参謀長の協調から、指揮官・部隊への実効ボーナスを算出する。
    /// 実効値パターン（基準値非破壊・倍率を返す）・test-first。データは <see cref="Staff"/>/<see cref="StaffRegistry"/>。
    /// </summary>
    public static class StaffRules
    {
        /// <summary>空席セクションの練度（下級参謀が穴埋めする想定の下限）。</summary>
        public const float EmptySectionEffectiveness = 0.4f;
        /// <summary>参謀長の協調影響幅（±15%）。</summary>
        public const float ChiefInfluence = 0.15f;

        /// <summary>
        /// 部隊参謀（幕僚団）が付く梯団か。史実準拠＝<b>艦隊長〜軍団長</b>の役職にのみ部隊参謀が就く。
        /// それ未満（分艦隊/戦隊）は副官止まり、それ超（軍/軍集団/宇宙艦隊）は大本営参謀本部の領分。
        /// </summary>
        public static bool RequiresFieldStaff(EchelonType echelon)
            => echelon == EchelonType.艦隊 || echelon == EchelonType.軍団;

        /// <summary>セクションが重視する能力（人事/兵站/通信=運営、情報/計画=情報、作戦=統率）。</summary>
        public static StaffStat RelevantStat(StaffSection section)
        {
            switch (section)
            {
                case StaffSection.作戦: return StaffStat.統率;
                case StaffSection.情報: return StaffStat.情報;
                case StaffSection.計画: return StaffStat.情報;
                default:               return StaffStat.運営; // 人事/兵站/通信
            }
        }

        /// <summary>担当参謀の能力（統率/運営/情報）から、そのセクションで効く能力値を選ぶ。</summary>
        public static float SectionScore(StaffSection section, float leadership, float operation, float intelligence)
        {
            switch (RelevantStat(section))
            {
                case StaffStat.統率: return leadership;
                case StaffStat.情報: return intelligence;
                default:             return operation;
            }
        }

        /// <summary>セクション練度(0..1)。officerStat 0..100、負（空席）は <see cref="EmptySectionEffectiveness"/>。</summary>
        public static float SectionEffectiveness(float officerStat)
            => officerStat < 0f ? EmptySectionEffectiveness : Mathf.Clamp01(officerStat / 100f);

        /// <summary>参謀長の統率による協調倍率（0.85〜1.15）。空席(負)は等倍寄りの下限。</summary>
        public static float ChiefFactor(float chiefLeadership)
        {
            if (chiefLeadership < 0f) return 1f - ChiefInfluence * 0.5f; // 参謀長空席＝協調やや低下
            float t = (Mathf.Clamp(chiefLeadership, 0f, 100f) - 50f) / 50f; // -1..+1
            return Mathf.Clamp(1f + t * ChiefInfluence, 1f - ChiefInfluence, 1f + ChiefInfluence);
        }

        /// <summary>幕僚団全体の質(0..~1.15)＝各セクション練度の平均 × 参謀長協調。</summary>
        public static float OverallQuality(IReadOnlyList<float> sectionEffectiveness, float chiefLeadership)
        {
            if (sectionEffectiveness == null || sectionEffectiveness.Count == 0) return EmptySectionEffectiveness;
            float sum = 0f;
            for (int i = 0; i < sectionEffectiveness.Count; i++) sum += Mathf.Clamp01(sectionEffectiveness[i]);
            return (sum / sectionEffectiveness.Count) * ChiefFactor(chiefLeadership);
        }

        /// <summary>セクション練度→実効ボーナス倍率（1.0基準＋練度×maxBonus）。実効値パターン。</summary>
        public static float DomainFactor(float sectionEffectiveness, float maxBonus)
            => 1f + Mathf.Clamp01(sectionEffectiveness) * Mathf.Max(0f, maxBonus);

        // 各セクションが効く先（既定スケール）。配線側はこれで指揮官/部隊の実効値を底上げする。
        public const float OpsBonusMax = 0.15f;   // 作戦(G3)→指揮/戦闘効率
        public const float IntelBonusMax = 0.20f; // 情報(G2)→索敵#2180
        public const float LogiBonusMax = 0.15f;  // 兵站(G4)→継戦#ORBAT-4
        public const float PersBonusMax = 0.10f;  // 人事(G1)→士気/補充

        /// <summary>作戦(G3)セクション練度→指揮/戦闘効率の倍率。</summary>
        public static float OperationsFactor(float opsEffectiveness) => DomainFactor(opsEffectiveness, OpsBonusMax);
        /// <summary>情報(G2)セクション練度→索敵の倍率。</summary>
        public static float IntelligenceFactor(float intelEffectiveness) => DomainFactor(intelEffectiveness, IntelBonusMax);
        /// <summary>兵站(G4)セクション練度→継戦の倍率。</summary>
        public static float LogisticsFactor(float logiEffectiveness) => DomainFactor(logiEffectiveness, LogiBonusMax);
        /// <summary>人事(G1)セクション練度→士気/補充の倍率。</summary>
        public static float PersonnelFactor(float persEffectiveness) => DomainFactor(persEffectiveness, PersBonusMax);
    }
}
