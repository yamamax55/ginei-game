using UnityEngine;

namespace Ginei
{
    /// <summary>人民投票的指導者民主主義の調整係数（ツェーザリズム＝Caesarism）。</summary>
    public readonly struct PlebiscitaryParams
    {
        /// <summary>カリスマ×大衆動員が直接の負託に換わる基礎係数。</summary>
        public readonly float mandateScale;
        /// <summary>議会を飛び越す力の係数（直接の負託が議会を空洞化する強さ）。</summary>
        public readonly float bypassScale;
        /// <summary>喝采（acclamation）が理性的討議を置き換える強さ。</summary>
        public readonly float acclamationScale;
        /// <summary>人民投票の正統性の移ろいやすさ（喝采依存ぶんの揮発係数）。</summary>
        public readonly float volatilityScale;

        public PlebiscitaryParams(float mandateScale, float bypassScale, float acclamationScale, float volatilityScale)
        {
            this.mandateScale = Mathf.Max(0f, mandateScale);
            this.bypassScale = Mathf.Max(0f, bypassScale);
            this.acclamationScale = Mathf.Max(0f, acclamationScale);
            this.volatilityScale = Mathf.Max(0f, volatilityScale);
        }

        /// <summary>既定＝負託1.0・迂回1.0・喝采0.8・揮発0.6。</summary>
        public static PlebiscitaryParams Default => new PlebiscitaryParams(1f, 1f, 0.8f, 0.6f);
    }

    /// <summary>
    /// 人民投票的指導者民主主義（plebiszitäre Führerdemokratie）の純ロジック（WEBR-4 #1533・マックス・ウェーバー参考）。
    /// カリスマ的指導者が議会を飛び越して大衆に直接訴え、人民投票（喝采＝acclamation）で正統性を得る＝ツェーザリズム
    /// （Caesarism）。直接の負託は「カリスマ×大衆動員」で生まれ、これが強いほど議会制を空洞化して迂回し（指導者と大衆が
    /// 中間団体＝政党・議会を飛ばして短絡する）、制度的歯止めが弱ければ人民投票的独裁へ傾く。喝采による正統性は大衆の熱に
    /// 依存し移ろいやすい（冷めれば崩れる）。
    /// 分担：<see cref="DemagogueRules"/>（扇動家の訴求力＝雄弁×恐怖）／<see cref="PlebisciteRules"/>（住民投票＝地域帰属の
    /// 是非投票）／<see cref="LeadershipElectionRules"/>（党首選出＝党内の票の解決）とは別＝こちらは「カリスマ×大衆直接動員×
    /// 議会迂回」で正統性を得る指導者民主主義そのもの（同 EPIC WEBR の <c>PoliticalVocationRules</c>＝政治の天職／責任倫理とも別）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PlebiscitaryRules
    {
        /// <summary>
        /// 直接の負託（0..1）＝カリスマ charisma(0..1)×大衆動員 massMobilization(0..1)×係数。
        /// 議会でなく大衆から正統性を引き出す核。どちらか欠ければ直接の負託は生まれない（積）。
        /// </summary>
        public static float DirectMandate(float charisma, float massMobilization, PlebiscitaryParams p)
        {
            float c = Mathf.Clamp01(charisma);
            float m = Mathf.Clamp01(massMobilization);
            return Mathf.Clamp01(c * m * p.mandateScale);
        }

        public static float DirectMandate(float charisma, float massMobilization)
            => DirectMandate(charisma, massMobilization, PlebiscitaryParams.Default);

        /// <summary>
        /// 議会の迂回度（0..1）＝直接の負託 directMandate(0..1)×係数×（1−議会の強さ parliamentStrength(0..1)）。
        /// 大衆からの負託が強く議会が弱いほど飛び越せる＝議会制の空洞化。強い議会は迂回をはね返す。
        /// </summary>
        public static float ParliamentaryBypass(float directMandate, float parliamentStrength, PlebiscitaryParams p)
        {
            float dm = Mathf.Clamp01(directMandate);
            float ps = Mathf.Clamp01(parliamentStrength);
            return Mathf.Clamp01(dm * p.bypassScale * (1f - ps));
        }

        public static float ParliamentaryBypass(float directMandate, float parliamentStrength)
            => ParliamentaryBypass(directMandate, parliamentStrength, PlebiscitaryParams.Default);

        /// <summary>
        /// 人民投票の正統性（0..1）＝直接の負託 directMandate(0..1)×投票参加 turnout(0..1)。
        /// 喝采による正統化＝動員の力と現に集まった参加で正統性を得る。参加ゼロなら正統性は生まれない（積）。
        /// </summary>
        public static float PlebiscitaryLegitimacy(float directMandate, float turnout)
        {
            float dm = Mathf.Clamp01(directMandate);
            float t = Mathf.Clamp01(turnout);
            return Mathf.Clamp01(dm * t);
        }

        /// <summary>
        /// ツェーザリズムのリスク（0..1）＝直接動員 directMandate(0..1)×（1−制度的歯止め institutionalChecks(0..1)）。
        /// 直接動員が強く制度の歯止めが弱いほど人民投票的独裁へ傾く。強い歯止めはリスクを抑える。
        /// </summary>
        public static float CaesarismRisk(float directMandate, float institutionalChecks)
        {
            float dm = Mathf.Clamp01(directMandate);
            float ic = Mathf.Clamp01(institutionalChecks);
            return Mathf.Clamp01(dm * (1f - ic));
        }

        /// <summary>
        /// 喝采の力学（0..1）＝カリスマ charisma(0..1)×大衆の情動 crowdEmotion(0..1)×係数。
        /// 大衆の喝采（acclamation）が理性的討議を置き換える度合い＝感情が高ぶるほど討議は喝采に取って代わられる。
        /// </summary>
        public static float AcclamationDynamics(float charisma, float crowdEmotion, PlebiscitaryParams p)
        {
            float c = Mathf.Clamp01(charisma);
            float e = Mathf.Clamp01(crowdEmotion);
            return Mathf.Clamp01(c * e * p.acclamationScale);
        }

        public static float AcclamationDynamics(float charisma, float crowdEmotion)
            => AcclamationDynamics(charisma, crowdEmotion, PlebiscitaryParams.Default);

        /// <summary>
        /// 指導者と大衆の短絡度（0..1）＝カリスマ charisma(0..1)×（1−中間団体 intermediaryInstitutions(0..1)）。
        /// 政党・議会など中間団体が薄いほど指導者と大衆が直結する＝媒介の消失。媒介が厚ければ短絡は起きない。
        /// </summary>
        public static float LeaderMassShortCircuit(float charisma, float intermediaryInstitutions)
        {
            float c = Mathf.Clamp01(charisma);
            float ii = Mathf.Clamp01(intermediaryInstitutions);
            return Mathf.Clamp01(c * (1f - ii));
        }

        /// <summary>
        /// 人民投票の正統性の移ろいやすさ（0..1）＝正統性 plebiscitaryLegitimacy(0..1)×揮発係数。
        /// 喝采に依存する正統性ほど大衆の熱が冷めると崩れる＝高く積み上げた正統性ほど揺らぎ幅も大きい。
        /// </summary>
        public static float MandateVolatility(float plebiscitaryLegitimacy, PlebiscitaryParams p)
        {
            float pl = Mathf.Clamp01(plebiscitaryLegitimacy);
            return Mathf.Clamp01(pl * p.volatilityScale);
        }

        public static float MandateVolatility(float plebiscitaryLegitimacy)
            => MandateVolatility(plebiscitaryLegitimacy, PlebiscitaryParams.Default);

        /// <summary>
        /// ツェーザリズム（人民投票的独裁）に陥ったか＝ツェーザリズムのリスク caesarismRisk と議会の迂回度
        /// parliamentaryBypass がともに閾値 threshold を超える（喝采による独裁への傾き＝大衆動員で議会を飛び越し制度の
        /// 歯止めも弱い状態）。
        /// </summary>
        public static bool IsCaesarist(float caesarismRisk, float parliamentaryBypass, float threshold)
        {
            float cr = Mathf.Clamp01(caesarismRisk);
            float pb = Mathf.Clamp01(parliamentaryBypass);
            float th = Mathf.Clamp01(threshold);
            return cr > th && pb > th;
        }
    }
}
