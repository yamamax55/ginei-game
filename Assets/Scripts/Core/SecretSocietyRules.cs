using UnityEngine;

namespace Ginei
{
    /// <summary>秘密結社（地球教型）の調整係数。</summary>
    public readonly struct SecretSocietyParams
    {
        /// <summary>浸透の基礎速度（per dt・吸引力1×絶望1のとき）。</summary>
        public readonly float infiltrationRate;
        /// <summary>絶望ゼロの社会でも応じる者の下限係数（0..1）。満ち足りた社会にも迷い子はいる。</summary>
        public readonly float despairFloor;
        /// <summary>政策の歪みの上限（0..1）。見えない手でも国を完全には操れない。</summary>
        public readonly float maxDistortion;
        /// <summary>活動が露見へ変換される係数（0..1）。大きく動くほど見える。</summary>
        public readonly float exposureFactor;
        /// <summary>一度の摘発で剥がせる浸透の最大割合（0..1）。1未満＝根絶やしにはできない。</summary>
        public readonly float maxCrackdownFraction;
        /// <summary>摘発後の再生速度（per dt）。残った根から網は戻る。</summary>
        public readonly float regrowthRate;

        public SecretSocietyParams(float infiltrationRate, float despairFloor, float maxDistortion,
            float exposureFactor, float maxCrackdownFraction, float regrowthRate)
        {
            this.infiltrationRate = Mathf.Max(0f, infiltrationRate);
            this.despairFloor = Mathf.Clamp01(despairFloor);
            this.maxDistortion = Mathf.Clamp01(maxDistortion);
            this.exposureFactor = Mathf.Clamp01(exposureFactor);
            this.maxCrackdownFraction = Mathf.Clamp01(maxCrackdownFraction);
            this.regrowthRate = Mathf.Max(0f, regrowthRate);
        }

        /// <summary>既定＝浸透0.1・絶望下限0.2・歪み上限0.5・露見0.8・摘発上限0.7・再生0.05。</summary>
        public static SecretSocietyParams Default
            => new SecretSocietyParams(0.1f, 0.2f, 0.5f, 0.8f, 0.7f, 0.05f);
    }

    /// <summary>
    /// 秘密結社の純ロジック（地球教型＝非国家の隠密網）。制度の裏で要職へ浸透する隠れた網＝
    /// 発覚するまで見えず、摘発は「見えている分」しか剥がせない＝秘密結社の力は秘密そのもの。
    /// 公然と信徒を集め社会を覆う宗教は <see cref="ReligionRules"/>、国家が運用する諜報網は
    /// <see cref="EspionageRules"/> が担い、ここは誰の機関でもない隠密網の浸透・操作・摘発・再生を扱う。
    /// 乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SecretSocietyRules
    {
        /// <summary>
        /// 浸透の1tick後の浸透度（0..1）。伸び
        /// ＝基礎速度×教義の吸引力 recruitmentAppeal(0..1)×絶望係数×残余(1−浸透)×dt。
        /// 絶望係数＝despairFloor〜1 を socialDespair(0..1) で補間＝**絶望が深い社会ほど招きに応じる者が多い**
        /// （地球教は敗者の宗教）。飽和に近づくほど伸びは鈍る（勧誘先が尽きる）。
        /// </summary>
        public static float InfiltrationTick(float penetration, float recruitmentAppeal, float socialDespair,
            float dt, SecretSocietyParams p)
        {
            float pen = Mathf.Clamp01(penetration);
            float despairFactor = Mathf.Lerp(p.despairFloor, 1f, Mathf.Clamp01(socialDespair));
            float growth = p.infiltrationRate * Mathf.Clamp01(recruitmentAppeal) * despairFactor
                * (1f - pen) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(pen + growth);
        }

        public static float InfiltrationTick(float penetration, float recruitmentAppeal, float socialDespair, float dt)
            => InfiltrationTick(penetration, recruitmentAppeal, socialDespair, dt, SecretSocietyParams.Default);

        /// <summary>
        /// 要職への到達度（0..1）＝浸透度の二乗。末端に広がるだけでは中枢に届かず、
        /// 深く浸透して初めて要職に手がかかる（浅い網は雑兵ばかり）。
        /// </summary>
        public static float HighOfficeReach(float penetration)
        {
            float pen = Mathf.Clamp01(penetration);
            return pen * pen;
        }

        /// <summary>
        /// 政策の歪み（0..maxDistortion）＝要職到達度×上限。見えない手＝操られている側は
        /// 自分の判断だと思っている（誰も気づかないから抵抗もない）。上限未満＝国を丸ごとは操れない。
        /// </summary>
        public static float PolicyDistortion(float highOfficeReach, SecretSocietyParams p)
        {
            return p.maxDistortion * Mathf.Clamp01(highOfficeReach);
        }

        public static float PolicyDistortion(float highOfficeReach)
            => PolicyDistortion(highOfficeReach, SecretSocietyParams.Default);

        /// <summary>
        /// 尻尾を出すリスク（0..1）＝浸透度×活動の活発さ operationTempo(0..1)×露見係数。
        /// 静かに潜む網（tempo=0）は完全に見えない＝見えない敵とは戦えない。
        /// 大きく動くほど接触点が増えて露見する（露見係数&lt;1＝確実には掴ませない）。
        /// </summary>
        public static float VisibilityRisk(float penetration, float operationTempo, SecretSocietyParams p)
        {
            return Mathf.Clamp01(p.exposureFactor * Mathf.Clamp01(penetration) * Mathf.Clamp01(operationTempo));
        }

        public static float VisibilityRisk(float penetration, float operationTempo)
            => VisibilityRisk(penetration, operationTempo, SecretSocietyParams.Default);

        /// <summary>
        /// 摘発後に残る浸透度（0..1）。剥がせる割合＝強度 crackdownIntensity(0..1)×当局の解明度
        /// intelligence(0..1)、ただし maxCrackdownFraction が天井＝**見えている分しか剥がせない**。
        /// 解明度ゼロ＝何も見えていない当局は空振り（浸透は無傷）。満点の摘発でも根は残り、
        /// 残った根から <see cref="RegrowthTick"/> で再生する。
        /// </summary>
        public static float CrackdownEffect(float penetration, float crackdownIntensity, float intelligence,
            SecretSocietyParams p)
        {
            float removedFraction = Mathf.Min(p.maxCrackdownFraction,
                Mathf.Clamp01(crackdownIntensity) * Mathf.Clamp01(intelligence));
            return Mathf.Clamp01(Mathf.Clamp01(penetration) * (1f - removedFraction));
        }

        public static float CrackdownEffect(float penetration, float crackdownIntensity, float intelligence)
            => CrackdownEffect(penetration, crackdownIntensity, intelligence, SecretSocietyParams.Default);

        /// <summary>
        /// 摘発後の再生の1tick後の浸透度（0..1）＝浸透＋再生速度×浸透×残余(1−浸透)×dt。
        /// 根絶やしにできなかった網は残った根の分だけ静かに戻る。浸透ゼロ（完全な根絶）なら二度と戻らない。
        /// </summary>
        public static float RegrowthTick(float penetration, float dt, SecretSocietyParams p)
        {
            float pen = Mathf.Clamp01(penetration);
            float growth = p.regrowthRate * pen * (1f - pen) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(pen + growth);
        }

        public static float RegrowthTick(float penetration, float dt)
            => RegrowthTick(penetration, dt, SecretSocietyParams.Default);
    }
}
