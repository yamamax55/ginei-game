using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍功授爵制＝始皇帝モデルの調整係数（#900-905・QIN）。
    /// 爵位閾値ラダー（戦功ポイント→tier）・授爵見込みの士気上限・制度畏怖・収奪崩壊速度をまとめる。
    /// 既定 <see cref="Default"/>。基準値は非破壊（実効値パターン）。
    /// </summary>
    public readonly struct MeritParams
    {
        /// <summary>爵位1級ぶんに必要な戦功ポイント（閾値ラダーの刻み）。</summary>
        public readonly float pointsPerTier;
        /// <summary>授爵できる最大tier（軍功授爵20級の簡略上限）。</summary>
        public readonly int maxTier;
        /// <summary>授爵見込みの士気ボーナス上限（QIN-2・制度駆動＝カリスマ非依存）。</summary>
        public readonly float maxMoraleBonus;
        /// <summary>士気ボーナスが上限に達する戦功ポイント（これ以上は頭打ち）。</summary>
        public readonly float moraleSaturation;
        /// <summary>制度の絶対化による威信補正の上限（QIN-3・畏怖）。</summary>
        public readonly float maxAwe;
        /// <summary>収奪的制度の短期出力ボーナス（QIN-5・法家の罠の最強期）。</summary>
        public readonly float extractivePeak;
        /// <summary>収奪崩壊の時定数（この時間で安定度を蝕み出力が剥落していく速さ）。</summary>
        public readonly float decayRate;

        public MeritParams(float pointsPerTier, int maxTier, float maxMoraleBonus, float moraleSaturation,
            float maxAwe, float extractivePeak, float decayRate)
        {
            this.pointsPerTier = pointsPerTier;
            this.maxTier = maxTier;
            this.maxMoraleBonus = maxMoraleBonus;
            this.moraleSaturation = moraleSaturation;
            this.maxAwe = maxAwe;
            this.extractivePeak = extractivePeak;
            this.decayRate = decayRate;
        }

        /// <summary>
        /// 既定係数：1級=10ポイント・上限20級・授爵士気上限0.3・士気飽和100pt・畏怖上限0.25・
        /// 短期出力+0.4・崩壊時定数20（戦略秒）。
        /// </summary>
        public static MeritParams Default => new MeritParams(10f, 20, 0.3f, 100f, 0.25f, 0.4f, 20f);
    }

    /// <summary>
    /// 軍功授爵制の数値解決（#900-905・始皇帝モデル・純ロジック test-first）。
    /// 成果（戦功ポイント）で爵位・士気を駆動するインセンティブ系を提供する：
    /// 授爵ラダー(<see cref="MeritToTier"/>)・授爵見込み士気(<see cref="IncentiveMoraleBonus"/>・QIN-2)・
    /// 制度畏怖(<see cref="InstitutionalAwe"/>・QIN-3)・収奪崩壊(<see cref="ExtractiveDecay"/>・QIN-5)・
    /// 授爵判定(<see cref="AwardRank"/>)。すべて決定論で基準値非破壊（実効値はローカル算出）。
    /// 爵位tierの序列比較は <see cref="RankSystem"/> に委譲（並行系を作らない）。
    /// </summary>
    public static class MeritRankRules
    {
        /// <summary>
        /// 戦功ポイントを爵位tierへ写像する（閾値ラダー）。負ポイントは0級、上限は <see cref="MeritParams.maxTier"/>。
        /// pointsPerTier 刻みで1級ずつ上がる（例：既定で 35pt→3級）。
        /// </summary>
        public static int MeritToTier(float meritPoints, MeritParams prm)
        {
            if (meritPoints <= 0f || prm.pointsPerTier <= 0f) return 0;
            int tier = Mathf.FloorToInt(meritPoints / prm.pointsPerTier);
            return Mathf.Clamp(tier, 0, prm.maxTier);
        }

        /// <summary>
        /// 授爵見込みによる士気ボーナス（QIN-2・制度駆動＝将のカリスマに依存しない）。
        /// 戦功ポイントに比例して 0..<see cref="MeritParams.maxMoraleBonus"/> へ飽和する（moraleSaturation で頭打ち）。
        /// </summary>
        public static float IncentiveMoraleBonus(float meritPoints, MeritParams prm)
        {
            if (meritPoints <= 0f || prm.moraleSaturation <= 0f) return 0f;
            float t = Mathf.Clamp01(meritPoints / prm.moraleSaturation);
            return t * prm.maxMoraleBonus;
        }

        /// <summary>
        /// 制度の絶対化（畏怖）による威信補正（QIN-3）。institutionAbsoluteness(0..1) に比例し
        /// 0..<see cref="MeritParams.maxAwe"/> を返す。制度が法として絶対化するほど威信が増す。
        /// </summary>
        public static float InstitutionalAwe(float institutionAbsoluteness, MeritParams prm)
        {
            float a = Mathf.Clamp01(institutionAbsoluteness);
            return a * prm.maxAwe;
        }

        /// <summary>
        /// 収奪的制度の短期最強・長期崩壊（QIN-5・法家の罠）。安定度(0..1)を基準に時間経過で蝕む係数を返す。
        /// 序盤は +<see cref="MeritParams.extractivePeak"/> の高出力、時間とともに指数減衰し、長期では安定度を割り込む。
        /// 返り値は実効出力係数（基準非破壊・ローカル算出）。timeUnits 0 で初期最強。
        /// </summary>
        public static float ExtractiveDecay(float stability, float timeUnits, MeritParams prm)
        {
            float s = Mathf.Clamp01(stability);
            float t = Mathf.Max(0f, timeUnits);
            // 短期ボーナスは exp 減衰で剥落し、長期では基準安定度を下回る（収奪の磨耗）。
            float decay = prm.decayRate > 0f ? Mathf.Exp(-t / prm.decayRate) : 0f;
            float bonus = prm.extractivePeak * decay;
            float erosion = (1f - decay) * s; // 経過ぶんだけ安定度を蝕む
            return Mathf.Max(0f, s + bonus - erosion);
        }

        /// <summary>
        /// 授爵で tier を上げるべきか（戦功ポイントが現tierの到達級を上回るなら昇爵）。
        /// 上限 <see cref="MeritParams.maxTier"/> に達していれば false。
        /// </summary>
        public static bool AwardRank(float meritPoints, int currentTier, MeritParams prm)
        {
            if (currentTier >= prm.maxTier) return false;
            return MeritToTier(meritPoints, prm) > currentTier;
        }
    }
}
