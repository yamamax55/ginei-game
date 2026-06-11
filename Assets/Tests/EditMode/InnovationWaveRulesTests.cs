using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 革新の波（コンドラチェフ循環・SCHU-4 #1591）の純ロジックテスト。
    /// フェーズ判定・相循環・不況がクラスターを準備・普及S字・飽和の後退・長波の谷を担保する。
    /// </summary>
    public class InnovationWaveRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>波位置が閾値（0.3/0.6/0.8）で 上昇→繁栄→後退→不況 へ切り替わる。</summary>
        [Test]
        public void PhaseOf_閾値で4フェーズへ写す()
        {
            Assert.AreEqual(KondratievPhase.上昇, InnovationWaveRules.PhaseOf(0.1f));
            Assert.AreEqual(KondratievPhase.上昇, InnovationWaveRules.PhaseOf(0.29f));
            Assert.AreEqual(KondratievPhase.繁栄, InnovationWaveRules.PhaseOf(0.3f));
            Assert.AreEqual(KondratievPhase.繁栄, InnovationWaveRules.PhaseOf(0.59f));
            Assert.AreEqual(KondratievPhase.後退, InnovationWaveRules.PhaseOf(0.6f));
            Assert.AreEqual(KondratievPhase.後退, InnovationWaveRules.PhaseOf(0.79f));
            Assert.AreEqual(KondratievPhase.不況, InnovationWaveRules.PhaseOf(0.8f));
            Assert.AreEqual(KondratievPhase.不況, InnovationWaveRules.PhaseOf(1f));
        }

        /// <summary>相は 上昇→繁栄→後退→不況→上昇 と決定論で巡る（不況の次は上昇＝次のクラスターが芽吹く）。</summary>
        [Test]
        public void NextPhase_不況の次は上昇で循環する()
        {
            Assert.AreEqual(KondratievPhase.繁栄, InnovationWaveRules.NextPhase(KondratievPhase.上昇));
            Assert.AreEqual(KondratievPhase.後退, InnovationWaveRules.NextPhase(KondratievPhase.繁栄));
            Assert.AreEqual(KondratievPhase.不況, InnovationWaveRules.NextPhase(KondratievPhase.後退));
            Assert.AreEqual(KondratievPhase.上昇, InnovationWaveRules.NextPhase(KondratievPhase.不況));
        }

        /// <summary>不況の圧力がクラスター形成の主因＝同じ科学ストック/起業家でも不況が深いほど大きな束になる。</summary>
        [Test]
        public void ClusterFormation_不況が革新を準備する()
        {
            // 重み：不況0.5・科学0.3・起業0.2
            // 不況なし：0.5*0 + 0.3*0.6 + 0.2*0.5 = 0.28
            float noDepression = InnovationWaveRules.ClusterFormation(0.6f, 0.5f, 0f);
            Assert.AreEqual(0.28f, noDepression, Eps);
            // 深い不況：0.5*1 + 0.3*0.6 + 0.2*0.5 = 0.78
            float deepDepression = InnovationWaveRules.ClusterFormation(0.6f, 0.5f, 1f);
            Assert.AreEqual(0.78f, deepDepression, Eps);
            // 不況がクラスターを大きくする＝シュンペーターの逆説
            Assert.Greater(deepDepression, noDepression);
        }

        /// <summary>普及はS字＝残余に比例して広がり、進むほど未開拓が減って立ち上がりが鈍る（単調増・1でクランプ）。</summary>
        [Test]
        public void DiffusionTick_S字で普及が広がる()
        {
            float a = 0.1f;
            float prevGain = 1f;
            // 強いクラスターで普及を進めると単調に増え、立ち上がり量はやがて飽和で逓減する
            for (int i = 0; i < 40; i++)
            {
                float next = InnovationWaveRules.DiffusionTick(a, 1f, 0.2f);
                Assert.GreaterOrEqual(next, a);            // 単調増
                Assert.LessOrEqual(next, 1f);              // 1でクランプ
                a = next;
            }
            Assert.Greater(a, 0.7f); // 十分に普及している
            Assert.LessOrEqual(a, 1f);
            // 中盤（残余大）は終盤（残余小）より一歩が大きい＝S字の鈍化
            float midStep = InnovationWaveRules.DiffusionTick(0.5f, 1f, 0.2f) - 0.5f;
            float lateStep = InnovationWaveRules.DiffusionTick(0.95f, 1f, 0.2f) - 0.95f;
            Assert.Greater(midStep, lateStep);
        }

        /// <summary>飽和抵抗は普及度の2乗（×1.0）＝終盤で急に効き、繁栄を後退へ転じさせる。</summary>
        [Test]
        public void SaturationDrag_飽和で成長が鈍る()
        {
            Assert.AreEqual(0f, InnovationWaveRules.SaturationDrag(0f), Eps);
            Assert.AreEqual(0.25f, InnovationWaveRules.SaturationDrag(0.5f), Eps); // 0.5^2*1.0
            Assert.AreEqual(1f, InnovationWaveRules.SaturationDrag(1f), Eps);      // 1^2*1.0
            // 2乗ゆえ低普及では小さく高普及で急に効く
            Assert.Greater(InnovationWaveRules.SaturationDrag(0.9f), InnovationWaveRules.SaturationDrag(0.5f));
        }

        /// <summary>波の振幅はクラスター強度に比例＝大きな革新ほど大きな波（基準0.2＋強度×0.8）。</summary>
        [Test]
        public void WaveAmplitude_強い革新ほど大きな波()
        {
            Assert.AreEqual(0.2f, InnovationWaveRules.WaveAmplitude(0f), Eps);   // 基準振幅
            Assert.AreEqual(0.6f, InnovationWaveRules.WaveAmplitude(0.5f), Eps); // 0.2+0.5*0.8
            Assert.AreEqual(1f, InnovationWaveRules.WaveAmplitude(1f), Eps);     // 0.2+0.8=1.0
            Assert.Greater(InnovationWaveRules.WaveAmplitude(1f), InnovationWaveRules.WaveAmplitude(0.2f));
        }

        /// <summary>景気の勢いは繁栄で最大・不況で最小、後退は飽和抵抗で削られる。</summary>
        [Test]
        public void EconomicMomentum_繁栄で最大不況で最小()
        {
            float boom = InnovationWaveRules.EconomicMomentum(KondratievPhase.繁栄, 0.8f);
            float depression = InnovationWaveRules.EconomicMomentum(KondratievPhase.不況, 0.8f);
            Assert.Greater(boom, depression);
            // 繁栄：0.8+0.2*0.8=0.96
            Assert.AreEqual(0.96f, boom, Eps);
            // 後退は飽和（普及高）で削られる：普及0.9のdrag=0.81 → 0.5*(1-0.81)=0.095
            float recession = InnovationWaveRules.EconomicMomentum(KondratievPhase.後退, 0.9f);
            Assert.AreEqual(0.095f, recession, Eps);
        }

        /// <summary>波位置は勢いで前進し、1.0を超えたら谷へ巻き戻る＝長波の循環。</summary>
        [Test]
        public void WavePositionTick_勢いで進み一周で巻き戻る()
        {
            // 0.5 + 進行率0.5*勢い1.0*dt0.2 = 0.5+0.1 = 0.6
            Assert.AreEqual(0.6f, InnovationWaveRules.WavePositionTick(0.5f, 1f, 0.2f), Eps);
            // 1.0手前から大きく進むと巻き戻る：0.95 + 0.5*1*0.2=1.05 → -1 = 0.05
            float wrapped = InnovationWaveRules.WavePositionTick(0.95f, 1f, 0.2f);
            Assert.AreEqual(0.05f, wrapped, Eps);
        }

        /// <summary>長波の谷（不況閾値0.8以上）＝次の革新クラスターの苗床を判定する。</summary>
        [Test]
        public void IsLongWaveTrough_不況の底を谷とみなす()
        {
            Assert.IsFalse(InnovationWaveRules.IsLongWaveTrough(0.5f)); // 既定閾値0.8
            Assert.IsFalse(InnovationWaveRules.IsLongWaveTrough(0.79f));
            Assert.IsTrue(InnovationWaveRules.IsLongWaveTrough(0.8f));
            Assert.IsTrue(InnovationWaveRules.IsLongWaveTrough(0.95f));
            // 明示閾値版
            Assert.IsTrue(InnovationWaveRules.IsLongWaveTrough(0.6f, 0.5f));
        }
    }
}
