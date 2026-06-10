using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 私掠免許を固定する：取り分×獲物で応募が集まり・後ろ盾が拿捕を合法化し・儲けが統制を緩め・
    /// 緩みが中立国事故を生み・戦後は海賊化する（退役金で買い取れる）＝「借りた暴力は返却日に高くつく」。
    /// </summary>
    public class PrivateerRulesTests
    {
        private static readonly PrivateerParams P = PrivateerParams.Default;
        // 応募倍率1.5/正統性下限0.5/緩み0.1/締め直し0.05/事故上限0.5/海賊化 下限0.2・上限0.8

        [Test]
        public void CommissionAttraction_PrizeAndPreyDraw()
        {
            // 取り分0.5×獲物0.8×1.5＝0.6
            Assert.AreEqual(0.6f, PrivateerRules.CommissionAttraction(0.5f, 0.8f, P), 1e-5f);
            // 満額の取り分×太い獲物＝満員（1.5は1へクランプ）
            Assert.AreEqual(1f, PrivateerRules.CommissionAttraction(1f, 1f, P), 1e-5f);
            // 取り分ゼロ／獲物ゼロ＝誰も来ない
            Assert.AreEqual(0f, PrivateerRules.CommissionAttraction(0f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, PrivateerRules.CommissionAttraction(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void RaidingEffectiveness_LegitimacyBacksTheCapture()
        {
            // 後ろ盾満点＝全力。後ろ盾なし＝下限0.5倍（拿捕品を売り捌けない）
            Assert.AreEqual(10f, PrivateerRules.RaidingEffectiveness(10f, 1f, P), 1e-5f);
            Assert.AreEqual(5f, PrivateerRules.RaidingEffectiveness(10f, 0f, P), 1e-5f);
            Assert.AreEqual(7.5f, PrivateerRules.RaidingEffectiveness(10f, 0.5f, P), 1e-5f);
            // 負の戦力は0へクランプ
            Assert.AreEqual(0f, PrivateerRules.RaidingEffectiveness(-5f, 1f, P), 1e-5f);
        }

        [Test]
        public void ControlSlippageTick_GreedLoosensCommand()
        {
            // 取り分1×監督0×dt1：緩み+0.1（儲けに味をしめる）
            Assert.AreEqual(0.1f, PrivateerRules.ControlSlippageTick(0f, 1f, 0f, 1f, P), 1e-5f);
            // 監督0.5＝欲は半分届かず＋締め直し：0.1×0.5−0.05×0.5＝0.025
            Assert.AreEqual(0.025f, PrivateerRules.ControlSlippageTick(0f, 1f, 0.5f, 1f, P), 1e-5f);
            // 上限1でクランプ
            Assert.AreEqual(1f, PrivateerRules.ControlSlippageTick(0.95f, 1f, 0f, 1f, P), 1e-5f);
        }

        [Test]
        public void ControlSlippageTick_OversightTightens()
        {
            // 取り分0×監督1：締め直し0.05で緩みが戻る
            Assert.AreEqual(0.45f, PrivateerRules.ControlSlippageTick(0.5f, 0f, 1f, 1f, P), 1e-5f);
            // 下限0でクランプ
            Assert.AreEqual(0f, PrivateerRules.ControlSlippageTick(0.01f, 0f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void NeutralIncident_SlippageTimesTraffic()
        {
            // 緩み1×中立交通1＝上限0.5。統制が効いていれば事故ゼロ
            Assert.AreEqual(0.5f, PrivateerRules.NeutralIncident(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, PrivateerRules.NeutralIncident(0f, 1f, P), 1e-5f);
            Assert.AreEqual(0.125f, PrivateerRules.NeutralIncident(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void PostwarPiracyConversion_BorrowedViolenceComesDue()
        {
            // 緩みきった私掠10・退役金なし＝8が海賊へ（PiracyRules の勢力になる）
            Assert.AreEqual(8f, PrivateerRules.PostwarPiracyConversion(10f, 1f, 0f, P), 1e-5f);
            // 統制が効いていても下限0.2＝2は野に下る（借りた暴力のツケ）
            Assert.AreEqual(2f, PrivateerRules.PostwarPiracyConversion(10f, 0f, 0f, P), 1e-5f);
        }

        [Test]
        public void PostwarPiracyConversion_DemobilizationPayBuysThemOut()
        {
            // 退役金満額＝全員カタギに戻る
            Assert.AreEqual(0f, PrivateerRules.PostwarPiracyConversion(10f, 1f, 1f, P), 1e-5f);
            // 半額＝半分買い取り：8→4
            Assert.AreEqual(4f, PrivateerRules.PostwarPiracyConversion(10f, 1f, 0.5f, P), 1e-5f);
            // 退役金は多いほど海賊が減る（単調）
            float none = PrivateerRules.PostwarPiracyConversion(10f, 0.5f, 0f, P);
            float some = PrivateerRules.PostwarPiracyConversion(10f, 0.5f, 0.3f, P);
            Assert.Less(some, none);
        }
    }
}
