using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 名誉の毀損と公的回復を固定する（KIKU-4・『菊と刀』＝名に対する義理）：
    /// 人前の侮辱ほど重く毀損し、雪辱の義務が募り、公的雪辱で回復、応酬は激化、仲介者が出口を作る。境界を担保。
    /// </summary>
    public class HonorRulesTests
    {
        private static readonly HonorParams P = HonorParams.Default;
        // 公然性重み0.6/公的回復0.8/応酬0.7/面目出口0.6/毀損閾値0.4

        [Test]
        public void HonorDamage_PublicInsultIsHeavier()
        {
            // pub=0：factor=(1-0.6)=0.4 → 0.8*0.4=0.32
            Assert.AreEqual(0.32f, HonorRules.HonorDamage(0.8f, 0f, P), 1e-4f);
            // pub=1：factor=1.0 → 0.8*1.0=0.8（人前は満額＝重い）
            Assert.AreEqual(0.8f, HonorRules.HonorDamage(0.8f, 1f, P), 1e-4f);
            // pub=0.5：factor=0.4+0.6*0.5=0.7 → 0.8*0.7=0.56
            Assert.AreEqual(0.56f, HonorRules.HonorDamage(0.8f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void ObligationToRestore_ScalesWithDamageAndSensitivity()
        {
            Assert.AreEqual(0.4f, HonorRules.ObligationToRestore(0.8f, 0.5f), 1e-4f); // 0.8*0.5
            Assert.AreEqual(0f, HonorRules.ObligationToRestore(0.9f, 0f), 1e-4f);    // 鈍感なら義務なし
        }

        [Test]
        public void PublicRestoration_RequiresWitnesses()
        {
            // act=1,w=1：1*1*0.8=0.8
            Assert.AreEqual(0.8f, HonorRules.PublicRestoration(1f, 1f, P), 1e-4f);
            // 証人ゼロでは公的回復は成立しない
            Assert.AreEqual(0f, HonorRules.PublicRestoration(1f, 0f, P), 1e-4f);
            // act=0.5,w=0.5：0.5*0.5*0.8=0.2
            Assert.AreEqual(0.2f, HonorRules.PublicRestoration(0.5f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void VengeanceImperative_EasesWithReconciliation()
        {
            Assert.AreEqual(0.8f, HonorRules.VengeanceImperative(0.8f, 0f), 1e-4f);  // 和解なし＝満額
            Assert.AreEqual(0.4f, HonorRules.VengeanceImperative(0.8f, 0.5f), 1e-4f); // 0.8*0.5
            Assert.AreEqual(0f, HonorRules.VengeanceImperative(0.8f, 1f), 1e-4f);    // 完全和解で鎮まる
        }

        [Test]
        public void EscalationRisk_RisesWithOpponentHonor()
        {
            // v=0.8,o=1：0.8*1*0.7=0.56
            Assert.AreEqual(0.56f, HonorRules.EscalationRisk(0.8f, 1f, P), 1e-4f);
            // 相手の名誉が低ければ引いて応酬は小さい：0.8*0.2*0.7=0.112
            Assert.AreEqual(0.112f, HonorRules.EscalationRisk(0.8f, 0.2f, P), 1e-4f);
        }

        [Test]
        public void HonorRecovered_AddsRestorationClamped()
        {
            Assert.AreEqual(0.8f, HonorRules.HonorRecovered(0.5f, 0.3f), 1e-4f);
            Assert.AreEqual(1f, HonorRules.HonorRecovered(0.7f, 0.6f), 1e-4f); // クランプ
        }

        [Test]
        public void PrivateVsPublicHealing_PublicNeededDespiteInnerPeace()
        {
            // 内心で折り合っても（0.5）公的雪辱（0.8）が残れば癒えきらない：0.8*(1-0.5)=0.4
            Assert.AreEqual(0.4f, HonorRules.PrivateVsPublicHealing(0.5f, 0.8f), 1e-4f);
            // 内面が完全納得（罪文化的）なら公的雪辱の必要は消える
            Assert.AreEqual(0f, HonorRules.PrivateVsPublicHealing(1f, 0.8f), 1e-4f);
        }

        [Test]
        public void IsHonorBreached_AtThreshold()
        {
            Assert.IsTrue(HonorRules.IsHonorBreached(0.4f));  // ちょうど閾値＝毀損
            Assert.IsFalse(HonorRules.IsHonorBreached(0.39f));
        }

        [Test]
        public void Story_PublicInsultDemandsVengeanceButMediatorDefusesIt()
        {
            // 人前（公然性1.0）で重い侮辱（0.9）を受ける＝名誉が大きく毀損される。
            float dmg = HonorRules.HonorDamage(0.9f, 1f, P); // factor=1.0 → 0.9
            Assert.AreEqual(0.9f, dmg, 1e-4f);
            Assert.IsTrue(HonorRules.IsHonorBreached(dmg)); // 閾値0.4超＝傷つけられた

            // 面目に敏感な当事者ほど雪辱の義務が募る。
            float obligation = HonorRules.ObligationToRestore(dmg, 0.9f); // 0.9*0.9=0.81
            Assert.AreEqual(0.81f, obligation, 1e-4f);

            // 和解の申し出が無ければ晴らさねばならない衝動は強く、相手の名誉も高ければ応酬は激化。
            float vengeanceNoOffer = HonorRules.VengeanceImperative(dmg, 0f); // 0.9
            float escalation = HonorRules.EscalationRisk(vengeanceNoOffer, 0.9f, P); // 0.9*0.9*0.7=0.567
            Assert.AreEqual(0.567f, escalation, 1e-4f);

            // しかし仲介者が面目を保つ出口を作れば（mediator=1.0）、メンツを潰さず収める余地が生まれ…
            float exit = HonorRules.FaceSavingExit(dmg, 1f, P); // 0.9*1*0.6=0.54
            Assert.AreEqual(0.54f, exit, 1e-4f);

            // 仲介を和解の申し出として通すと雪辱衝動が和らぎ、応酬リスクが下がる。
            float vengeanceWithExit = HonorRules.VengeanceImperative(dmg, exit); // 0.9*(1-0.54)=0.414
            float escalationDefused = HonorRules.EscalationRisk(vengeanceWithExit, 0.9f, P);
            Assert.AreEqual(0.414f, vengeanceWithExit, 1e-4f);
            Assert.Less(escalationDefused, escalation); // 仲介で応酬が収まる
        }
    }
}
