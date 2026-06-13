using UnityEngine;

namespace Ginei
{
    /// <summary>私掠免許の調整係数。</summary>
    public readonly struct PrivateerParams
    {
        /// <summary>取り分×獲物の旨味を応募率へ写す倍率（1超で「儲かる免許」は早く満員になる）。</summary>
        public readonly float attractionScale;
        /// <summary>免許の正統性ゼロ時の通商破壊効果の下限倍率（後ろ盾なき拿捕は売り捌けず効率が落ちる）。</summary>
        public readonly float minLegitimacyFactor;
        /// <summary>統制の緩み速度（per dt・取り分最大×監督ゼロのとき）＝儲けに味をしめる速さ。</summary>
        public readonly float slipRate;
        /// <summary>監督が緩みを締め直す速度（per dt・監督1のとき）。</summary>
        public readonly float disciplineRate;
        /// <summary>中立国船襲撃の事故率上限（緩み1×中立交通1のとき）。</summary>
        public readonly float neutralIncidentRate;
        /// <summary>戦後海賊化の下限割合（統制が効いていても食い扶持を失えばこれだけは野に下る）。</summary>
        public readonly float baseConversion;
        /// <summary>戦後海賊化の上限割合（緩みきった私掠船は大半が海賊になる）。</summary>
        public readonly float maxConversion;

        public PrivateerParams(float attractionScale, float minLegitimacyFactor, float slipRate,
                               float disciplineRate, float neutralIncidentRate,
                               float baseConversion, float maxConversion)
        {
            this.attractionScale = Mathf.Max(0f, attractionScale);
            this.minLegitimacyFactor = Mathf.Clamp01(minLegitimacyFactor);
            this.slipRate = Mathf.Max(0f, slipRate);
            this.disciplineRate = Mathf.Max(0f, disciplineRate);
            this.neutralIncidentRate = Mathf.Clamp01(neutralIncidentRate);
            this.baseConversion = Mathf.Clamp01(baseConversion);
            // 上限は必ず下限以上（逆転を許さない）
            this.maxConversion = Mathf.Max(this.baseConversion, Mathf.Clamp01(maxConversion));
        }

        /// <summary>既定＝応募倍率1.5・正統性下限0.5・緩み0.1・締め直し0.05・事故上限0.5・海賊化 下限0.2/上限0.8。</summary>
        public static PrivateerParams Default => new PrivateerParams(1.5f, 0.5f, 0.1f, 0.05f, 0.5f, 0.2f, 0.8f);
    }

    /// <summary>
    /// 私掠免許の純ロジック。民間武装を国家が公認して敵通商を襲わせる＝安価な戦力だが、儲けに味をしめた
    /// 私掠船は命令より獲物を選び（統制の緩み）、中立国船を襲って外交事故を起こし、戦後は食い扶持を失って
    /// 海賊になる＝「私掠は借りた暴力、返却日に高くつく」。無主の暴力 <see cref="PiracyRules"/> の制度化＝
    /// 対になる公認側で、<see cref="PostwarPiracyConversion"/> の戻り値は PiracyRules の海賊勢力へ流れ込む。
    /// 国家正規軍の通商破壊（<see cref="CommerceRaidingRules"/>）とも別系統。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PrivateerRules
    {
        /// <summary>
        /// 私掠免許への応募（0..1）＝戦利品の取り分 prizeShare(0..1)×敵通商の流量 enemyTradeVolume(0..1)×倍率。
        /// 儲かる獲物と気前のよい取り分が民間武装を集める＝どちらかゼロなら誰も来ない。
        /// </summary>
        public static float CommissionAttraction(float prizeShare, float enemyTradeVolume, PrivateerParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(prizeShare) * Mathf.Clamp01(enemyTradeVolume) * p.attractionScale);
        }

        public static float CommissionAttraction(float prizeShare, float enemyTradeVolume)
            => CommissionAttraction(prizeShare, enemyTradeVolume, PrivateerParams.Default);

        /// <summary>
        /// 通商破壊の実効戦力＝私掠戦力×正統性倍率（minLegitimacyFactor..1）。国家の後ろ盾
        /// letterOfMarqueLegitimacy(0..1) が拿捕を「合法」にする＝戦利品を堂々と売れ、中立国との摩擦も軽い。
        /// 基準戦力は非破壊（実効値パターン）。
        /// </summary>
        public static float RaidingEffectiveness(float privateerStrength, float letterOfMarqueLegitimacy, PrivateerParams p)
        {
            float s = Mathf.Max(0f, privateerStrength);
            return s * Mathf.Lerp(p.minLegitimacyFactor, 1f, Mathf.Clamp01(letterOfMarqueLegitimacy));
        }

        public static float RaidingEffectiveness(float privateerStrength, float letterOfMarqueLegitimacy)
            => RaidingEffectiveness(privateerStrength, letterOfMarqueLegitimacy, PrivateerParams.Default);

        /// <summary>
        /// 統制の緩み（0..1）の1tick後の値。取り分 prizeShare が大きいほど儲けに味をしめて緩み
        /// （監督 oversight(0..1) の届かないぶんだけ進む）、監督は disciplineRate で締め直す。
        /// 緩んだ私掠船は命令より獲物を選ぶ＝事故と戦後海賊化の火種。
        /// </summary>
        public static float ControlSlippageTick(float slippage, float prizeShare, float oversight, float dt, PrivateerParams p)
        {
            float sl = Mathf.Clamp01(slippage);
            float d = Mathf.Max(0f, dt);
            float watch = Mathf.Clamp01(oversight);
            float greed = p.slipRate * Mathf.Clamp01(prizeShare) * (1f - watch) * d;
            float tighten = p.disciplineRate * watch * d;
            return Mathf.Clamp01(sl + greed - tighten);
        }

        public static float ControlSlippageTick(float slippage, float prizeShare, float oversight, float dt)
            => ControlSlippageTick(slippage, prizeShare, oversight, dt, PrivateerParams.Default);

        /// <summary>
        /// 中立国船襲撃の外交事故率（0..neutralIncidentRate）＝統制の緩み×中立交通量 neutralTraffic(0..1)。
        /// 統制が効いていれば（slippage=0）事故ゼロ、緩むほど免許状の文言は読まれなくなる。
        /// </summary>
        public static float NeutralIncident(float slippage, float neutralTraffic, PrivateerParams p)
        {
            return p.neutralIncidentRate * Mathf.Clamp01(slippage) * Mathf.Clamp01(neutralTraffic);
        }

        public static float NeutralIncident(float slippage, float neutralTraffic)
            => NeutralIncident(slippage, neutralTraffic, PrivateerParams.Default);

        /// <summary>
        /// 戦後に海賊へ転じる私掠戦力＝戦力×Lerp(baseConversion, maxConversion, slippage)×(1−退役金 demobilizationPay)。
        /// 食い扶持を失った私掠船は <see cref="PiracyRules"/> の海賊勢力になる（戻り値を海賊勢力へ加える）＝
        /// 借りた暴力の返却日。退役金(0..1)で買い取れる＝満額なら全員カタギに戻る。統制が効いていても
        /// baseConversion ぶんは野に下る＝安価な戦力のツケ。
        /// </summary>
        public static float PostwarPiracyConversion(float privateerStrength, float slippage, float demobilizationPay, PrivateerParams p)
        {
            float s = Mathf.Max(0f, privateerStrength);
            float fraction = Mathf.Lerp(p.baseConversion, p.maxConversion, Mathf.Clamp01(slippage));
            return s * fraction * (1f - Mathf.Clamp01(demobilizationPay));
        }

        public static float PostwarPiracyConversion(float privateerStrength, float slippage, float demobilizationPay)
            => PostwarPiracyConversion(privateerStrength, slippage, demobilizationPay, PrivateerParams.Default);
    }
}
