using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 徽章システムの純ロジック（#徽章・基盤）。人物の状態（階級#14・兵科・資格〔SOF#SOF/参謀#参謀本部〕）から
    /// <b>身に着ける徽章を導出</b>する＝「何をしたか（勲章#2263）」でなく「何者か・何ができるか」の標章。
    /// 効果は識別/表示が主で、資格の認知による小さな名誉（recognition）のみ（勲章の名誉と二重計上しないよう上限低め）。
    /// 実効値パターン・test-first。
    /// </summary>
    public static class BadgeRules
    {
        public const float RecognitionPerSkillBadge = 3f; // 技能章1つあたりの認知名誉
        public const float MaxRecognition = 10f;          // 認知名誉の上限（勲章名誉#2263 と別・低め）

        /// <summary>階級章（rank insignia）。既定ラダーの階級名で表示（faction 非依存の既定名）。</summary>
        public static Badge RankInsignia(int rankTier)
            => new Badge(BadgeKind.階級章, RankSystem.DefaultRankName(rankTier), rankTier);

        /// <summary>兵科章（branch）。軍人＝艦隊兵科、文民＝行政。</summary>
        public static Badge BranchInsignia(PersonRole role)
            => new Badge(BadgeKind.兵科章, role == PersonRole.軍人 ? "艦隊兵科章" : "行政章");

        /// <summary>技能章（資格徽章）の表示名。</summary>
        public static string SkillBadgeName(SkillBadge skill)
        {
            switch (skill)
            {
                case SkillBadge.特殊作戦: return "特殊作戦徽章";
                case SkillBadge.参謀:     return "参謀徽章";
                default:                  return "操艦徽章";
            }
        }

        /// <summary>技能章を作る。</summary>
        public static Badge SkillInsignia(SkillBadge skill) => new Badge(BadgeKind.技能章, SkillBadgeName(skill));

        /// <summary>
        /// 人物の状態から着用する徽章一式を導出する（階級章＋兵科章＋資格に応じた技能章）。
        /// 部隊章（unit）は所属で別途付与（<see cref="BadgeRegistry"/>）。
        /// </summary>
        public static List<Badge> Derive(int rankTier, PersonRole role, bool isSpecialForces, bool isStaff)
        {
            var badges = new List<Badge>();
            if (rankTier > 0) badges.Add(RankInsignia(rankTier)); // 階級章
            badges.Add(BranchInsignia(role));                      // 兵科章
            if (isSpecialForces) badges.Add(SkillInsignia(SkillBadge.特殊作戦)); // SOF#SOF
            if (isStaff) badges.Add(SkillInsignia(SkillBadge.参謀));            // 参謀#参謀本部
            return badges;
        }

        /// <summary>資格の認知による小さな名誉（技能章の数×係数・上限あり）。勲章名誉#2263 とは別・低め。</summary>
        public static float RecognitionPrestige(IReadOnlyList<Badge> badges)
        {
            if (badges == null) return 0f;
            int skill = 0;
            for (int i = 0; i < badges.Count; i++) if (badges[i].kind == BadgeKind.技能章) skill++;
            return Mathf.Min(skill * RecognitionPerSkillBadge, MaxRecognition);
        }
    }
}
