using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>入植地の成長（集落→都市の発展）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class SettlementGrowthRulesTests
    {
        const float Eps = 1e-4f;
        const float SqrtEps = 1e-3f; // Sqrt を経る箇所のみ緩める

        [Test]
        public void 流入は雇用と生活水準と到達性の積_どれか欠けると細る()
        {
            // 0.8*0.7*0.5*1.0 = 0.28。
            Assert.AreEqual(0.28f, SettlementGrowthRules.Immigration(0.8f, 0.7f, 0.5f), Eps);
            // 到達性0なら流入ゼロ（積）。
            Assert.AreEqual(0f, SettlementGrowthRules.Immigration(0.8f, 0.7f, 0.0f), Eps);
        }

        [Test]
        public void 自然増は出生引く死亡_無人なら増減なし()
        {
            // (0.4-0.1)*1.0 = 0.3。
            Assert.AreEqual(0.3f, SettlementGrowthRules.NaturalGrowth(1000f, 0.4f, 0.1f), Eps);
            // 死亡が出生を上回ると負（衰退）。
            Assert.AreEqual(-0.2f, SettlementGrowthRules.NaturalGrowth(500f, 0.1f, 0.3f), Eps);
            // 人口0なら自然増は0。
            Assert.AreEqual(0f, SettlementGrowthRules.NaturalGrowth(0f, 0.4f, 0.1f), Eps);
        }

        [Test]
        public void 収容力はインフラと食料と土地の積_最小律で頭打ち()
        {
            // 0.8*0.6*0.5 = 0.24。
            Assert.AreEqual(0.24f, SettlementGrowthRules.CarryingCapacity(0.8f, 0.6f, 0.5f), Eps);
            // 食料0なら収容力ゼロ（最小律）。
            Assert.AreEqual(0f, SettlementGrowthRules.CarryingCapacity(0.8f, 0.0f, 0.5f), Eps);
        }

        [Test]
        public void 過密は収容力超過ぶん_以内なら0_収容力0は最大()
        {
            // 1.2/1.0 - 1 = 0.2。
            Assert.AreEqual(0.2f, SettlementGrowthRules.OvercrowdingPenalty(1.2f, 1.0f), Eps);
            // 収容力以内（人口0.5・収容力1.0）なら過密なし。
            Assert.AreEqual(0f, SettlementGrowthRules.OvercrowdingPenalty(0.5f, 1.0f), Eps);
            // 収容力0で人口ありは住めない＝最大1。
            Assert.AreEqual(1f, SettlementGrowthRules.OvercrowdingPenalty(0.5f, 0.0f), Eps);
        }

        [Test]
        public void 成長率は流入足す自然増引く過密()
        {
            // 0.28 + 0.3 - 0.2 = 0.38。
            Assert.AreEqual(0.38f, SettlementGrowthRules.GrowthRate(0.28f, 0.3f, 0.2f), Eps);
            // 過密が流入と自然増を食い潰すと負（衰退）：0.1 + 0.0 - 0.5 = -0.4。
            Assert.AreEqual(-0.4f, SettlementGrowthRules.GrowthRate(0.1f, 0.0f, 0.5f), Eps);
        }

        [Test]
        public void 都市化は人口規模の平方根と経済多様性の積()
        {
            // sqrt(0.64)=0.8, *0.5 = 0.4。
            Assert.AreEqual(0.4f, SettlementGrowthRules.Urbanization(0.64f, 0.5f), SqrtEps);
            // 同じ人口でも経済が単一（多様性0）なら都市化しない。
            Assert.AreEqual(0f, SettlementGrowthRules.Urbanization(0.64f, 0.0f), Eps);
        }

        [Test]
        public void アメニティはインフラ掛ける都市化引く過密()
        {
            // 0.8*0.5 - 0.1 = 0.3。
            Assert.AreEqual(0.3f, SettlementGrowthRules.AmenityLevel(0.8f, 0.5f, 0.1f), Eps);
            // 過密が利便を上回ると0クランプ：0.8*0.5 - 0.6 = -0.2 → 0。
            Assert.AreEqual(0f, SettlementGrowthRules.AmenityLevel(0.8f, 0.5f, 0.6f), Eps);
        }

        [Test]
        public void 繁栄は成長とアメニティの両方がしきい値以上()
        {
            // 既定しきい値0.3：両方超え→繁栄。
            Assert.IsTrue(SettlementGrowthRules.IsSettlementThriving(0.4f, 0.5f));
            // 成長が足りなければ繁栄でない。
            Assert.IsFalse(SettlementGrowthRules.IsSettlementThriving(0.1f, 0.5f));
            // アメニティが足りなければ繁栄でない。
            Assert.IsFalse(SettlementGrowthRules.IsSettlementThriving(0.4f, 0.2f));
        }

        [Test]
        public void 入力は全クランプ_符号付き出力は範囲内()
        {
            // 過大入力でも 1 を超えない。
            Assert.AreEqual(1f, SettlementGrowthRules.Immigration(5f, 5f, 5f), Eps);
            // 負の人口は無人扱いで自然増0。
            Assert.AreEqual(0f, SettlementGrowthRules.NaturalGrowth(-100f, 0.9f, 0.1f), Eps);
            // 成長率は -1..1 にクランプ（極端な負を下限へ）。
            Assert.AreEqual(-1f, SettlementGrowthRules.GrowthRate(0f, -1f, 1f), Eps);
        }

        [Test]
        public void 物語_僻地の集落が栄えて都市へ_過密で衰退に転じる()
        {
            var p = SettlementGrowthParams.Default;

            // 第1幕：雇用が生まれた新興入植地。流入が起き、自然増もあり、まだ余裕（過密なし）。
            float cap = SettlementGrowthRules.CarryingCapacity(0.7f, 0.7f, 0.7f); // 0.343
            float imm1 = SettlementGrowthRules.Immigration(0.8f, 0.7f, 0.6f, p);   // 0.336
            float nat1 = SettlementGrowthRules.NaturalGrowth(1000f, 0.4f, 0.15f, p); // 0.25
            float over1 = SettlementGrowthRules.OvercrowdingPenalty(0.3f, cap, p);  // 0.3<=0.343 → 0
            float growth1 = SettlementGrowthRules.GrowthRate(imm1, nat1, over1, p);  // ≈0.586 >0
            Assert.AreEqual(0f, over1, Eps);
            Assert.Greater(growth1, 0f);

            // 都市化が進み、アメニティも繁栄圏。
            float urban1 = SettlementGrowthRules.Urbanization(0.49f, 0.6f, p); // sqrt(0.49)=0.7 *0.6=0.42
            Assert.AreEqual(0.42f, urban1, SqrtEps);
            float amenity1 = SettlementGrowthRules.AmenityLevel(0.8f, urban1, over1); // 0.8*0.42-0 =0.336
            Assert.IsTrue(SettlementGrowthRules.IsSettlementThriving(growth1, amenity1),
                "栄えている新興入植地は繁栄判定が真");

            // 第2幕：人口が収容力を大きく超え過密化。過密が流入・自然増を食い潰し成長は止まる。
            float over2 = SettlementGrowthRules.OvercrowdingPenalty(0.7f, cap, p); // 0.7/0.343-1≈1.04→1
            Assert.AreEqual(1f, over2, Eps);
            float growth2 = SettlementGrowthRules.GrowthRate(imm1, nat1, over2, p); // 0.336+0.25-1 → -0.414
            Assert.Less(growth2, 0f);
            float amenity2 = SettlementGrowthRules.AmenityLevel(0.8f, urban1, over2); // 0.336-1 →0
            Assert.AreEqual(0f, amenity2, Eps);
            // 過密で衰退に転じた入植地はもはや繁栄でない。
            Assert.IsFalse(SettlementGrowthRules.IsSettlementThriving(growth2, amenity2),
                "過密で衰退した入植地は繁栄判定が偽");
        }
    }
}
