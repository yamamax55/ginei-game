using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦時経済・銃後の薄いオーケストレータ（合成のみ・純ロジック・test-first）。
    /// 既に各論で実装済みの未配線 Core を**呼び分けて一束に合成する**だけの窓口＝数式は持たず委譲する
    /// （二重実装しない）。状態は持たず引数で受ける＝決定論・全入力クランプ・基準値非破壊（実効値パターン）。
    /// 分担（委譲先）：
    /// - 軍需生産：<see cref="MobilizationRules"/>（動員プール＝労働を軍需へ回す）。
    /// - 銃後の支持：<see cref="HomeFrontRules"/>（銃後の士気）＋<see cref="MobilizationRules"/>（過大動員の厭戦#113）。
    /// - 封鎖の経済打撃：<see cref="BlockadeRules"/>（通過率→供給の締め上げ）。
    /// - 焦土の拒否価値：<see cref="ScorchedEarthRules"/>（撤退時に敵の利得を消す）。
    /// 軍産複合体（<see cref="WarIndustryRules"/>）・消耗交換（<see cref="AttritionExchangeRules"/>）は
    /// それぞれ専用窓口が担い、本クラスは戦時経済・銃後の基本フローのみを合成する。
    /// </summary>
    public static class WarEconomyTickRules
    {
        /// <summary>
        /// 軍需生産係数＝動員が労働を軍需へ回した量（<see cref="MobilizationRules.MobilizedPool"/> 委譲）。
        /// industryBase（平時の工業基盤＝労働力相当）に動員率を掛けて軍需へ振り向く生産量を返す
        /// ＝動員すれば軍需生産は増える（その裏で民需労働は痩せる＝<see cref="MobilizationRules.OutputFactor"/>）。
        /// industryBase≤0 や動員率0 なら0。決定論・非負。
        /// </summary>
        public static float WarProductionFactor(float mobilizationRate, float industryBase)
            => MobilizationRules.MobilizedPool(industryBase, mobilizationRate);

        /// <summary>
        /// 銃後の支持変化（−1..1）＝銃後の士気（厭戦の裏返し）から過大動員の厭戦#113 を差し引いた正味。
        /// 士気は <see cref="HomeFrontRules.HomefrontMorale"/> に「勝っていると信じられる度合い＝1−warWeariness」を渡して導く
        /// （厭戦が深いほど勝利を信じられず士気が落ちる）。動員の負荷は <see cref="MobilizationRules.SupportPenalty"/>
        /// （持続可能な動員率 threshold を超えた分だけ支持を削る）。
        /// 過大動員・厭戦が深いほど負に振れる。決定論・bounded。
        /// </summary>
        public static float HomeFrontSupportDelta(float mobilizationRate, float warWeariness)
        {
            float weariness = Mathf.Clamp01(warWeariness);
            // 勝てると信じる度合い＝1−厭戦。信念は満額(1)に置き、士気は勝利見込みで決まる。
            float morale = HomeFrontRules.HomefrontMorale(1f, 1f - weariness); // 0..1
            // 過大動員の厭戦（既定 threshold0.5/scale1.0）。
            float penalty = MobilizationRules.SupportPenalty(mobilizationRate, MobilizationThreshold, MobilizationScale);
            // 士気(0..1)を支持変化(-0.5..+0.5)へ中央0.5基準で写し、動員の厭戦を引く。
            return Mathf.Clamp((morale - 0.5f) - penalty, -1f, 1f);
        }

        /// <summary>持続可能な動員率（これ超で銃後の厭戦が立つ）。</summary>
        public const float MobilizationThreshold = 0.5f;
        /// <summary>過大動員の支持ペナルティ感度。</summary>
        public const float MobilizationScale = 1f;

        /// <summary>
        /// 封鎖の経済打撃（0..1）＝補給が締め上げられて届かなくなった割合×交易依存度
        /// （<see cref="BlockadeRules.Throughput"/> 委譲）。封鎖強度 blockadeIntensity(0..1) を
        /// 「封鎖側/護衛側の戦力比」に写し（強度1で完全封鎖比）、通過率の不足分(1−通過率)に交易依存度を掛ける
        /// ＝交易に頼る経済ほど封鎖は痛い。封鎖が緩い・交易に依存しないなら打撃は小さい。決定論・bounded。
        /// </summary>
        public static float BlockadeEconomicHit(float blockadeIntensity, float tradeDependence)
        {
            float intensity = Mathf.Clamp01(blockadeIntensity);
            float dependence = Mathf.Clamp01(tradeDependence);
            // 強度を戦力比へ：護衛1固定で、強度1のとき完全封鎖比(chokeRatio)に達するよう封鎖側戦力を作る。
            BlockadeParams bp = BlockadeParams.Default;
            float blockader = intensity * bp.chokeRatio;
            float throughput = BlockadeRules.Throughput(blockader, 1f, bp); // 0..1
            return Mathf.Clamp01((1f - throughput) * dependence);
        }

        /// <summary>
        /// 焦土の拒否価値（敵に渡らなくなる資産＝<see cref="ScorchedEarthRules.DeniedValue"/> 委譲）。
        /// 撤退時に焼却範囲 burnScope(0..1) ぶんの資産価値を徹底度で消す＝敵の現地調達を断つ拒否価値。
        /// 自国民の恨み・荒廃・損益は <see cref="ScorchedEarthRules"/> 各メソッドが別途扱う（ここは拒否価値のみ）。
        /// 決定論・非負。
        /// </summary>
        public static float ScorchedEarthDenial(float assetValue, float burnScope)
            => ScorchedEarthRules.DeniedValue(assetValue, burnScope);
    }
}
