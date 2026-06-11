using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>四面楚歌（心理的包囲）の純ロジックの担保（#1419）。</summary>
    public class PsychologicalSiegeMoraleRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>物理的包囲＝包囲度×退路の無さ。退路が開いていれば包囲は緩む。</summary>
        [Test]
        public void PhysicalEncirclement_退路の無さで決まる()
        {
            // 完全包囲・退路ゼロ＝1
            Assert.AreEqual(1f, PsychologicalSiegeMoraleRules.PhysicalEncirclement(1f, 0f), Eps);
            // 包囲されていても退路が半分開いていれば半減
            Assert.AreEqual(0.5f, PsychologicalSiegeMoraleRules.PhysicalEncirclement(1f, 0.5f), Eps);
            // 退路が完全に開いていれば0
            Assert.AreEqual(0f, PsychologicalSiegeMoraleRules.PhysicalEncirclement(1f, 1f), Eps);
        }

        /// <summary>心理的孤立＝味方の離反×絶望感の相乗。両方揃うと深い孤立。</summary>
        [Test]
        public void PsychologicalIsolation_離反と絶望の相乗()
        {
            // 両方最大＝1にクランプ
            Assert.AreEqual(1f, PsychologicalSiegeMoraleRules.PsychologicalIsolation(1f, 1f), Eps);
            // どちらか一方では孤立しきらない：(0.5+0)*0.5 + 0 = 0.25
            Assert.AreEqual(0.25f, PsychologicalSiegeMoraleRules.PsychologicalIsolation(0.5f, 0f), Eps);
            // 味方なし＝0
            Assert.AreEqual(0f, PsychologicalSiegeMoraleRules.PsychologicalIsolation(0f, 0f), Eps);
        }

        /// <summary>士気崩壊の加速＝物理包囲×心理孤立。両方揃うと相乗で一気に崩れる（四面楚歌の核）。</summary>
        [Test]
        public void MoraleCollapseAcceleration_物理と心理の相乗で跳ねる()
        {
            // 物理だけ（心理0）は緩やか：(1+0)*0.5*0.4 + 0 = 0.2
            float physOnly = PsychologicalSiegeMoraleRules.MoraleCollapseAcceleration(1f, 0f);
            Assert.AreEqual(0.2f, physOnly, Eps);
            // 心理だけ（物理0）も緩やか：同じ0.2
            float psychOnly = PsychologicalSiegeMoraleRules.MoraleCollapseAcceleration(0f, 1f);
            Assert.AreEqual(0.2f, psychOnly, Eps);
            // 両方揃うと跳ねる：1*0.4 + 1*0.6 = 1.0（緩やかな和の倍以上）
            float both = PsychologicalSiegeMoraleRules.MoraleCollapseAcceleration(1f, 1f);
            Assert.AreEqual(1f, both, Eps);
            Assert.Greater(both, physOnly + psychOnly - 0.01f);
        }

        /// <summary>絶望の伝播＝孤立度に比例して時間で広がる（敗北主義の蔓延）。</summary>
        [Test]
        public void DespairContagion_孤立に比例して伝播()
        {
            // 孤立1・dt1 = 0.15
            Assert.AreEqual(0.15f, PsychologicalSiegeMoraleRules.DespairContagion(1f, 1f), Eps);
            // 孤立半分なら半分
            Assert.AreEqual(0.075f, PsychologicalSiegeMoraleRules.DespairContagion(0.5f, 1f), Eps);
            // 孤立ゼロは伝播なし
            Assert.AreEqual(0f, PsychologicalSiegeMoraleRules.DespairContagion(0f, 1f), Eps);
        }

        /// <summary>敵の心理戦（楚歌）＝発信×脆弱性×増幅で孤立感を煽る。</summary>
        [Test]
        public void EnemyPsyOpEffect_脆弱な相手ほど効く()
        {
            // 発信0.5・脆弱性0.4 = 0.5*0.4*1.5 = 0.3
            Assert.AreEqual(0.3f, PsychologicalSiegeMoraleRules.EnemyPsyOpEffect(0.5f, 0.4f), Eps);
            // 全開＝1.5だが1にクランプ
            Assert.AreEqual(1f, PsychologicalSiegeMoraleRules.EnemyPsyOpEffect(1f, 1f), Eps);
            // 脆弱性ゼロ（鉄の結束）＝効かない
            Assert.AreEqual(0f, PsychologicalSiegeMoraleRules.EnemyPsyOpEffect(1f, 0f), Eps);
        }

        /// <summary>戦意の侵食＝崩壊加速度に比例して時間で削れる（自壊）。</summary>
        [Test]
        public void WillToFightErosion_崩壊加速で戦意が削れる()
        {
            // 加速度1・dt1 = 0.2
            Assert.AreEqual(0.2f, PsychologicalSiegeMoraleRules.WillToFightErosion(1f, 1f), Eps);
            // 加速度半分なら半分
            Assert.AreEqual(0.1f, PsychologicalSiegeMoraleRules.WillToFightErosion(0.5f, 1f), Eps);
            // 加速度ゼロは侵食なし
            Assert.AreEqual(0f, PsychologicalSiegeMoraleRules.WillToFightErosion(0f, 1f), Eps);
        }

        /// <summary>玉砕か潰走か＝包囲下で指導者のカリスマが分ける（項羽は奮戦・兵は逃散）。</summary>
        [Test]
        public void LastStandOrRout_カリスマで玉砕か潰走か()
        {
            // 完全包囲＋高カリスマ＝玉砕（+1）
            Assert.AreEqual(1f, PsychologicalSiegeMoraleRules.LastStandOrRout(1f, 1f), Eps);
            // 完全包囲＋カリスマなし＝潰走（−1）
            Assert.AreEqual(-1f, PsychologicalSiegeMoraleRules.LastStandOrRout(1f, 0f), Eps);
            // 完全包囲＋中庸カリスマ＝拮抗（0）：0.5*1 - 0.5*1 = 0
            Assert.AreEqual(0f, PsychologicalSiegeMoraleRules.LastStandOrRout(1f, 0.5f), Eps);
        }

        /// <summary>四面楚歌判定＝物理＋心理の崩壊加速度が閾値以上。物理だけでは成立しない。</summary>
        [Test]
        public void IsFourSidedSiege_物理と心理が揃って成立()
        {
            // 物理＋心理が揃う（加速度1.0 ≥ 0.5）＝四面楚歌
            Assert.IsTrue(PsychologicalSiegeMoraleRules.IsFourSidedSiege(1f, 1f));
            // 物理だけ（加速度0.2 < 0.5）＝成立しない＝逃げ道はなくとも心が折れていない
            Assert.IsFalse(PsychologicalSiegeMoraleRules.IsFourSidedSiege(1f, 0f));
            // 心理だけ（加速度0.2 < 0.5）＝成立しない
            Assert.IsFalse(PsychologicalSiegeMoraleRules.IsFourSidedSiege(0f, 1f));
        }
    }
}
