using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 債務外交を固定する：返せる借金は力にならず返せない借金がレバレッジになる、差し押さえは
    /// レバレッジ×戦略資産、低採算×大型融資が罠、取り立てるほど反発、そして大きすぎる債務は
    /// 借り手の逆カード（デフォルトカード）になる＝債務は双方向の武器。クランプを担保。
    /// </summary>
    public class DebtDiplomacyRulesTests
    {
        private static readonly DebtDiplomacyParams P = DebtDiplomacyParams.Default;
        // 閾値0.5/飽和2.0/罠スケール100/反発係数0.3/差押反発0.2/逆カードスケール100

        [Test]
        public void DebtLeverage_RepayableDebtIsNoPower()
        {
            // 債務比率0.5以下＝返せる借金＝力にならない
            Assert.AreEqual(0f, DebtDiplomacyRules.DebtLeverage(50f, 100f, P), 1e-5f);
            Assert.AreEqual(0f, DebtDiplomacyRules.DebtLeverage(10f, 100f, P), 1e-5f);
            // 比率1.25＝閾値0.5→飽和2.0の中間＝0.5
            Assert.AreEqual(0.5f, DebtDiplomacyRules.DebtLeverage(125f, 100f, P), 1e-5f);
            // 比率2以上＝返せない借金＝レバレッジ最大
            Assert.AreEqual(1f, DebtDiplomacyRules.DebtLeverage(200f, 100f, P), 1e-5f);
            Assert.AreEqual(1f, DebtDiplomacyRules.DebtLeverage(1000f, 100f, P), 1e-5f);
            // 経済規模ゼロで債務あり＝即最大、債務ゼロ＝ゼロ
            Assert.AreEqual(1f, DebtDiplomacyRules.DebtLeverage(10f, 0f, P), 1e-5f);
            Assert.AreEqual(0f, DebtDiplomacyRules.DebtLeverage(0f, 100f, P), 1e-5f);
        }

        [Test]
        public void AssetSeizureValue_LeverageTimesAssets()
        {
            // 完全レバレッジ×一級の港＝99年租借が通る
            Assert.AreEqual(1f, DebtDiplomacyRules.AssetSeizureValue(1f, 1f), 1e-5f);
            Assert.AreEqual(0.4f, DebtDiplomacyRules.AssetSeizureValue(0.8f, 0.5f), 1e-5f);
            // レバレッジ無し or 取る物なし＝ゼロ
            Assert.AreEqual(0f, DebtDiplomacyRules.AssetSeizureValue(0f, 1f), 1e-5f);
            Assert.AreEqual(0f, DebtDiplomacyRules.AssetSeizureValue(1f, 0f), 1e-5f);
        }

        [Test]
        public void DebtTrapDesign_BigLoanToBadProject()
        {
            // 採算ゼロの事業へ満額融資＝完全な罠（ハンバントタ型）
            Assert.AreEqual(1f, DebtDiplomacyRules.DebtTrapDesign(100f, 0f, P), 1e-5f);
            // 同じ大型融資でも健全事業なら罠ではない
            Assert.AreEqual(0f, DebtDiplomacyRules.DebtTrapDesign(100f, 1f, P), 1e-5f);
            // 小口×低採算＝罠度は規模で割引
            Assert.AreEqual(0.25f, DebtDiplomacyRules.DebtTrapDesign(50f, 0.5f, P), 1e-5f);
            // 規模成分はスケールで飽和
            Assert.AreEqual(1f, DebtDiplomacyRules.DebtTrapDesign(1000f, 0f, P), 1e-5f);
        }

        [Test]
        public void DebtorResentment_GrowsWithSeizures()
        {
            // レバレッジだけでも嫌われる：1×0.3=0.3
            Assert.AreEqual(0.3f, DebtDiplomacyRules.DebtorResentment(1f, 0, P), 1e-5f);
            // 取り立てるたびに+0.2：0.3+2×0.2=0.7
            Assert.AreEqual(0.7f, DebtDiplomacyRules.DebtorResentment(1f, 2, P), 1e-5f);
            // 上限1（取り立てすぎ）
            Assert.AreEqual(1f, DebtDiplomacyRules.DebtorResentment(1f, 10, P), 1e-5f);
            // 負の件数はクランプ
            Assert.AreEqual(0f, DebtDiplomacyRules.DebtorResentment(0f, -3, P), 1e-5f);
        }

        [Test]
        public void DefaultCardStrength_DebtorsReverseCard()
        {
            // 大口債務×薄い自己資本の貸し手＝銀行が人質
            Assert.AreEqual(1f, DebtDiplomacyRules.DefaultCardStrength(100f, 1f, P), 1e-5f);
            // 同じ債務でも貸し手の資本が厚ければカードにならない
            Assert.AreEqual(0.1f, DebtDiplomacyRules.DefaultCardStrength(100f, 0.1f, P), 1e-5f);
            // 小口債務＝借り手の問題のまま
            Assert.AreEqual(0.1f, DebtDiplomacyRules.DefaultCardStrength(10f, 1f, P), 1e-5f);
            // 債務ゼロ・負値＝カード無し
            Assert.AreEqual(0f, DebtDiplomacyRules.DefaultCardStrength(0f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, DebtDiplomacyRules.DefaultCardStrength(-50f, 1f, P), 1e-5f);
        }

        [Test]
        public void DebtIsTwoWayWeapon()
        {
            // 同じ債務200が両刃になる：
            // 借り手（経済100）には返せない＝貸し手のレバレッジ最大
            float leverage = DebtDiplomacyRules.DebtLeverage(200f, 100f, P);
            Assert.AreEqual(1f, leverage, 1e-5f);
            // 貸し手の自己資本に対して大きすぎる＝借り手のデフォルトカードも最大
            float card = DebtDiplomacyRules.DefaultCardStrength(200f, 1f, P);
            Assert.AreEqual(1f, card, 1e-5f);
            // 貸し込むほど両方の武器が同時に育つ（単調増加）
            Assert.Less(DebtDiplomacyRules.DebtLeverage(60f, 100f, P), DebtDiplomacyRules.DebtLeverage(120f, 100f, P));
            Assert.Less(DebtDiplomacyRules.DefaultCardStrength(30f, 0.5f, P), DebtDiplomacyRules.DefaultCardStrength(60f, 0.5f, P));
        }

        [Test]
        public void Params_CtorClampsInputs()
        {
            // 負値・逆転した閾値/飽和もクランプされ壊れない
            var p = new DebtDiplomacyParams(-1f, -2f, -5f, -1f, -1f, 0f);
            Assert.AreEqual(0f, p.leverageThreshold, 1e-5f);
            Assert.Greater(p.leverageSaturation, p.leverageThreshold); // 必ず閾値より上＝ゼロ除算なし
            Assert.Greater(p.trapLoanScale, 0f);
            Assert.AreEqual(0f, p.resentmentLeverageScale, 1e-5f);
            Assert.AreEqual(0f, p.resentmentPerSeizure, 1e-5f);
            Assert.Greater(p.cardDebtScale, 0f);
            // クランプ後のParamsでも全APIが0..1を返す
            Assert.GreaterOrEqual(DebtDiplomacyRules.DebtLeverage(100f, 100f, p), 0f);
            Assert.LessOrEqual(DebtDiplomacyRules.DebtLeverage(100f, 100f, p), 1f);
        }
    }
}
