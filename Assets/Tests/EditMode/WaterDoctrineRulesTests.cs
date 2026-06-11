using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 柔弱（じゅうじゃく）ドクトリン（老子・上善は水の如し）を固定する：柔軟は圧力に押されて短期は退くが
    /// （水は低きに流れる＝短期劣後）、長期は折れずに回復し（しなやかなものは折れない）、剛強は折れたら戻らない
    /// 非対称、柔は硬い相手に長期で勝ち（水が岩を穿つ）、柔軟＋持続は守りに浸透し（点滴石を穿つ）、士気の床を
    /// 与え、剛強なら砕けるストレス下でも折れない。物語テスト＝柔よく剛を制す。クランプを担保。
    /// </summary>
    public class WaterDoctrineRulesTests
    {
        private static readonly WaterDoctrineParams P = WaterDoctrineParams.Default;
        // 屈服0.6/回復0.1/浸透0.5/士気の床上限0.5/折れ閾値0.7

        [Test]
        public void ShortTermYield_FlexibleRetreatsUnderPressure()
        {
            // 柔軟1・圧力1：1−1×1×0.6=0.4＝大きく退く（水は低きに流れる＝短期劣後）
            Assert.AreEqual(0.4f, WaterDoctrineRules.ShortTermYield(1f, 1f, P), 1e-5f);
            // 剛強（柔軟0）は短期に退かない＝1（折れる前兆）
            Assert.AreEqual(1f, WaterDoctrineRules.ShortTermYield(0f, 1f, P), 1e-5f);
            // 圧力なしなら柔軟でも退かない
            Assert.AreEqual(1f, WaterDoctrineRules.ShortTermYield(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void LongTermRecovery_FlexibleRecoversRigidDoesNot()
        {
            // 柔軟1・dt2：1×0.1×2=0.2＝時間で戻る（しなやかなものは折れず回復）
            Assert.AreEqual(0.2f, WaterDoctrineRules.LongTermRecovery(1f, 2f, P), 1e-5f);
            // 剛強（柔軟0）は回復しない＝折れたら戻らない
            Assert.AreEqual(0f, WaterDoctrineRules.LongTermRecovery(0f, 5f, P), 1e-5f);
            // 上限1でクランプ
            Assert.AreEqual(1f, WaterDoctrineRules.LongTermRecovery(1f, 100f, P), 1e-5f);
        }

        [Test]
        public void RecoveryAsymmetry_FlexibleReturnsRigidBreaks()
        {
            // 柔軟0.8・剛強0：0.8×1=0.8＝満額に近い回復見込み
            Assert.AreEqual(0.8f, WaterDoctrineRules.RecoveryAsymmetry(0.8f, 0f), 1e-5f);
            // 同じ柔軟でも剛強分が高いと戻らない：0.8×(1−0.75)=0.2
            Assert.AreEqual(0.2f, WaterDoctrineRules.RecoveryAsymmetry(0.8f, 0.75f), 1e-5f);
            // 完全に硬い＝折れて戻らない
            Assert.AreEqual(0f, WaterDoctrineRules.RecoveryAsymmetry(0.8f, 1f), 1e-5f);
        }

        [Test]
        public void SoftOvercomesHard_WaterWearsStone()
        {
            // 柔軟0.9・相手が硬い1.0：0.9×1=0.9＝硬い相手ほど長期で勝つ（水が岩を穿つ）
            Assert.AreEqual(0.9f, WaterDoctrineRules.SoftOvercomesHard(0.9f, 1f), 1e-5f);
            // 相手も柔らかい＝穿てない（しなる者同士）
            Assert.AreEqual(0f, WaterDoctrineRules.SoftOvercomesHard(0.9f, 0f), 1e-5f);
            // 硬い相手ほど柔の勝ち度合いが上がる
            Assert.Greater(
                WaterDoctrineRules.SoftOvercomesHard(0.7f, 0.9f),
                WaterDoctrineRules.SoftOvercomesHard(0.7f, 0.3f));
        }

        [Test]
        public void PenetrationForce_FlexibilityAndPersistenceBoreThrough()
        {
            // 柔軟1・持続1：1×1×0.5=0.5＝点滴石を穿つ
            Assert.AreEqual(0.5f, WaterDoctrineRules.PenetrationForce(1f, 1f, P), 1e-5f);
            // 持続なし＝一撃では穿てない
            Assert.AreEqual(0f, WaterDoctrineRules.PenetrationForce(1f, 0f, P), 1e-5f);
            // 柔軟なし＝硬く弾かれる
            Assert.AreEqual(0f, WaterDoctrineRules.PenetrationForce(0f, 1f, P), 1e-5f);
        }

        [Test]
        public void ResilientMoraleFloor_FlexibilityGivesFloor()
        {
            // 柔軟1：1×0.5=0.5＝押されても下回らない最低士気
            Assert.AreEqual(0.5f, WaterDoctrineRules.ResilientMoraleFloor(1f, P), 1e-5f);
            // 柔軟0.5：0.25
            Assert.AreEqual(0.25f, WaterDoctrineRules.ResilientMoraleFloor(0.5f, P), 1e-5f);
            // 剛強（柔軟0）＝床なし＝崩れうる
            Assert.AreEqual(0f, WaterDoctrineRules.ResilientMoraleFloor(0f, P), 1e-5f);
        }

        [Test]
        public void AdaptiveResponse_SoftDeflectsThreat()
        {
            // 脅威1・柔軟0.75：1×(1−0.75)=0.25＝柔で受け流して残る脅威は小さい
            Assert.AreEqual(0.25f, WaterDoctrineRules.AdaptiveResponse(1f, 0.75f), 1e-5f);
            // 剛強（柔軟0）＝正面から受けて脅威がそのまま残る
            Assert.AreEqual(1f, WaterDoctrineRules.AdaptiveResponse(1f, 0f), 1e-5f);
            // 柔軟1＝脅威を完全に流し去る
            Assert.AreEqual(0f, WaterDoctrineRules.AdaptiveResponse(1f, 1f), 1e-5f);
        }

        [Test]
        public void IsUnbreakable_FlexibleBendsRigidShatters()
        {
            // 剛強（柔軟0）は閾値0.7超のストレス0.8で折れる
            Assert.IsFalse(WaterDoctrineRules.IsUnbreakable(0f, 0.8f, P));
            // 同じストレス0.8でも柔軟0.5なら実効閾値0.7+0.5×0.3=0.85＞0.8＝折れない
            Assert.IsTrue(WaterDoctrineRules.IsUnbreakable(0.5f, 0.8f, P));
            // 柔軟1＝実効閾値1＝最大ストレスでも決して折れない
            Assert.IsTrue(WaterDoctrineRules.IsUnbreakable(1f, 1f, P));
            // 閾値内なら剛強でも折れない
            Assert.IsTrue(WaterDoctrineRules.IsUnbreakable(0f, 0.5f, P));
        }

        [Test]
        public void Story_SoftYieldsShortTermButOvercomesLongTerm()
        {
            // 柔弱な水のドクトリン(柔軟0.9)は強い圧力(1.0)に短期は大きく退くが（短期劣後）、
            // 折れずに長期で回復し、硬い剛強の相手(剛強1.0)を最後には穿つ＝柔よく剛を制す。
            const float flexibility = 0.9f;

            // 短期：押されて退く
            float shortTerm = WaterDoctrineRules.ShortTermYield(flexibility, 1f, P);
            Assert.Less(shortTerm, 1f);

            // だが折れない（剛強なら砕けるストレス0.9でも）
            Assert.IsTrue(WaterDoctrineRules.IsUnbreakable(flexibility, 0.9f, P));
            Assert.IsFalse(WaterDoctrineRules.IsUnbreakable(0f, 0.9f, P));

            // 長期：時間で回復し、硬い相手に勝る
            float recovered = WaterDoctrineRules.LongTermRecovery(flexibility, 10f, P);
            float overcome = WaterDoctrineRules.SoftOvercomesHard(flexibility, 1f);
            Assert.Greater(recovered, 0f);   // 折れず戻る
            Assert.Greater(overcome, 0.5f);  // 硬い剛強を長期で穿つ
        }
    }
}
