using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 市民権を固定する：参政権人口の正規化、二級市民の複利の恨み（排除年数で膨張・上限飽和）、
    /// 拡大の統合効果（包摂は最強の同化）、既得市民の反発（急拡大×特権）、従軍による取得（血の代価）、
    /// 体制外化リスク（排除の二乗）、純効果＝壁ではなく門にせよ。境界を担保。
    /// </summary>
    public class CitizenshipRulesTests
    {
        private static readonly CitizenshipParams P = CitizenshipParams.Default;
        // 不満0.3/複利0.02/統合0.5/反発0.4/体制外化0.6

        [Test]
        public void EnfranchisedShare_ClampsToUnitRange()
        {
            Assert.AreEqual(0.7f, CitizenshipRules.EnfranchisedShare(0.7f), 1e-5f);
            Assert.AreEqual(0f, CitizenshipRules.EnfranchisedShare(-0.5f), 1e-5f);
            Assert.AreEqual(1f, CitizenshipRules.EnfranchisedShare(1.5f), 1e-5f);
        }

        [Test]
        public void SecondClassGrievance_CompoundsAcrossGenerations()
        {
            // 排除直後＝基本規模のみ：1.0×0.3×1=0.3
            Assert.AreEqual(0.3f, CitizenshipRules.SecondClassGrievance(1f, 0f, P), 1e-5f);
            // 排除35年（≒一世代）＝複利1.02^35≒2.0でほぼ倍：0.5×0.3×2≒0.3
            Assert.AreEqual(0.3f, CitizenshipRules.SecondClassGrievance(0.5f, 35f, P), 1e-3f);
            // 何世紀も排除すれば上限1で飽和（恨みは無限には測らない）
            Assert.AreEqual(1f, CitizenshipRules.SecondClassGrievance(1f, 500f, P), 1e-5f);
            // 排除がなければ恨みもない
            Assert.AreEqual(0f, CitizenshipRules.SecondClassGrievance(0f, 100f, P), 1e-5f);
        }

        [Test]
        public void IntegrationGain_InclusionIsStrongestAssimilation()
        {
            Assert.AreEqual(0.5f, CitizenshipRules.IntegrationGain(1f, P), 1e-5f);
            Assert.AreEqual(0.25f, CitizenshipRules.IntegrationGain(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, CitizenshipRules.IntegrationGain(-1f, P), 1e-5f); // 負入力はクランプ
        }

        [Test]
        public void OldCitizenBacklash_FastDilutionAngersIncumbents()
        {
            // 急拡大×大特権＝最大反発0.4
            Assert.AreEqual(0.4f, CitizenshipRules.OldCitizenBacklash(1f, 1f, P), 1e-5f);
            // 門は徐々に開けよ＝速度0なら反発なし
            Assert.AreEqual(0f, CitizenshipRules.OldCitizenBacklash(0f, 1f, P), 1e-5f);
            // 希釈される特権がなければ反発もない
            Assert.AreEqual(0f, CitizenshipRules.OldCitizenBacklash(1f, 0f, P), 1e-5f);
            Assert.AreEqual(0.1f, CitizenshipRules.OldCitizenBacklash(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void MilitaryServicePath_BloodPriceProgress()
        {
            Assert.AreEqual(0.5f, CitizenshipRules.MilitaryServicePath(10f, 20f), 1e-5f);
            Assert.AreEqual(1f, CitizenshipRules.MilitaryServicePath(25f, 20f), 1e-5f); // 満了で取得
            Assert.AreEqual(0f, CitizenshipRules.MilitaryServicePath(-5f, 20f), 1e-5f); // 負年数はクランプ
            Assert.AreEqual(1f, CitizenshipRules.MilitaryServicePath(0f, 0f), 1e-5f);   // 必要年数なし＝即時取得
        }

        [Test]
        public void StatelessRisk_GrowsSuperlinearly()
        {
            // 失うものがない者は怖いものもない＝全排除で最大0.6
            Assert.AreEqual(0.6f, CitizenshipRules.StatelessRisk(1f, P), 1e-5f);
            // 二乗則＝半分の排除はリスク1/4：0.25×0.6=0.15
            Assert.AreEqual(0.15f, CitizenshipRules.StatelessRisk(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, CitizenshipRules.StatelessRisk(0f, P), 1e-5f);
        }

        [Test]
        public void NetOpennessEffect_GateBeatsWall()
        {
            // 門＝緩やかに開き排除を残さない：統合0.4×0.5=0.2−反発0.2×0.5×0.4=0.04 → +0.16
            Assert.AreEqual(0.16f, CitizenshipRules.NetOpennessEffect(0.4f, 0.2f, 0.5f, 0f, 0f, P), 1e-5f);
            // 壁＝開かず半数を排除：0−0−不満0.15−体制外化0.15 → −0.3（火種だけが残る）
            Assert.AreEqual(-0.3f, CitizenshipRules.NetOpennessEffect(0f, 0f, 1f, 0.5f, 0f, P), 1e-5f);
            // 何もしない均質国家（排除なし・拡大なし）＝ゼロ（基準点）
            Assert.AreEqual(0f, CitizenshipRules.NetOpennessEffect(0f, 0f, 0f, 0f, 0f, P), 1e-5f);
        }
    }
}
