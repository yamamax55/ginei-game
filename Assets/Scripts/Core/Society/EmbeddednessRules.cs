using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 市場の埋め込み度の純データ（POLA-1 #1588・ポランニー『大転換』の embeddedness）。
    /// 経済が社会関係・慣習・制度の中にどれだけ埋め込まれているかを表す。
    /// 解決は <see cref="EmbeddednessRules"/> が唯一の窓口。
    /// </summary>
    [Serializable]
    public struct MarketEmbeddedness
    {
        /// <summary>埋め込み度（0..1）。1で完全に社会に埋め込み・0で完全な自己調整市場（脱埋め込み）。</summary>
        public float embeddedness;
        /// <summary>社会的紐帯の強さ（0..1）。共同体・互酬の網の濃さ＝埋め込みの土台。</summary>
        public float socialTies;
        /// <summary>規制・制度の厚さ（0..1）。市場を社会の規範へ縛り付ける制度の量。</summary>
        public float regulation;

        public MarketEmbeddedness(float embeddedness, float socialTies, float regulation)
        {
            this.embeddedness = Mathf.Clamp01(embeddedness);
            this.socialTies = Mathf.Clamp01(socialTies);
            this.regulation = Mathf.Clamp01(regulation);
        }
    }

    /// <summary>市場の埋め込み度の調整係数（POLA-1 #1588・脱埋め込み／埋め戻しの速度・トレードオフ強度）。</summary>
    public readonly struct EmbeddednessParams
    {
        /// <summary>埋め込み度算出での社会的紐帯の重み。</summary>
        public readonly float tiesWeight;
        /// <summary>埋め込み度算出での規制の重み。</summary>
        public readonly float regulationWeight;
        /// <summary>埋め込み度算出での慣習的交換の重み。</summary>
        public readonly float customaryWeight;
        /// <summary>脱埋め込み（埋め込みが0なら効率最大）での市場効率の幅＝(1−emb)に掛ける。</summary>
        public readonly float efficiencyGain;
        /// <summary>脱埋め込みでも残る基礎効率（emb=1でもこの分は出る）。</summary>
        public readonly float efficiencyFloor;
        /// <summary>埋め込みが社会安定へ寄与する幅＝embに掛ける。</summary>
        public readonly float stabilityScale;
        /// <summary>脱埋め込みでも残る基礎安定（emb=0でもこの分は残る）。</summary>
        public readonly float stabilityFloor;
        /// <summary>自由化が紐帯を切り脱埋め込みへ進める基礎速度（年あたり）。</summary>
        public readonly float disembedRate;
        /// <summary>保護（二重運動）が市場を社会へ埋め戻す基礎速度（年あたり）。</summary>
        public readonly float reembedRate;
        /// <summary>脱埋め込みが生む社会的混乱（dislocation）リスクの強さ。</summary>
        public readonly float dislocationScale;
        /// <summary>市場が社会から引き剥がされたとみなす既定の埋め込み度しきい値（これ以下で脱埋め込み）。</summary>
        public readonly float disembedThreshold;

        public EmbeddednessParams(float tiesWeight, float regulationWeight, float customaryWeight,
                                  float efficiencyGain, float efficiencyFloor, float stabilityScale, float stabilityFloor,
                                  float disembedRate, float reembedRate, float dislocationScale, float disembedThreshold)
        {
            this.tiesWeight = Mathf.Max(0f, tiesWeight);
            this.regulationWeight = Mathf.Max(0f, regulationWeight);
            this.customaryWeight = Mathf.Max(0f, customaryWeight);
            this.efficiencyGain = Mathf.Clamp01(efficiencyGain);
            this.efficiencyFloor = Mathf.Clamp01(efficiencyFloor);
            this.stabilityScale = Mathf.Clamp01(stabilityScale);
            this.stabilityFloor = Mathf.Clamp01(stabilityFloor);
            this.disembedRate = Mathf.Max(0f, disembedRate);
            this.reembedRate = Mathf.Max(0f, reembedRate);
            this.dislocationScale = Mathf.Clamp01(dislocationScale);
            this.disembedThreshold = Mathf.Clamp01(disembedThreshold);
        }

        /// <summary>
        /// 既定＝紐帯重み0.4・規制重み0.3・慣習重み0.3（合計1＝加重平均）・効率幅0.6/効率床0.4・
        /// 安定幅0.6/安定床0.4・脱埋め込み速度0.5・埋め戻し速度0.3・混乱の強さ0.8・脱埋め込みしきい値0.3。
        /// </summary>
        public static EmbeddednessParams Default =>
            new EmbeddednessParams(0.4f, 0.3f, 0.3f, 0.6f, 0.4f, 0.6f, 0.4f, 0.5f, 0.3f, 0.8f, 0.3f);
    }

    /// <summary>
    /// 市場の埋め込み度の純ロジック（POLA-1 #1588・ポランニー『大転換』の<b>埋め込み embeddedness</b>）。
    /// 前近代では経済は社会関係・慣習・制度に埋め込まれていた。自己調整市場は経済を社会から引き剥がす
    /// ＝<b>脱埋め込み disembedding</b>。「市場が社会に埋め込まれているほど安定だが効率は低く、
    /// 自由化（脱埋め込み）は効率を上げるが社会を不安定化させる」というトレードオフを式にする。
    /// 二重運動の<b>保護側</b>ラチェット（市場圧力→保護需要→制度建設）は <see cref="SocialProtectionRules"/>、
    /// 労働・土地・貨幣を商品化する擬制商品の側は <see cref="FictitiousCommodityRules"/>（同EPIC POLA）、
    /// 単一財の需給価格は <see cref="MarketRules"/> が扱い、ここは市場の社会への<b>埋め込み度の指標</b>と
    /// その効率／安定のトレードオフ・脱埋め込み／埋め戻しの動学のみを扱う。
    /// 係数は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。全入力クランプ。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EmbeddednessRules
    {
        /// <summary>
        /// 埋め込み度（0..1）。社会的紐帯・規制・慣習的交換の加重平均で出す
        /// ＝経済が社会の網（紐帯）・制度の縛り（規制）・伝統の取引（慣習）にどれだけ織り込まれているか。
        /// すべて高ければ完全に埋め込み（1）、すべて崩れれば自己調整市場（0＝脱埋め込み）。
        /// </summary>
        public static float EmbeddednessLevel(float socialTies, float regulation, float customaryExchange, EmbeddednessParams p)
        {
            float ties = Mathf.Clamp01(socialTies);
            float reg = Mathf.Clamp01(regulation);
            float cust = Mathf.Clamp01(customaryExchange);
            float wsum = p.tiesWeight + p.regulationWeight + p.customaryWeight;
            if (wsum <= 0f) return 0f;
            float weighted = p.tiesWeight * ties + p.regulationWeight * reg + p.customaryWeight * cust;
            return Mathf.Clamp01(weighted / wsum);
        }

        public static float EmbeddednessLevel(float socialTies, float regulation, float customaryExchange)
            => EmbeddednessLevel(socialTies, regulation, customaryExchange, EmbeddednessParams.Default);

        /// <summary>
        /// 市場効率（0..1）。脱埋め込み（低 embeddedness）ほど高い＝自由化が市場の調整力を解き放つ。
        /// floor + gain×(1−emb)＝emb=0で floor+gain（最大）、emb=1で floor（埋め込みは効率を縛る）。
        /// 産出・取引効率へ掛ける実効値。
        /// </summary>
        public static float MarketEfficiency(float embeddedness, EmbeddednessParams p)
        {
            float emb = Mathf.Clamp01(embeddedness);
            return Mathf.Clamp01(p.efficiencyFloor + p.efficiencyGain * (1f - emb));
        }

        public static float MarketEfficiency(float embeddedness)
            => MarketEfficiency(embeddedness, EmbeddednessParams.Default);

        /// <summary>
        /// 社会安定（0..1）。埋め込みが深いほど高い＝経済が社会に守られて生活が揺らがない。
        /// floor + scale×emb＝emb=1で floor+scale（最大）、emb=0で floor（自己調整市場は社会を守らない）。
        /// 内政の安定度へ足し込む係数（<see cref="GovernanceRules"/> 側で消費）。
        /// </summary>
        public static float SocialStability(float embeddedness, EmbeddednessParams p)
        {
            float emb = Mathf.Clamp01(embeddedness);
            return Mathf.Clamp01(p.stabilityFloor + p.stabilityScale * emb);
        }

        public static float SocialStability(float embeddedness)
            => SocialStability(embeddedness, EmbeddednessParams.Default);

        /// <summary>
        /// 脱埋め込み（1tick後の埋め込み度 0..1）。自由化（liberalization 0..1）が紐帯・制度を切り
        /// 埋め込み度を下げる＝効率と引き換えに市場を社会から引き剥がす。下がる量＝disembedRate×自由化×dt。dt は年単位。
        /// </summary>
        public static float DisembeddingTick(float embeddedness, float liberalization, float dt, EmbeddednessParams p)
        {
            float emb = Mathf.Clamp01(embeddedness);
            float lib = Mathf.Clamp01(liberalization);
            float drop = p.disembedRate * lib * Mathf.Max(0f, dt);
            return Mathf.Clamp01(emb - drop);
        }

        public static float DisembeddingTick(float embeddedness, float liberalization, float dt)
            => DisembeddingTick(embeddedness, liberalization, dt, EmbeddednessParams.Default);

        /// <summary>
        /// 埋め戻し（1tick後の埋め込み度 0..1）。保護（protection 0..1＝二重運動の反作用）が
        /// 市場を社会の規範へ縛り直す＝埋め込み度を上げる。上がる量＝reembedRate×保護×dt。
        /// 入力 protection は <see cref="SocialProtectionRules"/> の保護水準と連動する。dt は年単位。
        /// </summary>
        public static float ReembeddingTick(float embeddedness, float protection, float dt, EmbeddednessParams p)
        {
            float emb = Mathf.Clamp01(embeddedness);
            float prot = Mathf.Clamp01(protection);
            float rise = p.reembedRate * prot * Mathf.Max(0f, dt);
            return Mathf.Clamp01(emb + rise);
        }

        public static float ReembeddingTick(float embeddedness, float protection, float dt)
            => ReembeddingTick(embeddedness, protection, dt, EmbeddednessParams.Default);

        /// <summary>
        /// 効率と安定のトレードオフ＝<see cref="MarketEfficiency"/>×<see cref="SocialStability"/>の積（0..1）。
        /// 効率は脱埋め込みで上がり安定は埋め込みで上がる＝積は<b>中庸で最大</b>になりうる山形
        /// （両極では一方が犠牲になり積が痩せる）。最適点を探る指標。
        /// </summary>
        public static float EfficiencyStabilityTradeoff(float embeddedness, EmbeddednessParams p)
        {
            return Mathf.Clamp01(MarketEfficiency(embeddedness, p) * SocialStability(embeddedness, p));
        }

        public static float EfficiencyStabilityTradeoff(float embeddedness)
            => EfficiencyStabilityTradeoff(embeddedness, EmbeddednessParams.Default);

        /// <summary>
        /// 社会的混乱（dislocation）のリスク（0..1）。脱埋め込み（低 embeddedness）ほど大きい
        /// ＝市場が社会から剥がれるほど生活の混乱・反発が増す（dislocationScale×(1−emb)）。
        /// 反乱圧・二重運動の保護需要（<see cref="SocialProtectionRules.ProtectionDemand"/> の入力）の火種。
        /// </summary>
        public static float DislocationRisk(float embeddedness, EmbeddednessParams p)
        {
            float emb = Mathf.Clamp01(embeddedness);
            return Mathf.Clamp01(p.dislocationScale * (1f - emb));
        }

        public static float DislocationRisk(float embeddedness)
            => DislocationRisk(embeddedness, EmbeddednessParams.Default);

        /// <summary>
        /// 市場が社会から引き剥がされた（脱埋め込み）か。埋め込み度がしきい値以下なら true
        /// ＝自己調整市場が社会から自律しており、混乱リスクが高い（既定しきい値は <see cref="EmbeddednessParams"/>）。
        /// </summary>
        public static bool IsDisembedded(float embeddedness, float threshold)
            => Mathf.Clamp01(embeddedness) <= Mathf.Clamp01(threshold);

        public static bool IsDisembedded(float embeddedness)
            => IsDisembedded(embeddedness, EmbeddednessParams.Default.disembedThreshold);
    }
}
