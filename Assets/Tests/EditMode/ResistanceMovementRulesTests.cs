using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>占領下抵抗運動（地下組織の成長・秘匿・支援・士気・存続）の純ロジックを EditMode で担保する。</summary>
    public class ResistanceMovementRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        /// <summary>細胞成長：訴求×共感×秘匿で太り、どれか欠ければ太らない。</summary>
        [Test]
        public void CellGrowth_NeedsAppealSympathyAndSecrecy()
        {
            // 0.7*0.6*0.5 = 0.21 ; *0.6 = 0.126
            float g = ResistanceMovementRules.CellGrowth(0.7f, 0.6f, 0.5f);
            Assert.AreEqual(0.126f, g, Eps);

            // 秘匿0＝露見を恐れて人が集まらない
            float noSecrecy = ResistanceMovementRules.CellGrowth(1f, 1f, 0f);
            Assert.AreEqual(0f, noSecrecy, Eps);

            // 住民共感0＝協力者が得られない
            float noSympathy = ResistanceMovementRules.CellGrowth(1f, 0f, 1f);
            Assert.AreEqual(0f, noSympathy, Eps);
        }

        /// <summary>作戦保安：分散秘匿/(秘匿+敵防諜圧)＝相対比で露見しにくさが決まる。</summary>
        [Test]
        public void OperationalSecurity_IsRelativeToCounterIntel()
        {
            // 0.8 / (0.8 + 0.4) = 0.8/1.2 = 0.6667
            float sec = ResistanceMovementRules.OperationalSecurity(0.8f, 0.4f);
            Assert.AreEqual(0.6667f, sec, PowEps);

            // 防諜圧が無ければ保安は最大（0.8/0.8=1）
            float safe = ResistanceMovementRules.OperationalSecurity(0.8f, 0f);
            Assert.AreEqual(1f, safe, Eps);

            // 秘匿が無ければ保安0（分母ガードで0除算しない）
            float exposed = ResistanceMovementRules.OperationalSecurity(0f, 0.5f);
            Assert.AreEqual(0f, exposed, Eps);
        }

        /// <summary>外部後ろ盾：亡命政府×外国支援の幾何平均（どちらか0なら後ろ盾なし）。</summary>
        [Test]
        public void ExternalBacking_IsGeometricMean()
        {
            // sqrt(0.64*0.25) = sqrt(0.16) = 0.4
            float backing = ResistanceMovementRules.ExternalBacking(0.64f, 0.25f);
            Assert.AreEqual(0.4f, backing, PowEps);

            // 外国支援が無ければ後ろ盾なし
            float none = ResistanceMovementRules.ExternalBacking(0.8f, 0f);
            Assert.AreEqual(0f, none, Eps);
        }

        /// <summary>破壊活動：組織力×保安＝安全に動けるほど打撃を出せる。</summary>
        [Test]
        public void SabotageOutput_ScalesWithSecurity()
        {
            // 0.6*0.5 = 0.3
            float sab = ResistanceMovementRules.SabotageOutput(0.6f, 0.5f);
            Assert.AreEqual(0.3f, sab, Eps);

            // 露見必至（保安0）なら活動を出せない
            float silenced = ResistanceMovementRules.SabotageOutput(1f, 0f);
            Assert.AreEqual(0f, silenced, Eps);
        }

        /// <summary>弾圧耐性：保安×住民共感の幾何平均（露見せず民が匿うほど生き延びる）。</summary>
        [Test]
        public void RepressionResilience_IsGeometricMean()
        {
            // sqrt(0.5*0.5) = 0.5
            float res = ResistanceMovementRules.RepressionResilience(0.5f, 0.5f);
            Assert.AreEqual(0.5f, res, PowEps);

            // 民が匿わなければ（共感0）弾圧に耐えられない
            float crushed = ResistanceMovementRules.RepressionResilience(1f, 0f);
            Assert.AreEqual(0f, crushed, Eps);
        }

        /// <summary>組織士気：後ろ盾0.4＋戦果0.3＋希望0.3の重み付き和。</summary>
        [Test]
        public void OrganizationMorale_BlendsBackingSabotageAndHope()
        {
            // 0.5*0.4 + 0.4*0.3 + 0.6*0.3 = 0.2 + 0.12 + 0.18 = 0.5
            float morale = ResistanceMovementRules.OrganizationMorale(0.5f, 0.4f, 0.6f);
            Assert.AreEqual(0.5f, morale, Eps);

            // すべて0なら士気も0
            float despair = ResistanceMovementRules.OrganizationMorale(0f, 0f, 0f);
            Assert.AreEqual(0f, despair, Eps);

            // すべて満点なら士気満点（配分の和=1）
            float peak = ResistanceMovementRules.OrganizationMorale(1f, 1f, 1f);
            Assert.AreEqual(1f, peak, Eps);
        }

        /// <summary>総合勢力：成長×弾圧耐性×士気の幾何平均（三乗根・どれか崩れると伸びない）。</summary>
        [Test]
        public void MovementStrength_IsGeometricMeanOfThree()
        {
            // 0.8*0.8*0.8 = 0.512 ; cbrt = 0.8
            float s = ResistanceMovementRules.MovementStrength(0.8f, 0.8f, 0.8f);
            Assert.AreEqual(0.8f, s, PowEps);

            // 士気が崩れる（0）と総合勢力も0（三本柱のどれか0で潰れる）
            float collapsed = ResistanceMovementRules.MovementStrength(0.9f, 0.9f, 0f);
            Assert.AreEqual(0f, collapsed, PowEps);
        }

        /// <summary>存続判定：運動勢力×保安が閾値以上＝勢力も保安も要る。</summary>
        [Test]
        public void IsResistanceViable_NeedsStrengthAndSecurity()
        {
            // 0.6*0.5 = 0.3 >= 0.25 ＝存続できる
            Assert.IsTrue(ResistanceMovementRules.IsResistanceViable(0.6f, 0.5f));

            // 勢力はあるが露見気味（0.6*0.3=0.18 < 0.25）＝潰される
            Assert.IsFalse(ResistanceMovementRules.IsResistanceViable(0.6f, 0.3f));

            // 保安はあるが勢力不足（0.2*1=0.2 < 0.25）＝消える
            Assert.IsFalse(ResistanceMovementRules.IsResistanceViable(0.2f, 1f));
        }

        /// <summary>物語：占領初期の脆い地下細胞が、亡命政府の支援と住民共感を得て自立した抵抗運動へ育つ。</summary>
        [Test]
        public void Narrative_FledglingCellMaturesIntoViableMovement()
        {
            // 初期：訴求も共感も秘匿も低く、敵防諜が強い＝存続不能
            float earlyGrowth = ResistanceMovementRules.CellGrowth(0.2f, 0.2f, 0.3f);
            float earlySec = ResistanceMovementRules.OperationalSecurity(0.2f, 0.8f);
            float earlyBacking = ResistanceMovementRules.ExternalBacking(0f, 0f);
            float earlySab = ResistanceMovementRules.SabotageOutput(0.2f, earlySec);
            float earlyRes = ResistanceMovementRules.RepressionResilience(earlySec, 0.2f);
            float earlyMorale = ResistanceMovementRules.OrganizationMorale(earlyBacking, earlySab, 0.1f);
            float earlyStrength = ResistanceMovementRules.MovementStrength(earlyGrowth, earlyRes, earlyMorale);
            Assert.IsFalse(ResistanceMovementRules.IsResistanceViable(earlyStrength, earlySec),
                "占領初期の孤立した地下細胞は存続できない");

            // 成熟：分散秘匿が進み防諜が弱まり、亡命政府＋外国支援＋住民共感＋解放の希望がそろう
            float lateGrowth = ResistanceMovementRules.CellGrowth(0.8f, 0.8f, 0.8f);
            float lateSec = ResistanceMovementRules.OperationalSecurity(0.85f, 0.25f);
            float lateBacking = ResistanceMovementRules.ExternalBacking(0.8f, 0.7f);
            float lateSab = ResistanceMovementRules.SabotageOutput(0.7f, lateSec);
            float lateRes = ResistanceMovementRules.RepressionResilience(lateSec, 0.8f);
            float lateMorale = ResistanceMovementRules.OrganizationMorale(lateBacking, lateSab, 0.7f);
            float lateStrength = ResistanceMovementRules.MovementStrength(lateGrowth, lateRes, lateMorale);
            Assert.IsTrue(ResistanceMovementRules.IsResistanceViable(lateStrength, lateSec),
                "支援と共感を得た成熟した抵抗運動は存続できる");

            // 成熟期はあらゆる指標で初期を上回る
            Assert.Greater(lateStrength, earlyStrength);
            Assert.Greater(lateSec, earlySec);
            Assert.Greater(lateMorale, earlyMorale);
        }

        /// <summary>既定 Params の具体値を固定（回帰防止）。</summary>
        [Test]
        public void Default_HasExpectedValues()
        {
            var p = ResistanceMovementParams.Default;
            Assert.AreEqual(0.6f, p.growthWeight, Eps);
            Assert.AreEqual(1.0f, p.counterIntelWeight, Eps);
            Assert.AreEqual(0.05f, p.securityFloor, Eps);
            Assert.AreEqual(1.0f, p.backingWeight, Eps);
            Assert.AreEqual(1.0f, p.sabotageWeight, Eps);
            Assert.AreEqual(1.0f, p.resilienceWeight, Eps);
            Assert.AreEqual(0.4f, p.moraleBackingWeight, Eps);
            Assert.AreEqual(0.3f, p.moraleSabotageWeight, Eps);
            Assert.AreEqual(0.3f, p.moraleHopeWeight, Eps);
            Assert.AreEqual(0.25f, ResistanceMovementRules.DefaultViabilityThreshold, Eps);
        }
    }
}
