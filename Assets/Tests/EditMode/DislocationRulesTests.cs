using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>心理的瓦解（ディスロケーション・間接アプローチ）の純ロジックの担保（#1344）。</summary>
    public class DislocationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>脅威の方向数＝0で0、増えるほど飽和的に上がる（1方向は対処でき多方向で崩れる）。</summary>
        [Test]
        public void ThreatDirectionFactor_方向が増えるほど飽和的に上がる()
        {
            // 方向0＝脅威なし
            Assert.AreEqual(0f, DislocationRules.ThreatDirectionFactor(0), Eps);
            // 2方向：x=0.35*2=0.7、0.7/1.7=0.41176
            Assert.AreEqual(0.41176f, DislocationRules.ThreatDirectionFactor(2), Eps);
            // 方向が増えるほど単調増だが飽和する（1未満）
            float four = DislocationRules.ThreatDirectionFactor(4);
            Assert.Greater(four, DislocationRules.ThreatDirectionFactor(2));
            Assert.Less(four, 1f);
        }

        /// <summary>心理的瓦解度＝多方向の脅威を土台に不意・退路遮断が上乗せ。</summary>
        [Test]
        public void PsychologicalDislocation_脅威に不意と退路遮断が上乗せ()
        {
            // 脅威なし・不意なし・退路あり＝0
            Assert.AreEqual(0f, DislocationRules.PsychologicalDislocation(0f, 0f, 0f), Eps);
            // 脅威0.41176・不意全開・退路完全遮断：room=0.58824、added=0.58824*(0.4+0.5)=0.52941、計0.94118
            Assert.AreEqual(0.94118f, DislocationRules.PsychologicalDislocation(0.41176f, 1f, 1f), Eps);
            // 脅威のみ（不意・退路遮断なし）はそのまま脅威値
            Assert.AreEqual(0.5f, DislocationRules.PsychologicalDislocation(0.5f, 0f, 0f), Eps);
        }

        /// <summary>士気低下の加速倍率＝瓦解度0で等倍・瓦解1で最大倍率（実効値パターン）。</summary>
        [Test]
        public void MoraleCollapseAcceleration_瓦解で士気低下を加速する倍率()
        {
            // 瓦解ゼロ＝等倍（基準drainを変えない）
            Assert.AreEqual(1f, DislocationRules.MoraleCollapseAcceleration(0f, 10f), Eps);
            // 瓦解半分＝Lerp(1,2.5,0.5)=1.75
            Assert.AreEqual(1.75f, DislocationRules.MoraleCollapseAcceleration(0.5f, 10f), Eps);
            // 瓦解全開＝最大2.5
            Assert.AreEqual(2.5f, DislocationRules.MoraleCollapseAcceleration(1f, 10f), Eps);
        }

        /// <summary>物理損害より心理瓦解が先に効く度合い＝瓦解優位ほど1へ。</summary>
        [Test]
        public void PhysicalVsPsychological_瓦解優位ほど心理が先に効く()
        {
            // 物理0.2・心理0.8＝0.8/1.0=0.8（心理が優位）
            Assert.AreEqual(0.8f, DislocationRules.PhysicalVsPsychological(0.2f, 0.8f), Eps);
            // 拮抗＝0.5
            Assert.AreEqual(0.5f, DislocationRules.PhysicalVsPsychological(0.5f, 0.5f), Eps);
            // 両者ゼロ＝0（ゼロ除算回避）
            Assert.AreEqual(0f, DislocationRules.PhysicalVsPsychological(0f, 0f), Eps);
        }

        /// <summary>予期と実際の軸のズレ＝真逆で最大・同方向で0。側背面から来るほど動揺。</summary>
        [Test]
        public void ExpectationUpset_軸のズレが大きいほど動揺()
        {
            // 同方向＝0
            Assert.AreEqual(0f, DislocationRules.ExpectationUpset(0f, 0f), Eps);
            // 直角＝0.5
            Assert.AreEqual(0.5f, DislocationRules.ExpectationUpset(0f, 90f), Eps);
            // 真逆＝1（正面と思った敵が背後から）
            Assert.AreEqual(1f, DislocationRules.ExpectationUpset(0f, 180f), Eps);
            // 270度は反対回りで90度＝0.5（円環の最短角）
            Assert.AreEqual(0.5f, DislocationRules.ExpectationUpset(0f, 270f), Eps);
        }

        /// <summary>立て直し抵抗力＝指揮の結束が瓦解を上回れば立て直せる。</summary>
        [Test]
        public void RecoveryResistance_結束が瓦解を打ち消す()
        {
            // 結束1・瓦解0.5：1*0.7 - 0.5*0.3 = 0.7-0.15=0.55
            Assert.AreEqual(0.55f, DislocationRules.RecoveryResistance(1f, 0.5f), Eps);
            // 結束なし・瓦解全開＝立て直せない（下限0）
            Assert.AreEqual(0f, DislocationRules.RecoveryResistance(0f, 1f), Eps);
            // 結束が高いほど抵抗力が上がる
            Assert.Greater(DislocationRules.RecoveryResistance(1f, 0.5f),
                           DislocationRules.RecoveryResistance(0.5f, 0.5f));
        }

        /// <summary>敗走誘発＝瓦解度が士気の床を割ると true。</summary>
        [Test]
        public void RoutTrigger_瓦解が士気の床を割ると敗走()
        {
            // 瓦解0.8 > 床0.5 ＝敗走
            Assert.IsTrue(DislocationRules.RoutTrigger(0.8f, 0.5f));
            // 瓦解0.3 ≤ 床0.5 ＝持ちこたえる
            Assert.IsFalse(DislocationRules.RoutTrigger(0.3f, 0.5f));
        }

        /// <summary>瓦解判定＝瓦解度が閾値以上で心理的に瓦解（戦わずして無力化）。</summary>
        [Test]
        public void IsDislocated_閾値超で瓦解()
        {
            // 既定閾値0.5ちょうどで成立
            Assert.IsTrue(DislocationRules.IsDislocated(0.5f));
            // 閾値未満は成立しない
            Assert.IsFalse(DislocationRules.IsDislocated(0.4f));
        }

        /// <summary>
        /// 物語：複数方向からの不意の脅威に退路を断たれた敵は心理が瓦解し、物理損害が乏しくとも
        /// 士気低下が加速して敗走へ至り、戦わずして無力化される＝間接アプローチのディスロケーション。
        /// </summary>
        [Test]
        public void 物語_多方向の不意と退路遮断が士気崩壊を加速し敗走を誘発する()
        {
            // 三方向から脅かされ（方向係数が立つ）
            float threat = DislocationRules.ThreatDirectionFactor(3);
            Assert.Greater(threat, 0.4f);

            // 不意を突かれ退路を断たれて心理的に瓦解
            float dislocation = DislocationRules.PsychologicalDislocation(threat, 0.9f, 1f);
            Assert.Greater(dislocation, 0.8f);

            // 物理損害は乏しい（10%）が心理瓦解が圧倒的に先に効く
            float psychLead = DislocationRules.PhysicalVsPsychological(0.1f, dislocation);
            Assert.Greater(psychLead, 0.8f);

            // 士気低下が大きく加速（ほぼ最大倍率）
            float accel = DislocationRules.MoraleCollapseAcceleration(dislocation, 10f);
            Assert.Greater(accel, 2.0f);

            // 心理的に瓦解と判定され、士気の床を割って敗走
            Assert.IsTrue(DislocationRules.IsDislocated(dislocation));
            Assert.IsTrue(DislocationRules.RoutTrigger(dislocation, 0.5f));

            // 対照：結束の固い指揮なら同じ瓦解からでも立て直せる（残存抵抗力が出る）
            float resilient = DislocationRules.RecoveryResistance(1f, dislocation);
            float brittle = DislocationRules.RecoveryResistance(0.2f, dislocation);
            Assert.Greater(resilient, brittle);
        }
    }
}
