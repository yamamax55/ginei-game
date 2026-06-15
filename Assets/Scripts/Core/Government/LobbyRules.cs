using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 圧力団体＝ロビーの政治力学の純ロジック（唯一の窓口・test-first）。
    /// 業界・地域・団体の陳情が政策を歪める一般化されたモデル：
    /// 資金×会員×政権アクセスがロビーの影響力を決め（<see cref="LobbyRules.InfluenceStrength"/>）、
    /// 公益に反するロビーほど政策を歪め（<see cref="LobbyRules.PolicyDistortion"/>＝公益と一致したロビーは無害）、
    /// **少数が大きく得て多数が薄く損する**集中利益・分散費用の非対称が抵抗を組織させず
    /// （<see cref="LobbyRules.ConcentratedBenefitDiffusedCost"/>＝オルソンの集合行為論）、
    /// 影響力が監督を上回ると規制官庁が取り込まれ（<see cref="LobbyRules.RegulatoryCapture"/>＝規制の虜）、
    /// 拮抗する対抗ロビーは歪みを相殺し（<see cref="LobbyRules.CounterLobbyBalance"/>）、
    /// 個別最適のロビーの集積が全体厚生を損なう（<see cref="LobbyRules.AggregateWelfareLoss"/>）。
    /// 分担：`PartyRules`＝政党・議席・選挙（制度内の代表）／`WarIndustryRules`＝軍需に特化したロビー力学（戦争利得の固有版）／
    /// **本クラス＝業種・地域・団体を問わない一般化されたロビー一般**（陳情→政策歪み→規制の虜→全体厚生損失の汎用層。政党・選挙そのものは扱わない）。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。調整値は <see cref="LobbyParams"/>（既定 <see cref="LobbyParams.Default"/>）。
    /// </summary>
    public static class LobbyRules
    {
        /// <summary>ロビー力学の調整値（影響力の各重み・歪み感度・集中利益の非対称指数・規制の虜閾値）。ctor で全てクランプ。</summary>
        public readonly struct LobbyParams
        {
            /// <summary>影響力に占める資金の重み（0..1）。</summary>
            public readonly float fundingWeight;
            /// <summary>影響力に占める会員規模の重み（0..1）。</summary>
            public readonly float membershipWeight;
            /// <summary>影響力に占める政権アクセスの重み（0..1。fundingWeight＋membershipWeight＋これで内部正規化）。</summary>
            public readonly float accessWeight;
            /// <summary>政策歪みの感度（≥0）。</summary>
            public readonly float distortionScale;
            /// <summary>集中利益・分散費用の非対称指数（≥1。少数の受益者ほど抵抗されにくい＝超線形）。</summary>
            public readonly float asymmetryExponent;
            /// <summary>規制の虜の閾値（影響力−監督がこれ以上で取り込まれる。0..1）。</summary>
            public readonly float captureThreshold;

            public LobbyParams(
                float fundingWeight, float membershipWeight, float accessWeight,
                float distortionScale, float asymmetryExponent, float captureThreshold)
            {
                this.fundingWeight = Mathf.Clamp01(fundingWeight);
                this.membershipWeight = Mathf.Clamp01(membershipWeight);
                this.accessWeight = Mathf.Clamp01(accessWeight);
                this.distortionScale = Mathf.Max(0f, distortionScale);
                this.asymmetryExponent = Mathf.Max(1f, asymmetryExponent);       // 線形未満にしない＝集中利益の非対称を保証
                this.captureThreshold = Mathf.Clamp01(captureThreshold);
            }

            /// <summary>
            /// 既定＝資金重み0.4・会員重み0.3・アクセス重み0.3（合計1＝正規化済み）・
            /// 歪み感度1・非対称指数2・規制の虜閾値0.5。
            /// </summary>
            public static LobbyParams Default
                => new LobbyParams(0.4f, 0.3f, 0.3f, 1f, 2f, 0.5f);
        }

        /// <summary>ロビーの政治影響力（既定 Params）。</summary>
        public static float InfluenceStrength(float funding, float membership, float accessToPower)
            => InfluenceStrength(funding, membership, accessToPower, LobbyParams.Default);

        /// <summary>
        /// ロビーの政治影響力（0..1）＝資金・会員規模・政権アクセスの重み付き和（重みは内部で正規化）。
        /// 金（funding）・数（membership）・コネ（accessToPower）の三本柱で政治力が決まる。
        /// 重み合計が0なら単純平均にフォールバック（0除算防止）。
        /// </summary>
        public static float InfluenceStrength(float funding, float membership, float accessToPower, LobbyParams p)
        {
            float f = Mathf.Clamp01(funding);
            float m = Mathf.Clamp01(membership);
            float a = Mathf.Clamp01(accessToPower);
            float wsum = p.fundingWeight + p.membershipWeight + p.accessWeight;
            if (wsum <= 0f) return Mathf.Clamp01((f + m + a) / 3f);               // 重み未設定は均等
            return Mathf.Clamp01((f * p.fundingWeight + m * p.membershipWeight + a * p.accessWeight) / wsum);
        }

        /// <summary>政策の歪み（既定 Params）。</summary>
        public static float PolicyDistortion(float influence, float publicInterestAlignment)
            => PolicyDistortion(influence, publicInterestAlignment, LobbyParams.Default);

        /// <summary>
        /// 政策の歪み（0..1）＝影響力×(1−公益との一致度)×感度。
        /// 公益に反するロビー（publicInterestAlignment 低）ほど政策を私的利益へ歪め、
        /// **公益と完全に一致したロビーは無害**（alignment=1で歪み0）＝陳情そのものが悪なのではなく、公益との乖離が害になる。
        /// 呼び出し側は内政#109/安定度・正統性#867 からこの分を差し引く想定（基準非破壊）。
        /// </summary>
        public static float PolicyDistortion(float influence, float publicInterestAlignment, LobbyParams p)
        {
            float inf = Mathf.Clamp01(influence);
            float align = Mathf.Clamp01(publicInterestAlignment);
            return Mathf.Clamp01(inf * (1f - align) * p.distortionScale);
        }

        /// <summary>集中利益・分散費用の非対称（既定 Params）。</summary>
        public static float ConcentratedBenefitDiffusedCost(float beneficiaryShare)
            => ConcentratedBenefitDiffusedCost(beneficiaryShare, LobbyParams.Default);

        /// <summary>
        /// 集中利益・分散費用の非対称度（0..1）＝オルソンの集合行為論。受益者シェア beneficiaryShare が小さいほど高い：
        /// **少数が大きく得て多数が薄く損する政策ほど抵抗されにくい**（一人あたりの損が小さい多数は組織化のコストに見合わず沈黙する）。
        /// (1−beneficiaryShare) の asymmetryExponent 乗で、受益者が少ないほど超線形に非対称が深まる。
        /// 呼び出し側は政策通過の容易さ・反対動員の弱さへ掛ける想定＝**分散した多数は組織できず、集中した少数が勝つ**。
        /// </summary>
        public static float ConcentratedBenefitDiffusedCost(float beneficiaryShare, LobbyParams p)
        {
            float share = Mathf.Clamp01(beneficiaryShare);
            return Mathf.Clamp01(Mathf.Pow(1f - share, p.asymmetryExponent));     // 受益者が少ないほど超線形に高い
        }

        /// <summary>規制の虜（既定 Params）。</summary>
        public static bool RegulatoryCapture(float influence, float oversightStrength)
            => RegulatoryCapture(influence, oversightStrength, LobbyParams.Default);

        /// <summary>
        /// 規制の虜の判定＝監督官庁がロビーに取り込まれるか。影響力−監督の強さが captureThreshold 以上で成立。
        /// ロビーの影響力が監督を上回ると、規制する側が規制される側の利益代弁者になる（回転ドア・天下り）。
        /// 監督が強ければ高い影響力でも虜にならない（歯止めは効く）。<see cref="WarIndustryRules.RevolvingDoorCorruption"/> の一般版。
        /// </summary>
        public static bool RegulatoryCapture(float influence, float oversightStrength, LobbyParams p)
            => Mathf.Clamp01(influence) - Mathf.Clamp01(oversightStrength) >= p.captureThreshold;

        /// <summary>対抗ロビーの綱引き（影響力 0..1 を二つ受け取り、正味の歪み圧を返す）。</summary>
        public static float CounterLobbyBalance(float lobbyA, float lobbyB)
        {
            float a = Mathf.Clamp01(lobbyA);
            float b = Mathf.Clamp01(lobbyB);
            return Mathf.Clamp(a - b, -1f, 1f);                                   // 拮抗（a≈b）なら相殺して0付近
        }

        /// <summary>個別最適の集積が生む全体厚生損失（複数の政策歪みを合成、既定 Params）。</summary>
        public static float AggregateWelfareLoss(float[] distortions)
            => AggregateWelfareLoss(distortions, LobbyParams.Default);

        /// <summary>
        /// 個別最適の集積が生む全体厚生損失（0..1）＝各ロビーの政策歪みを補完合成
        /// （1−Π(1−distortion_i)＝独立に重なる害の積み上げ）。一つ一つは小さくても、業界・地域・団体の陳情が積み重なると
        /// 全体最適が掘り崩される＝**個別最適の集積が全体最適を壊す**。null/空なら0。
        /// </summary>
        public static float AggregateWelfareLoss(float[] distortions, LobbyParams p)
        {
            if (distortions == null || distortions.Length == 0) return 0f;
            float survival = 1f;                                                 // 害を受けずに残る厚生の割合
            for (int i = 0; i < distortions.Length; i++)
                survival *= (1f - Mathf.Clamp01(distortions[i]));
            return Mathf.Clamp01(1f - survival);
        }
    }
}
