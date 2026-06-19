using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 政略結婚（婚姻同盟と血統政治）を固定する：縁組価値は血統格と政治的有用性の混合、格差は釣り合いを崩し、
    /// 絆は価値×釣り合い（格差婚でも床まで）、世継ぎ正統性は血と承認の混合、外戚は正統な世継ぎ×野心で台頭、
    /// 不和は確率的OR、絆は不和に蝕まれ、損得は絆と不和の差で決まる。物語テスト＝格差婚が外戚を生み破綻する。
    /// 既定Params期待値を固定しクランプを担保。
    /// </summary>
    public class DynasticMarriageRulesTests
    {
        private static readonly DynasticMarriageParams P = DynasticMarriageParams.Default;
        // 有用性重み0.5/格差ペナルティ1.0/釣り合い床0.4/承認重み0.5/野心増幅0.6/不和侵食0.8

        [Test]
        public void MatchValue_BlendsPrestigeAndUtility()
        {
            // avg=(0.8+0.6)/2=0.7、Lerp(0.7,0.4,0.5)=0.55
            Assert.AreEqual(0.55f, DynasticMarriageRules.MatchValue(0.8f, 0.6f, 0.4f, P), 1e-4f);
            // Default委譲版も同値
            Assert.AreEqual(0.55f, DynasticMarriageRules.MatchValue(0.8f, 0.6f, 0.4f), 1e-4f);
            // 入力クランプ（過大値は1扱い）：avg=1、Lerp(1,1,0.5)=1
            Assert.AreEqual(1f, DynasticMarriageRules.MatchValue(5f, 5f, 5f, P), 1e-4f);
        }

        [Test]
        public void PrestigeBalance_GapErodesBalance()
        {
            // 近い格は釣り合う：1-|0.8-0.6|*1=0.8
            Assert.AreEqual(0.8f, DynasticMarriageRules.PrestigeBalance(0.8f, 0.6f, P), 1e-4f);
            // 格差が大きいと格下扱い：1-0.6=0.4
            Assert.AreEqual(0.4f, DynasticMarriageRules.PrestigeBalance(0.9f, 0.3f, P), 1e-4f);
            // 下限0でクランプ（負にならない）
            Assert.AreEqual(0f, DynasticMarriageRules.PrestigeBalance(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void AllianceBond_ValueTimesBalanceWithFloor()
        {
            // balFactor=Lerp(0.4,1,0.8)=0.88、0.55*0.88=0.484
            Assert.AreEqual(0.484f, DynasticMarriageRules.AllianceBond(0.55f, 0.8f, P), 1e-4f);
            // 釣り合い0でも床0.4は残る：0.6*0.4=0.24（格差婚でも同盟は一応成る）
            Assert.AreEqual(0.24f, DynasticMarriageRules.AllianceBond(0.6f, 0f, P), 1e-4f);
        }

        [Test]
        public void HeirLegitimacy_BlendsBloodAndRecognition()
        {
            // Lerp(0.8,0.6,0.5)=0.7
            Assert.AreEqual(0.7f, DynasticMarriageRules.HeirLegitimacy(0.8f, 0.6f, P), 1e-4f);
            // 承認0でも血0.8が支える：Lerp(0.8,0,0.5)=0.4
            Assert.AreEqual(0.4f, DynasticMarriageRules.HeirLegitimacy(0.8f, 0f, P), 1e-4f);
        }

        [Test]
        public void MaternalKinRise_LegitimacyTimesAmbition()
        {
            // ambitionFactor=Clamp01(1*0.6)=0.6、0.7*0.6=0.42
            Assert.AreEqual(0.42f, DynasticMarriageRules.MaternalKinRise(0.7f, 1f, P), 1e-4f);
            // 正統性0なら後ろ盾なし＝外戚は台頭しない
            Assert.AreEqual(0f, DynasticMarriageRules.MaternalKinRise(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void MaritalDiscord_IsProbabilisticOr()
        {
            // 1-(1-0.5)(1-0.5)=0.75（どちらかが高ければ不和は募る）
            Assert.AreEqual(0.75f, DynasticMarriageRules.MaritalDiscord(0.5f, 0.5f, P), 1e-4f);
            // 両方0なら静穏
            Assert.AreEqual(0f, DynasticMarriageRules.MaritalDiscord(0f, 0f, P), 1e-4f);
            // 片方1なら不和1
            Assert.AreEqual(1f, DynasticMarriageRules.MaritalDiscord(1f, 0.3f, P), 1e-4f);
        }

        [Test]
        public void BondDecay_DiscordErodesBond()
        {
            // erosion=0.5*0.75*0.8=0.3、0.5-0.3=0.2
            Assert.AreEqual(0.2f, DynasticMarriageRules.BondDecay(0.5f, 0.75f, P), 1e-4f);
            // 不和0なら絆は無傷
            Assert.AreEqual(0.5f, DynasticMarriageRules.BondDecay(0.5f, 0f, P), 1e-4f);
        }

        [Test]
        public void IsMarriageAdvantageous_BondMustExceedDiscord()
        {
            // 絆>不和＝有利
            Assert.IsTrue(DynasticMarriageRules.IsMarriageAdvantageous(0.484f, 0.2f, 0f));
            // 絆<不和＝不利
            Assert.IsFalse(DynasticMarriageRules.IsMarriageAdvantageous(0.2f, 0.75f, 0f));
            // 既定閾値委譲版（threshold=0）：等しければ有利
            Assert.IsTrue(DynasticMarriageRules.IsMarriageAdvantageous(0.5f, 0.5f));
        }

        [Test]
        public void Story_UnevenMatchBreedsKinPowerThenBreaksApart()
        {
            // 名門(0.9)と格下(0.3)の格差婚だが政略上は有用(0.8)
            float match = DynasticMarriageRules.MatchValue(0.9f, 0.3f, 0.8f, P);   // avg=0.6→Lerp(0.6,0.8,0.5)=0.7
            float balance = DynasticMarriageRules.PrestigeBalance(0.9f, 0.3f, P);   // 0.4
            float bond = DynasticMarriageRules.AllianceBond(match, balance, P);     // 0.7*Lerp(0.4,1,0.4)=0.7*0.64=0.448
            Assert.AreEqual(0.7f, match, 1e-4f);
            Assert.AreEqual(0.4f, balance, 1e-4f);
            Assert.AreEqual(0.448f, bond, 1e-4f);

            // 宮廷の承認(0.9)が血の薄さ(0.6)を補い、世継ぎは正統に
            float heir = DynasticMarriageRules.HeirLegitimacy(0.6f, 0.9f, P);       // Lerp(0.6,0.9,0.5)=0.75
            float kin = DynasticMarriageRules.MaternalKinRise(heir, 1f, P);        // 0.75*0.6=0.45
            Assert.AreEqual(0.75f, heir, 1e-4f);
            Assert.AreEqual(0.45f, kin, 1e-4f);
            Assert.Greater(kin, 0.4f); // 外戚が台頭する

            // だが格差ゆえの個人不和(0.7)と政治軋轢(0.6)が同盟を蝕む
            float discord = DynasticMarriageRules.MaritalDiscord(0.7f, 0.6f, P);    // 1-0.3*0.4=0.88
            float remaining = DynasticMarriageRules.BondDecay(bond, discord, P);    // 0.448-0.448*0.88*0.8≈0.1325
            Assert.AreEqual(0.88f, discord, 1e-4f);
            Assert.Less(remaining, bond);
            // 蝕まれた絆は不和を下回り、縁組は破綻に傾く
            Assert.IsFalse(DynasticMarriageRules.IsMarriageAdvantageous(remaining, discord));
        }
    }
}
