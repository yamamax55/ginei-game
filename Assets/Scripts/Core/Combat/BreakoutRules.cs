using UnityEngine;

namespace Ginei
{
    /// <summary>包囲突破の調整係数。突破口の集中・脱出路の開き・殿の崩れ・損害の重みを束ねる。</summary>
    public readonly struct BreakoutParams
    {
        /// <summary>包囲環が一様に厚いときに残る最弱点の脆さの底（一様でも穴は皆無ではない・0..1）。</summary>
        public readonly float weaknessFloor;
        /// <summary>突破口を破る指数（集中打撃の効きの非線形性・小さいほど少ない集中で破れる）。</summary>
        public readonly float ruptureExponent;
        /// <summary>脱出路の幅が脱出割合へ効く感度（被包囲規模あたりの通り抜けやすさ）。</summary>
        public readonly float corridorEscapeRate;
        /// <summary>殿の崩壊が脱出割合を削る比重（崩れると脱出路が閉じる・0..1）。</summary>
        public readonly float rearguardPenalty;
        /// <summary>突破に伴う損害の基準（脱出割合が低く殿が崩れるほど損害が増える）。</summary>
        public readonly float lossScale;

        public BreakoutParams(float weaknessFloor, float ruptureExponent, float corridorEscapeRate,
            float rearguardPenalty, float lossScale)
        {
            this.weaknessFloor = Mathf.Clamp01(weaknessFloor);
            this.ruptureExponent = Mathf.Clamp(ruptureExponent, 0.1f, 4f);
            this.corridorEscapeRate = Mathf.Max(0.0001f, corridorEscapeRate);
            this.rearguardPenalty = Mathf.Clamp01(rearguardPenalty);
            this.lossScale = Mathf.Clamp01(lossScale);
        }

        /// <summary>既定＝最弱点の底0.1・突破指数1.5・脱出感度1・殿崩壊比重0.6・損害基準0.5。</summary>
        public static BreakoutParams Default => new BreakoutParams(0.1f, 1.5f, 1f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 包囲突破の純ロジック（包囲された軍が一点に戦力を集中して囲みを破り脱出する）。
    /// 包囲された側が、包囲環の最も薄い一点に戦力を集中して突破口を開き、脱出する。突破には集中と勢いが要り、
    /// 殿（しんがり）が崩れると脱出路が閉じて壊滅する＝「集中で穴を開け、殿で道を保ち、大軍ほど抜けにくい」。
    /// <see cref="EncirclementRules"/>（包囲度・降伏・包囲する側の締め上げ）に対し、本クラスは被包囲側の脱出に特化する。
    /// 戦力の一点集中そのものは <see cref="ForceConcentrationRules"/> が担い、ここはその集中を「囲みを破る」文脈で消費する＝別系統。
    /// 全入力クランプ・乱数なし決定論・基準値非破壊（係数/割合を返すのみ）。盤面非依存の plain 引数。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BreakoutRules
    {
        public const float SuccessThreshold = 0.4f; // 既定の突破成功とみなす脱出割合

        /// <summary>包囲環の最弱点の脆さ（0..1）。包囲が一様(=1)なほど穴が無く weaknessFloor へ、不均一(=0)なほど1へ。</summary>
        public static float BreakoutPointWeakness(float encirclementUniformity, BreakoutParams p)
        {
            float u = Mathf.Clamp01(encirclementUniformity);
            // 不均一さ(1-u)が脆さを生む。一様でも floor の穴は残る。
            return Mathf.Clamp01(p.weaknessFloor + (1f - p.weaknessFloor) * (1f - u));
        }

        public static float BreakoutPointWeakness(float encirclementUniformity)
            => BreakoutPointWeakness(encirclementUniformity, BreakoutParams.Default);

        /// <summary>突破口に集中した打撃＝突破戦力×最弱点の脆さ（薄い一点へ戦力を集める）。</summary>
        public static float ConcentratedPunch(float breakoutForce, float breakoutPointWeakness)
            => Mathf.Max(0f, breakoutForce) * Mathf.Clamp01(breakoutPointWeakness);

        /// <summary>包囲環を破る度合い（0..1）＝集中打撃が突破口の包囲戦力をどれだけ上回るか。</summary>
        public static float RingRupture(float concentratedPunch, float encirclingStrengthAtPoint, BreakoutParams p)
        {
            float punch = Mathf.Max(0f, concentratedPunch);
            float hold = Mathf.Max(0f, encirclingStrengthAtPoint);
            float total = punch + hold;
            if (total <= 0f) return 0f;
            float ratio = punch / total; // 0.5＝拮抗、punch 優位ほど1へ
            return Mathf.Clamp01(Mathf.Pow(ratio, p.ruptureExponent));
        }

        public static float RingRupture(float concentratedPunch, float encirclingStrengthAtPoint)
            => RingRupture(concentratedPunch, encirclingStrengthAtPoint, BreakoutParams.Default);

        /// <summary>開いた脱出路の幅（0..1）＝包囲環の破れがそのまま道の広さになる。</summary>
        public static float EscapeCorridorWidth(float ringRupture)
            => Mathf.Clamp01(ringRupture);

        /// <summary>脱出できる兵力の割合（0..1）。道が広いほど高く、被包囲規模が大きいほど抜けにくい。</summary>
        public static float EscapeFraction(float escapeCorridorWidth, float encircledSize, BreakoutParams p)
        {
            float width = Mathf.Clamp01(escapeCorridorWidth);
            float size = Mathf.Max(1f, encircledSize);
            // 道の通せる量 = 幅×感度。規模に対する相対量で割合を出す＝大軍ほど 1 から遠ざかる。
            float capacity = width * p.corridorEscapeRate;
            return Mathf.Clamp01(capacity / (capacity + size));
        }

        public static float EscapeFraction(float escapeCorridorWidth, float encircledSize)
            => EscapeFraction(escapeCorridorWidth, encircledSize, BreakoutParams.Default);

        /// <summary>殿が崩れて脱出路が閉じるリスク（0..1）＝包囲側の圧力が殿の戦力を上回るほど高い。</summary>
        public static float RearguardCollapse(float rearguardStrength, float encirclingPressure)
        {
            float guard = Mathf.Max(0f, rearguardStrength);
            float pressure = Mathf.Max(0f, encirclingPressure);
            float total = guard + pressure;
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(pressure / total); // 殿が薄く圧力が高いほど1へ
        }

        /// <summary>突破に伴う損害（0..1）。脱出できない兵力＋殿の崩壊で損害が増える。</summary>
        public static float BreakoutLosses(float escapeFraction, float rearguardCollapse, BreakoutParams p)
        {
            float trapped = 1f - Mathf.Clamp01(escapeFraction); // 抜けられなかったぶん
            float collapse = Mathf.Clamp01(rearguardCollapse);
            // 取り残し損害＋殿崩壊による上乗せ損害。
            float loss = trapped + (1f - trapped) * collapse * p.rearguardPenalty;
            return Mathf.Clamp01(loss * (0.5f + p.lossScale));
        }

        public static float BreakoutLosses(float escapeFraction, float rearguardCollapse)
            => BreakoutLosses(escapeFraction, rearguardCollapse, BreakoutParams.Default);

        /// <summary>包囲を突破できたか＝脱出割合が閾値以上。</summary>
        public static bool IsBreakoutSuccessful(float escapeFraction, float threshold = SuccessThreshold)
            => Mathf.Clamp01(escapeFraction) >= Mathf.Clamp01(threshold);
    }
}
