using UnityEngine;

namespace Ginei
{
    /// <summary>国家状態の合成 Tick の調整係数。</summary>
    public readonly struct FactionStateParams
    {
        /// <summary>希望が「合意と正統性の平均」へ追従する速さ/秒。</summary>
        public readonly float hopeDrift;

        public FactionStateParams(float hopeDrift) { this.hopeDrift = hopeDrift; }
        public static FactionStateParams Default => new FactionStateParams(0.3f);
    }

    /// <summary>
    /// 国家状態の合成ロジック（社会・政治シミュ層の統合）。1 tick で因果連鎖を回す：
    /// ①王朝＝腐敗が進み正統性が下がる（<see cref="DynastyRules"/>）→ ②統治スタイル(収奪/包摂)が抑圧を決め、
    /// 正統性は王朝に従う → ③合意＝抑圧で下がり正統性で回復（<see cref="ConsentRules"/>）→ ④希望＝合意と正統性へ
    /// ドリフトし、尽きれば末人（<see cref="HopeRules"/>）。＝収奪は即効だが腐敗・離反・末人で崩れ、包摂は遅いが続く。
    /// テーマ（神話→歴史・救い）を盤面のルールに宿す統合層。test-first。
    /// </summary>
    public static class FactionStateRules
    {
        /// <summary>国家状態を dt 進める（王朝→抑圧→合意→希望の因果連鎖）。</summary>
        public static void Tick(FactionState s, float dt, FactionStateParams p)
        {
            if (s == null || dt <= 0f) return;

            // ① 王朝：腐敗（制度疲労）→正統性低下（徳で減速）
            DynastyRules.Tick(s.regime, dt);

            // ② 統治スタイル→抑圧（収奪的=低 inclusiveness ほど抑圧高）。正統性は王朝の天命に従う。
            s.polity.oppression = Mathf.Clamp01(1f - s.inclusiveness);
            s.polity.legitimacy = s.regime.legitimacy;

            // ③ 合意：抑圧で下がり正統性で回復（権力は借り物）
            ConsentRules.Tick(s.polity, dt);

            // ④ 希望：合意と正統性の平均へドリフト→末人（ロンドン派）判定
            float target = 0.5f * s.polity.cooperation + 0.5f * s.regime.legitimacy;
            s.community.hope = Mathf.Clamp01(s.community.hope + (target - s.community.hope) * p.hopeDrift * dt);
            HopeRules.UpdateDissent(s.community);
        }

        public static void Tick(FactionState s, float dt) => Tick(s, dt, FactionStateParams.Default);

        /// <summary>総合的な安定度 0..1＝正統性・合意・結束・希望の平均。</summary>
        public static float Stability(FactionState s)
        {
            if (s == null) return 0f;
            return (s.regime.legitimacy + s.polity.cooperation + s.organization.cohesion + s.community.hope) / 4f;
        }

        /// <summary>崩壊しているか＝天命喪失／統治不能／組織崩壊／末人のいずれか。</summary>
        public static bool IsCollapsing(FactionState s)
        {
            if (s == null) return true;
            return DynastyRules.MandateLost(s.regime)
                || ConsentRules.IsUngovernable(s.polity)
                || s.organization.fragmented
                || s.community.dissent;
        }
    }
}
