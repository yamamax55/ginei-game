using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>知足（ちそく）安定の純ロジック（LAOZ-3 #1554）の EditMode テスト。既定 Params 具体値で固定。</summary>
    public class ContentmentRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>満足度＝理想ちょうどで1、過小・過大いずれの乖離でも下がる（身の丈＝知足）。</summary>
        [Test]
        public void ContentmentLevel_理想ちょうどで最大_乖離で下がる()
        {
            Assert.AreEqual(1f, ContentmentRules.ContentmentLevel(0.5f, 0.5f), Eps);
            // 過小（理想0.6に対し現0.4）も過大（現0.8）も同じ乖離0.2＝同じ満足。
            Assert.AreEqual(0.8f, ContentmentRules.ContentmentLevel(0.4f, 0.6f), Eps);
            Assert.AreEqual(0.8f, ContentmentRules.ContentmentLevel(0.8f, 0.6f), Eps);
        }

        /// <summary>適正規模ボーナス＝満足0で1.0・満足1で1.2（知足者富む・実効値≥1.0）。</summary>
        [Test]
        public void AdequacyBonus_満足ほど安定ボーナス()
        {
            Assert.AreEqual(1f, ContentmentRules.AdequacyBonus(0f), Eps);
            Assert.AreEqual(1.2f, ContentmentRules.AdequacyBonus(1f), Eps); // 1 + 1*0.2
            Assert.AreEqual(1.1f, ContentmentRules.AdequacyBonus(0.5f), Eps);
            Assert.GreaterOrEqual(ContentmentRules.AdequacyBonus(0.3f), 1f);
        }

        /// <summary>貪欲ペナルティ＝身の丈の内なら0・超過分を冪で非線形に効かせる（足るを知らざる禍）。</summary>
        [Test]
        public void GreedPenalty_理想超過のみ非線形に効く()
        {
            // 身の丈の内（現≤理想）は無傷。
            Assert.AreEqual(0f, ContentmentRules.GreedPenalty(0.5f, 0.5f), Eps);
            Assert.AreEqual(0f, ContentmentRules.GreedPenalty(0.3f, 0.6f), Eps);
            // 超過0.3 → 0.3^2 = 0.09。
            Assert.AreEqual(0.09f, ContentmentRules.GreedPenalty(0.6f, 0.3f), Eps);
            // 超過が大きいほど加速して禍が増す（非線形）。
            Assert.Greater(ContentmentRules.GreedPenalty(0.9f, 0.3f),
                ContentmentRules.GreedPenalty(0.6f, 0.3f));
        }

        /// <summary>貪欲ペナルティは上限0.5でクランプされる。</summary>
        [Test]
        public void GreedPenalty_上限でクランプ()
        {
            // 超過1.0 → 1^2=1 だが上限0.5。
            float pen = ContentmentRules.GreedPenalty(1f, 0f);
            Assert.AreEqual(0.5f, pen, Eps);
        }

        /// <summary>小国寡民＝小さくまとまった国ほど安定し、大きく散在する国は不安定。</summary>
        [Test]
        public void SmallStateStability_小さくまとまるほど安定()
        {
            // 小国（規模0）かつ完全一体化（1）＝最も安定。scaleTerm=Lerp(1,1,0.4)=1。
            Assert.AreEqual(1f, ContentmentRules.SmallStateStability(0f, 1f), Eps);
            // 大国（規模1）かつ完全一体化＝小ささ重みで割引。scaleTerm=Lerp(1,0,0.4)=0.6。
            Assert.AreEqual(0.6f, ContentmentRules.SmallStateStability(1f, 1f), Eps);
            // 小さいほうが大きいほうより安定（同じ一体化なら）。
            Assert.Greater(ContentmentRules.SmallStateStability(0.2f, 0.8f),
                ContentmentRules.SmallStateStability(0.9f, 0.8f));
            // 一体化が崩れれば安定も下がる。
            Assert.AreEqual(0.4f, ContentmentRules.SmallStateStability(0f, 0.4f), Eps);
        }

        /// <summary>拡大願望ドリフト＝外圧で膨らみ、外圧が無ければ知足へ静まる。</summary>
        [Test]
        public void AspirationDrift_外圧で膨らみ平時に静まる()
        {
            // 外圧1.0で願望UP：0.5 + 0.3*1*1 - 0.15*0*1 = 0.8。
            Assert.AreEqual(0.8f, ContentmentRules.AspirationDrift(0.5f, 1f, 1f), Eps);
            // 外圧0で知足へ減衰：0.5 + 0 - 0.15*1*1 = 0.35。
            Assert.AreEqual(0.35f, ContentmentRules.AspirationDrift(0.5f, 0f, 1f), Eps);
            // クランプ：高願望+強外圧でも1を超えない。
            Assert.AreEqual(1f, ContentmentRules.AspirationDrift(0.95f, 1f, 1f), Eps);
        }

        /// <summary>知足の安定＝物質的充足を土台に知足が安定へ昇華（足るを知れば富む）。</summary>
        [Test]
        public void SatisfactionStability_充足と知足の両立で最大()
        {
            // 充足1・知足1＝最大。
            Assert.AreEqual(1f, ContentmentRules.SatisfactionStability(1f, 1f), Eps);
            // 充足1・知足0＝土台だけ＝0.5（足りていても満ち足りなければ不満）。
            Assert.AreEqual(0.5f, ContentmentRules.SatisfactionStability(0f, 1f), Eps);
            // 物質が乏しければ知足があっても安定は限られる：m=0.4,c=1 → 0.4*1=0.4。
            Assert.AreEqual(0.4f, ContentmentRules.SatisfactionStability(1f, 0.4f), Eps);
            // 物質ゼロなら安定なし。
            Assert.AreEqual(0f, ContentmentRules.SatisfactionStability(1f, 0f), Eps);
        }

        /// <summary>適正規模＝統治能力＋身の丈余裕、過拡張判定は理想超過が閾値超で true。</summary>
        [Test]
        public void OptimalScale_と_過拡張判定()
        {
            // 統治能力0.5 + 余裕0.1 = 0.6 が適正規模。
            Assert.AreEqual(0.6f, ContentmentRules.OptimalScale(0.5f), Eps);
            // 能力以上には広げない＝上限1でクランプ。
            Assert.AreEqual(1f, ContentmentRules.OptimalScale(1f), Eps);

            // 理想0.5に対し現0.8＝超過0.3>閾値0.1＝過拡張（足るを知らず）。
            Assert.IsTrue(ContentmentRules.IsOverreaching(0.8f, 0.5f, 0.1f));
            // 身の丈の内なら過拡張ではない。
            Assert.IsFalse(ContentmentRules.IsOverreaching(0.55f, 0.5f, 0.1f));
            Assert.IsFalse(ContentmentRules.IsOverreaching(0.3f, 0.5f, 0.1f));
        }
    }
}
