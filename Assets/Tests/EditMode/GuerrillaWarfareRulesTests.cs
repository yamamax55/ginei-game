using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ゲリラ戦（非正規戦）の継戦力学＝純ロジックを EditMode で担保する。</summary>
    public class GuerrillaWarfareRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>一撃離脱：機動×奇襲×敵の隙の積で効き、どれか0なら戦果は消える。</summary>
        [Test]
        public void HitAndRunEffect_MultipliesFactors()
        {
            // 1.0 * 0.8 * 0.5 * 0.5 = 0.2
            Assert.AreEqual(0.2f, GuerrillaWarfareRules.HitAndRunEffect(0.8f, 0.5f, 0.5f), Eps);

            // 奇襲0＝待ち伏せが成らず戦果なし
            Assert.AreEqual(0f, GuerrillaWarfareRules.HitAndRunEffect(1f, 0f, 1f), Eps);

            // 全て最大＝1.0
            Assert.AreEqual(1f, GuerrillaWarfareRules.HitAndRunEffect(1f, 1f, 1f), Eps);
        }

        /// <summary>潜伏：地形と住民支持の加重平均（既定重み0.5で半々）。</summary>
        [Test]
        public void Concealment_BlendsTerrainAndSupport()
        {
            // 0.6*0.5 + 0.4*0.5 = 0.5
            Assert.AreEqual(0.5f, GuerrillaWarfareRules.Concealment(0.6f, 0.4f), Eps);

            // 地形も支持も無ければ隠れられない
            Assert.AreEqual(0f, GuerrillaWarfareRules.Concealment(0f, 0f), Eps);

            // 住民が完全に匿えば地形0でも 0*0.5 + 1*0.5 = 0.5
            Assert.AreEqual(0.5f, GuerrillaWarfareRules.Concealment(0f, 1f), Eps);
        }

        /// <summary>支持基盤：不満×規律で得る支持＋占領者の暴虐の押し上げ。略奪は支持を削る。</summary>
        [Test]
        public void PopularSupportBase_EarnedPlusBrutalityPush()
        {
            // earned = 0.5*0.6*Lerp(0.6,1,0.5) = 0.3*0.8 = 0.24 ; pushed = 0.4*0.5 = 0.2 ; = 0.44
            Assert.AreEqual(0.44f, GuerrillaWarfareRules.PopularSupportBase(0.5f, 0.5f, 0.4f), Eps);

            // 規律0（略奪一辺倒）＝earned = 0.5*0.6*Lerp(0.6,1,0)=0.3*0.6=0.18 ; +0 = 0.18
            Assert.AreEqual(0.18f, GuerrillaWarfareRules.PopularSupportBase(0.5f, 0f, 0f), Eps);

            // 不満0でも占領者の暴虐だけで底上げ＝0 + 1*0.5 = 0.5
            Assert.AreEqual(0.5f, GuerrillaWarfareRules.PopularSupportBase(0f, 1f, 1f), Eps);
        }

        /// <summary>敵の消耗強要：一撃離脱×作戦頻度×重み。頻度0なら消耗を強いられない。</summary>
        [Test]
        public void EnemyAttrition_ScalesWithEffectAndTempo()
        {
            // 0.8 * 0.5 * 0.6 = 0.24
            Assert.AreEqual(0.24f, GuerrillaWarfareRules.EnemyAttrition(0.5f, 0.6f), Eps);

            // 作戦頻度0＝消耗を強いられない
            Assert.AreEqual(0f, GuerrillaWarfareRules.EnemyAttrition(1f, 0f), Eps);
        }

        /// <summary>掃討脆弱性：潜伏と支持が高いほど下がる（下限まで・完全には消えない）。</summary>
        [Test]
        public void SweepVulnerability_DropsWithConcealmentAndSupport()
        {
            // shield = (0.6+0.4)/2 = 0.5 ; Lerp(1,0.1,0.5) = 0.55
            Assert.AreEqual(0.55f, GuerrillaWarfareRules.SweepVulnerability(0.6f, 0.4f), Eps);

            // 隠れも支持も無ければ丸見え＝1.0
            Assert.AreEqual(1f, GuerrillaWarfareRules.SweepVulnerability(0f, 0f), Eps);

            // 潜伏も支持も完璧でも下限 0.1 までしか下がらない
            Assert.AreEqual(0.1f, GuerrillaWarfareRules.SweepVulnerability(1f, 1f), Eps);
        }

        /// <summary>継戦力：支持×潜伏（魚と水の積）＋外部補給の上乗せ。</summary>
        [Test]
        public void Sustainability_CorePlusExternalSupply()
        {
            // core = 0.6*0.5 = 0.3 ; supply = 0.5*0.4 = 0.2 ; = 0.5
            Assert.AreEqual(0.5f, GuerrillaWarfareRules.Sustainability(0.6f, 0.5f, 0.5f), Eps);

            // 支持が崩れれば核は0＝外部補給だけが残る（1*0.4=0.4）
            Assert.AreEqual(0.4f, GuerrillaWarfareRules.Sustainability(0f, 1f, 1f), Eps);
        }

        /// <summary>厭戦強要：敵消耗×長期化×重み。長期戦は弱者の武器。</summary>
        [Test]
        public void WarWeariness_ScalesWithAttritionAndDuration()
        {
            // 0.7 * 0.6 * 0.5 = 0.21
            Assert.AreEqual(0.21f, GuerrillaWarfareRules.WarWeariness(0.6f, 0.5f), Eps);

            // 短期決戦（長期化0）＝厭戦を強いられない
            Assert.AreEqual(0f, GuerrillaWarfareRules.WarWeariness(1f, 0f), Eps);
        }

        /// <summary>成立判定：継戦力×(1-掃討脆弱性) が既定閾値0.25以上で成立。</summary>
        [Test]
        public void IsGuerrillaViable_ThresholdOnSustainabilityAndSweep()
        {
            // 0.5*(1-0.5) = 0.25 >= 0.25 → 成立
            Assert.IsTrue(GuerrillaWarfareRules.IsGuerrillaViable(0.5f, 0.5f));

            // 脆弱性が高い（掃討で潰される）＝成立せず：0.5*(1-0.9)=0.05 < 0.25
            Assert.IsFalse(GuerrillaWarfareRules.IsGuerrillaViable(0.5f, 0.9f));

            // 明示閾値版：viability 0.25 はちょうど 0.3 未満で不成立
            Assert.IsFalse(GuerrillaWarfareRules.IsGuerrillaViable(0.5f, 0.5f, 0.3f));
        }

        /// <summary>入力クランプ：範囲外でも壊れず Clamp して計算する。</summary>
        [Test]
        public void Inputs_AreClamped()
        {
            // 一撃離脱は入力が範囲外でも 0..1 にクランプ後の積（2,2,2→1,1,1）
            Assert.AreEqual(1f, GuerrillaWarfareRules.HitAndRunEffect(2f, 2f, 2f), Eps);

            // 負の支持/潜伏は0扱い＝継戦力0
            Assert.AreEqual(0f, GuerrillaWarfareRules.Sustainability(-1f, -1f, 0f), Eps);
        }

        /// <summary>
        /// 物語テスト：山岳に潜み住民に匿われた弱者が、規律ある一撃離脱を繰り返し、
        /// 報復掃討を生き延び、長期戦で占領者に厭戦を強いて戦い続ける一連を検算する。
        /// </summary>
        [Test]
        public void Narrative_MountainGuerrillasOutlastTheOccupier()
        {
            // 占領者の暴虐(0.8)が不満(0.7)を煽り、規律ある(0.8)ゲリラが支持を得る。
            // earned = 0.7*0.6*Lerp(0.6,1,0.8) = 0.42*0.92 = 0.3864 ; pushed = 0.8*0.5 = 0.4 ; = 0.7864
            float support = GuerrillaWarfareRules.PopularSupportBase(0.7f, 0.8f, 0.8f);
            Assert.AreEqual(0.7864f, support, Eps);

            // 山岳(0.9)に潜み、厚い支持で潜伏：0.9*0.5 + 0.7864*0.5 = 0.45+0.3932 = 0.8432
            float conceal = GuerrillaWarfareRules.Concealment(0.9f, support);
            Assert.AreEqual(0.8432f, conceal, Eps);

            // 高機動(0.9)・奇襲(0.8)・敵の隙(0.6)で一撃離脱：1.0*0.9*0.8*0.6 = 0.432
            float hitRun = GuerrillaWarfareRules.HitAndRunEffect(0.9f, 0.8f, 0.6f);
            Assert.AreEqual(0.432f, hitRun, Eps);

            // 高頻度(0.7)で敵に出血を強いる：0.8*0.432*0.7 = 0.24192
            float attrition = GuerrillaWarfareRules.EnemyAttrition(hitRun, 0.7f);
            Assert.AreEqual(0.24192f, attrition, Eps);

            // 報復掃討には見つかりにくい：shield=(0.8432+0.7864)/2=0.8148 ; Lerp(1,0.1,0.8148)=1-0.9*0.8148=0.26668
            float sweep = GuerrillaWarfareRules.SweepVulnerability(conceal, support);
            Assert.AreEqual(0.26668f, sweep, Eps);

            // 外部補給(0.5)も得て継戦：core=0.7864*0.8432=0.66309248 ; supply=0.5*0.4=0.2 ; =0.86309248
            float sustain = GuerrillaWarfareRules.Sustainability(support, conceal, 0.5f);
            Assert.AreEqual(0.86309248f, sustain, Eps);

            // 長期化(0.9)で占領者に厭戦：0.7*0.24192*0.9 = 0.1524096
            float weary = GuerrillaWarfareRules.WarWeariness(attrition, 0.9f);
            Assert.AreEqual(0.1524096f, weary, Eps);

            // 継戦力厚く掃討脆弱性低い＝ゲリラ戦は成立：0.86309248*(1-0.26668)=0.632... >= 0.25
            Assert.IsTrue(GuerrillaWarfareRules.IsGuerrillaViable(sustain, sweep));
        }
    }
}
