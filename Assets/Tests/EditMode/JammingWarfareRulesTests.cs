using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>電子妨害戦（ジャミング）純ロジックの EditMode テスト。既定 Params で期待値を固定。</summary>
    public class JammingWarfareRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 妨害出力_既定距離減衰で近接度に比例して減衰する()
        {
            // emitter=0.8, proximity=0.5, falloff=1: 0.8 * 0.5/(0.5 + 1*0.5) = 0.8*0.5 = 0.4
            Assert.AreEqual(0.4f, JammingWarfareRules.JammingPower(0.8f, 0.5f), Eps);
            // 至近(proximity=1)は減衰なし＝出力そのまま
            Assert.AreEqual(0.8f, JammingWarfareRules.JammingPower(0.8f, 1f), Eps);
            // 遠方(proximity=0)は falloff=1 でゼロ
            Assert.AreEqual(0f, JammingWarfareRules.JammingPower(0.8f, 0f), Eps);
        }

        [Test]
        public void 妨害出力_距離減衰ゼロなら出力一定()
        {
            var p = new JammingWarfareParams(1f, 1f, 1f, 1f, 1f, 0f, 0.3f);
            // falloff=0: denom = prox + 0 = prox, falloff係数 = prox/prox = 1（prox>0）
            Assert.AreEqual(0.6f, JammingWarfareRules.JammingPower(0.6f, 0.3f, p), Eps);
            // prox=0 かつ falloff=0 は denom=0 → ゼロ扱い
            Assert.AreEqual(0f, JammingWarfareRules.JammingPower(0.6f, 0f, p), Eps);
        }

        [Test]
        public void センサー劣化_対妨害装備で軽減される()
        {
            // jp=0.4, hardening=0.25: 0.4*(1-0.25) = 0.3
            Assert.AreEqual(0.3f, JammingWarfareRules.SensorDegradation(0.4f, 0.25f), Eps);
            // 完全耐妨害(hardening=1)なら劣化ゼロ
            Assert.AreEqual(0f, JammingWarfareRules.SensorDegradation(0.4f, 1f), Eps);
        }

        [Test]
        public void 命中率低下_センサー劣化に等しい既定重み()
        {
            // degr=0.3 * accuracyWeight=1.0 = 0.3
            Assert.AreEqual(0.3f, JammingWarfareRules.AccuracyReduction(0.3f), Eps);
        }

        [Test]
        public void 誘導兵器撹乱_シーカー性能で逸れにくくなる()
        {
            // jp=0.4, seeker=0.5: 0.4*(1-0.5) = 0.2
            Assert.AreEqual(0.2f, JammingWarfareRules.GuidanceDisruption(0.4f, 0.5f), Eps);
        }

        [Test]
        public void 通信混乱_冗長性で混乱しにくくなる()
        {
            // jp=0.4, redundancy=0.5: 0.4*(1-0.5) = 0.2
            Assert.AreEqual(0.2f, JammingWarfareRules.CommsDisruption(0.4f, 0.5f), Eps);
            // 完全冗長(redundancy=1)なら混乱ゼロ
            Assert.AreEqual(0f, JammingWarfareRules.CommsDisruption(0.4f, 1f), Eps);
        }

        [Test]
        public void 対抗妨害_自軍と敵の差で優劣を符号付きで返す()
        {
            // own=0.7, enemy=0.3: 0.4（自軍優勢）
            Assert.AreEqual(0.4f, JammingWarfareRules.CounterJamming(0.7f, 0.3f), Eps);
            // own=0.2, enemy=0.9: -0.7（敵優勢）
            Assert.AreEqual(-0.7f, JammingWarfareRules.CounterJamming(0.2f, 0.9f), Eps);
            // 拮抗
            Assert.AreEqual(0f, JammingWarfareRules.CounterJamming(0.5f, 0.5f), Eps);
        }

        [Test]
        public void 妨害有効判定_既定閾値で境界を含む()
        {
            // 既定閾値 0.3：境界は有効(true)
            Assert.IsTrue(JammingWarfareRules.IsJammingEffective(0.3f));
            Assert.IsFalse(JammingWarfareRules.IsJammingEffective(0.29f));
            Assert.IsTrue(JammingWarfareRules.IsJammingEffective(0.5f));
        }

        [Test]
        public void 物語_妨害で敵の命中と連携を乱すが自位置を暴露し対抗妨害と綱引きになる()
        {
            var p = JammingWarfareParams.Default;

            // 至近(proximity=0.5)から強い放射源(0.8)で妨害＝実効出力0.4
            float jp = JammingWarfareRules.JammingPower(0.8f, 0.5f, p);
            Assert.AreEqual(0.4f, jp, Eps);

            // 敵の対妨害装備は薄い(0.2)＝センサーは大きく劣化
            float degr = JammingWarfareRules.SensorDegradation(jp, 0.2f, p);
            Assert.AreEqual(0.32f, degr, Eps); // 0.4*0.8

            // 妨害は効いている（劣化0.32 >= 閾値0.3）
            Assert.IsTrue(JammingWarfareRules.IsJammingEffective(degr, p.effectiveThreshold));

            // 敵の命中率が落ち、通信連携も乱れる
            float acc = JammingWarfareRules.AccuracyReduction(degr, p);
            float comms = JammingWarfareRules.CommsDisruption(jp, 0.3f, p); // redundancy=0.3
            Assert.AreEqual(0.32f, acc, Eps);
            Assert.AreEqual(0.28f, comms, Eps); // 0.4*0.7
            Assert.Greater(acc, 0f);
            Assert.Greater(comms, 0f);

            // だが妨害は自分の位置を暴露する（撃てば己も目立つ）
            float exposure = JammingWarfareRules.EmissionExposure(jp, p);
            Assert.AreEqual(0.4f, exposure, Eps);
            Assert.Greater(exposure, 0f);

            // 妨害合戦の綱引き：敵の対抗妨害(0.6)が自軍(0.4)を上回れば電子戦は劣勢
            float edge = JammingWarfareRules.CounterJamming(0.4f, 0.6f);
            Assert.AreEqual(-0.2f, edge, Eps);
            Assert.Less(edge, 0f);
        }
    }
}
