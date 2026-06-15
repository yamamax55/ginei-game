using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>特殊作戦部隊（SOF）の選抜段階（米軍 SEAL の BUD/S を参考＝多段の苛烈な脱落選抜）。</summary>
    public enum SofStage { 基礎課程, 地獄週, 卒業課程 }

    /// <summary>SOF 選抜の候補（id＋選抜スコア）。</summary>
    public readonly struct SofCandidate
    {
        public readonly int id;
        public readonly float score;
        public SofCandidate(int id, float score) { this.id = id; this.score = score; }
    }

    /// <summary>
    /// 特殊作戦部隊（SOF）の純ロジック（史実：米海軍 SEAL の選抜 BUD/S 参考）。
    /// <b>選抜</b>＝多段（基礎課程→地獄週→卒業課程）の苛烈な脱落で少数のみ認定（卒業率 ~25%＝狭き門）。
    /// <b>出身者は提督としても能力が上昇</b>（常時の戦闘力底上げ）し、<b>艦隊単独の特殊作戦</b>
    /// （後方かく乱・周りこみ＝側背攻撃/包囲）で更に大きなボーナスを得る。実効値パターン・test-first。
    /// 選抜ファンネルは `MilitaryAcademyRules`/`ImperialExamRules` と同型。
    /// </summary>
    public static class SpecialForcesRules
    {
        public const float AdmiralCombatBonus = 0.05f; // 出身者の常時戦闘力上昇(+5%)
        public const float SpecialOpBonus = 0.20f;     // 特殊作戦（側背/包囲）時の追加(+20%)

        /// <summary>選抜スコア(0..100)。不屈＝統率/機動を重視し攻撃も少し（SEALの心身の頑健さ）。</summary>
        public static float SelectionScore(float leadership, float mobility, float attack)
        {
            float l = Mathf.Clamp(leadership, 0f, 100f);
            float m = Mathf.Clamp(mobility, 0f, 100f);
            float a = Mathf.Clamp(attack, 0f, 100f);
            return 0.4f * l + 0.4f * m + 0.2f * a;
        }

        /// <summary>段階別の通過率（累積で低い＝狭き門。基礎0.5×地獄週0.5×卒業0.9 ≒ 0.225）。</summary>
        public static float StagePassRate(SofStage stage)
        {
            switch (stage)
            {
                case SofStage.基礎課程: return 0.5f;
                case SofStage.地獄週:   return 0.5f; // 最大の脱落
                default:                return 0.9f; // 卒業課程
            }
        }

        /// <summary>その段階で通過する人数（残数×通過率の切り上げ・1人以上は残す）。</summary>
        public static int QuotaPassing(int n, SofStage stage)
        {
            if (n <= 0) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(n * StagePassRate(stage)));
        }

        /// <summary>
        /// 候補をスコア順に多段で篩い、認定者(SOF出身)のidを返す。各段で上位 QuotaPassing 人だけが残る。
        /// </summary>
        public static List<int> Funnel(IReadOnlyList<SofCandidate> candidates)
        {
            var survivors = new List<SofCandidate>();
            if (candidates != null) survivors.AddRange(candidates);
            survivors.Sort((x, y) => y.score.CompareTo(x.score)); // スコア降順

            foreach (SofStage stage in new[] { SofStage.基礎課程, SofStage.地獄週, SofStage.卒業課程 })
            {
                int keep = QuotaPassing(survivors.Count, stage);
                if (keep < survivors.Count) survivors.RemoveRange(keep, survivors.Count - keep);
            }

            var ids = new List<int>(survivors.Count);
            for (int i = 0; i < survivors.Count; i++) ids.Add(survivors[i].id);
            return ids;
        }

        /// <summary>提督としての常時戦闘力倍率（SOF出身は底上げ）。</summary>
        public static float AdmiralCombatFactor(bool isSof) => isSof ? 1f + AdmiralCombatBonus : 1f;

        /// <summary>特殊作戦（側背/包囲＝後方かく乱・周りこみ）時の与ダメ倍率（SOF出身のみ追加）。</summary>
        public static float SpecialOpFactor(bool isSof, bool isSpecialOp)
            => (isSof && isSpecialOp) ? 1f + SpecialOpBonus : 1f;
    }
}
