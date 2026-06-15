using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 席次主義 vs 実力主義の純ロジック（LIFE-5/6 共有 #155/#156）。軍の<b>ハンモックナンバー</b>（卒業席次）と
    /// 文官の<b>合格順位</b>は同型＝「席次が初期序列を決めるが、実務 merit が席次を追い越せる。追い越しやすさは政体で変わる」を
    /// 単一の数式で表す（旧海軍の席次主義 ↔ 実力主義の緊張＝群像の燃料）。基準値非破壊（実効値パターン）。test-first。
    /// </summary>
    public static class SeniorityRules
    {
        /// <summary>初期 tier 算出の調整値（席次→初期序列）。</summary>
        public readonly struct SeniorityParams
        {
            public readonly int topTier;    // 首席（rank1）の初期 tier
            public readonly int groupSize;  // この席次ぶんで tier が1段下がる
            public readonly int floorTier;  // 下限 tier

            public SeniorityParams(int topTier, int groupSize, int floorTier)
            {
                this.topTier = topTier;
                this.groupSize = Mathf.Max(1, groupSize);
                this.floorTier = floorTier;
            }

            /// <summary>既定＝首席tier6・20席ごとに−1・下限tier5。</summary>
            public static SeniorityParams Default => new SeniorityParams(6, 20, 5);
        }

        /// <summary>席次（1=首席。小さいほど上位）から初期 tier を出す。下限でクランプ。</summary>
        public static int InitialTier(int rank, SeniorityParams p)
        {
            if (rank < 1) rank = 1;
            int tier = p.topTier - (rank - 1) / p.groupSize;
            return Mathf.Max(p.floorTier, tier);
        }

        /// <summary>政体（文民統制型 #117/#145）ごとの席次の固さ（0..1。高いほど席次が実力を上書きされにくい）。</summary>
        public static float PoliticalRigidity(CivilianControlType control)
        {
            switch (control)
            {
                case CivilianControlType.君主統帥: return 0.9f; // 王党派＝家柄＋席次が固い
                case CivilianControlType.文民統制: return 0.6f; // 民主派＝席次＋実力の折衷
                case CivilianControlType.党軍:     return 0.3f; // 共産＝党性が席次を上書き
                case CivilianControlType.軍部優位: return 0.2f; // 軍閥＝実力が席次を上書き
                case CivilianControlType.未分化:   return 0.2f;
                default: return 0.6f;
            }
        }

        /// <summary>
        /// 実効的な序列スコア＝席次由来(1/rank) と 実務 merit(0..1) を <paramref name="rigidity"/> で混ぜる。
        /// rigidity 高＝席次が支配／低＝merit が席次を追い越せる。大きいほど上位。
        /// </summary>
        public static float EffectiveStanding(int rank, float merit, float rigidity)
        {
            if (rank < 1) rank = 1;
            float seniorityScore = 1f / rank;       // 首席=1.0
            float r = Mathf.Clamp01(rigidity);
            float m = Mathf.Clamp01(merit);
            return r * seniorityScore + (1f - r) * m;
        }

        /// <summary>
        /// 下位席次の俊英（juniorRank・高merit）が上位席次の凡才（seniorRank・低merit）を実効的に追い越すか。
        /// 政体の <paramref name="rigidity"/> が緩いほど追い越しやすい（軍閥/共産）／固いほど起きにくい（王党派）。
        /// </summary>
        public static bool MeritOvertakes(int seniorRank, float seniorMerit, int juniorRank, float juniorMerit, float rigidity)
        {
            float senior = EffectiveStanding(seniorRank, seniorMerit, rigidity);
            float junior = EffectiveStanding(juniorRank, juniorMerit, rigidity);
            return junior > senior;
        }
    }
}
