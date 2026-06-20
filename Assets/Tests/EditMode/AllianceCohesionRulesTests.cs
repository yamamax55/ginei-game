using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 同盟結束を固定する：共通脅威×0.5 ＋ 好感度正規化×0.5 − 非対称×0.4 − 超過加盟国×0.05。
    /// 脅威・好感度で上がり、非対称・加盟国数で下がる（外敵がいて仲の良い対等な少数同盟ほど固い）。
    /// [0,1] クランプ、WillHold は結束≥負荷で持ちこたえる。
    /// </summary>
    public class AllianceCohesionRulesTests
    {
        private static readonly AllianceCohesionRules.Params P = AllianceCohesionRules.Params.Default;
        // 脅威0.5/好感度0.5/非対称0.4/加盟国0.05

        [Test]
        public void Cohesion_IsDeterministic()
        {
            float a = AllianceCohesionRules.Cohesion(0.5f, 0f, 0.2f, 2, P);
            float b = AllianceCohesionRules.Cohesion(0.5f, 0f, 0.2f, 2, P);
            Assert.AreEqual(a, b, 1e-5f);
        }

        [Test]
        public void Cohesion_KnownValue()
        {
            // 脅威0.6×0.5=0.3、好感度50→正規化0.75×0.5=0.375、非対称0=0、加盟国2→超過0
            // 合計 0.675
            Assert.AreEqual(0.675f, AllianceCohesionRules.Cohesion(0.6f, 50f, 0f, 2, P), 1e-5f);
        }

        [Test]
        public void Cohesion_SharedThreatRaises()
        {
            float low = AllianceCohesionRules.Cohesion(0.1f, 0f, 0.2f, 2, P);
            float high = AllianceCohesionRules.Cohesion(0.9f, 0f, 0.2f, 2, P);
            Assert.Less(low, high); // 共通の敵が結束を固める
        }

        [Test]
        public void Cohesion_GoodOpinionRaises()
        {
            float bad = AllianceCohesionRules.Cohesion(0.4f, -80f, 0.2f, 2, P);
            float good = AllianceCohesionRules.Cohesion(0.4f, 80f, 0.2f, 2, P);
            Assert.Less(bad, good); // 仲が良いほど結束
        }

        [Test]
        public void Cohesion_PowerAsymmetryLowers()
        {
            float even = AllianceCohesionRules.Cohesion(0.6f, 50f, 0.0f, 2, P);
            float lopsided = AllianceCohesionRules.Cohesion(0.6f, 50f, 0.8f, 2, P);
            Assert.Greater(even, lopsided); // 突出した盟主は不公平感を生む
        }

        [Test]
        public void Cohesion_MoreMembersLowers()
        {
            float few = AllianceCohesionRules.Cohesion(0.6f, 50f, 0.2f, 2, P);
            float many = AllianceCohesionRules.Cohesion(0.6f, 50f, 0.2f, 8, P);
            Assert.Greater(few, many); // 加盟国が多いほど調整コストで下がる
        }

        [Test]
        public void Cohesion_Clamped()
        {
            // 全部最悪＝下限0
            Assert.AreEqual(0f, AllianceCohesionRules.Cohesion(0f, -100f, 1f, 20, P), 1e-5f);
            // 全部最良でも上限1（脅威0.5+好感度0.5=1.0）
            Assert.AreEqual(1f, AllianceCohesionRules.Cohesion(1f, 100f, 0f, 2, P), 1e-5f);
            // 入力範囲外もクランプ
            Assert.AreEqual(1f, AllianceCohesionRules.Cohesion(5f, 999f, -1f, 2, P), 1e-5f);
        }

        [Test]
        public void Cohesion_DefaultOverloadMatches()
        {
            Assert.AreEqual(
                AllianceCohesionRules.Cohesion(0.6f, 50f, 0.2f, 3, P),
                AllianceCohesionRules.Cohesion(0.6f, 50f, 0.2f, 3),
                1e-5f);
        }

        [Test]
        public void WillHold_ThresholdBehavior()
        {
            Assert.IsTrue(AllianceCohesionRules.WillHold(0.7f, 0.5f));  // 結束>負荷＝持ちこたえる
            Assert.IsTrue(AllianceCohesionRules.WillHold(0.5f, 0.5f));  // 境界（等しい）＝持ちこたえる
            Assert.IsFalse(AllianceCohesionRules.WillHold(0.4f, 0.5f)); // 結束<負荷＝崩壊
        }
    }
}
