using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 階層的情報歪曲（#1383・SHP-4『失敗の本質』型）の純ロジック検証。
    /// 既定 InformationDistortionParams（基礎圧縮0.2/責任回避重み0.5/累積速度0.1/破裂閾0.6/妄想閾0.5）で
    /// 期待値を固定。悪報の圧縮・階層ごとの歪み・認識と現実の乖離・歪みの累積・楽観バイアス・
    /// 認識ギャップの破裂・正直な報告チャネル・幻想の指揮判定を担保。
    /// </summary>
    public class InformationDistortionRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>悪報は階層を上るほど圧縮され、責任回避が強いほど薄まる（階層0なら無傷）。</summary>
        [Test]
        public void BadNewsCompression_階層を上るほど薄まる()
        {
            // 階層0＝削られず無傷でトップへ届く。
            Assert.AreEqual(0.8f, InformationDistortionRules.BadNewsCompression(0.8f, 0f, 0.4f), Eps);
            // 階層深さ1（≈5階層）＝perLevelLoss=0.2+0.4*0.5=0.4 → 残存0.6^5=0.07776 → 0.8*0.07776。
            Assert.AreEqual(0.062208f, InformationDistortionRules.BadNewsCompression(0.8f, 1f, 0.4f), Eps);
            // 責任回避が強いほど（圧縮が増し）トップに残る悪報が少ない。
            float low = InformationDistortionRules.BadNewsCompression(0.8f, 1f, 0.1f);
            float high = InformationDistortionRules.BadNewsCompression(0.8f, 1f, 0.9f);
            Assert.Greater(low, high);
        }

        /// <summary>1階層ごとの歪み＝否定的な報告ほど・上申圧力が強いほど大きく楽観へ歪む。</summary>
        [Test]
        public void DistortionPerLevel_悪報と上申圧力の積()
        {
            Assert.AreEqual(0.5f, InformationDistortionRules.DistortionPerLevel(1f, 0.5f), Eps);
            // 良い報告（否定性0）は歪まない＝悪報だけが選択的に書き換わる。
            Assert.AreEqual(0f, InformationDistortionRules.DistortionPerLevel(0f, 1f), Eps);
            // 圧力ゼロなら歪まない。
            Assert.AreEqual(0f, InformationDistortionRules.DistortionPerLevel(1f, 0f), Eps);
        }

        /// <summary>累積歪みのぶんだけトップの認識が現実から楽観方向へ乖離する。</summary>
        [Test]
        public void PerceivedVsReality_歪みで現実から離れる()
        {
            // 現実0.3を歪み0.8で楽観へ＝0.3+0.7*0.8=0.86 → 乖離0.86-0.3=0.56。
            Assert.AreEqual(0.56f, InformationDistortionRules.PerceivedVsReality(0.3f, 0.8f), Eps);
            // 歪みゼロなら乖離ゼロ（認識＝現実）。
            Assert.AreEqual(0f, InformationDistortionRules.PerceivedVsReality(0.3f, 0f), Eps);
            // 歪みが増すほど乖離が広がる。
            Assert.Greater(InformationDistortionRules.PerceivedVsReality(0.3f, 0.9f),
                           InformationDistortionRules.PerceivedVsReality(0.3f, 0.4f));
        }

        /// <summary>報告が階層を上るうちに歪みが累積し1へ漸近する。</summary>
        [Test]
        public void CumulativeDistortionTick_階層を上るうちに膨らむ()
        {
            // step=1*0.1*1=0.1 → MoveTowards(0,1,0.1)=0.1。
            Assert.AreEqual(0.1f, InformationDistortionRules.CumulativeDistortionTick(0f, 1f, 1f), Eps);
            // 上った階層がゼロなら累積しない。
            Assert.AreEqual(0.3f, InformationDistortionRules.CumulativeDistortionTick(0.3f, 0f, 1f), Eps);
            // 反復で1へ漸近する。
            float d = 0f;
            for (int i = 0; i < 50; i++) d = InformationDistortionRules.CumulativeDistortionTick(d, 1f, 1f);
            Assert.AreEqual(1f, d, Eps);
        }

        /// <summary>楽観バイアス＝責任回避と面子はいずれも歪め、両立で最大。</summary>
        [Test]
        public void OptimismBias_責任回避と面子で楽観へ()
        {
            // 1-(1-0.6)(1-0.5)=1-0.2=0.8。
            Assert.AreEqual(0.8f, InformationDistortionRules.OptimismBias(0.6f, 0.5f), Eps);
            // どちらもゼロなら歪まない。
            Assert.AreEqual(0f, InformationDistortionRules.OptimismBias(0f, 0f), Eps);
            // 両方最大で1（完全に楽観へ）。
            Assert.AreEqual(1f, InformationDistortionRules.OptimismBias(1f, 1f), Eps);
        }

        /// <summary>乖離が閾を超え突然の露見が表面化させると認識ギャップが破裂する。</summary>
        [Test]
        public void PerceptionGapRupture_乖離と露見で破裂()
        {
            // 既定破裂閾0.6＝乖離0.6以上かつ露見が(1-乖離)以上で破裂。
            // 乖離0.7・露見0.5 → 0.7>=0.6 かつ 0.5>=(1-0.7=0.3) → true。
            Assert.IsTrue(InformationDistortionRules.PerceptionGapRupture(0.7f, 0.5f));
            // 乖離が閾未満なら破裂しない（まだ現実が露呈しない）。
            Assert.IsFalse(InformationDistortionRules.PerceptionGapRupture(0.4f, 1f));
            // 乖離は十分でも露見が小さければ表面化せず破裂しない。
            Assert.IsFalse(InformationDistortionRules.PerceptionGapRupture(0.65f, 0.2f));
        }

        /// <summary>正直な報告チャネル＝心理的安全と悪報への寛容の両方が要る。</summary>
        [Test]
        public void HonestReportingChannel_悪報を罰しない文化()
        {
            // 0.8*0.5=0.4。
            Assert.AreEqual(0.4f, InformationDistortionRules.HonestReportingChannel(0.8f, 0.5f), Eps);
            // 寛容がゼロ（悪報を罰する）なら真実は通らない。
            Assert.AreEqual(0f, InformationDistortionRules.HonestReportingChannel(1f, 0f), Eps);
            // 両者が高いほど真実が届く＝悪報を罰しない組織ほど歪まない。
            Assert.Greater(InformationDistortionRules.HonestReportingChannel(0.9f, 0.9f),
                           InformationDistortionRules.HonestReportingChannel(0.3f, 0.3f));
        }

        /// <summary>乖離が妄想閾以上ならトップは幻想で指揮していると判定される。</summary>
        [Test]
        public void IsDelusionalCommand_幻想の指揮()
        {
            // 既定妄想閾0.5。
            Assert.IsTrue(InformationDistortionRules.IsDelusionalCommand(0.56f));
            Assert.IsFalse(InformationDistortionRules.IsDelusionalCommand(0.3f));
            // 閾ちょうどで true（以上）。
            Assert.IsTrue(InformationDistortionRules.IsDelusionalCommand(0.5f));
        }
    }
}
