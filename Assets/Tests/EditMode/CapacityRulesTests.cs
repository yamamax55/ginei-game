using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>器量＝度量（『項羽と劉邦』型 #1409・KORY-2）の純ロジック検証。</summary>
    public class CapacityRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>指導者の器量＝度量×安心感の重み付き正規化（劉邦＝高／項羽＝低）。</summary>
        [Test]
        public void LeaderCapacity_WeightsMagnanimityAndSecurity()
        {
            // 度量0.8・安心感0.5・既定（度量重み0.6/安心感重み0.4）→ (0.8*0.6+0.5*0.4)/1.0 = 0.68
            float cap = CapacityRules.LeaderCapacity(0.8f, 0.5f);
            Assert.AreEqual(0.68f, cap, Eps);

            // 度量・安心感ともに低い項羽は器量が小さい。
            float liuBang = CapacityRules.LeaderCapacity(0.9f, 0.9f);
            float xiangYu = CapacityRules.LeaderCapacity(0.2f, 0.2f);
            Assert.Greater(liuBang, xiangYu);
        }

        /// <summary>才人の活用＝器量が才能の天井（器量＜才能で器量がボトルネック）。</summary>
        [Test]
        public void TalentUtilization_CapacityIsCeiling()
        {
            // 器量0.4 ＜ 部下の才能0.9 → 器量がボトルネック＝0.4 までしか活かせない。
            Assert.AreEqual(0.4f, CapacityRules.TalentUtilization(0.4f, 0.9f), Eps);
            // 器量0.9 ≧ 才能0.5 → 才を満額活かせる＝0.5。
            Assert.AreEqual(0.5f, CapacityRules.TalentUtilization(0.9f, 0.5f), Eps);
        }

        /// <summary>傑出の脅威＝部下の才が器量を超えた分だけ警戒が募る（韓信粛清の芽）。</summary>
        [Test]
        public void OverShadowingThreat_RisesWhenTalentExceedsCapacity()
        {
            // 才能0.9・器量0.4・既定脅威幅0.8 → (0.9-0.4)*0.8 = 0.4
            Assert.AreEqual(0.4f, CapacityRules.OverShadowingThreat(0.9f, 0.4f), Eps);
            // 才が器量以下なら脅威0（活かしても怖くない大器）。
            Assert.AreEqual(0f, CapacityRules.OverShadowingThreat(0.5f, 0.9f), Eps);
        }

        /// <summary>嫉妬のペナルティ＝小器ほど優れた部下の才を抑え込む（才を殺す）。</summary>
        [Test]
        public void JealousyPenalty_SmallVesselSuppressesBrilliance()
        {
            // 器量0.2・輝き0.8・既定嫉妬幅0.7 → (1-0.2)*0.8*0.7 = 0.448
            Assert.AreEqual(0.448f, CapacityRules.JealousyPenalty(0.2f, 0.8f), Eps);
            // 器量1ならペナルティ0（嫉妬しない大器）。
            Assert.AreEqual(0f, CapacityRules.JealousyPenalty(1f, 0.9f), Eps);
        }

        /// <summary>委任の有効性＝器量が大きく課題が複雑なほど委任が効く（劉邦は任せ項羽は抱え込んだ）。</summary>
        [Test]
        public void DelegationEffectiveness_GrowsWithCapacityAndComplexity()
        {
            // 器量0.8・複雑さ1.0 → 0.8*(0.5+0.5*1.0) = 0.8
            Assert.AreEqual(0.8f, CapacityRules.DelegationEffectiveness(0.8f, 1.0f), Eps);
            // 同器量でも単純な課題は委任の利得が小さい。
            float complex = CapacityRules.DelegationEffectiveness(0.8f, 1.0f);
            float simple = CapacityRules.DelegationEffectiveness(0.8f, 0.0f);
            Assert.Greater(complex, simple);
        }

        /// <summary>器量による定着＝活かされる満足×厚遇で才人が留まる。</summary>
        [Test]
        public void TalentRetentionFromCapacity_NeedsBothCapacityAndTreatment()
        {
            // 器量0.9・処遇0.5 → 0.6*0.9+0.4*0.5 = 0.74
            Assert.AreEqual(0.74f, CapacityRules.TalentRetentionFromCapacity(0.9f, 0.5f), Eps);
            // 器量ある主君のほうが同じ厚遇でも才人が留まる。
            float greatVessel = CapacityRules.TalentRetentionFromCapacity(0.9f, 0.6f);
            float smallVessel = CapacityRules.TalentRetentionFromCapacity(0.2f, 0.6f);
            Assert.Greater(greatVessel, smallVessel);
        }

        /// <summary>小器の上限＝器量が組織の天井になる（小さい器には大きい才が入らない）。</summary>
        [Test]
        public void SmallVesselCeiling_EqualsCapacity()
        {
            Assert.AreEqual(0.3f, CapacityRules.SmallVesselCeiling(0.3f), Eps);
            // 才人の活用は天井を超えない。
            float ceiling = CapacityRules.SmallVesselCeiling(0.3f);
            Assert.LessOrEqual(CapacityRules.TalentUtilization(0.3f, 1f), ceiling + Eps);
        }

        /// <summary>大器判定＝己より優れた者を使える容量（既定閾0.6）か否か。</summary>
        [Test]
        public void IsGreatVessel_ThresholdGate()
        {
            // 既定しきい値0.6。劉邦型（度量0.9/安心感0.9＝器量0.9）は大器。
            Assert.IsTrue(CapacityRules.IsGreatVessel(CapacityRules.LeaderCapacity(0.9f, 0.9f)));
            // 項羽型（度量0.2/安心感0.2＝器量0.2）は大器ではない。
            Assert.IsFalse(CapacityRules.IsGreatVessel(CapacityRules.LeaderCapacity(0.2f, 0.2f)));
            // 閾値ちょうどは大器（以上）。
            Assert.IsTrue(CapacityRules.IsGreatVessel(0.6f, 0.6f));
        }

        /// <summary>CapacityTolerance 構築時に各フィールドが 0..1 にクランプされる。</summary>
        [Test]
        public void CapacityTolerance_ClampsFields()
        {
            var tol = new CapacityTolerance(1.5f, -0.3f, 0.7f);
            Assert.AreEqual(1f, tol.magnanimity, Eps);
            Assert.AreEqual(0f, tol.security, Eps);
            Assert.AreEqual(0.7f, tol.delegation, Eps);
            // struct オーバーロードは度量×安心感で器量を返す（安心感0→器量は度量重みぶんのみ）。
            Assert.AreEqual(CapacityRules.LeaderCapacity(1f, 0f), CapacityRules.LeaderCapacity(tol), Eps);
        }
    }
}
