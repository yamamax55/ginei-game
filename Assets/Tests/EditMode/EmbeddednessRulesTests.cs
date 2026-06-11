using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>市場の埋め込み度（POLA-1 #1588・ポランニー『大転換』の embeddedness／脱埋め込み）の純ロジックテスト。</summary>
    public class EmbeddednessRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>埋め込み度＝社会的紐帯×規制×慣習的交換の加重平均（紐帯0.4・規制0.3・慣習0.3）。</summary>
        [Test]
        public void EmbeddednessLevel_三要素の加重平均()
        {
            // 0.4×0.8 + 0.3×0.6 + 0.3×0.4 = 0.32+0.18+0.12 = 0.62
            Assert.AreEqual(0.62f, EmbeddednessRules.EmbeddednessLevel(0.8f, 0.6f, 0.4f), Eps);
            // すべて1＝完全に埋め込み
            Assert.AreEqual(1f, EmbeddednessRules.EmbeddednessLevel(1f, 1f, 1f), Eps);
            // すべて0＝自己調整市場（脱埋め込み）
            Assert.AreEqual(0f, EmbeddednessRules.EmbeddednessLevel(0f, 0f, 0f), Eps);
        }

        /// <summary>市場効率＝脱埋め込み（低 embeddedness）ほど高い（自由化が効率を解き放つ）。</summary>
        [Test]
        public void MarketEfficiency_脱埋め込みほど効率が高い()
        {
            // emb=0 → floor0.4 + gain0.6×1 = 1.0（最大）
            Assert.AreEqual(1f, EmbeddednessRules.MarketEfficiency(0f), Eps);
            // emb=0.5 → 0.4 + 0.6×0.5 = 0.7
            Assert.AreEqual(0.7f, EmbeddednessRules.MarketEfficiency(0.5f), Eps);
            // emb=1 → floor0.4（埋め込みは効率を縛る）
            Assert.AreEqual(0.4f, EmbeddednessRules.MarketEfficiency(1f), Eps);
            // 埋め込みが深いほど効率は低い
            Assert.Less(EmbeddednessRules.MarketEfficiency(1f), EmbeddednessRules.MarketEfficiency(0f));
        }

        /// <summary>社会安定＝埋め込みが深いほど高い（経済が社会に守られる）。</summary>
        [Test]
        public void SocialStability_埋め込みほど安定()
        {
            // emb=1 → floor0.4 + scale0.6×1 = 1.0（最大）
            Assert.AreEqual(1f, EmbeddednessRules.SocialStability(1f), Eps);
            // emb=0.5 → 0.4 + 0.6×0.5 = 0.7
            Assert.AreEqual(0.7f, EmbeddednessRules.SocialStability(0.5f), Eps);
            // emb=0 → floor0.4（自己調整市場は社会を守らない）
            Assert.AreEqual(0.4f, EmbeddednessRules.SocialStability(0f), Eps);
            // 効率とは逆向き＝トレードオフ
            Assert.Greater(EmbeddednessRules.SocialStability(1f), EmbeddednessRules.SocialStability(0f));
        }

        /// <summary>脱埋め込み＝自由化が紐帯を切り埋め込み度を下げる（効率と引き換えに社会から剥がす）。</summary>
        [Test]
        public void DisembeddingTick_自由化が市場を引き剥がす()
        {
            // emb=0.8, lib=0.5, dt=1: drop=0.5×0.5×1=0.25 → 0.55
            Assert.AreEqual(0.55f, EmbeddednessRules.DisembeddingTick(0.8f, 0.5f, 1f), Eps);
            // 自由化ゼロなら剥がれない
            Assert.AreEqual(0.8f, EmbeddednessRules.DisembeddingTick(0.8f, 0f, 1f), Eps);
            // 自由化が強いほど深く剥がす
            Assert.Less(EmbeddednessRules.DisembeddingTick(0.8f, 1f, 1f),
                        EmbeddednessRules.DisembeddingTick(0.8f, 0.3f, 1f));
        }

        /// <summary>埋め戻し＝保護（二重運動）が市場を社会へ縛り直し埋め込み度を上げる。</summary>
        [Test]
        public void ReembeddingTick_保護が市場を埋め戻す()
        {
            // emb=0.5, protection=0.6, dt=1: rise=0.3×0.6×1=0.18 → 0.68
            Assert.AreEqual(0.68f, EmbeddednessRules.ReembeddingTick(0.5f, 0.6f, 1f), Eps);
            // 保護ゼロなら埋め戻らない
            Assert.AreEqual(0.5f, EmbeddednessRules.ReembeddingTick(0.5f, 0f, 1f), Eps);
        }

        /// <summary>効率×安定のトレードオフ＝中庸（emb=0.5）が両極を上回る山形（一方を犠牲にすると積が痩せる）。</summary>
        [Test]
        public void EfficiencyStabilityTradeoff_中庸が最適()
        {
            // emb=0.5: 0.7×0.7 = 0.49
            float mid = EmbeddednessRules.EfficiencyStabilityTradeoff(0.5f);
            Assert.AreEqual(0.49f, mid, Eps);
            // emb=0: 1.0×0.4 = 0.4 / emb=1: 0.4×1.0 = 0.4（両極は痩せる）
            Assert.AreEqual(0.4f, EmbeddednessRules.EfficiencyStabilityTradeoff(0f), Eps);
            Assert.AreEqual(0.4f, EmbeddednessRules.EfficiencyStabilityTradeoff(1f), Eps);
            Assert.Greater(mid, EmbeddednessRules.EfficiencyStabilityTradeoff(0f));
            Assert.Greater(mid, EmbeddednessRules.EfficiencyStabilityTradeoff(1f));
        }

        /// <summary>混乱リスク＝脱埋め込み（低 embeddedness）ほど大きい（市場が剥がれると生活が混乱）。</summary>
        [Test]
        public void DislocationRisk_脱埋め込みが混乱を生む()
        {
            // emb=0 → 0.8×1 = 0.8（最大）
            Assert.AreEqual(0.8f, EmbeddednessRules.DislocationRisk(0f), Eps);
            // emb=0.5 → 0.8×0.5 = 0.4
            Assert.AreEqual(0.4f, EmbeddednessRules.DislocationRisk(0.5f), Eps);
            // emb=1 → 完全に埋め込み＝混乱なし
            Assert.AreEqual(0f, EmbeddednessRules.DislocationRisk(1f), Eps);
        }

        /// <summary>脱埋め込み判定＝埋め込み度が既定しきい値0.3以下で「社会から引き剥がされた」とみなす。</summary>
        [Test]
        public void IsDisembedded_しきい値で脱埋め込み()
        {
            Assert.IsFalse(EmbeddednessRules.IsDisembedded(0.4f));
            Assert.IsTrue(EmbeddednessRules.IsDisembedded(0.3f));
            Assert.IsTrue(EmbeddednessRules.IsDisembedded(0.1f));
        }
    }
}
