using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 連合内の隠れた目標乖離（スペイン内戦型・#1398）の純ロジック検証。
    /// 既定 Params（脅威結束0.7・点火0.5・侵食0.6・粛清0.5・分裂0.8）で期待値を固定。
    /// </summary>
    public class AllianceDivergenceRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>戦後の利益相反＝各パートナー目標と共通目標の乖離の平均。</summary>
        [Test]
        public void PostwarInterestConflict_平均乖離を返す()
        {
            // |0.9-0.5| + |0.1-0.5| = 0.4 + 0.4 = 0.8、平均 0.4。
            float c = AllianceDivergenceRules.PostwarInterestConflict(new[] { 0.9f, 0.1f }, 0.5f);
            Assert.AreEqual(0.4f, c, Eps);
        }

        /// <summary>全員が共通目標と一致すれば相反ゼロ。</summary>
        [Test]
        public void PostwarInterestConflict_目標一致でゼロ()
        {
            float c = AllianceDivergenceRules.PostwarInterestConflict(new[] { 0.6f, 0.6f, 0.6f }, 0.6f);
            Assert.AreEqual(0f, c, Eps);
        }

        /// <summary>空・null配列は安全に0（パートナーなしは相反なし）。</summary>
        [Test]
        public void PostwarInterestConflict_空配列安全()
        {
            Assert.AreEqual(0f, AllianceDivergenceRules.PostwarInterestConflict(new float[0], 0.5f), Eps);
            Assert.AreEqual(0f, AllianceDivergenceRules.PostwarInterestConflict(null, 0.5f), Eps);
        }

        /// <summary>脅威下の団結＝強い外敵が戦後対立を圧縮する（敵が弱るとバラける）。</summary>
        [Test]
        public void UnityUnderThreat_強い敵が内部対立を抑える()
        {
            // 強敵: 0.8 * (1 - 1.0*0.7) = 0.8*0.3 = 0.24。
            Assert.AreEqual(0.24f, AllianceDivergenceRules.UnityUnderThreat(1f, 0.8f), Eps);
            // 敵が消えると戦後対立がそのまま表に出る。
            Assert.AreEqual(0.8f, AllianceDivergenceRules.UnityUnderThreat(0f, 0.8f), Eps);
        }

        /// <summary>内部抗争は勝利接近で激化する（戦後が見えると主導権争いが燃える）。</summary>
        [Test]
        public void InternalRivalryTick_勝利接近で激化()
        {
            // target = 0.8*1.0 = 0.8、MoveTowards(0, 0.8, 0.5*1)=0.5。
            float r = AllianceDivergenceRules.InternalRivalryTick(0f, 0.8f, 1f, 1f);
            Assert.AreEqual(0.5f, r, Eps);
        }

        /// <summary>勝利が遠いうちは抗争が育たない（敵が倒れる前は静か）。</summary>
        [Test]
        public void InternalRivalryTick_勝利遠いと据え置き()
        {
            // target = 0.8*0.0 = 0 ≤ current=0.3 ＝ 一方向なので据え置き。
            float r = AllianceDivergenceRules.InternalRivalryTick(0.3f, 0.8f, 0f, 1f);
            Assert.AreEqual(0.3f, r, Eps);
        }

        /// <summary>対外戦の侵食＝内部抗争が戦力を空費する。</summary>
        [Test]
        public void WarEffortDrain_抗争が戦争遂行を蝕む()
        {
            // 0.5 * 0.6 = 0.3。
            Assert.AreEqual(0.3f, AllianceDivergenceRules.WarEffortDrain(0.5f), Eps);
            Assert.AreEqual(0f, AllianceDivergenceRules.WarEffortDrain(0f), Eps);
        }

        /// <summary>先制粛清＝抗争×自派勢力（無力な派は仕掛けられない）。</summary>
        [Test]
        public void PreemptivePurge_抗争と勢力の積()
        {
            // 0.8 * 0.6 * 0.5 = 0.24。
            Assert.AreEqual(0.24f, AllianceDivergenceRules.PreemptivePurge(0.8f, 0.6f), Eps);
            // 勢力ゼロの派は粛清を仕掛けられない。
            Assert.AreEqual(0f, AllianceDivergenceRules.PreemptivePurge(0.9f, 0f), Eps);
        }

        /// <summary>連合の結束＝共通の敵への集中×（1−内部抗争）。</summary>
        [Test]
        public void AllianceCohesion_集中と抗争のせめぎ合い()
        {
            // 0.9 * (1 - 0.4) = 0.54。
            Assert.AreEqual(0.54f, AllianceDivergenceRules.AllianceCohesion(0.9f, 0.4f), Eps);
        }

        /// <summary>分裂リスク＝戦後対立×敵の弱化（敵が強いうちは割れない）。</summary>
        [Test]
        public void CoalitionFractureRisk_敵の弱化で割れる()
        {
            // 0.7 * 0.5 * 0.8 = 0.28。
            Assert.AreEqual(0.28f, AllianceDivergenceRules.CoalitionFractureRisk(0.7f, 0.5f), Eps);
            // 敵がまだ強い＝弱化ゼロなら割れない。
            Assert.AreEqual(0f, AllianceDivergenceRules.CoalitionFractureRisk(0.7f, 0f), Eps);
        }

        /// <summary>連合崩壊判定＝抗争が閾値超かつ結束が低い。</summary>
        [Test]
        public void IsAllianceUnraveling_内から崩れる()
        {
            // 閾値0.5: 抗争0.7>0.5 かつ 結束0.3<0.5 ＝ 崩壊。
            Assert.IsTrue(AllianceDivergenceRules.IsAllianceUnraveling(0.7f, 0.3f, 0.5f));
            // 結束が高ければ崩れない。
            Assert.IsFalse(AllianceDivergenceRules.IsAllianceUnraveling(0.7f, 0.8f, 0.5f));
            // 抗争が低ければ崩れない。
            Assert.IsFalse(AllianceDivergenceRules.IsAllianceUnraveling(0.3f, 0.3f, 0.5f));
        }

        /// <summary>データ struct はクランプして保持する。</summary>
        [Test]
        public void AllianceDivergence_コンストラクタでクランプ()
        {
            var d = new AllianceDivergence(1.5f, -0.2f, 0.5f);
            Assert.AreEqual(1f, d.sharedEnemyPriority, Eps);
            Assert.AreEqual(0f, d.postwarConflict, Eps);
            Assert.AreEqual(0.5f, d.internalRivalry, Eps);
        }
    }
}
