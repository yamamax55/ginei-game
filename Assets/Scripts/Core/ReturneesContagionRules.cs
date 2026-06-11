using UnityEngine;

namespace Ginei
{
    /// <summary>帰還兵の厭戦伝播の調整係数（レマルク『西部戦線異状なし』）。</summary>
    public readonly struct ReturneesContagionParams
    {
        /// <summary>戦場のトラウマとプロパガンダとの落差が幻滅を深める勾配（帰還兵の幻滅係数）。</summary>
        public readonly float disillusionGain;
        /// <summary>幻滅した帰還兵の割合が厭戦を後方へ伝播させる速さ（伝播係数）。</summary>
        public readonly float contagionGain;
        /// <summary>帰還兵の厭戦が後方の希望/戦争支持を侵食する速さ（侵食係数・dtに掛かる）。</summary>
        public readonly float erosionRate;
        /// <summary>封殺しても厭戦が地下で広がる残り火の強さ（地下浸透係数）。</summary>
        public readonly float undergroundGain;
        /// <summary>一次情報の重み＝帰還兵の証言がプロパガンダより説得力を増す係数（実体験プレミアム）。</summary>
        public readonly float credibilityGain;
        /// <summary>厭戦が後方へ広がり戦意が崩れつつあるとみなす伝播の閾値。</summary>
        public readonly float spreadThreshold;

        public ReturneesContagionParams(float disillusionGain, float contagionGain, float erosionRate, float undergroundGain, float credibilityGain, float spreadThreshold)
        {
            this.disillusionGain = Mathf.Clamp01(disillusionGain);
            this.contagionGain = Mathf.Max(0f, contagionGain);
            this.erosionRate = Mathf.Max(0f, erosionRate);
            this.undergroundGain = Mathf.Clamp01(undergroundGain);
            this.credibilityGain = Mathf.Max(0f, credibilityGain);
            this.spreadThreshold = Mathf.Clamp01(spreadThreshold);
        }

        /// <summary>既定＝幻滅0.7・伝播1.2・侵食0.5・地下0.6・信用0.8・伝播閾値0.4。</summary>
        public static ReturneesContagionParams Default => new ReturneesContagionParams(0.7f, 1.2f, 0.5f, 0.6f, 0.8f, 0.4f);
    }

    /// <summary>
    /// 帰還兵の厭戦伝播＝厭戦が伝染する純ロジック（RMK-6 #1418・レマルク『西部戦線異状なし』）。
    /// 前線から戻った兵士が、銃後（後方社会）へ<b>厭戦・幻滅・トラウマ</b>を持ち込み、後方に残っていた
    /// 希望や戦意を内側から侵食する＝帰還兵が語る現実が、プロパガンダが支えていた銃後の士気を崩す。
    /// 多くの幻滅した帰還兵が戻るほど厭戦は速く伝播し、当局はその証言を封じようとするが、封じても
    /// 厭戦は地下で広がる（公に言えないが皆が知る）。帰還兵の証言は実体験ゆえプロパガンダより信用が高い。
    /// 「厭戦は伝染する」を式に出す。乱数なし・決定論。
    /// <see cref="RefugeeRules"/>（戦火による難民の流入負担と統合）・<see cref="HopeRules"/>（共同体の希望と末人）とは別
    /// ＝こちらは帰還兵が銃後へ厭戦と幻滅を持ち込む厭戦の伝播（侵食先は <see cref="HopeRules"/> の希望へ）。
    /// 前線と銃後の情報非対称の乖離崩壊は <see cref="HomeFrontRules"/>（同EPIC RMK）、
    /// 世論操作の効果は <see cref="PropagandaRules"/> が扱う＝分担。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReturneesContagionRules
    {
        /// <summary>
        /// 帰還兵の幻滅（0..1）＝戦場のトラウマ combatTrauma(0..1) と、プロパガンダとの落差 propagandaGap(0..1)
        /// が幻滅を深める。悲惨な現実を見たうえに銃後の英雄譚との落差を知るほど深い＝trauma×gap×(1+disillusionGain)
        /// を上限1へ。トラウマが無ければ持ち込む幻滅は無く、落差が無ければ（銃後も現実を知る）幻滅は生じない。
        /// </summary>
        public static float ReturneeDisillusionment(float combatTrauma, float propagandaGap, ReturneesContagionParams p)
        {
            float trauma = Mathf.Clamp01(combatTrauma);
            float gap = Mathf.Clamp01(propagandaGap);
            return Mathf.Clamp01(trauma * gap * (1f + p.disillusionGain));
        }

        public static float ReturneeDisillusionment(float combatTrauma, float propagandaGap)
            => ReturneeDisillusionment(combatTrauma, propagandaGap, ReturneesContagionParams.Default);

        /// <summary>
        /// 厭戦の伝播速度（0..1）＝帰還兵の割合 returneeFraction(0..1) ×幻滅 returneeDisillusionment(0..1)。
        /// 多くの幻滅した帰還兵が戻るほど厭戦が速く後方へ伝播する＝fraction×disillusionment×contagionGain を上限1へ。
        /// 帰還兵が居なければ（fraction=0）伝播せず、幻滅が無ければ持ち込むものが無い（どちらか欠ければ回らない）。
        /// </summary>
        public static float ContagionRate(float returneeFraction, float returneeDisillusionment, ReturneesContagionParams p)
        {
            float frac = Mathf.Clamp01(returneeFraction);
            float dis = Mathf.Clamp01(returneeDisillusionment);
            return Mathf.Clamp01(frac * dis * p.contagionGain);
        }

        public static float ContagionRate(float returneeFraction, float returneeDisillusionment)
            => ContagionRate(returneeFraction, returneeDisillusionment, ReturneesContagionParams.Default);

        /// <summary>
        /// 希望の侵食（0..1）＝帰還兵の厭戦 contagionRate(0..1) が後方の希望 homefrontHope(0..1) を時間 dt で侵食する。
        /// 伝播が強いほど希望が速く削れる＝hope − hope×contagionRate×erosionRate×dt を下限0へ（<see cref="HopeRules"/> へ）。
        /// 伝播が無ければ希望は減らない。dt 非依存・timeScale 追従は呼び出し側で dt を渡す。
        /// </summary>
        public static float HopeErosionTick(float homefrontHope, float contagionRate, float dt, ReturneesContagionParams p)
        {
            float hope = Mathf.Clamp01(homefrontHope);
            float rate = Mathf.Clamp01(contagionRate);
            float step = hope * rate * p.erosionRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(hope - step);
        }

        public static float HopeErosionTick(float homefrontHope, float contagionRate, float dt)
            => HopeErosionTick(homefrontHope, contagionRate, dt, ReturneesContagionParams.Default);

        /// <summary>
        /// 後方の戦争支持の崩壊（0..1）＝帰還兵の証言（伝播 contagionRate 0..1）が戦争支持 homefrontSupport(0..1) を
        /// 時間 dt で崩す。現実が幻想を破る＝support − support×contagionRate×erosionRate×dt を下限0へ。
        /// 伝播が無ければ支持は崩れない。希望の侵食と同系統で支持を別途削る。
        /// </summary>
        public static float WarSupportDecay(float homefrontSupport, float contagionRate, float dt, ReturneesContagionParams p)
        {
            float support = Mathf.Clamp01(homefrontSupport);
            float rate = Mathf.Clamp01(contagionRate);
            float step = support * rate * p.erosionRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(support - step);
        }

        public static float WarSupportDecay(float homefrontSupport, float contagionRate, float dt)
            => WarSupportDecay(homefrontSupport, contagionRate, dt, ReturneesContagionParams.Default);

        /// <summary>
        /// 封殺の圧力（0..1）＝当局の公式の物語 officialNarrative(0..1) と帰還兵の証言 returneeTestimony(0..1) が
        /// 衝突するほど、当局は証言を封じようとする（厭戦の伝播を止めようとする検閲）＝narrative×testimony。
        /// 物語が強く証言も大きいほど封殺が激しい。物語が無いか証言が無ければ封じる動機は薄い。
        /// </summary>
        public static float SilencingPressure(float officialNarrative, float returneeTestimony)
        {
            float narrative = Mathf.Clamp01(officialNarrative);
            float testimony = Mathf.Clamp01(returneeTestimony);
            return Mathf.Clamp01(narrative * testimony);
        }

        /// <summary>
        /// 地下の不満（0..1）＝封殺の圧力 silencingPressure(0..1) があっても、伝播 contagionRate(0..1) は地下で広がる。
        /// 公には言えないが皆が知る＝封じた残り火が地下に潜る＝contagionRate×(silencingPressure×undergroundGain + (1−silencingPressure))
        /// を上限1へ＝封殺が弱ければ表で広がり、強く封じても undergroundGain ぶんは地下で残る（完全には消えない）。
        /// </summary>
        public static float UndergroundDiscontent(float silencingPressure, float contagionRate, ReturneesContagionParams p)
        {
            float sil = Mathf.Clamp01(silencingPressure);
            float rate = Mathf.Clamp01(contagionRate);
            float channel = sil * p.undergroundGain + (1f - sil);
            return Mathf.Clamp01(rate * channel);
        }

        public static float UndergroundDiscontent(float silencingPressure, float contagionRate)
            => UndergroundDiscontent(silencingPressure, contagionRate, ReturneesContagionParams.Default);

        /// <summary>
        /// 帰還兵の証言の信用（0..1）＝証言 returneeTestimony(0..1) は実体験の権威 firsthandAuthority(0..1) ゆえ
        /// プロパガンダより説得力が高い＝testimony×(1+firsthandAuthority×credibilityGain) を上限1へ。
        /// 一次情報の重みで信用が底上げされる。権威がゼロでも証言そのものは届く（素地は残る）。
        /// </summary>
        public static float VeteranCredibility(float returneeTestimony, float firsthandAuthority, ReturneesContagionParams p)
        {
            float testimony = Mathf.Clamp01(returneeTestimony);
            float authority = Mathf.Clamp01(firsthandAuthority);
            return Mathf.Clamp01(testimony * (1f + authority * p.credibilityGain));
        }

        public static float VeteranCredibility(float returneeTestimony, float firsthandAuthority)
            => VeteranCredibility(returneeTestimony, firsthandAuthority, ReturneesContagionParams.Default);

        /// <summary>
        /// 厭戦が広がりつつあるか＝伝播速度 contagionRate(0..1) が閾値 threshold(0..1) を超えたか。
        /// 厭戦が後方へ広がり戦意が内側から崩れつつある成立判定。
        /// </summary>
        public static bool IsWarWearinessSpreading(float contagionRate, float threshold)
            => Mathf.Clamp01(contagionRate) >= Mathf.Clamp01(threshold);

        public static bool IsWarWearinessSpreading(float contagionRate)
            => IsWarWearinessSpreading(contagionRate, ReturneesContagionParams.Default.spreadThreshold);
    }
}
