using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>人物どうしの二者関係の種別（TKO-3・#2480）。正の縁（親愛/恩義/同期/師弟/上官-部下）と負の縁（遺恨）。</summary>
    public enum RelationKind { 上官, 部下, 同期, 師弟, 恩義, 遺恨, 親愛 }

    /// <summary>一本の有向関係（from が to に対して抱く縁）。strength は縁の強さ 0..1（種別が符号＝遺恨は負の縁）。</summary>
    public class PersonRelation
    {
        public int fromId;
        public int toId;
        public RelationKind kind;
        public float strength; // 0..1

        public PersonRelation() { }
        public PersonRelation(int fromId, int toId, RelationKind kind, float strength)
        {
            this.fromId = fromId;
            this.toId = toId;
            this.kind = kind;
            this.strength = Mathf.Clamp01(strength);
        }
    }

    /// <summary>
    /// 人物二者関係のグラフ（TKO-3・#2480・純データ）。<b>一人あたりの関係数に上限</b>（<see cref="maxDegreePerPerson"/>）を設け、
    /// 超過時は最弱の縁から落とす＝無制限増加しない（PERF・終盤ラグ回避）。落とした件数は <see cref="droppedCount"/> で可視化
    /// （silent truncation 禁止）。集合演算・閥の敷設は <see cref="PersonRelationRules"/> が窓口。
    /// </summary>
    public class PersonRelationGraph
    {
        public readonly List<PersonRelation> relations = new List<PersonRelation>();
        /// <summary>一人（from）あたりの保持上限。超過は最弱から落とす。</summary>
        public int maxDegreePerPerson = 16;
        /// <summary>上限超過で落とした縁の累計（観測用）。</summary>
        public int droppedCount;

        /// <summary>有向の縁を取得（無ければ null）。</summary>
        public PersonRelation Get(int fromId, int toId, RelationKind kind)
        {
            for (int i = 0; i < relations.Count; i++)
            {
                var r = relations[i];
                if (r.fromId == fromId && r.toId == toId && r.kind == kind) return r;
            }
            return null;
        }

        /// <summary>
        /// 縁を張る/更新する（同 from→to×種別は強さを上書き）。張った後 from の本数が上限を超えたら最弱を落とす。
        /// 自己ループ（from==to）は無視。
        /// </summary>
        public void Set(int fromId, int toId, RelationKind kind, float strength)
        {
            if (fromId == toId) return;
            var existing = Get(fromId, toId, kind);
            if (existing != null) { existing.strength = Mathf.Clamp01(strength); return; }
            relations.Add(new PersonRelation(fromId, toId, kind, strength));
            PruneFrom(fromId);
        }

        /// <summary>from を起点に持つ縁。</summary>
        public List<PersonRelation> From(int fromId)
        {
            var list = new List<PersonRelation>();
            for (int i = 0; i < relations.Count; i++)
                if (relations[i].fromId == fromId) list.Add(relations[i]);
            return list;
        }

        /// <summary>from の縁が上限を超えていたら最弱から落とす。</summary>
        private void PruneFrom(int fromId)
        {
            int count = 0;
            for (int i = 0; i < relations.Count; i++) if (relations[i].fromId == fromId) count++;
            while (count > maxDegreePerPerson)
            {
                int weakestIdx = -1;
                float weakest = float.PositiveInfinity;
                for (int i = 0; i < relations.Count; i++)
                {
                    var r = relations[i];
                    if (r.fromId != fromId) continue;
                    if (r.strength < weakest) { weakest = r.strength; weakestIdx = i; }
                }
                if (weakestIdx < 0) break;
                relations.RemoveAt(weakestIdx);
                droppedCount++;
                count--;
            }
        }

        /// <summary>全消去（戦役リセット用）。</summary>
        public void Clear() { relations.Clear(); droppedCount = 0; }
    }

    /// <summary>
    /// 人物二者関係の純ロジック（TKO-3・太閤立志伝V・#2480・唯一の窓口）。「誰に仕え誰と縁を結ぶか」＝人脈/師弟/恩義/遺恨を
    /// <b>二者関係グラフ</b>で持ち、推挙・引き立て・遺恨の人事力学の土台にする。集団の結束（学閥/同期）は
    /// <see cref="CareerPipelineRules.CliqueBond"/> を流用して<b>二者の縁へ展開</b>する（派閥システム#113/学閥を作り直さない＝additive）。
    /// <b>スケーラビリティ規律（PERF）</b>＝全ペア走査（N²）を避けるため、敷設は呼び出し側が渡す<b>少数の集合</b>
    /// （主人公＋同期/周辺）に限り、グラフは一人あたりの本数を有界化する。決定論・test-first。
    /// </summary>
    public static class PersonRelationRules
    {
        /// <summary>関係種別の符号つき係数（正の縁＝＋／遺恨＝−）。純愛情の正味（net affinity）を出すのに使う。</summary>
        public static float KindSign(RelationKind kind) => kind == RelationKind.遺恨 ? -1f : 1f;

        /// <summary>
        /// 二者の正味の親密度（−1..+1）。from→to の全種別の縁を符号つきで合算しクランプ。
        /// 上官/部下/同期/師弟/恩義/親愛は積み増し、遺恨は差し引く＝引き立てと讒言の綱引き。
        /// </summary>
        public static float NetAffinity(PersonRelationGraph g, int fromId, int toId)
        {
            if (g == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < g.relations.Count; i++)
            {
                var r = g.relations[i];
                if (r.fromId == fromId && r.toId == toId) sum += KindSign(r.kind) * r.strength;
            }
            return Mathf.Clamp(sum, -1f, 1f);
        }

        /// <summary>
        /// 上官↔部下の双方向の縁を張る（指揮系統）。superior が上位 tier であること（<see cref="RankSystem.IsHigher"/>）。
        /// superior→subordinate に「部下」、subordinate→superior に「上官」を <paramref name="strength"/> で張る。
        /// </summary>
        public static void LinkCommand(PersonRelationGraph g, Person superior, Person subordinate, float strength)
        {
            if (g == null || superior == null || subordinate == null) return;
            if (!RankSystem.IsHigher(superior.rankTier, subordinate.rankTier)) return;
            g.Set(superior.id, subordinate.id, RelationKind.部下, strength);
            g.Set(subordinate.id, superior.id, RelationKind.上官, strength);
        }

        /// <summary>師→弟・弟→師の双方向の縁（恩義の源）。師が上位 tier 前提。</summary>
        public static void LinkMentorship(PersonRelationGraph g, Person mentor, Person disciple, float strength)
        {
            if (g == null || mentor == null || disciple == null) return;
            g.Set(mentor.id, disciple.id, RelationKind.師弟, strength);
            g.Set(disciple.id, mentor.id, RelationKind.恩義, strength);
        }

        /// <summary>
        /// 学閥/同期を二者の縁へ展開する：<paramref name="people"/> の各ペアについて <see cref="CareerPipelineRules.CliqueBond"/> が
        /// 正なら双方向に「同期」の縁を張る（強さ＝CliqueBond）。<b>N²回避</b>＝呼び出し側は主人公＋同期など<b>少数集合</b>を渡すこと
        /// （勢力ロスター全体を渡さない）。閥そのものは <see cref="CareerPipelineRules"/> が持つ（作り直さない）。
        /// </summary>
        public static void SeedFromCliques(PersonRelationGraph g, IReadOnlyList<Person> people,
            CareerPipelineRules.CliqueParams clique)
        {
            if (g == null || people == null) return;
            for (int i = 0; i < people.Count; i++)
            {
                Person a = people[i];
                if (a == null) continue;
                for (int j = i + 1; j < people.Count; j++)
                {
                    Person b = people[j];
                    if (b == null) continue;
                    float bond = CareerPipelineRules.CliqueBond(a, b, clique);
                    if (bond <= 0f) continue;
                    g.Set(a.id, b.id, RelationKind.同期, bond);
                    g.Set(b.id, a.id, RelationKind.同期, bond);
                }
            }
        }

        /// <summary>既定の閥パラメータで学閥/同期を敷設。</summary>
        public static void SeedFromCliques(PersonRelationGraph g, IReadOnlyList<Person> people)
            => SeedFromCliques(g, people, CareerPipelineRules.CliqueParams.Default);

        /// <summary>
        /// 引き立てバイアス（−1..+1）＝上官 superior が部下 subordinate を推挙したい度合い。
        /// 正味の親密度（<see cref="NetAffinity"/>）に、恩顧主義（<see cref="PatronageRules"/>）由来の縁故重みを混ぜず
        /// 「縁の強さ」だけを返す薄い窓口（恩顧の数値は PatronageRules、人脈は本モジュール＝二重実装しない）。
        /// 配属希望の内示（TKO-5）や登用が読む。
        /// </summary>
        public static float PromotionFavor(PersonRelationGraph g, int superiorId, int subordinateId)
            => NetAffinity(g, superiorId, subordinateId);
    }
}
