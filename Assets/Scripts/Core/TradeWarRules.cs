using UnityEngine;

namespace Ginei
{
    /// <summary>貿易戦争（関税・通商報復）の調整係数。</summary>
    public readonly struct TradeWarParams
    {
        /// <summary>関税打撃の最大幅（関税率×貿易量がこの割合まで相手を削る）。</summary>
        public readonly float maxTariffImpact;
        /// <summary>報復の応酬度の効き（双方関税の幾何平均に掛かる）。</summary>
        public readonly float escalationScale;
        /// <summary>自国産業の保護の効き（関税×輸入競合に掛かる）。</summary>
        public readonly float protectionScale;
        /// <summary>消費者負担（物価上昇）の効き（関税×輸入依存に掛かる）。</summary>
        public readonly float burdenScale;
        /// <summary>供給網の混乱の効き（報復×経済統合度に掛かる）。</summary>
        public readonly float disruptionScale;
        /// <summary>双方の経済損失の効き（混乱×貿易量に掛かる）。</summary>
        public readonly float lossScale;
        /// <summary>デカップリングの効き（報復×代替供給先に掛かる）。</summary>
        public readonly float decouplingScale;

        public TradeWarParams(float maxTariffImpact, float escalationScale, float protectionScale,
            float burdenScale, float disruptionScale, float lossScale, float decouplingScale)
        {
            this.maxTariffImpact = Mathf.Clamp01(maxTariffImpact);
            this.escalationScale = Mathf.Clamp01(escalationScale);
            this.protectionScale = Mathf.Clamp01(protectionScale);
            this.burdenScale = Mathf.Clamp01(burdenScale);
            this.disruptionScale = Mathf.Clamp01(disruptionScale);
            this.lossScale = Mathf.Clamp01(lossScale);
            this.decouplingScale = Mathf.Clamp01(decouplingScale);
        }

        /// <summary>既定＝打撃幅0.5・応酬1.0・保護0.6・負担0.7・混乱0.8・損失0.9・分断1.0。</summary>
        public static TradeWarParams Default => new TradeWarParams(0.5f, 1f, 0.6f, 0.7f, 0.8f, 0.9f, 1f);
    }

    /// <summary>
    /// 貿易戦争（関税・輸出規制の応酬）の純ロジック。関税は相手への打撃となるが、報復関税が連鎖して
    /// 応酬がエスカレートし、自国産業の保護（恩恵）と消費者の負担（物価上昇＝痛み）を同時にもたらす。
    /// 報復が深まるとサプライチェーンが混乱し、貿易量に比例して双方が経済損失を被る（貿易戦争に勝者なし）。
    /// 代替供給先が育つとデカップリング（経済分断）が進む。正味（保護−負担−損失）は -1..1。
    /// 経済制裁（<see cref="SanctionsRules"/>＝産出締め）とは別系統。倍率は実効値パターンで掛けて使う
    /// （基準非破壊）。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TradeWarRules
    {
        /// <summary>
        /// 関税の打撃（0..maxTariffImpact）＝関税率(0..1)×貿易量(0..1)×最大幅。
        /// 太い貿易に高関税を掛けるほど相手を削る。
        /// </summary>
        public static float TariffImpact(float tariffRate, float tradeVolume, TradeWarParams p)
        {
            return Mathf.Clamp01(tariffRate) * Mathf.Clamp01(tradeVolume) * p.maxTariffImpact;
        }

        public static float TariffImpact(float tariffRate, float tradeVolume)
            => TariffImpact(tariffRate, tradeVolume, TradeWarParams.Default);

        /// <summary>
        /// 報復の応酬度（0..1）＝双方の関税の幾何平均×係数。どちらかが関税0なら応酬は起きない
        /// （片方だけの関税は「戦争」にならない）。両者が高関税で噛み合うほど跳ね上がる。
        /// </summary>
        public static float RetaliationEscalation(float ownTariff, float opponentTariff, TradeWarParams p)
        {
            float a = Mathf.Clamp01(ownTariff);
            float b = Mathf.Clamp01(opponentTariff);
            float mutual = Mathf.Sqrt(a * b); // 幾何平均＝双方が掛け合わないと立ち上がらない
            return Mathf.Clamp01(mutual * p.escalationScale);
        }

        public static float RetaliationEscalation(float ownTariff, float opponentTariff)
            => RetaliationEscalation(ownTariff, opponentTariff, TradeWarParams.Default);

        /// <summary>
        /// 自国産業の保護（0..1）＝関税率×輸入競合(0..1)×係数。輸入品と競合する国内産業ほど
        /// 関税の恩恵が大きい（関税障壁の正の側面）。
        /// </summary>
        public static float DomesticProtection(float tariffRate, float importCompetition, TradeWarParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(tariffRate) * Mathf.Clamp01(importCompetition) * p.protectionScale);
        }

        public static float DomesticProtection(float tariffRate, float importCompetition)
            => DomesticProtection(tariffRate, importCompetition, TradeWarParams.Default);

        /// <summary>
        /// 消費者の負担＝物価上昇（0..1）＝関税率×輸入依存(0..1)×係数。輸入に依存しているほど
        /// 関税が消費者へ転嫁され物価が上がる（関税障壁の負の側面）。
        /// </summary>
        public static float ConsumerBurden(float tariffRate, float importDependency, TradeWarParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(tariffRate) * Mathf.Clamp01(importDependency) * p.burdenScale);
        }

        public static float ConsumerBurden(float tariffRate, float importDependency)
            => ConsumerBurden(tariffRate, importDependency, TradeWarParams.Default);

        /// <summary>
        /// 供給網の混乱（0..1）＝報復応酬度×経済統合度(0..1)×係数。互いに深く統合した経済ほど
        /// 報復の応酬がサプライチェーンを大きく断ち切る（相互依存の罠）。
        /// </summary>
        public static float SupplyChainDisruption(float retaliationEscalation, float integration, TradeWarParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(retaliationEscalation) * Mathf.Clamp01(integration) * p.disruptionScale);
        }

        public static float SupplyChainDisruption(float retaliationEscalation, float integration)
            => SupplyChainDisruption(retaliationEscalation, integration, TradeWarParams.Default);

        /// <summary>
        /// 双方の経済損失（0..1）＝供給網の混乱×貿易量(0..1)×係数。貿易が太いほど混乱の被害は
        /// 双方に大きい（貿易戦争に勝者なし＝両損）。
        /// </summary>
        public static float MutualLoss(float supplyChainDisruption, float tradeVolume, TradeWarParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(supplyChainDisruption) * Mathf.Clamp01(tradeVolume) * p.lossScale);
        }

        public static float MutualLoss(float supplyChainDisruption, float tradeVolume)
            => MutualLoss(supplyChainDisruption, tradeVolume, TradeWarParams.Default);

        /// <summary>
        /// デカップリング＝経済分断（0..1）＝報復応酬度×代替供給先(0..1)×係数。報復が長引き、かつ
        /// 代替の供給先が確保できるほど、相手から切り離して別経済圏へ移る。
        /// </summary>
        public static float Decoupling(float retaliationEscalation, float alternativeSuppliers, TradeWarParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(retaliationEscalation) * Mathf.Clamp01(alternativeSuppliers) * p.decouplingScale);
        }

        public static float Decoupling(float retaliationEscalation, float alternativeSuppliers)
            => Decoupling(retaliationEscalation, alternativeSuppliers, TradeWarParams.Default);

        /// <summary>
        /// 貿易戦争の正味（-1..1）＝自国産業の保護（恩恵）−消費者の負担−双方の損失（痛み）。
        /// 正なら国内的に得、負なら自滅。各入力は 0..1 にクランプして使う。
        /// </summary>
        public static float NetTradeWarValue(float domesticProtection, float consumerBurden, float mutualLoss, TradeWarParams p)
        {
            float gain = Mathf.Clamp01(domesticProtection);
            float pain = Mathf.Clamp01(consumerBurden) + Mathf.Clamp01(mutualLoss);
            return Mathf.Clamp(gain - pain, -1f, 1f);
        }

        public static float NetTradeWarValue(float domesticProtection, float consumerBurden, float mutualLoss)
            => NetTradeWarValue(domesticProtection, consumerBurden, mutualLoss, TradeWarParams.Default);
    }
}
