using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>創造的破壊（SCHU-1 #1581）の純ロジック検証。既定 Params の具体値で期待値を固定。</summary>
    public class CreativeDestructionRulesTests
    {
        /// <summary>破壊力＝革新×陳腐化×感度。片方0なら破壊なし、両方高いと跳ねる。</summary>
        [Test]
        public void DestructionForce_革新と陳腐化の積()
        {
            // 既定感度1。革新0.8×陳腐化0.5=0.4
            Assert.AreEqual(0.4f, CreativeDestructionRules.DestructionForce(0.8f, 0.5f), 1e-4f);
            // 陳腐化0＝健全な旧産業は淘汰されない
            Assert.AreEqual(0f, CreativeDestructionRules.DestructionForce(1f, 0f), 1e-4f);
            // 革新0＝破壊は起きない
            Assert.AreEqual(0f, CreativeDestructionRules.DestructionForce(0f, 1f), 1e-4f);
        }

        /// <summary>旧産業シェアは破壊力×dt×現シェアぶん時間で萎縮する（古いものが食われる）。</summary>
        [Test]
        public void IncumbentDecayTick_旧産業が萎縮する()
        {
            // share0.8・破壊力0.5・dt1 → loss=0.8*0.5*1=0.4 → 0.4
            Assert.AreEqual(0.4f, CreativeDestructionRules.IncumbentDecayTick(0.8f, 0.5f, 1f), 1e-4f);
            // 破壊力0なら不変
            Assert.AreEqual(0.8f, CreativeDestructionRules.IncumbentDecayTick(0.8f, 0f, 1f), 1e-4f);
            // 0未満にはならない
            Assert.AreEqual(0f, CreativeDestructionRules.IncumbentDecayTick(0.2f, 1f, 5f), 1e-4f);
        }

        /// <summary>創造の成長＝革新×吸収能力。受け止める器が無ければ新産業は育たない。</summary>
        [Test]
        public void CreationGain_吸収能力で受け止める()
        {
            // 革新0.6×吸収0.5=0.3
            Assert.AreEqual(0.3f, CreativeDestructionRules.CreationGain(0.6f, 0.5f), 1e-4f);
            // 吸収能力0＝革新があっても育たない
            Assert.AreEqual(0f, CreativeDestructionRules.CreationGain(1f, 0f), 1e-4f);
        }

        /// <summary>純成長＝創造−破壊（嵐の収支）。破壊が勝てば負成長。</summary>
        [Test]
        public void NetGrowth_創造マイナス破壊()
        {
            Assert.AreEqual(0.2f, CreativeDestructionRules.NetGrowth(0.5f, 0.3f), 1e-4f);
            Assert.AreEqual(-0.4f, CreativeDestructionRules.NetGrowth(0.2f, 0.6f), 1e-4f);
            // −1..1 にクランプ
            Assert.AreEqual(-1f, CreativeDestructionRules.NetGrowth(0f, 1f), 1e-4f);
        }

        /// <summary>置換ショック＝破壊力×(1−労働移動性)。流動的なほど和らぐ。</summary>
        [Test]
        public void DisplacementShock_労働移動性が和らげる()
        {
            // 破壊力0.6×(1−0.25)=0.45
            Assert.AreEqual(0.45f, CreativeDestructionRules.DisplacementShock(0.6f, 0.25f), 1e-4f);
            // 完全に流動的なら衝撃ゼロ
            Assert.AreEqual(0f, CreativeDestructionRules.DisplacementShock(1f, 1f), 1e-4f);
            // 硬直的なら破壊力がそのまま
            Assert.AreEqual(0.6f, CreativeDestructionRules.DisplacementShock(0.6f, 0f), 1e-4f);
        }

        /// <summary>適応の遅れ＝ショック×(1−再訓練)。再訓練が摩擦を縮める。</summary>
        [Test]
        public void AdaptationLag_再訓練が遅れを縮める()
        {
            // ショック0.5×(1−0.4)=0.3
            Assert.AreEqual(0.3f, CreativeDestructionRules.AdaptationLag(0.5f, 0.4f), 1e-4f);
            // 再訓練万全なら遅れゼロ
            Assert.AreEqual(0f, CreativeDestructionRules.AdaptationLag(0.8f, 1f), 1e-4f);
        }

        /// <summary>シュンペーターのレント＝革新×模倣の遅さ×感度0.5。模倣が速いと利潤は即消える。</summary>
        [Test]
        public void SchumpeterianRent_束の間の超過利潤()
        {
            // 革新0.8×模倣遅さ0.5×感度0.5=0.2
            Assert.AreEqual(0.2f, CreativeDestructionRules.SchumpeterianRent(0.8f, 0.5f), 1e-4f);
            // 模倣が即座（delay0）なら利潤ゼロ
            Assert.AreEqual(0f, CreativeDestructionRules.SchumpeterianRent(1f, 0f), 1e-4f);
        }

        /// <summary>ディスラプション判定＝破壊力が閾値（既定0.5）超でtrue。</summary>
        [Test]
        public void IsDisruption_閾値で破壊的革新を分ける()
        {
            Assert.IsTrue(CreativeDestructionRules.IsDisruption(0.6f));   // 既定閾値0.5超
            Assert.IsFalse(CreativeDestructionRules.IsDisruption(0.5f));  // 等しいは超えない
            Assert.IsFalse(CreativeDestructionRules.IsDisruption(0.3f));  // 漸進的改良
            // 明示閾値
            Assert.IsTrue(CreativeDestructionRules.IsDisruption(0.3f, 0.2f));
        }
    }
}
