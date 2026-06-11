using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 義賊組織化（梁山泊型・SHZ-1 #1357）の純ロジックの担保。
    /// 形成閾値・リクルート・結束の源・結束の変動・解体リスク・存続性・非国家権力・確立判定を既定Paramsの具体値で固定。
    /// </summary>
    public class OutlawOrganizationRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>形成圧＝不満×（行き場×0.5＋拠点×0.5）の積。三因が揃って閾値0.5を超えれば梁山泊が立つ。</summary>
        [Test]
        public void FormationThreshold_三因が揃えば閾値を超える()
        {
            // 不満1×(0.5+0.5)=1.0 ≧ 0.5 ＝ 形成。
            Assert.IsTrue(OutlawOrganizationRules.FormationThreshold(1f, 1f, 1f));
            // 不満1×(0.5×0.5+0.5×0.5)=0.5 ≧ 0.5 ＝ 形成（ぎりぎり）。
            Assert.IsTrue(OutlawOrganizationRules.FormationThreshold(1f, 0.5f, 0.5f));
            // 不満があっても拠点も行き場も無ければ圧0＝立たない。
            Assert.IsFalse(OutlawOrganizationRules.FormationThreshold(1f, 0f, 0f));
            // 拠点だけ（不満0）でも圧0＝立たない。
            Assert.IsFalse(OutlawOrganizationRules.FormationThreshold(0f, 1f, 1f));
        }

        /// <summary>形成圧の具体値＝不満0.8×(0.4×0.5+0.6×0.5)=0.8×0.5=0.4。</summary>
        [Test]
        public void FormationPressure_重み付き合成の具体値()
        {
            float pressure = OutlawOrganizationRules.FormationPressure(0.8f, 0.4f, 0.6f);
            Assert.AreEqual(0.4f, pressure, Eps);
        }

        /// <summary>リクルートはロジスティック＝0.2×(不満0.8×威名0.5)×0.4×0.6×1=0.01536 を規模0.4に加算。</summary>
        [Test]
        public void RecruitmentTick_不満と拠点の威名で駆け込みが増える()
        {
            float m = OutlawOrganizationRules.RecruitmentTick(0.4f, 0.8f, 0.5f, 1f);
            Assert.AreEqual(0.4f + 0.2f * 0.4f * 0.4f * 0.6f, m, Eps);
            // 規模0なら核が無く広がらない。
            Assert.AreEqual(0f, OutlawOrganizationRules.RecruitmentTick(0f, 1f, 1f, 1f), Eps);
        }

        /// <summary>結束の源＝(義兄弟0.8×0.4＋大義0.6×0.35＋カリスマ0.4×0.25)/(0.4+0.35+0.25)=0.63/1=0.63。</summary>
        [Test]
        public void CohesionSource_義兄弟と大義とカリスマの加重平均()
        {
            float c = OutlawOrganizationRules.CohesionSource(0.8f, 0.6f, 0.4f);
            float expected = (0.8f * 0.4f + 0.6f * 0.35f + 0.4f * 0.25f) / (0.4f + 0.35f + 0.25f);
            Assert.AreEqual(expected, c, Eps);
        }

        /// <summary>外圧（官の弾圧）は結束を逆に固め、内部対立は緩める＝敵が結束を強いる。</summary>
        [Test]
        public void CohesionTick_内部対立で緩み外圧で固まる()
        {
            // 内部対立のみ：0.5 − 0.15×1 = 0.35。
            float internalOnly = OutlawOrganizationRules.CohesionTick(0.5f, 1f, 0f, 1f);
            Assert.AreEqual(0.35f, internalOnly, Eps);
            // 外圧のみ：0.5 ＋ 0.1×1 = 0.6（弾圧で逆に固まる）。
            float externalOnly = OutlawOrganizationRules.CohesionTick(0.5f, 0f, 1f, 1f);
            Assert.AreEqual(0.6f, externalOnly, Eps);
            Assert.Greater(externalOnly, internalOnly);
        }

        /// <summary>解体リスク＝(弾圧0.6×0.5＋分裂0.4×0.5)×(1−結束0.2)=0.5×0.8=0.4。結束が蓋。</summary>
        [Test]
        public void DissolutionRisk_弾圧と分裂を結束が割り引く()
        {
            float risk = OutlawOrganizationRules.DissolutionRisk(0.2f, 0.6f, 0.4f);
            Assert.AreEqual(0.4f, risk, Eps);
            // 固く結束（1.0）すれば弾圧・分裂が高くても瓦解しない。
            Assert.AreEqual(0f, OutlawOrganizationRules.DissolutionRisk(1f, 1f, 1f), Eps);
        }

        /// <summary>存続性＝(支持0.8×0.6＋略奪0.5×0.4)/(1+規模0.5)=0.68/1.5≒0.4533。民が匿うほど存続。</summary>
        [Test]
        public void OutlawSustainability_住民支持と略奪で規模を養う()
        {
            float s = OutlawOrganizationRules.OutlawSustainability(0.5f, 0.8f, 0.5f);
            float expected = (0.8f * 0.6f + 0.5f * 0.4f) / (1f + 0.5f);
            Assert.AreEqual(expected, s, Eps);
        }

        /// <summary>非国家権力＝正統性0.8×(支配領域0.5×0.6)=0.8×0.3=0.24。どちらか欠ければ野盗。</summary>
        [Test]
        public void NonStateAuthority_正統性と支配領域で地域権力化()
        {
            float a = OutlawOrganizationRules.NonStateAuthority(0.8f, 0.5f);
            Assert.AreEqual(0.24f, a, Eps);
            // 領域ゼロ＝権力に届かない。
            Assert.AreEqual(0f, OutlawOrganizationRules.NonStateAuthority(1f, 0f), Eps);
        }

        /// <summary>確立判定＝規模×結束が閾値以上。人数だけ・少数精鋭だけでは確立しない。</summary>
        [Test]
        public void IsEstablishedOutlawBand_規模と結束の両方が要る()
        {
            // 規模0.8×結束0.7=0.56 ≧ 0.5 ＝ 確立した梁山泊。
            Assert.IsTrue(OutlawOrganizationRules.IsEstablishedOutlawBand(0.8f, 0.7f, 0.5f));
            // 規模0.9でも結束0.2＝0.18 < 0.5 ＝ 烏合の衆。
            Assert.IsFalse(OutlawOrganizationRules.IsEstablishedOutlawBand(0.9f, 0.2f, 0.5f));
        }
    }
}
