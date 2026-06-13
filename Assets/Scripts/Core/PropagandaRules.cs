using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 世論戦の純データ（プロパガンダ＝同盟・帝国双方が戦争を支える情報操作）。publicSupport は世論の戦争支持(0..1)、
    /// credibility は当局の信用残高(0..1)、reach は媒体の到達率(0..1)。解決は <see cref="PropagandaRules"/> が窓口。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class PropagandaState
    {
        public float publicSupport;  // 世論の支持 0..1
        public float credibility;    // 当局の信用 0..1（嘘がばれると下がる）
        public float reach;          // 媒体の到達率 0..1

        public PropagandaState() { credibility = 1f; }

        public PropagandaState(float publicSupport, float credibility = 1f, float reach = 0.5f)
        {
            this.publicSupport = Mathf.Clamp01(publicSupport);
            this.credibility = Mathf.Clamp01(credibility);
            this.reach = Mathf.Clamp01(reach);
        }
    }

    /// <summary>世論戦の調整係数（プロパガンダ）。</summary>
    public readonly struct PropagandaParams
    {
        /// <summary>1tick の支持シフトの最大幅。</summary>
        public readonly float maxShift;
        /// <summary>信用浸食率（誇張＝真実との乖離 truthGap が信用を削る速さ・per dt）。</summary>
        public readonly float credibilityErosion;
        /// <summary>信用回復率（乖離が無いとき信用が戻る速さ・per dt）。</summary>
        public readonly float credibilityRecovery;
        /// <summary>製造された合意とみなす乖離の閾値（これ以上の誇張で支えた支持は脆い）。</summary>
        public readonly float manufactureThreshold;

        public PropagandaParams(float maxShift, float credibilityErosion, float credibilityRecovery, float manufactureThreshold)
        {
            this.maxShift = Mathf.Max(0f, maxShift);
            this.credibilityErosion = Mathf.Max(0f, credibilityErosion);
            this.credibilityRecovery = Mathf.Max(0f, credibilityRecovery);
            this.manufactureThreshold = Mathf.Clamp01(manufactureThreshold);
        }

        /// <summary>既定＝最大シフト0.1・信用浸食0.3・回復0.1・製造閾値0.5。</summary>
        public static PropagandaParams Default => new PropagandaParams(0.1f, 0.3f, 0.1f, 0.5f);
    }

    /// <summary>
    /// 世論戦の純ロジック（プロパガンダ）。プロパガンダの効果は「到達率×信用×主張の強さ」で決まり、
    /// 自軍プロパガンダと敵の対抗宣伝の差分が世論支持を押す。だが真実との乖離（truthGap）が大きい誇張は
    /// 信用を浸食し、いずれ嘘が露見して効力を失う＝力でなく信用で支持を作る。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PropagandaRules
    {
        /// <summary>
        /// プロパガンダの実効効果（0..1）＝到達率×信用×主張の強さ messageStrength（0..1）×（1−検閲突破の難しさ censorship）。
        /// censorship は敵の検閲・情報統制で、こちらの主張が届かなくする 0..1。
        /// </summary>
        public static float Effectiveness(float reach, float credibility, float messageStrength, float censorship, PropagandaParams p)
        {
            float e = Mathf.Clamp01(reach) * Mathf.Clamp01(credibility) * Mathf.Clamp01(messageStrength) * (1f - Mathf.Clamp01(censorship));
            return Mathf.Clamp01(e);
        }

        public static float Effectiveness(float reach, float credibility, float messageStrength, float censorship)
            => Effectiveness(reach, credibility, messageStrength, censorship, PropagandaParams.Default);

        /// <summary>
        /// 世論支持の1tickシフト後の値（0..1）。自軍プロパガンダ効果 ownEffect と敵の対抗宣伝 enemyEffect の差分を
        /// maxShift にスケールして現支持へ加える（綱引き）。
        /// </summary>
        public static float SupportShift(float currentSupport, float ownEffect, float enemyEffect, PropagandaParams p)
        {
            float net = Mathf.Clamp01(ownEffect) - Mathf.Clamp01(enemyEffect);
            return Mathf.Clamp01(currentSupport + net * p.maxShift);
        }

        public static float SupportShift(float currentSupport, float ownEffect, float enemyEffect)
            => SupportShift(currentSupport, ownEffect, enemyEffect, PropagandaParams.Default);

        /// <summary>
        /// 信用の更新（0..1）。真実との乖離 truthGap(0..1) が manufactureThreshold を超える分だけ信用を浸食し、
        /// 乖離が無ければ credibilityRecovery で回復する。大きな嘘ほど速く信用を失う。
        /// </summary>
        public static float UpdateCredibility(float credibility, float truthGap, float dt, PropagandaParams p)
        {
            float c = Mathf.Clamp01(credibility);
            float gap = Mathf.Clamp01(truthGap);
            float d = Mathf.Max(0f, dt);
            float over = Mathf.Max(0f, gap - p.manufactureThreshold);
            if (over > 0f)
                c -= over * p.credibilityErosion * d;
            else
                c += p.credibilityRecovery * d;
            return Mathf.Clamp01(c);
        }

        public static float UpdateCredibility(float credibility, float truthGap, float dt)
            => UpdateCredibility(credibility, truthGap, dt, PropagandaParams.Default);

        /// <summary>
        /// 製造された合意か＝高い支持が大きな誇張（truthGap≥manufactureThreshold）で支えられている状態。
        /// 信用が崩れれば一気に剥落する脆い支持。supportFloor 未満の支持は対象外（そもそも支えていない）。
        /// </summary>
        public static bool IsManufacturedConsent(float publicSupport, float truthGap, PropagandaParams p, float supportFloor = 0.5f)
        {
            return Mathf.Clamp01(publicSupport) >= Mathf.Clamp01(supportFloor)
                && Mathf.Clamp01(truthGap) >= p.manufactureThreshold;
        }

        public static bool IsManufacturedConsent(float publicSupport, float truthGap)
            => IsManufacturedConsent(publicSupport, truthGap, PropagandaParams.Default);
    }
}
