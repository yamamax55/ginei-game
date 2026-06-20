using UnityEngine;

namespace Ginei
{
    /// <summary>前線-後方の情報非対称の調整係数（レマルク『西部戦線異状なし』）。</summary>
    public readonly struct HomeFrontParams
    {
        /// <summary>公式の物語×検閲が銃後の幻想を膨らませる強さ（プロパガンダ膨張係数）。</summary>
        public readonly float inflationGain;
        /// <summary>前線が悲惨な現実を知ったときの幻滅の勾配（認識ギャップ×被害目撃に掛かる）。</summary>
        public readonly float disillusionGain;
        /// <summary>真実の漏れが乖離を一気に崩す非線形度（露呈が幻想を砕く勢い）。</summary>
        public readonly float collapseGain;
        /// <summary>帰還兵の疎外の強さ（前線の幻滅×銃後の無邪気さに掛かる）。</summary>
        public readonly float alienationGain;
        /// <summary>物語崩壊とみなす乖離崩壊の閾値（これを超えると幻想が砕け銃後が現実に直面）。</summary>
        public readonly float collapseThreshold;

        public HomeFrontParams(float inflationGain, float disillusionGain, float collapseGain, float alienationGain, float collapseThreshold)
        {
            this.inflationGain = Mathf.Max(0f, inflationGain);
            this.disillusionGain = Mathf.Clamp01(disillusionGain);
            this.collapseGain = Mathf.Max(0f, collapseGain);
            this.alienationGain = Mathf.Clamp01(alienationGain);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
        }

        /// <summary>既定＝膨張0.5・幻滅0.8・崩壊勾配1.5・疎外0.7・崩壊閾値0.6。</summary>
        public static HomeFrontParams Default => new HomeFrontParams(0.5f, 0.8f, 1.5f, 0.7f, 0.6f);
    }

    /// <summary>
    /// 前線-後方の情報非対称＝乖離崩壊の純ロジック（RMK-4 #1412・レマルク『西部戦線異状なし』）。
    /// 前線の兵士が見る戦争の<b>悲惨な現実</b>（泥と血と無意味な死）と、銃後がプロパガンダで信じる
    /// <b>英雄的な幻想</b>（祖国のための栄光の戦い）の間に深刻な乖離があり、後方ほど美化された像を信じる。
    /// 公式の物語と検閲が銃後の幻想を膨らませ、前線は落差ゆえに幻滅し、真実が漏れると幻想が一気に崩壊して
    /// 銃後の士気がショックを受け、帰還兵は現実を知らない銃後に疎外を感じる（失われた世代）。乱数なし・決定論。
    /// <see cref="PropagandaRules"/>（世論操作の効果＝到達×信用×主張）・
    /// <see cref="PublicOpinionRules"/>（世論場の多数派専制と情報品質）とは別＝こちらは前線の現実と銃後の幻想の
    /// 乖離（情報非対称の崩壊）。帰還兵の厭戦の伝播は <see cref="ReturneesContagionRules"/>（同EPIC RMK）、
    /// 世代の断絶は <see cref="GenerationalWoundRules"/>（同EPIC）が扱う＝分担。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HomeFrontRules
    {
        /// <summary>
        /// 認識ギャップ（0..1）＝前線の現実 frontlineReality(0..1・悲惨さ) と銃後の信念 homefrontBelief(0..1・英雄的幻想)
        /// の乖離。後方ほど美化された像を信じる＝現実が悲惨で信念が高いほどギャップが開く＝belief×reality の積。
        /// 銃後が現実を悲惨と理解する（belief 低い）か前線も平穏（reality 低い）ならギャップは小さい。
        /// </summary>
        public static float PerceptionGap(float frontlineReality, float homefrontBelief)
        {
            float reality = Mathf.Clamp01(frontlineReality);
            float belief = Mathf.Clamp01(homefrontBelief);
            return Mathf.Clamp01(reality * belief);
        }

        /// <summary>
        /// プロパガンダ膨張（0..1）＝公式の物語 officialNarrative(0..1) と検閲 censorship(0..1) が銃後の幻想を膨らませる。
        /// 栄光の戦いという虚像＝物語が強く検閲で都合の悪い情報が遮断されるほど膨らむ＝narrative×(1+censorship×inflationGain)。
        /// 検閲がゼロでも物語そのものは届く（narrative の素地は残る）。
        /// </summary>
        public static float PropagandaInflation(float officialNarrative, float censorship, HomeFrontParams p)
        {
            float narrative = Mathf.Clamp01(officialNarrative);
            float cens = Mathf.Clamp01(censorship);
            return Mathf.Clamp01(narrative * (1f + cens * p.inflationGain));
        }

        public static float PropagandaInflation(float officialNarrative, float censorship)
            => PropagandaInflation(officialNarrative, censorship, HomeFrontParams.Default);

        /// <summary>
        /// 前線の幻滅（0..1）＝認識ギャップ perceptionGap(0..1) と被害目撃 casualtiesSeen(0..1) が幻滅を深める。
        /// プロパガンダとの落差を知るほど、また悲惨な死を目撃するほど幻滅する＝gap×casualties×(1+disillusionGain)
        /// を上限1へ。ギャップが無ければ（銃後も現実を知る）落差による幻滅は生じない。
        /// </summary>
        public static float FrontlineDisillusionment(float perceptionGap, float casualtiesSeen, HomeFrontParams p)
        {
            float gap = Mathf.Clamp01(perceptionGap);
            float cas = Mathf.Clamp01(casualtiesSeen);
            return Mathf.Clamp01(gap * cas * (1f + p.disillusionGain));
        }

        public static float FrontlineDisillusionment(float perceptionGap, float casualtiesSeen)
            => FrontlineDisillusionment(perceptionGap, casualtiesSeen, HomeFrontParams.Default);

        /// <summary>
        /// 銃後の士気（0..1）＝銃後の信念 homefrontBelief(0..1) と認識される勝利 perceivedVictory(0..1) が支える。
        /// 幻想に支えられ、勝っていると信じる間は高い＝belief×perceivedVictory。
        /// 勝利が信じられなくなる（perceivedVictory 低下）と幻想が士気を支えきれない。
        /// </summary>
        public static float HomefrontMorale(float homefrontBelief, float perceivedVictory)
        {
            float belief = Mathf.Clamp01(homefrontBelief);
            float victory = Mathf.Clamp01(perceivedVictory);
            return Mathf.Clamp01(belief * victory);
        }

        /// <summary>
        /// 乖離の崩壊（0..1）＝真実の漏れ truthLeakage(0..1) が大きな認識ギャップ perceptionGap(0..1) を一気に崩す。
        /// 敗北・大量戦死の露呈で幻想が砕ける＝gap×leakage を collapseGain で非線形に増幅し上限1へ。
        /// ギャップが小さければ崩すべき幻想が無く、漏れが無ければ崩壊は起きない（どちらか欠ければ回らない）。
        /// </summary>
        public static float GapCollapse(float perceptionGap, float truthLeakage, HomeFrontParams p)
        {
            float gap = Mathf.Clamp01(perceptionGap);
            float leak = Mathf.Clamp01(truthLeakage);
            return Mathf.Clamp01(gap * leak * (1f + p.collapseGain));
        }

        public static float GapCollapse(float perceptionGap, float truthLeakage)
            => GapCollapse(perceptionGap, truthLeakage, HomeFrontParams.Default);

        /// <summary>
        /// 帰還兵の疎外（0..1）＝前線の幻滅 frontlineDisillusionment(0..1) と銃後の無邪気さ homefrontNaivety(0..1)。
        /// 現実を知らない銃後に話が通じず疎外を感じる（失われた世代）＝幻滅×無邪気さ×alienationGain を上限1へ。
        /// 銃後も現実を理解していれば（naivety 低い）話が通じ疎外は薄い。
        /// </summary>
        public static float ReturneeAlienation(float frontlineDisillusionment, float homefrontNaivety, HomeFrontParams p)
        {
            float dis = Mathf.Clamp01(frontlineDisillusionment);
            float naivety = Mathf.Clamp01(homefrontNaivety);
            return Mathf.Clamp01(dis * naivety * (1f + p.alienationGain));
        }

        public static float ReturneeAlienation(float frontlineDisillusionment, float homefrontNaivety)
            => ReturneeAlienation(frontlineDisillusionment, homefrontNaivety, HomeFrontParams.Default);

        /// <summary>
        /// 露呈時の士気ショック（0..1）＝幻想（プロパガンダ膨張 propagandaInflation 0..1）が大きいほど、
        /// 真実露呈 realityRevealed(0..1) のときの士気ショックが大きい＝高く持ち上げた分だけ落ちる
        /// ＝inflation×realityRevealed。膨張させていなければ落差は無く、露呈が無ければショックも無い。
        /// </summary>
        public static float MoraleShockOnRevelation(float propagandaInflation, float realityRevealed)
        {
            float infl = Mathf.Clamp01(propagandaInflation);
            float revealed = Mathf.Clamp01(realityRevealed);
            return Mathf.Clamp01(infl * revealed);
        }

        /// <summary>
        /// 物語崩壊か＝乖離の崩壊 gapCollapse(0..1) が閾値 threshold(0..1) を超えたか。
        /// 公式の物語が崩壊し銃後が現実に直面した＝幻想が砕けた成立判定。
        /// </summary>
        public static bool IsNarrativeCollapse(float gapCollapse, float threshold)
            => Mathf.Clamp01(gapCollapse) >= Mathf.Clamp01(threshold);

        public static bool IsNarrativeCollapse(float gapCollapse)
            => IsNarrativeCollapse(gapCollapse, HomeFrontParams.Default.collapseThreshold);
    }
}
