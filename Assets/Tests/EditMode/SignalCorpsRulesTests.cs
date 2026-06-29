using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 通信兵科を固定する：回線確立（機材×技量の幾何平均）、帯域容量、距離×中継の遅延、
    /// 通信量による輻輳、実効スループット、代替手段、信頼性、総合的な通信の質。境界を担保。
    /// </summary>
    public class SignalCorpsRulesTests
    {
        private static readonly SignalCorpsParams P = SignalCorpsParams.Default;
        // 最大帯域100・遅延0.01/距離・0.05/ホップ・輻輳感度1・代替寄与0.4・正規化基準100

        [Test]
        public void LinkEstablishment_GeometricMean()
        {
            Assert.AreEqual(1f, SignalCorpsRules.LinkEstablishment(1f, 1f, P), 1e-3f);       // 共に満点
            Assert.AreEqual(0.25f, SignalCorpsRules.LinkEstablishment(0.25f, 0.25f, P), 1e-3f); // sqrt(0.0625)
            Assert.AreEqual(0f, SignalCorpsRules.LinkEstablishment(1f, 0f, P), 1e-3f);       // 技量0＝確立せず
            // clamp：入力超過は満点止まり
            Assert.AreEqual(1f, SignalCorpsRules.LinkEstablishment(5f, 5f, P), 1e-3f);
        }

        [Test]
        public void Bandwidth_LinkTimesInfra()
        {
            Assert.AreEqual(100f, SignalCorpsRules.Bandwidth(1f, 1f, P), 1e-4f);   // 太い回線×整備
            Assert.AreEqual(25f, SignalCorpsRules.Bandwidth(0.5f, 0.5f, P), 1e-4f); // 0.5×0.5×100
            Assert.AreEqual(0f, SignalCorpsRules.Bandwidth(1f, 0f, P), 1e-4f);     // インフラ皆無
        }

        [Test]
        public void Latency_DistancePlusHops()
        {
            Assert.AreEqual(0.2f, SignalCorpsRules.Latency(10f, 2f, P), 1e-4f); // 0.1+0.1
            Assert.AreEqual(0f, SignalCorpsRules.Latency(0f, 0f, P), 1e-4f);    // 直結・至近＝即達
            Assert.AreEqual(1f, SignalCorpsRules.Latency(1000f, 0f, P), 1e-4f); // 遠方＝上限クランプ
        }

        [Test]
        public void CongestionFactor_OnlyAboveBandwidth()
        {
            Assert.AreEqual(0f, SignalCorpsRules.CongestionFactor(50f, 100f, P), 1e-4f);  // 帯域内＝輻輳なし
            Assert.AreEqual(0.5f, SignalCorpsRules.CongestionFactor(150f, 100f, P), 1e-4f); // 1.5倍＝0.5
            Assert.AreEqual(1f, SignalCorpsRules.CongestionFactor(200f, 100f, P), 1e-4f); // 2倍＝完全輻輳
            Assert.AreEqual(1f, SignalCorpsRules.CongestionFactor(10f, 0f, P), 1e-4f);    // 帯域ゼロ＝詰まる
        }

        [Test]
        public void ThroughputEffective_BandwidthMinusCongestion()
        {
            Assert.AreEqual(50f, SignalCorpsRules.ThroughputEffective(100f, 0.5f, P), 1e-4f);
            Assert.AreEqual(100f, SignalCorpsRules.ThroughputEffective(100f, 0f, P), 1e-4f); // 無輻輳＝満額
            Assert.AreEqual(0f, SignalCorpsRules.ThroughputEffective(100f, 1f, P), 1e-4f);   // 完全輻輳＝停止
        }

        [Test]
        public void FallbackCapability_GeometricMean()
        {
            Assert.AreEqual(1f, SignalCorpsRules.FallbackCapability(1f, 1f, P), 1e-3f);
            Assert.AreEqual(0.72f, SignalCorpsRules.FallbackCapability(0.64f, 0.81f, P), 1e-3f); // sqrt(0.5184)
            Assert.AreEqual(0f, SignalCorpsRules.FallbackCapability(0.25f, 0f, P), 1e-3f); // 即応皆無
        }

        [Test]
        public void Reliability_FallbackPropsUpLink()
        {
            Assert.AreEqual(0.6f, SignalCorpsRules.Reliability(1f, 0f, P), 1e-4f); // 1×0.6
            Assert.AreEqual(0.4f, SignalCorpsRules.Reliability(0f, 1f, P), 1e-4f); // 代替のみで下支え
            Assert.AreEqual(1f, SignalCorpsRules.Reliability(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void CommunicationQuality_ProductOfThreeFactors()
        {
            // thr=50/100=0.5 ×(1-0.2)=0.8 ×rel0.6 = 0.24
            Assert.AreEqual(0.24f, SignalCorpsRules.CommunicationQuality(50f, 0.2f, 0.6f, P), 1e-4f);
            // 遅延満タンは質を殺す
            Assert.AreEqual(0f, SignalCorpsRules.CommunicationQuality(100f, 1f, 1f, P), 1e-4f);
            // 三拍子満点
            Assert.AreEqual(1f, SignalCorpsRules.CommunicationQuality(100f, 0f, 1f, P), 1e-4f);
        }

        [Test]
        public void Story_FrontlineCommsUnderLoad()
        {
            // 前線：機材良好(0.9)・熟練兵(0.9)で回線確立、インフラ7割で帯域を引く
            float link = SignalCorpsRules.LinkEstablishment(0.9f, 0.9f, P);   // ≈0.9
            float bw = SignalCorpsRules.Bandwidth(link, 0.7f, P);             // ≈63
            // 戦況逼迫で通信量が帯域の倍＝完全輻輳→実効スループット0
            float cong = SignalCorpsRules.CongestionFactor(bw * 2f, bw, P);   // ＝1
            float thr = SignalCorpsRules.ThroughputEffective(bw, cong, P);    // ＝0
            Assert.AreEqual(0f, thr, 1e-3f);
            // 予備系統(0.8)と機転(0.8)で代替手段→信頼性は下支えされる
            float fallback = SignalCorpsRules.FallbackCapability(0.8f, 0.8f, P); // 0.8
            float rel = SignalCorpsRules.Reliability(link, fallback, P);          // ≈0.84
            Assert.Greater(rel, 0.8f);
            // だがスループット0なので総合品質は0＝信頼できても何も流れない
            float lat = SignalCorpsRules.Latency(20f, 3f, P);                     // 0.2+0.15=0.35
            float quality = SignalCorpsRules.CommunicationQuality(thr, lat, rel, P);
            Assert.AreEqual(0f, quality, 1e-4f);

            // 輻輳が引けば（通信量=帯域以内）品質が立ち上がる
            float cong2 = SignalCorpsRules.CongestionFactor(bw * 0.5f, bw, P);    // 0
            float thr2 = SignalCorpsRules.ThroughputEffective(bw, cong2, P);      // ≈63
            float quality2 = SignalCorpsRules.CommunicationQuality(thr2, lat, rel, P);
            Assert.Greater(quality2, 0f);
        }
    }
}
