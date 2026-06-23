using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// DataLinkRules（データリンク＝艦隊間の戦術データ共有）の純ロジック検証。
    /// 既定 DataLinkParams での期待値を手計算で固定。
    /// </summary>
    public class DataLinkRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void SharedPicture_LinksAndSensorBuildPicture()
        {
            // reach=8/(8+8)=0.5 ×寄与1.0 = 0.5
            Assert.AreEqual(0.5f, DataLinkRules.SharedPicture(8, 1.0f), Eps);
            // リンク艦0隻なら図は無い
            Assert.AreEqual(0f, DataLinkRules.SharedPicture(0, 1.0f), Eps);
            // 負の隻数はclampで0扱い
            Assert.AreEqual(0f, DataLinkRules.SharedPicture(-5, 1.0f), Eps);
        }

        [Test]
        public void SharedPicture_MoreLinksMoreReach()
        {
            // 隻数が増えるほど飽和カーブで単調増加
            float few = DataLinkRules.SharedPicture(2, 1.0f);
            float many = DataLinkRules.SharedPicture(20, 1.0f);
            Assert.Less(few, many);
        }

        [Test]
        public void SensorFusion_QualityWeightsFusion()
        {
            // 図0.5・質満点：Lerp(0.5,1,1)=1.0 ×0.5 = 0.5
            Assert.AreEqual(0.5f, DataLinkRules.SensorFusion(0.5f, 1.0f), Eps);
            // 図0.5・質ゼロ：Lerp(0.5,1,0)=0.5 ×0.5 = 0.25
            Assert.AreEqual(0.25f, DataLinkRules.SensorFusion(0.5f, 0.0f), Eps);
        }

        [Test]
        public void TargetSharing_BandwidthConstrains()
        {
            // 帯域満点：制約0 → 共有=統合
            Assert.AreEqual(1.0f, DataLinkRules.TargetSharing(0.5f, 1.0f), Eps);
            // 帯域ゼロ：制約1 → 0.5/(0.5+1)=1/3
            Assert.AreEqual(0.33333f, DataLinkRules.TargetSharing(0.5f, 0.0f), Eps);
            // 統合ゼロは共有ゼロ
            Assert.AreEqual(0f, DataLinkRules.TargetSharing(0f, 0.5f), Eps);
        }

        [Test]
        public void DataFreshness_RateAndLatency()
        {
            // 高頻度・遅延なし＝鮮度満点
            Assert.AreEqual(1.0f, DataLinkRules.DataFreshness(1.0f, 0.0f), Eps);
            // 0.8×(1-0.25)=0.6
            Assert.AreEqual(0.6f, DataLinkRules.DataFreshness(0.8f, 0.25f), Eps);
            // 遅延最大は鮮度ゼロ
            Assert.AreEqual(0f, DataLinkRules.DataFreshness(1.0f, 1.0f), Eps);
        }

        [Test]
        public void CooperativeEngagement_NeedsSharingAndFreshness()
        {
            // 共有満点・鮮度満点＝CEC満点
            Assert.AreEqual(1.0f, DataLinkRules.CooperativeEngagement(1.0f, 1.0f), Eps);
            // 鮮度ゼロでも重み0.6なので Lerp(0.4,1,0)=0.4 残る
            Assert.AreEqual(0.4f, DataLinkRules.CooperativeEngagement(1.0f, 0.0f), Eps);
        }

        [Test]
        public void LinkDisruptionImpact_AutonomyOffsets()
        {
            // 途絶満点・自律ゼロ＝打撃満点
            Assert.AreEqual(1.0f, DataLinkRules.LinkDisruptionImpact(1.0f, 0.0f), Eps);
            // 途絶満点でも自律満点なら打撃ゼロ（各個分散で戦える）
            Assert.AreEqual(0f, DataLinkRules.LinkDisruptionImpact(1.0f, 1.0f), Eps);
            // 自律0.5で半減
            Assert.AreEqual(0.5f, DataLinkRules.LinkDisruptionImpact(1.0f, 0.5f), Eps);
        }

        [Test]
        public void NetworkedLethality_And_Advantage()
        {
            // CEC1.0×図0.5=0.5
            Assert.AreEqual(0.5f, DataLinkRules.NetworkedLethality(1.0f, 0.5f), Eps);
            // 既定閾値0.5以上は優位
            Assert.IsTrue(DataLinkRules.IsLinkAdvantaged(0.5f));
            Assert.IsFalse(DataLinkRules.IsLinkAdvantaged(0.49f));
            // 明示閾値版
            Assert.IsTrue(DataLinkRules.IsLinkAdvantaged(0.7f, 0.6f));
            Assert.IsFalse(DataLinkRules.IsLinkAdvantaged(0.5f, 0.6f));
        }

        [Test]
        public void ClampsAreSafe()
        {
            // 全出力が0..1へclamp（過大入力でも破綻しない）
            Assert.AreEqual(1.0f, DataLinkRules.SharedPicture(99999, 5.0f), Eps);
            Assert.AreEqual(1.0f, DataLinkRules.SensorFusion(5.0f, 5.0f), Eps);
            Assert.AreEqual(1.0f, DataLinkRules.TargetSharing(5.0f, 5.0f), Eps);
            Assert.AreEqual(0f, DataLinkRules.DataFreshness(-1f, 0.0f), Eps);
            Assert.AreEqual(0f, DataLinkRules.CooperativeEngagement(-1f, -1f), Eps);
            Assert.AreEqual(0f, DataLinkRules.LinkDisruptionImpact(-1f, 5.0f), Eps);
        }

        /// <summary>
        /// 物語テスト：旗艦のセンサーが妨害で潰れても、健在な僚艦8隻のデータリンクが共通図を描き、
        /// 帯域を確保し新鮮な航跡を回せば、自艦に映らない敵を僚艦データで撃ち抜きネットワーク優位を保つ。
        /// 一方でリンクを叩き切られても各個自律が高い艦隊は崩れない。
        /// </summary>
        [Test]
        public void Story_NetworkedFleetFightsThroughSensorLossButDisruptionFavorsAutonomy()
        {
            var p = DataLinkParams.Default;

            // 8隻リンク・センサー寄与高・共通図を構築
            float picture = DataLinkRules.SharedPicture(8, 0.9f, p);
            // 質の高い統合
            float fusion = DataLinkRules.SensorFusion(picture, 0.9f, p);
            // 広帯域で目標共有が通る
            float sharing = DataLinkRules.TargetSharing(fusion, 0.9f, p);
            // 高頻度・低遅延で新鮮
            float fresh = DataLinkRules.DataFreshness(0.95f, 0.1f, p);
            // 協調交戦が成立
            float cec = DataLinkRules.CooperativeEngagement(sharing, fresh, p);
            float lethality = DataLinkRules.NetworkedLethality(cec, picture, p);

            // 図と統合と協調は正の値を持つ
            Assert.Greater(picture, 0f);
            Assert.Greater(fusion, 0f);
            Assert.Greater(cec, 0f);
            Assert.Greater(lethality, 0f);

            // リンクを完全に切られると：自律が高い艦隊は打撃が小さい
            float impactAutonomous = DataLinkRules.LinkDisruptionImpact(1.0f, 0.8f, p);
            float impactRigid = DataLinkRules.LinkDisruptionImpact(1.0f, 0.1f, p);
            Assert.Less(impactAutonomous, impactRigid);

            // 同じ統合でも帯域を絞ると共有が落ちる（パイプが詰まる）
            float sharingNarrow = DataLinkRules.TargetSharing(fusion, 0.1f, p);
            Assert.Less(sharingNarrow, sharing);
        }
    }
}
