using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 空母打撃（艦載機/スパルタニアン）の調整係数。発艦数・アウトレンジ・サイクル・損耗・脆さを束ねる。
    /// </summary>
    public readonly struct CarrierStrikeParams
    {
        /// <summary>艦載機1機あたりの基礎打撃力（発艦数に乗る）。</summary>
        public readonly float damagePerFighter;
        /// <summary>敵の対空（ポイントディフェンス）が打撃を削る最大割合。</summary>
        public readonly float maxPointDefenseCut;
        /// <summary>敵の対空が艦載機を撃墜する最大割合（出撃あたり）。</summary>
        public readonly float maxAttritionRatio;
        /// <summary>1サイクル（発艦→攻撃→帰投→再発艦）の基準時間（秒）。</summary>
        public readonly float baseCycleSeconds;
        /// <summary>艦載機を出すほど空母本体が手薄になる脆さ係数。</summary>
        public readonly float vulnerabilityPerFighter;

        public CarrierStrikeParams(float damagePerFighter, float maxPointDefenseCut, float maxAttritionRatio,
                                   float baseCycleSeconds, float vulnerabilityPerFighter)
        {
            this.damagePerFighter = Mathf.Max(0f, damagePerFighter);
            this.maxPointDefenseCut = Mathf.Clamp01(maxPointDefenseCut);
            this.maxAttritionRatio = Mathf.Clamp01(maxAttritionRatio);
            this.baseCycleSeconds = Mathf.Max(0.01f, baseCycleSeconds);
            this.vulnerabilityPerFighter = Mathf.Max(0f, vulnerabilityPerFighter);
        }

        /// <summary>既定＝打撃2/機・対空削り上限80%・損耗上限60%・基準サイクル30秒・脆さ0.01/機。</summary>
        public static CarrierStrikeParams Default
            => new CarrierStrikeParams(2f, 0.8f, 0.6f, 30f, 0.01f);
    }

    /// <summary>
    /// 空母打撃の純ロジック（空母→艦載機を本艦射程外から発進させ一方的に叩く）。
    /// 担当は「発艦容量・アウトレンジ（射程外打撃）・サイクル時間・艦載機損耗・空母自身の脆さ」。
    /// 既存 `CarrierRules`（艦載機の打撃力×搭乗員技量と防空の綱引き＝1出撃の与ダメ/迎撃/損耗の核）とは
    /// 責務を分け、本クラスは打撃の「運用サイクルとアウトレンジの幾何」に特化して二重実装しない。
    /// `EscortShip`（Game 層の配下艦 MonoBehaviour）とも別物＝盤面非依存の plain 引数のみ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。実効値パターン（基準値非破壊）。
    /// </summary>
    public static class CarrierStrikeRules
    {
        /// <summary>一度に発艦できる艦載機数＝空母数×甲板容量（非負・整数）。</summary>
        public static int SortieCapacity(int carrierCount, int deckCapacity, CarrierStrikeParams p)
        {
            int carriers = Mathf.Max(0, carrierCount);
            int deck = Mathf.Max(0, deckCapacity);
            return carriers * deck;
        }

        public static int SortieCapacity(int carrierCount, int deckCapacity)
            => SortieCapacity(carrierCount, deckCapacity, CarrierStrikeParams.Default);

        /// <summary>
        /// 空母の射程外から届く打撃距離＝空母の安全離隔（standoff）＋艦載機の航続。
        /// 本艦は敵射程外（standoff）に居つつ、艦載機がさらに先（fighterRange）まで運ぶ。
        /// </summary>
        public static float StrikeReach(float carrierStandoffRange, float fighterRange, CarrierStrikeParams p)
        {
            return Mathf.Max(0f, carrierStandoffRange) + Mathf.Max(0f, fighterRange);
        }

        public static float StrikeReach(float carrierStandoffRange, float fighterRange)
            => StrikeReach(carrierStandoffRange, fighterRange, CarrierStrikeParams.Default);

        /// <summary>
        /// 艦載機の打撃量＝発艦数×単機打撃×技量×（1−対空削り）。
        /// 対空削り＝maxPointDefenseCut×def/(def+1)＝敵の対空が固いほど打撃が削られる。
        /// </summary>
        public static float StrikeDamage(int fightersLaunched, float fighterQuality, float enemyPointDefense, CarrierStrikeParams p)
        {
            int fighters = Mathf.Max(0, fightersLaunched);
            float quality = Mathf.Max(0f, fighterQuality);
            float def = Mathf.Max(0f, enemyPointDefense);
            float cut = p.maxPointDefenseCut * (def / (def + 1f));
            float raw = fighters * p.damagePerFighter * quality;
            return raw * (1f - cut);
        }

        public static float StrikeDamage(int fightersLaunched, float fighterQuality, float enemyPointDefense)
            => StrikeDamage(fightersLaunched, fighterQuality, enemyPointDefense, CarrierStrikeParams.Default);

        /// <summary>
        /// 出撃あたりの艦載機損耗数＝発艦数×損耗率。損耗率＝maxAttritionRatio×def/(def+1)＝
        /// 敵の対空が固いほど多く撃墜される（決定論・floor）。
        /// </summary>
        public static int FighterAttrition(int fightersLaunched, float enemyPointDefense, CarrierStrikeParams p)
        {
            int fighters = Mathf.Max(0, fightersLaunched);
            float def = Mathf.Max(0f, enemyPointDefense);
            float ratio = p.maxAttritionRatio * (def / (def + 1f));
            return Mathf.FloorToInt(fighters * ratio);
        }

        public static int FighterAttrition(int fightersLaunched, float enemyPointDefense)
            => FighterAttrition(fightersLaunched, enemyPointDefense, CarrierStrikeParams.Default);

        /// <summary>
        /// 発艦→攻撃→帰投→再発艦の1サイクル時間＝基準時間×甲板回転負荷／整備効率。
        /// 甲板容量が大きいほど捌く機数が増えて1サイクルが伸び、整備効率が高いほど短縮。
        /// </summary>
        public static float SortieCycleTime(int deckCapacity, float turnaroundEfficiency, CarrierStrikeParams p)
        {
            int deck = Mathf.Max(0, deckCapacity);
            float eff = Mathf.Max(0.01f, turnaroundEfficiency);
            float deckLoad = 1f + deck * 0.05f;
            return p.baseCycleSeconds * deckLoad / eff;
        }

        public static float SortieCycleTime(int deckCapacity, float turnaroundEfficiency)
            => SortieCycleTime(deckCapacity, turnaroundEfficiency, CarrierStrikeParams.Default);

        /// <summary>
        /// 艦載機を出した空母自身の脆さ（0..1）＝発艦数×脆さ係数を護衛で割り引く。
        /// 多くの機を出すほど手薄になり、護衛戦力で補える＝raw/(raw+escort)。
        /// </summary>
        public static float CarrierVulnerability(int fightersLaunched, float escortStrength, CarrierStrikeParams p)
        {
            int fighters = Mathf.Max(0, fightersLaunched);
            float escort = Mathf.Max(0f, escortStrength);
            float raw = fighters * p.vulnerabilityPerFighter;
            if (raw <= 0f) return 0f;
            return Mathf.Clamp01(raw / (raw + 1f + escort));
        }

        public static float CarrierVulnerability(int fightersLaunched, float escortStrength)
            => CarrierVulnerability(fightersLaunched, escortStrength, CarrierStrikeParams.Default);

        /// <summary>
        /// 射程外から一方的に叩くアウトレンジ優位＝自打撃距離−敵打撃距離（負なら相手が上）。
        /// </summary>
        public static float StandoffAdvantage(float strikeReach, float enemyReach)
        {
            return Mathf.Max(0f, strikeReach) - Mathf.Max(0f, enemyReach);
        }

        /// <summary>空母打撃が支配的か＝アウトレンジ優位が閾値を超える（射程外から安全に叩ける）。</summary>
        public static bool IsCarrierDominant(float standoffAdvantage, float threshold)
        {
            return standoffAdvantage > Mathf.Max(0f, threshold);
        }
    }
}
