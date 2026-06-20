using UnityEngine;

namespace Ginei
{
    /// <summary>継承戦争の調整係数（#1095）。</summary>
    public readonly struct SuccessionWarParams
    {
        /// <summary>諸侯がなびく際、請求者の強さに対する感応度（大きいほど強者総取り＝雪崩）。</summary>
        public readonly float bandwagonStrength;
        /// <summary>諸侯の自己利益が旗幟を揺らす振れ幅（roll で±に効く）。</summary>
        public readonly float selfInterestSwing;
        /// <summary>無政府度→中央権威崩壊（安定度低下）への倍率。</summary>
        public readonly float collapseScale;
        /// <summary>請求者の総合力の重み：血統の正統性。</summary>
        public readonly float legitimacyWeight;
        /// <summary>請求者の総合力の重み：武力。</summary>
        public readonly float militaryWeight;
        /// <summary>請求者の総合力の重み：諸侯の支持。</summary>
        public readonly float nobleWeight;

        public SuccessionWarParams(float bandwagonStrength, float selfInterestSwing, float collapseScale,
            float legitimacyWeight, float militaryWeight, float nobleWeight)
        {
            this.bandwagonStrength = Mathf.Max(0f, bandwagonStrength);
            this.selfInterestSwing = Mathf.Clamp01(selfInterestSwing);
            this.collapseScale = Mathf.Clamp01(collapseScale);
            this.legitimacyWeight = Mathf.Max(0f, legitimacyWeight);
            this.militaryWeight = Mathf.Max(0f, militaryWeight);
            this.nobleWeight = Mathf.Max(0f, nobleWeight);
        }

        /// <summary>既定＝なびき感応1.5・自己利益振れ0.3・崩壊倍率0.9・重み 正統性0.4/武力0.35/諸侯支持0.25。</summary>
        public static SuccessionWarParams Default =>
            new SuccessionWarParams(1.5f, 0.3f, 0.9f, 0.4f, 0.35f, 0.25f);
    }

    /// <summary>
    /// 継承戦争の純ロジック（#1095・The Anarchy／Pillars of the Earth）。明確な後継者なき君主の死が
    /// 複数の請求者を生み、諸侯が各請求者へ旗幟を選んで国が割れる＝無政府状態（アナーキー）化。
    /// 「明確な世継ぎは戦争を防ぎ、曖昧な継承は国を割る」を式に出す：後継が明確（heirClarity=1・請求者1人）なら
    /// 危機ゼロ、請求者が多く後継が曖昧なほど危機が深まる。拮抗する請求者が並立するほど中央権威は消えて
    /// 無法化し、突出した1人がいれば収束する。
    /// 旗幟カスケードの本体は <see cref="LoyaltyRules"/>（会戦の寝返りカスケード・実装済み）を国家分裂スケールへ
    /// 拡大した同型のロジック（強い側へなびき＋自己利益で揺れ、不動点で実効兵力が決まる）。
    /// 継承法そのもの（長子/分割/指名/選挙）の規定は <see cref="SuccessionLawRules"/>（PDX-1）が担い、
    /// 一度割れた後の長期消耗・経済崩壊・対外無防備は <see cref="CivilWarRules"/>（内戦・実装済み）が担う。
    /// ここは「危機の発生→請求者並立→諸侯の旗幟→無政府化→中央権威崩壊→決着」の継承固有部分を扱う。
    /// 全入力クランプ・乱数は roll 引数で決定論・配列は手書きループ・基準非破壊（係数を返す）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SuccessionWarRules
    {
        /// <summary>
        /// 継承危機の深刻度（0..1）。請求者が多く後継が曖昧なほど高い。
        /// 明確な世継ぎ1人（claimantCount&lt;=1）なら危機ゼロ。請求者が2人以上でも heirClarity=1（指名が明白）なら
        /// 危機は抑えられ、heirClarity=0（誰が正統か不明）なら請求者数に比例して跳ね上がる。
        /// </summary>
        public static float CrisisSeverity(int claimantCount, float heirClarity)
        {
            if (claimantCount <= 1) return 0f; // 明確な世継ぎ1人＝戦争は起きない
            float clarity = Mathf.Clamp01(heirClarity);
            // 請求者の並立度（2人で0.5→多数で1へ漸近）× 後継の曖昧さ(1-clarity)
            float rivalry = 1f - 1f / claimantCount;
            return Mathf.Clamp01(rivalry * (1f - clarity));
        }

        /// <summary>
        /// 一請求者の総合力（0..1）＝血統の正統性×武力×諸侯の支持の重み付き和。
        /// 重みは <see cref="SuccessionWarParams"/>（既定 正統性0.4/武力0.35/諸侯支持0.25）。
        /// </summary>
        public static float ClaimantStrength(float legitimacy, float militaryBacking, float nobleSupport, SuccessionWarParams p)
        {
            float wSum = p.legitimacyWeight + p.militaryWeight + p.nobleWeight;
            if (wSum <= 0f) return 0f;
            float raw = Mathf.Clamp01(legitimacy) * p.legitimacyWeight
                      + Mathf.Clamp01(militaryBacking) * p.militaryWeight
                      + Mathf.Clamp01(nobleSupport) * p.nobleWeight;
            return Mathf.Clamp01(raw / wSum);
        }

        public static float ClaimantStrength(float legitimacy, float militaryBacking, float nobleSupport)
            => ClaimantStrength(legitimacy, militaryBacking, nobleSupport, SuccessionWarParams.Default);

        /// <summary>
        /// 諸侯がどの請求者に付くかを返す（請求者インデックス・該当なしは-1）。
        /// 強い請求者になびき（bandwagonStrength でべき乗的に偏らせる）＋自己利益(0..1)で揺れる
        /// ＝<see cref="LoyaltyRules.ResolveCascade"/> の国家版（会戦の寝返りカスケードを継承戦争スケールへ）。
        /// roll(0..1) で決定論的に選ぶ：各請求者の「重み」を累積し roll が落ちた区間の請求者を返す。
        /// nobleSelfInterest が高いほど重みが平らに均され、弱小請求者にも付きうる（私心が雪崩を鈍らせる）。
        /// </summary>
        public static int NobleAllegiance(float[] claimantStrengths, float nobleSelfInterest, float roll, SuccessionWarParams p)
        {
            if (claimantStrengths == null || claimantStrengths.Length == 0) return -1;
            float selfInterest = Mathf.Clamp01(nobleSelfInterest) * p.selfInterestSwing;

            // 各請求者の重み：強さをべき乗で偏らせ、自己利益ぶんを均等成分で混ぜる
            float total = 0f;
            int n = claimantStrengths.Length;
            float[] weights = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = Mathf.Clamp01(claimantStrengths[i]);
                float bandwagon = Mathf.Pow(s, 1f + p.bandwagonStrength); // 強者へ偏る
                float flat = 1f / n;                                       // 私心＝弱小にも付きうる
                weights[i] = Mathf.Lerp(bandwagon, flat, selfInterest);
                total += weights[i];
            }
            if (total <= 0f) return -1; // 全請求者が無力＝誰も担がれない

            float target = Mathf.Clamp01(roll) * total;
            float cum = 0f;
            for (int i = 0; i < n; i++)
            {
                cum += weights[i];
                if (target <= cum) return i;
            }
            return n - 1;
        }

        public static int NobleAllegiance(float[] claimantStrengths, float nobleSelfInterest, float roll)
            => NobleAllegiance(claimantStrengths, nobleSelfInterest, roll, SuccessionWarParams.Default);

        /// <summary>
        /// 無政府状態の度合い（0..1）。拮抗する請求者が並立するほど中央権威が消えて無法化し、
        /// 突出した1人がいれば収束する。最大者のシェアが1に近いほど無政府度0、均等に割れるほど1へ。
        /// ＝1−（最大シェア−次点シェア の優勢度）を基にした「突出の無さ」。請求者0/1人は無政府ゼロ。
        /// </summary>
        public static float AnarchyLevel(float[] claimantStrengths)
        {
            if (claimantStrengths == null || claimantStrengths.Length <= 1) return 0f;
            float total = 0f, max = 0f, second = 0f;
            int n = claimantStrengths.Length;
            for (int i = 0; i < n; i++)
            {
                float s = Mathf.Max(0f, claimantStrengths[i]);
                total += s;
                if (s > max) { second = max; max = s; }
                else if (s > second) { second = s; }
            }
            if (total <= 0f) return 1f; // 誰も力がない＝完全な空白＝無法
            // 首位の支配シェアが高いほど秩序＝無政府度はその裏返し
            float dominance = max / total;       // 0..1（独占で1）
            float contention = second / max;     // 次点が首位に迫るほど1（拮抗）
            // 突出が無い（dominance低）かつ拮抗（contention高）ほど無政府
            float anarchy = (1f - dominance) * Mathf.Lerp(0.5f, 1f, contention);
            return Mathf.Clamp01(anarchy * 2f); // 並立(2分=0.5独占)で~1へ寄せる
        }

        /// <summary>
        /// 中央権威の崩壊度（0..1）＝無政府度×崩壊倍率。内戦中は安定度がこの分だけ下がり、
        /// 利権が実力で奪い合われる（中央が裁定できない）＝<see cref="GovernanceRules"/> の安定度・反乱へ波及する係数。
        /// </summary>
        public static float CentralAuthorityCollapse(float anarchyLevel, SuccessionWarParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(anarchyLevel) * p.collapseScale);
        }

        public static float CentralAuthorityCollapse(float anarchyLevel)
            => CentralAuthorityCollapse(anarchyLevel, SuccessionWarParams.Default);

        /// <summary>
        /// 安定度倍率（1−中央権威崩壊度）。<see cref="GovernanceRules.OutputFactor"/> 等の安定度係数に掛けて使う
        /// （実効値パターン・基準非破壊）。
        /// </summary>
        public static float StabilityFactor(float anarchyLevel, SuccessionWarParams p)
        {
            return 1f - CentralAuthorityCollapse(anarchyLevel, p);
        }

        public static float StabilityFactor(float anarchyLevel)
            => StabilityFactor(anarchyLevel, SuccessionWarParams.Default);

        /// <summary>
        /// 継承戦争の決着＝実効兵力（請求者の総合力）が最大の請求者が勝者（インデックス・同値は先勝ち）。
        /// 全請求者が無力（合計0）なら勝者なし(-1)＝王朝の断絶。
        /// <see cref="LoyaltyRules.ResolveWinner"/> と同じ「実効最大者が勝つ」不動点ロジックの再利用。
        /// </summary>
        public static int WarResolution(float[] claimantStrengths)
        {
            if (claimantStrengths == null || claimantStrengths.Length == 0) return -1;
            int best = -1;
            float bestVal = 0f;
            for (int i = 0; i < claimantStrengths.Length; i++)
            {
                float s = Mathf.Max(0f, claimantStrengths[i]);
                if (s > bestVal) { bestVal = s; best = i; }
            }
            return best; // 誰も力を持たなければ -1（断絶）
        }
    }
}
