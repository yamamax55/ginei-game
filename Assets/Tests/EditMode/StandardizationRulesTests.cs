using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>規格化の外部性（CNTR-3 #1614）の純ロジックを既定 Params で固定検証する。</summary>
    public class StandardizationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>ネットワーク価値は採用度の二乗で非線形に跳ねる（メトカーフ的）。</summary>
        [Test]
        public void NetworkValue_採用度の二乗で跳ねる()
        {
            // valueScale=1.0 → 0.5²=0.25、1.0²=1.0、ゼロは0。
            Assert.AreEqual(0f, StandardizationRules.NetworkValue(0f), Eps);
            Assert.AreEqual(0.25f, StandardizationRules.NetworkValue(0.5f), Eps);
            Assert.AreEqual(1.0f, StandardizationRules.NetworkValue(1f), Eps);
            // 二乗ゆえ採用が倍でも価値は4倍＝非線形（0.25→1.0）。
            Assert.Greater(StandardizationRules.NetworkValue(1f) - StandardizationRules.NetworkValue(0.5f),
                           StandardizationRules.NetworkValue(0.5f) - StandardizationRules.NetworkValue(0.25f));
        }

        /// <summary>輸送コスト低減は採用度に比例して安くなる（上限 maxCostReduction=0.4）。</summary>
        [Test]
        public void TransportCostReduction_採用度に比例して安くなる()
        {
            Assert.AreEqual(0f, StandardizationRules.TransportCostReduction(0f), Eps);
            Assert.AreEqual(0.2f, StandardizationRules.TransportCostReduction(0.5f), Eps);
            Assert.AreEqual(0.4f, StandardizationRules.TransportCostReduction(1f), Eps);
            // クランプ：1超でも上限0.4。
            Assert.AreEqual(0.4f, StandardizationRules.TransportCostReduction(2f), Eps);
        }

        /// <summary>採用は採用を呼ぶ自己強化＝既採用が多いほど（中盤で）速く伸び、互換コストが抵抗する。</summary>
        [Test]
        public void AdoptionTick_自己強化と互換コストの抵抗()
        {
            // 誘因1・互換コスト0・dt1：a=0.5 で gain=baseRate0.5×(2×0.5)×(1−0.5)=0.25 → 0.75。
            float mid = StandardizationRules.AdoptionTick(0.5f, 1f, 0f, 1f);
            Assert.AreEqual(0.75f, mid, Eps);
            // 互換コスト1：抵抗 1−0.6×1=0.4 → gain 0.25×0.4=0.10 → 0.60。
            float resisted = StandardizationRules.AdoptionTick(0.5f, 1f, 1f, 1f);
            Assert.AreEqual(0.60f, resisted, Eps);
            Assert.Less(resisted, mid); // 互換コストが伸びを抑える
        }

        /// <summary>採用ゼロからは外部性が立ち上がらない＝誰も使わない規格は広まらない。</summary>
        [Test]
        public void AdoptionTick_採用ゼロは立ち上がらない()
        {
            // a=0 → 2a=0 で利得0、留まる。
            Assert.AreEqual(0f, StandardizationRules.AdoptionTick(0f, 1f, 0f, 1f), Eps);
        }

        /// <summary>未採用者の誘因はみなが使うほど上がり、採用済みは追加誘因なし。孤立コストは二乗で跳ねる。</summary>
        [Test]
        public void Incentive_と_HoldoutPenalty()
        {
            // 誘因：未採用かつ普及0.8 → 0.8、採用済みは0。
            Assert.AreEqual(0.8f, StandardizationRules.AdoptionIncentive(0.8f, false), Eps);
            Assert.AreEqual(0f, StandardizationRules.AdoptionIncentive(0.8f, true), Eps);
            // 孤立コスト：maxHoldout0.5×0.8²=0.32、採用済みは0。
            Assert.AreEqual(0.32f, StandardizationRules.HoldoutPenalty(0.8f, false), Eps);
            Assert.AreEqual(0f, StandardizationRules.HoldoutPenalty(0.8f, true), Eps);
        }

        /// <summary>臨界採用度（ティッピングポイント）を越えると一気に標準化へ向かう。</summary>
        [Test]
        public void TippingPoint_臨界を境に切り替わる()
        {
            Assert.IsFalse(StandardizationRules.TippingPoint(0.5f, 0.6f)); // 未達
            Assert.IsTrue(StandardizationRules.TippingPoint(0.6f, 0.6f));  // 到達（境界含む）
            Assert.IsTrue(StandardizationRules.TippingPoint(0.9f, 0.6f));  // 超過
        }

        /// <summary>二規格競争は先行する側へ傾く＝勝者総取り。拮抗は0.5。</summary>
        [Test]
        public void StandardWar_先行側へ傾く勝者総取り()
        {
            // 同採用度＝拮抗0.5。
            Assert.AreEqual(0.5f, StandardizationRules.StandardWar(0.5f, 0.5f), Eps);
            // 両者ゼロ＝拮抗0.5。
            Assert.AreEqual(0.5f, StandardizationRules.StandardWar(0f, 0f), Eps);
            // A=0.6,B=0.4：価値0.36/(0.36+0.16)=0.6923…＞0.5＝先行Aへ傾く。
            float war = StandardizationRules.StandardWar(0.6f, 0.4f);
            Assert.AreEqual(0.36f / 0.52f, war, Eps);
            Assert.Greater(war, 0.5f);
            // 二乗ゆえ僅差の採用差でも価値差が拡大＝勝者総取り（採用差0.2でも勝率は0.69へ）。
            Assert.Greater(war, 0.6f);
        }

        /// <summary>入力クランプ＝範囲外でも破綻しない。</summary>
        [Test]
        public void 入力クランプ()
        {
            Assert.AreEqual(1.0f, StandardizationRules.NetworkValue(5f), Eps);   // 採用度クランプ
            Assert.AreEqual(0f, StandardizationRules.NetworkValue(-1f), Eps);
            Assert.AreEqual(0f, StandardizationRules.HoldoutPenalty(-1f, false), Eps);
            // 負dtは伸びず留まる。
            Assert.AreEqual(0.5f, StandardizationRules.AdoptionTick(0.5f, 1f, 0f, -1f), Eps);
        }
    }
}
