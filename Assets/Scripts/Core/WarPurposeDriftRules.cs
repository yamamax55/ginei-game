using UnityEngine;

namespace Ginei
{
    /// <summary>開戦理由の腐食（三十年戦争型）の調整係数。</summary>
    public readonly struct WarPurposeDriftParams
    {
        /// <summary>戦争目的が理想（大義）から権力政治へ漂流する基礎速度（毎秒これだけドリフトする）。</summary>
        public readonly float driftRate;
        /// <summary>戦争の長期化がドリフトを加速する重み（長引くほど大義が薄れる）。</summary>
        public readonly float durationWeight;
        /// <summary>大義の形骸化の基礎速度（誰も信じなくなる毎秒の進行）。</summary>
        public readonly float hollowingRate;
        /// <summary>冷笑（シニシズム）が形骸化を加速する重み（口実と化す）。</summary>
        public readonly float cynicismWeight;
        /// <summary>利害一致がイデオロギー的同盟を逆転させる強さ（大義より実利＝カトリック仏が新教側へ）。</summary>
        public readonly float realignmentWeight;
        /// <summary>戦争目的の腐食が正統性を蝕む強さ（何のために戦うか分からなくなる）。</summary>
        public readonly float legitimacyErosionWeight;
        /// <summary>利益動機が傭兵戦争の論理を強める重み（略奪・領土）。</summary>
        public readonly float profitWeight;
        /// <summary>純然たる権力闘争戦争と見なす権力政治割合の閾値。</summary>
        public readonly float powerPoliticsThreshold;

        public WarPurposeDriftParams(float driftRate, float durationWeight, float hollowingRate,
            float cynicismWeight, float realignmentWeight, float legitimacyErosionWeight,
            float profitWeight, float powerPoliticsThreshold)
        {
            this.driftRate = Mathf.Clamp01(driftRate);
            this.durationWeight = Mathf.Clamp01(durationWeight);
            this.hollowingRate = Mathf.Clamp01(hollowingRate);
            this.cynicismWeight = Mathf.Clamp01(cynicismWeight);
            this.realignmentWeight = Mathf.Clamp01(realignmentWeight);
            this.legitimacyErosionWeight = Mathf.Clamp01(legitimacyErosionWeight);
            this.profitWeight = Mathf.Clamp01(profitWeight);
            this.powerPoliticsThreshold = Mathf.Clamp01(powerPoliticsThreshold);
        }

        /// <summary>既定＝ドリフト速度0.1/s・長期化重み0.6・形骸化速度0.1/s・冷笑重み0.7・同盟逆転重み0.8・正統性侵食0.5・利益重み0.6・権力闘争閾値0.6。</summary>
        public static WarPurposeDriftParams Default =>
            new WarPurposeDriftParams(0.1f, 0.6f, 0.1f, 0.7f, 0.8f, 0.5f, 0.6f, 0.6f);
    }

    /// <summary>
    /// 開戦理由の腐食の純ロジック（TYW-3 #1426・三十年戦争）＝戦争目的の変質。
    /// 「戦争が長引くと当初の大義（宗教＝新教vs旧教）が形骸化し、純粋な権力政治・領土欲へドリフトする＝
    /// 最後はカトリックのフランスが新教側で参戦するなど、イデオロギー的同盟が逆転し、宗教戦争が王朝間の
    /// 覇権争いに変質した」を式に出す：戦争が長引くほど当初の理想的大義（宗教・イデオロギー）が薄れ権力政治へ
    /// 漂流し（<see cref="PurposeDrift"/>）、戦争目的に占める純粋な権力・領土欲の割合が増え
    /// （<see cref="PowerPoliticsShare"/>）、イデオロギー的には敵でも利害が一致すれば同盟が逆転し
    /// （<see cref="IdeologicalAllianceReversal"/>）、大義は形骸化して誰も信じなくなり（<see cref="CauseHollowing"/>）、
    /// 大義が消えると戦争が利益（略奪・領土）の論理で動き（<see cref="MercenaryWarLogic"/>）、当初の大義からの
    /// 逸脱が戦争の正統性を蝕み（<see cref="LegitimacyErosionFromDrift"/>）、大義が薄れると同盟が利害でころころ
    /// 変わり（<see cref="RealignmentVolatility"/>）、ついには純然たる権力闘争になる（<see cref="IsPowerPoliticsWar"/>）。
    /// <see cref="WarGoalRules"/>（戦争目標・厭戦・講和受諾）とは別＝あちらは戦い続けるか手打ちにするかの決断、
    /// こちらは戦争目的が当初の大義から権力闘争へ変質する＝開戦理由の腐食。
    /// <see cref="DiplomacyRules"/>（外交状態の遷移・関係値）とは別＝こちらは大義のドリフトに伴う同盟逆転の力学。
    /// <see cref="SovereigntyNormRules"/>（主権規範・ウェストファリア体制）とは別＝あちらは惨禍が規範を生む戦後の後段、
    /// こちらは戦中に進む戦争目的の漂流。<see cref="KontributionRules"/>（軍税徴発・戦争が戦争を養う）とは別＝
    /// 大義が消えた戦争が利益の論理で動く（<see cref="MercenaryWarLogic"/>）でそちらと接続する。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WarPurposeDriftRules
    {
        /// <summary>0..1 に丸める。</summary>
        public static float Clamp01(float v) => Mathf.Clamp01(v);

        /// <summary>
        /// 戦争目的のドリフト（戻り値＝新しい腐食度0..1）。戦争が長引くほど当初の理想的大義（宗教・イデオロギー）が
        /// 薄れ権力政治へ漂流する：ドリフト駆動＝ドリフト速度×(1−当初の理想的大義の純度)×(長期化×durationWeight)。
        /// 当初の理想が強いほど（idealisticPurpose 高い）腐食しにくく、戦争が長引くほど（warDuration 高い）速く漂流する。
        /// dtは負を0に。
        /// </summary>
        public static float PurposeDrift(float idealisticPurpose, float warDuration, float dt, WarPurposeDriftParams p)
        {
            float ideal = Mathf.Clamp01(idealisticPurpose);
            float dur = Mathf.Clamp01(warDuration);
            float t = Mathf.Max(0f, dt);
            float drive = p.driftRate * (1f - ideal) * (dur * p.durationWeight);
            return Mathf.Clamp01(drive * t);
        }

        public static float PurposeDrift(float idealisticPurpose, float warDuration, float dt)
            => PurposeDrift(idealisticPurpose, warDuration, dt, WarPurposeDriftParams.Default);

        /// <summary>
        /// 権力政治の割合（0..1）＝戦争目的に占める純粋な権力・領土欲の割合＝腐食度そのもの。
        /// 理想が腐食した分だけ戦争は権力政治になる＝ドリフトに正比例（宗教戦争が王朝間の覇権争いに変質した分）。
        /// </summary>
        public static float PowerPoliticsShare(float purposeDrift)
        {
            return Mathf.Clamp01(purposeDrift);
        }

        /// <summary>
        /// イデオロギー的同盟の逆転（0..1＝逆転の強さ）。イデオロギー的には敵でも利害が一致すれば同盟が逆転する＝
        /// 利害一致(powerInterest)×(1−イデオロギー整合)×realignmentWeight。イデオロギー的に対立する(ideologyAlignment 低い)
        /// ほど、かつ利害が一致するほど逆転が強い＝カトリックのフランスが新教側で参戦する（大義より実利）。
        /// </summary>
        public static float IdeologicalAllianceReversal(float ideologyAlignment, float powerInterest, WarPurposeDriftParams p)
        {
            float align = Mathf.Clamp01(ideologyAlignment);
            float interest = Mathf.Clamp01(powerInterest);
            return Mathf.Clamp01(interest * (1f - align) * p.realignmentWeight);
        }

        public static float IdeologicalAllianceReversal(float ideologyAlignment, float powerInterest)
            => IdeologicalAllianceReversal(ideologyAlignment, powerInterest, WarPurposeDriftParams.Default);

        /// <summary>
        /// 大義の形骸化（戻り値＝新しい形骸化度0..1）。大義が形骸化し誰も信じなくなる（戦争の口実と化す）：
        /// 形骸化駆動＝形骸化速度×(1−当初の理想的大義の純度)×(冷笑×cynicismWeight)。当初の大義が薄く(idealisticPurpose 低い)、
        /// 冷笑(cynicism)が深いほど速く形骸化する＝大義は誰も信じない口実になる。dtは負を0に。
        /// </summary>
        public static float CauseHollowing(float idealisticPurpose, float cynicism, float dt, WarPurposeDriftParams p)
        {
            float ideal = Mathf.Clamp01(idealisticPurpose);
            float cyn = Mathf.Clamp01(cynicism);
            float t = Mathf.Max(0f, dt);
            float drive = p.hollowingRate * (1f - ideal) * (cyn * p.cynicismWeight);
            return Mathf.Clamp01(drive * t);
        }

        public static float CauseHollowing(float idealisticPurpose, float cynicism, float dt)
            => CauseHollowing(idealisticPurpose, cynicism, dt, WarPurposeDriftParams.Default);

        /// <summary>
        /// 傭兵戦争の論理（0..1）。大義が消えると戦争が利益（略奪・領土）の論理で動く＝
        /// 権力政治割合(powerPoliticsShare)×(利益動機×profitWeight)。大義を失った戦争(powerPoliticsShare 高い)ほど、
        /// かつ利益動機(profitMotive)が強いほど傭兵戦争の論理が支配する＝<see cref="KontributionRules"/>（軍税徴発）と接続。
        /// </summary>
        public static float MercenaryWarLogic(float powerPoliticsShare, float profitMotive, WarPurposeDriftParams p)
        {
            float share = Mathf.Clamp01(powerPoliticsShare);
            float profit = Mathf.Clamp01(profitMotive);
            return Mathf.Clamp01(share * (profit * p.profitWeight));
        }

        public static float MercenaryWarLogic(float powerPoliticsShare, float profitMotive)
            => MercenaryWarLogic(powerPoliticsShare, profitMotive, WarPurposeDriftParams.Default);

        /// <summary>
        /// 腐食による正統性の侵食（0..1＝失われる正統性）。当初の大義からの逸脱が戦争の正統性を蝕む＝
        /// 何のために戦うのか分からなくなる：腐食度(purposeDrift)×当初の大義の強さ(originalJustification)×legitimacyErosionWeight。
        /// 当初の正当化が強かった戦争ほど、そこからの逸脱（腐食）が正統性を大きく損なう＝裏切りの落差。
        /// </summary>
        public static float LegitimacyErosionFromDrift(float purposeDrift, float originalJustification, WarPurposeDriftParams p)
        {
            float drift = Mathf.Clamp01(purposeDrift);
            float orig = Mathf.Clamp01(originalJustification);
            return Mathf.Clamp01(drift * orig * p.legitimacyErosionWeight);
        }

        public static float LegitimacyErosionFromDrift(float purposeDrift, float originalJustification)
            => LegitimacyErosionFromDrift(purposeDrift, originalJustification, WarPurposeDriftParams.Default);

        /// <summary>
        /// 再編の流動性（0..1）。大義が薄れると同盟が利害でころころ変わる（流動的な陣営）＝
        /// 利害(powerInterest)×(1−イデオロギー整合)。イデオロギーの拘束(ideologyAlignment)が弱いほど、かつ利害(powerInterest)が
        /// 大きいほど陣営が利害で流動する＝大義のタガが外れた戦争は同盟が固定されない。
        /// </summary>
        public static float RealignmentVolatility(float powerInterest, float ideologyAlignment)
        {
            float interest = Mathf.Clamp01(powerInterest);
            float align = Mathf.Clamp01(ideologyAlignment);
            return Mathf.Clamp01(interest * (1f - align));
        }

        /// <summary>
        /// 純然たる権力闘争戦争の判定＝権力政治割合が threshold 以上か。閾値超えで「大義を失い純然たる
        /// 権力闘争になった戦争＝宗教戦争が王朝間の覇権争いに変質した」と見なす。
        /// </summary>
        public static bool IsPowerPoliticsWar(float powerPoliticsShare, float threshold)
        {
            return Mathf.Clamp01(powerPoliticsShare) >= Mathf.Clamp01(threshold);
        }

        public static bool IsPowerPoliticsWar(float powerPoliticsShare, WarPurposeDriftParams p)
            => IsPowerPoliticsWar(powerPoliticsShare, p.powerPoliticsThreshold);

        public static bool IsPowerPoliticsWar(float powerPoliticsShare)
            => IsPowerPoliticsWar(powerPoliticsShare, WarPurposeDriftParams.Default.powerPoliticsThreshold);
    }
}
