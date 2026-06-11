using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>立法者パラドックスの純ロジックの担保（#1464・ルソー『社会契約論』立法者）。</summary>
    public class LawgiverRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>構成のパラドックス＝両方欠ければ循環最大・どちらか満ちれば弱まる（鶏と卵）。</summary>
        [Test]
        public void ConstitutiveParadox_両方欠如で最大_片方充足で弱まる()
        {
            // 既定paradoxStrength=1.0。両方ゼロ→(1)(1)=1.0
            Assert.AreEqual(1f, LawgiverRules.ConstitutiveParadox(0f, 0f), Eps);
            // 良き法0.0・良き市民1.0→(1)(0)=0＝足場あり
            Assert.AreEqual(0f, LawgiverRules.ConstitutiveParadox(0f, 1f), Eps);
            // 双方0.5→(0.5)(0.5)=0.25
            Assert.AreEqual(0.25f, LawgiverRules.ConstitutiveParadox(0.5f, 0.5f), Eps);
        }

        /// <summary>建国の好機＝立法者の知恵×人民の機運（どちらか欠ければ閉じる）。</summary>
        [Test]
        public void FoundingMomentValue_知恵と機運の積()
        {
            Assert.AreEqual(0.48f, LawgiverRules.FoundingMomentValue(0.8f, 0.6f), Eps);
            // 機運ゼロ＝賢者がいても好機は開かない
            Assert.AreEqual(0f, LawgiverRules.FoundingMomentValue(1f, 0f), Eps);
            Assert.AreEqual(1f, LawgiverRules.FoundingMomentValue(1f, 1f), Eps);
        }

        /// <summary>制度外の権威＝知恵×(1−通常権力)＝権力を持たぬほど純粋な権威。</summary>
        [Test]
        public void ExtraInstitutionalAuthority_権力を持たぬほど純粋()
        {
            // 知恵0.9・通常権力0.0→0.9×1=0.9
            Assert.AreEqual(0.9f, LawgiverRules.ExtraInstitutionalAuthority(0.9f, 0f), Eps);
            // 知恵0.9・通常権力1.0→0.9×0=0＝統治者に堕す
            Assert.AreEqual(0f, LawgiverRules.ExtraInstitutionalAuthority(0.9f, 1f), Eps);
            // 知恵0.8・通常権力0.25→0.8×0.75=0.6
            Assert.AreEqual(0.6f, LawgiverRules.ExtraInstitutionalAuthority(0.8f, 0.25f), Eps);
        }

        /// <summary>一回性の初期化＝好機がそのまま最初の刻印（クランプのみ）。</summary>
        [Test]
        public void OneTimeInitialization_好機を初期刻印として返す()
        {
            Assert.AreEqual(0.7f, LawgiverRules.OneTimeInitialization(0.7f), Eps);
            Assert.AreEqual(1f, LawgiverRules.OneTimeInitialization(1.5f), Eps); // クランプ
            Assert.AreEqual(0f, LawgiverRules.OneTimeInitialization(-0.2f), Eps);
        }

        /// <summary>カリスマ的説得＝権威×聖性（理性でなく神の名を借りる）。</summary>
        [Test]
        public void CharismaticPersuasion_権威と聖性の積()
        {
            Assert.AreEqual(0.56f, LawgiverRules.CharismaticPersuasion(0.8f, 0.7f), Eps);
            // 聖性ゼロ＝論証だけでは人民は動かない
            Assert.AreEqual(0f, LawgiverRules.CharismaticPersuasion(1f, 0f), Eps);
        }

        /// <summary>制度の刻印＝最初の型が高い持続率で長く残る（経路依存・dtで漸減）。</summary>
        [Test]
        public void InstitutionalImprint_持続率で長く残る()
        {
            // 既定imprintPersistence=0.9。init=1.0・dt=1→1−1×(1−0.9)×1=0.9
            Assert.AreEqual(0.9f, LawgiverRules.InstitutionalImprint(1f, 1f), Eps);
            // dt=0＝刻んだ瞬間は満額
            Assert.AreEqual(1f, LawgiverRules.InstitutionalImprint(1f, 0f), Eps);
            // init=0.5・dt=1→0.5−0.5×0.1=0.45
            Assert.AreEqual(0.45f, LawgiverRules.InstitutionalImprint(0.5f, 1f), Eps);
        }

        /// <summary>立法者の退場＝権力を手放していく（FounderTrajectory の自己廃絶と整合）。</summary>
        [Test]
        public void LawgiverSelfRemoval_権力を時間とともに手放す()
        {
            // 既定selfRemovalRate=0.5。power=1.0・dt=1→1−1×0.5×1=0.5
            Assert.AreEqual(0.5f, LawgiverRules.LawgiverSelfRemoval(1f, 1f), Eps);
            // dt=2→1−1×0.5×2=0
            Assert.AreEqual(0f, LawgiverRules.LawgiverSelfRemoval(1f, 2f), Eps);
            // dt=0＝まだ去らず満額
            Assert.AreEqual(1f, LawgiverRules.LawgiverSelfRemoval(1f, 0f), Eps);
        }

        /// <summary>成功した建国＝良き法を与え（好機≥閾値）身を引いた（残存権力≤1−閾値）。</summary>
        [Test]
        public void IsSuccessfulFounding_法を与え身を引けば成功()
        {
            // 既定foundingThreshold引数=0.5。好機0.7≥0.5 かつ 残存0.2≤0.5→成功
            Assert.IsTrue(LawgiverRules.IsSuccessfulFounding(0.7f, 0.2f, 0.5f));
            // 好機が低い＝良き法を与えていない→失敗
            Assert.IsFalse(LawgiverRules.IsSuccessfulFounding(0.3f, 0.2f, 0.5f));
            // 権力に居座る＝残存0.8>0.5→失敗
            Assert.IsFalse(LawgiverRules.IsSuccessfulFounding(0.7f, 0.8f, 0.5f));
            // 境界＝好機ちょうど0.5・残存ちょうど0.5→成功
            Assert.IsTrue(LawgiverRules.IsSuccessfulFounding(0.5f, 0.5f, 0.5f));
        }

        /// <summary>既定Params＝パラドックス1.0・刻印持続0.9・退場0.5・建国閾値0.5。</summary>
        [Test]
        public void Default_既定値()
        {
            var p = LawgiverParams.Default;
            Assert.AreEqual(1f, p.paradoxStrength, Eps);
            Assert.AreEqual(0.9f, p.imprintPersistence, Eps);
            Assert.AreEqual(0.5f, p.selfRemovalRate, Eps);
            Assert.AreEqual(0.5f, p.foundingThreshold, Eps);
        }
    }
}
