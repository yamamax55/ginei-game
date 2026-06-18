using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>主人公の重大な岐路（TKO-7・#2484）。忠勤＝仕え続ける／下野＝軍を辞す／亡命＝敵勢力へ／独立＝旗揚げ。</summary>
    public enum CareerFork { 忠勤, 下野, 亡命, 独立 }

    /// <summary>
    /// 主人公個人の岐路の純ロジック（TKO-7・太閤立志伝V「忠臣にも梟雄にも」・#2484・唯一の窓口）。忠誠・不満（冷遇/主命失敗の蓄積）・
    /// 武勲（実力）・君主との関係・階級から、その人物に<b>どの岐路が開かれているか</b>を判定する＝「仕える／去る／寝返る／旗揚げ」の自由意志。
    /// <b>実行（寝返り/独立勢力生成/亡命受入）はマクロ窓口へ委譲</b>＝`BattleAllegianceRules`/`CivilWarRules`/`DiplomacyRules`/`LoyaltyRules`
    /// （本ルールは可否・推奨の判定のみ・並行系を作らない＝additive）。後段フェーズ＝まず「忠勤」路線（TKO-1〜11）が整ってから配線。
    /// 決定論・基準値非破壊・test-first。
    /// </summary>
    public static class CareerForkRules
    {
        /// <summary>岐路判定の調整値（閾値）。</summary>
        public readonly struct ForkParams
        {
            /// <summary>これ未満の不満なら岐路を考えない（忠勤）。</summary>
            public readonly float grievanceToConsider;
            /// <summary>これ以上の忠誠なら不満があっても忠勤に留まる。</summary>
            public readonly float loyaltyToStay;
            /// <summary>亡命が開く＝君主との関係がこれ以下（橋を焼いた）。</summary>
            public readonly float defectFavorCeiling;
            /// <summary>独立が開く武勲（実力スコア0..1）の下限。</summary>
            public readonly float independenceMeritFloor;
            /// <summary>独立が開く階級 tier の下限（自前の旗を立てる stature）。</summary>
            public readonly int independenceTierFloor;

            public ForkParams(float grievanceToConsider, float loyaltyToStay, float defectFavorCeiling,
                float independenceMeritFloor, int independenceTierFloor)
            {
                this.grievanceToConsider = Mathf.Clamp01(grievanceToConsider);
                this.loyaltyToStay = Mathf.Clamp01(loyaltyToStay);
                this.defectFavorCeiling = Mathf.Clamp(defectFavorCeiling, -1f, 1f);
                this.independenceMeritFloor = Mathf.Clamp01(independenceMeritFloor);
                this.independenceTierFloor = independenceTierFloor;
            }

            /// <summary>既定＝不満0.5で検討・忠誠0.6で留まる・関係−0.3以下で亡命・独立は武勲0.6かつ大将(8)以上。</summary>
            public static ForkParams Default => new ForkParams(0.5f, 0.6f, -0.3f, 0.6f, 8);
        }

        /// <summary>離反を考える状態か＝不満が閾値以上 かつ 忠誠が引き留め閾値未満。</summary>
        public static bool IsDisaffected(float loyalty, float grievance, ForkParams p)
            => Mathf.Clamp01(grievance) >= p.grievanceToConsider && Mathf.Clamp01(loyalty) < p.loyaltyToStay;

        /// <summary>
        /// その人物に開かれている岐路の一覧。忠勤は常に含む（留まる選択は常にある）。離反状態なら下野を加え、
        /// 武勲・階級が高ければ独立、君主との関係が冷え切っていれば亡命を加える。
        /// </summary>
        public static List<CareerFork> AvailableForks(float loyalty, float grievance, float merit01,
            float favorWithSovereign, int rankTier, ForkParams p)
        {
            var forks = new List<CareerFork> { CareerFork.忠勤 };
            if (!IsDisaffected(loyalty, grievance, p)) return forks;

            forks.Add(CareerFork.下野); // 不満ある者にはいつでも静かな退場がある
            if (Mathf.Clamp01(merit01) >= p.independenceMeritFloor && rankTier >= p.independenceTierFloor)
                forks.Add(CareerFork.独立); // 実力と stature があれば旗を立てられる
            if (Mathf.Clamp(favorWithSovereign, -1f, 1f) <= p.defectFavorCeiling)
                forks.Add(CareerFork.亡命); // 君主との橋を焼いた者は敵に迎えられうる
            return forks;
        }

        /// <summary>
        /// 最も起こりやすい岐路を推奨する。忠勤でなければ、独立（可能なら）＞亡命（橋を焼いていれば）＞下野 の優先で返す。
        /// 実行可否・受入は実際にはマクロ窓口（`CivilWarRules`/`DiplomacyRules`）が決める＝これは「本人の傾き」。
        /// </summary>
        public static CareerFork Recommend(float loyalty, float grievance, float merit01,
            float favorWithSovereign, int rankTier, ForkParams p)
        {
            var forks = AvailableForks(loyalty, grievance, merit01, favorWithSovereign, rankTier, p);
            if (forks.Contains(CareerFork.独立)) return CareerFork.独立;
            if (forks.Contains(CareerFork.亡命)) return CareerFork.亡命;
            if (forks.Contains(CareerFork.下野)) return CareerFork.下野;
            return CareerFork.忠勤;
        }
    }
}
