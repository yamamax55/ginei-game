using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>相続・継承ロジック（#1038）の決定論テスト。長子相続の集中と分割相続の散逸、無相続人の国庫帰属を担保。</summary>
    public class InheritanceRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>長子相続＝長男が大半（既定0.8）・残りを弟妹で均等割。</summary>
        [Test]
        public void HeirShares_長子相続は長男が大半()
        {
            var s = InheritanceRules.HeirShares(100f, 3, InheritancePattern.長子相続);
            Assert.AreEqual(3, s.Length);
            Assert.AreEqual(80f, s[0], Eps);
            Assert.AreEqual(10f, s[1], Eps);
            Assert.AreEqual(10f, s[2], Eps);
        }

        /// <summary>分割相続＝全員均等割＝資産が散る。</summary>
        [Test]
        public void HeirShares_分割相続は均等割()
        {
            var s = InheritanceRules.HeirShares(100f, 4, InheritancePattern.分割相続);
            Assert.AreEqual(4, s.Length);
            foreach (var v in s) Assert.AreEqual(25f, v, Eps);
        }

        /// <summary>分割相続は世代を経るほど集中度が指数的に細る＝長子相続は家を保つ。</summary>
        [Test]
        public void DynastyConcentration_分割は細り長子は保つ()
        {
            // 分割: (1/2)^2 = 0.25
            Assert.AreEqual(0.25f, InheritanceRules.DynastyConcentration(InheritancePattern.分割相続, 2, 2), Eps);
            // 長子: 0.8^3 = 0.512
            Assert.AreEqual(0.512f, InheritanceRules.DynastyConcentration(InheritancePattern.長子相続, 3, 2), Eps);
        }

        /// <summary>相続税は基礎控除超過分にのみ課税＝世代継承で国庫が取り分を得る。</summary>
        [Test]
        public void InheritanceTax_控除超過分のみ課税()
        {
            // (100-20)*0.4 = 32
            Assert.AreEqual(32f, InheritanceRules.InheritanceTax(100f, 0.4f, 20f), Eps);
            // 控除以下は非課税
            Assert.AreEqual(0f, InheritanceRules.InheritanceTax(15f, 0.4f, 20f), Eps);
        }

        /// <summary>相続争いは相続人複数＋分割＋曖昧さで上がり、相続人1人なら0。</summary>
        [Test]
        public void DisputeRisk_人数と分割と曖昧さで増える()
        {
            // 0.1 + 2*0.05 + 0.3(分割) + 0(明白) = 0.5
            Assert.AreEqual(0.5f, InheritanceRules.DisputeRisk(3, InheritancePattern.分割相続, 1f), Eps);
            // 相続人1人＝争いなし
            Assert.AreEqual(0f, InheritanceRules.DisputeRisk(1, InheritancePattern.分割相続, 0f), Eps);
        }

        /// <summary>分割相続は世代を経て指数的に細分化する。</summary>
        [Test]
        public void FragmentationOverGenerations_分割は指数的に細る()
        {
            // 1000 * (1/2)^3 = 125
            Assert.AreEqual(125f, InheritanceRules.FragmentationOverGenerations(1000f, InheritancePattern.分割相続, 2, 3), Eps);
            // 長子は本流が保つ: 1000 * 0.8^2 = 640
            Assert.AreEqual(640f, InheritanceRules.FragmentationOverGenerations(1000f, InheritancePattern.長子相続, 2, 2), Eps);
        }

        /// <summary>無相続人なら全資産が国庫へ、相続人がいれば0。</summary>
        [Test]
        public void EscheatToState_無相続人は国庫帰属()
        {
            Assert.AreEqual(500f, InheritanceRules.EscheatToState(0, 500f), Eps);
            Assert.AreEqual(0f, InheritanceRules.EscheatToState(2, 500f), Eps);
        }

        /// <summary>負入力はクランプ＝資産は非負・相続人0は空配列。</summary>
        [Test]
        public void Clamp_負入力は安全に処理()
        {
            Assert.AreEqual(0, InheritanceRules.HeirShares(100f, 0, InheritancePattern.長子相続).Length);
            Assert.AreEqual(0f, InheritanceRules.InheritanceTax(-50f, 0.4f, 20f), Eps);
            // 既定Params確認
            Assert.AreEqual(0.8f, InheritanceParams.Default.primogenitureMainShare, Eps);
        }
    }
}
