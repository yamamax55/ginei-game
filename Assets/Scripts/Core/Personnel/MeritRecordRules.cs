using UnityEngine;

namespace Ginei
{
    /// <summary>主人公個人の功績の種別（TKO-2・#2479）。会戦戦果・任務達成・採用された建白＝「自分の手柄」。</summary>
    public enum ExploitKind { 撃沈, 旗艦撃破, 防衛達成, 任務達成, 建白採用 }

    /// <summary>
    /// 一人の人物の功績台帳（TKO-2・#2479・純データ）。功績を<b>スカラの累積点</b>として持つ（無制限増加するリストを持たない＝
    /// 本質的に有界・PERF）。点の積分は <see cref="MeritRecordRules"/> が唯一の窓口（並行系を作らない）。
    /// 始皇帝の軍功授爵（爵位＝<see cref="MeritRankRules"/>）とは別軸＝こちらは<b>個人の階級昇進</b>を駆動する。
    /// </summary>
    public class MeritRecord
    {
        /// <summary>対象人物（<see cref="ICharacter.Id"/>）。</summary>
        public int personId;
        /// <summary>累積功績点（≥0）。</summary>
        public float points;
        /// <summary>記録した功績の件数（観測用）。</summary>
        public int exploitCount;
        /// <summary>これまでに功績で得た昇進段数（年功ベースに上乗せ済みの段数＝二重昇進を防ぐ）。</summary>
        public int meritPromotionsApplied;

        public MeritRecord() { }
        public MeritRecord(int personId) { this.personId = personId; }
    }

    /// <summary>
    /// 個人武勲→昇進の出世回路の純ロジック（TKO-2・太閤立志伝V・#2479・唯一の窓口）。主人公個人の功績（会戦戦果＝
    /// 撃沈/旗艦撃破/防衛達成・任務達成・採用された建白）を累積点に積み、<b>年功（席次）ベースの昇進に「手柄ぶん」を上乗せ</b>する
    /// ＝太閤立志伝Vの「自分の手柄で抜擢される」。昇進先 tier の解決は <see cref="RankSystem.NextRankTier"/>、席次↔実力の追い越しは
    /// <see cref="SeniorityRules"/> に委譲する（作り直さない＝additive・マクロ昇進を後退させない＝功績は<b>加点</b>であって置換でない）。
    /// 爵位（<see cref="MeritRankRules"/>）とは別軸。基準値非破壊・決定論・test-first。
    /// </summary>
    public static class MeritRecordRules
    {
        /// <summary>功績→点・昇進の調整値。</summary>
        public readonly struct MeritRecordParams
        {
            public readonly float weight撃沈;       // 1隻撃沈あたりの点
            public readonly float weight旗艦撃破;   // 敵旗艦撃破の点
            public readonly float weight防衛達成;   // 防衛戦達成の点
            public readonly float weight任務達成;   // 任務達成の点
            public readonly float weight建白採用;   // 建白が採用・執行された点
            public readonly float pointsPerPromotion; // この点ごとに1段の昇進加点
            public readonly float saturationPoints;   // 実力スコア(0..1)が1.0へ飽和する点

            public MeritRecordParams(float w撃沈, float w旗艦撃破, float w防衛達成, float w任務達成,
                float w建白採用, float pointsPerPromotion, float saturationPoints)
            {
                this.weight撃沈 = w撃沈;
                this.weight旗艦撃破 = w旗艦撃破;
                this.weight防衛達成 = w防衛達成;
                this.weight任務達成 = w任務達成;
                this.weight建白採用 = w建白採用;
                this.pointsPerPromotion = Mathf.Max(1f, pointsPerPromotion);
                this.saturationPoints = Mathf.Max(1f, saturationPoints);
            }

            /// <summary>既定＝撃沈1/旗艦撃破10/防衛8/任務5/建白4・50点ごとに1段昇進・200点で実力飽和。</summary>
            public static MeritRecordParams Default
                => new MeritRecordParams(1f, 10f, 8f, 5f, 4f, 50f, 200f);
        }

        /// <summary>功績種別の基準重み（<paramref name="prm"/>）。</summary>
        public static float BaseWeight(ExploitKind kind, MeritRecordParams prm)
        {
            switch (kind)
            {
                case ExploitKind.撃沈:     return prm.weight撃沈;
                case ExploitKind.旗艦撃破: return prm.weight旗艦撃破;
                case ExploitKind.防衛達成: return prm.weight防衛達成;
                case ExploitKind.任務達成: return prm.weight任務達成;
                default:                   return prm.weight建白採用; // 建白採用
            }
        }

        /// <summary>1件の功績の点＝基準重み×規模（撃沈数など。負の規模は0）。</summary>
        public static float PointsFor(ExploitKind kind, float magnitude, MeritRecordParams prm)
            => BaseWeight(kind, prm) * Mathf.Max(0f, magnitude);

        /// <summary>功績を台帳に積む（点・件数を加算）。<paramref name="magnitude"/> は規模（既定1＝1件ぶん）。</summary>
        public static void Record(MeritRecord rec, ExploitKind kind, float magnitude, MeritRecordParams prm)
        {
            if (rec == null) return;
            rec.points += PointsFor(kind, magnitude, prm);
            rec.exploitCount++;
        }

        /// <summary>既定パラメータで規模1の功績を積む。</summary>
        public static void Record(MeritRecord rec, ExploitKind kind)
            => Record(rec, kind, 1f, MeritRecordParams.Default);

        /// <summary>累積点を実力スコア 0..1 へ正規化（飽和）。<see cref="SeniorityRules.EffectiveStanding"/> の merit 項に渡す用。</summary>
        public static float MeritScore01(MeritRecord rec, MeritRecordParams prm)
            => rec == null ? 0f : Mathf.Clamp01(rec.points / prm.saturationPoints);

        /// <summary>累積点が表す昇進段数（年功ベースに上乗せできる総段数）。点÷pointsPerPromotion の整数部。</summary>
        public static int EarnedPromotionGrades(MeritRecord rec, MeritRecordParams prm)
            => rec == null ? 0 : Mathf.FloorToInt(Mathf.Max(0f, rec.points) / prm.pointsPerPromotion);

        /// <summary>まだ反映していない功績昇進があるか（<see cref="MeritRecord.meritPromotionsApplied"/> 比）。</summary>
        public static bool HasPendingPromotion(MeritRecord rec, MeritRecordParams prm)
            => rec != null && EarnedPromotionGrades(rec, prm) > rec.meritPromotionsApplied;

        /// <summary>
        /// 年功ベース tier <paramref name="baseTier"/> に、功績ぶんの昇進段数を上乗せした実効 tier を返す。
        /// 上乗せは <see cref="RankSystem.NextRankTier"/> を功績段数ぶん適用＝勢力の階級表に存在する tier だけを辿る
        /// （最高位で頭打ち＝過大功績の上限）。功績0なら baseTier をそのまま返す（年功のみ＝後方互換）。
        /// <b>マクロ昇進（baseTier）を後退させない加点</b>。台帳は変更しない（純関数）。
        /// </summary>
        public static int PromotedTier(FactionData faction, int baseTier, MeritRecord rec, MeritRecordParams prm)
        {
            int grades = EarnedPromotionGrades(rec, prm);
            int tier = baseTier;
            for (int i = 0; i < grades; i++)
            {
                int next = RankSystem.NextRankTier(faction, tier);
                if (next == tier) break; // 最高位＝頭打ち
                tier = next;
            }
            return tier;
        }

        /// <summary>
        /// 功績昇進を1段確定して台帳へ記録する（<see cref="MeritRecord.meritPromotionsApplied"/> を進める）。
        /// 反映できる段が残っていれば次の tier を返し true、無ければ現 tier で false。配線層（Game）が階級を書き換える起点。
        /// </summary>
        public static bool TryApplyPromotion(FactionData faction, int currentTier, MeritRecord rec,
            MeritRecordParams prm, out int newTier)
        {
            newTier = currentTier;
            if (!HasPendingPromotion(rec, prm)) return false;
            int next = RankSystem.NextRankTier(faction, currentTier);
            if (next == currentTier) return false; // 最高位
            rec.meritPromotionsApplied++;
            newTier = next;
            return true;
        }
    }
}
