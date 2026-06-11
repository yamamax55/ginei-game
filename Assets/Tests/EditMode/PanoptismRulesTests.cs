using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// パノプティコン係数（#1507）の純ロジックテスト。既定 PanoptismParams の具体値で期待値を固定する。
    /// 見られている感覚／事前抑止／規律の内面化／自己規制／監視コスト／反発／萎縮効果／パノプティコン的支配を担保。
    /// </summary>
    public class PanoptismRulesTests
    {
        /// <summary>見られている感覚＝密度×不確実性の効き。不確実性1で密度満額、0なら半減（確実に分かれば隙を突かれる）。</summary>
        [Test]
        public void PerceivedVisibility_密度に不確実性が効く()
        {
            // 既定 uncertaintyWeight=0.5
            Assert.AreEqual(0.8f, PanoptismRules.PerceivedVisibility(0.8f, 1.0f), 1e-4f); // factor=1.0
            Assert.AreEqual(0.4f, PanoptismRules.PerceivedVisibility(0.8f, 0.0f), 1e-4f); // factor=0.5
            Assert.AreEqual(0.6f, PanoptismRules.PerceivedVisibility(0.8f, 0.5f), 1e-4f); // factor=0.75
            // 入力は 0..1 にクランプ（密度2→1.0・不確実性1.0でfactor=1.0＝最大可視性1.0）
            Assert.AreEqual(1.0f, PanoptismRules.PerceivedVisibility(2f, 1.0f), 1e-4f);
        }

        /// <summary>事前抑止＝見られている感覚×deterrenceScale(0.9)。摘発より前に自制させる。</summary>
        [Test]
        public void DeterrenceEffect_感覚が事前抑止を生む()
        {
            Assert.AreEqual(0.72f, PanoptismRules.DeterrenceEffect(0.8f), 1e-4f);
            Assert.AreEqual(0f, PanoptismRules.DeterrenceEffect(0f), 1e-4f);
        }

        /// <summary>規律の内面化＝監視が続くと焼き付き、監視意識が薄れると緩やかに減衰する。</summary>
        [Test]
        public void DisciplineInternalization_監視の継続で内面化_途切れで減衰()
        {
            // gain=0.2*0.8*1=0.16, loss=0.05*(1-0.8)=0.01 → 0.5+0.15=0.65
            Assert.AreEqual(0.65f, PanoptismRules.DisciplineInternalization(0.5f, 0.8f, 1.0f, 1.0f), 1e-4f);
            // 監視意識ゼロ（perceivedVisibility=0）→ gain=0, loss=0.05 → 0.5-0.05=0.45
            Assert.AreEqual(0.45f, PanoptismRules.DisciplineInternalization(0.5f, 0f, 1.0f, 1.0f), 1e-4f);
            // dt=0 なら不変
            Assert.AreEqual(0.5f, PanoptismRules.DisciplineInternalization(0.5f, 0.8f, 1.0f, 0f), 1e-4f);
        }

        /// <summary>自己規制＝内面化された規律そのもの（監視者不要のパノプティコン完成）。</summary>
        [Test]
        public void SelfRegulation_内面化規律が自己規制になる()
        {
            Assert.AreEqual(0.65f, PanoptismRules.SelfRegulation(0.65f), 1e-4f);
            Assert.AreEqual(1f, PanoptismRules.SelfRegulation(2f), 1e-4f); // クランプ
        }

        /// <summary>監視コスト＝固定の演出コスト(0.1)＋密度比例(0.8)。可能性の演出は安く全員監視は高い。</summary>
        [Test]
        public void SurveillanceCost_演出は安く全員監視は高い()
        {
            Assert.AreEqual(0.9f, PanoptismRules.SurveillanceCost(1.0f), 1e-4f);  // 0.1+0.8
            Assert.AreEqual(0.1f, PanoptismRules.SurveillanceCost(0f), 1e-4f);    // 演出のみ
            Assert.AreEqual(0.5f, PanoptismRules.SurveillanceCost(0.5f), 1e-4f);  // 0.1+0.4
        }

        /// <summary>反発＝閾値(0.6)超過分×プライバシー重視。閾値以下なら反発しない。</summary>
        [Test]
        public void ResistanceAwareness_過度な監視に反発()
        {
            // excess=(0.8-0.6)/0.4=0.5, privacy=1 → 0.5
            Assert.AreEqual(0.5f, PanoptismRules.ResistanceAwareness(0.8f, 1.0f), 1e-4f);
            // 閾値以下は反発なし
            Assert.AreEqual(0f, PanoptismRules.ResistanceAwareness(0.5f, 1.0f), 1e-4f);
            // プライバシーを重んじないなら反発も小さい
            Assert.AreEqual(0.25f, PanoptismRules.ResistanceAwareness(0.8f, 0.5f), 1e-4f);
        }

        /// <summary>萎縮効果＝見られている感覚×異論。監視が異論を口ごもらせる。</summary>
        [Test]
        public void ChillingEffect_監視が異論を萎縮させる()
        {
            Assert.AreEqual(0.48f, PanoptismRules.ChillingEffect(0.8f, 0.6f), 1e-4f);
            Assert.AreEqual(0f, PanoptismRules.ChillingEffect(0f, 0.6f), 1e-4f);
        }

        /// <summary>パノプティコン的支配＝内面化規律が閾値(既定0.7)以上で成立（監視者不要）。</summary>
        [Test]
        public void IsPanopticControl_規律内面化で監視者不要()
        {
            Assert.IsTrue(PanoptismRules.IsPanopticControl(0.75f));   // 既定閾値0.7以上
            Assert.IsFalse(PanoptismRules.IsPanopticControl(0.6f));
            // 明示閾値
            Assert.IsTrue(PanoptismRules.IsPanopticControl(0.5f, 0.5f));
            Assert.IsFalse(PanoptismRules.IsPanopticControl(0.4f, 0.5f));
        }
    }
}
