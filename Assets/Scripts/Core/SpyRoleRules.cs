using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 孫子「用間篇」の五間（スパイの種類）。郷間/内間/反間/死間/生間で
    /// 情報量・発覚リスク・偽情報注入・運用コストが異なる。
    /// </summary>
    public enum SpyRole
    {
        /// <summary>郷間＝敵地の住民を使う。安価だが浅い情報。</summary>
        郷間,
        /// <summary>内間＝敵の官吏を使う。高価だが深い情報（高位ほど価値）。</summary>
        内間,
        /// <summary>反間＝敵スパイを寝返らせ二重スパイ化する。敵の信頼を毒に変える。</summary>
        反間,
        /// <summary>死間＝偽情報を持たせ捨てる。必ず露見するが偽情報を信じさせる。</summary>
        死間,
        /// <summary>生間＝往復する熟練諜報員。低リスクで継続的に情報を持ち帰る。</summary>
        生間
    }

    /// <summary>用間の調整係数（マジックナンバー禁止＝集約）。</summary>
    public readonly struct SpyRoleParams
    {
        /// <summary>内間（敵官吏）の情報量上限係数。深い機密へ到達できる。</summary>
        public readonly float insiderIntelScale;
        /// <summary>郷間（住民）の情報量上限係数。表層情報に留まる。</summary>
        public readonly float localIntelScale;
        /// <summary>生間（熟練諜報員）の情報量上限係数。往復で堅実に集める。</summary>
        public readonly float roverIntelScale;
        /// <summary>死間の偽情報注入の基準強度（捨て駒の偽報）。</summary>
        public readonly float deadIntelDisinfo;
        /// <summary>反間の二重スパイ価値の基準（敵の信頼を逆用する）。</summary>
        public readonly float turncoatScale;
        /// <summary>内間の運用コスト（高位官吏の買収は高い）。</summary>
        public readonly float insiderCost;

        public SpyRoleParams(float insiderIntelScale, float localIntelScale, float roverIntelScale,
            float deadIntelDisinfo, float turncoatScale, float insiderCost)
        {
            this.insiderIntelScale = Mathf.Clamp01(insiderIntelScale);
            this.localIntelScale = Mathf.Clamp01(localIntelScale);
            this.roverIntelScale = Mathf.Clamp01(roverIntelScale);
            this.deadIntelDisinfo = Mathf.Clamp01(deadIntelDisinfo);
            this.turncoatScale = Mathf.Clamp01(turncoatScale);
            this.insiderCost = Mathf.Clamp01(insiderCost);
        }

        /// <summary>既定＝内間情報1.0・郷間0.4・生間0.7・死間偽報0.8・反間価値1.0・内間コスト0.9。</summary>
        public static SpyRoleParams Default => new SpyRoleParams(1f, 0.4f, 0.7f, 0.8f, 1f, 0.9f);
    }

    /// <summary>
    /// 用間五種体系の純ロジック（孫子「用間篇」・#1127）。五間＝郷間/内間/反間/死間/生間を
    /// 役割別にモデル化し、種類ごとに情報量・発覚リスク・偽情報注入・反間価値・運用コストが変わる。
    /// 諜報一般（任務成否・潜入・破壊工作）は <see cref="EspionageRules"/> が担い、ここは「間者の種類」の分類層。
    /// 反間（寝返らせた二重スパイ）の防諜側の運用は <see cref="CounterIntelligenceRules"/>（防諜＝反間と対）へ、
    /// 死間の偽情報を敵に信じさせる欺瞞は DeceptionRules（欺瞞＝死間と接続）へ接続する想定。
    /// 五間倶に起こりて其の道を知る莫きを「神紀」と謂う＝五種を同時運用すると相乗で発覚しにくくなる
    /// （<see cref="GodlikeWebFactor"/>）。乱数は呼び出し側が roll(0..1) を渡す＝決定論。値の clamp を徹底。test-first。
    /// </summary>
    public static class SpyRoleRules
    {
        /// <summary>
        /// 取得情報量（0..1）。間者の種類で上限が変わる。内間（敵官吏）は最も深く、
        /// 郷間（住民）は浅い。accessLevel(0..1)＝対象への接近度に比例する。
        /// 反間は寝返った敵スパイ経由で内間並みに深い。死間は持ち帰らない＝情報量ゼロ。
        /// </summary>
        public static float IntelYield(SpyRole role, float accessLevel, SpyRoleParams p)
        {
            float a = Mathf.Clamp01(accessLevel);
            float scale = role switch
            {
                SpyRole.内間 => p.insiderIntelScale,
                SpyRole.郷間 => p.localIntelScale,
                SpyRole.生間 => p.roverIntelScale,
                // 反間は敵内部の信頼を引き継ぐ＝内間並みの深度。
                SpyRole.反間 => p.insiderIntelScale,
                // 死間は捨て駒で偽情報を運ぶだけ＝情報は持ち帰らない。
                SpyRole.死間 => 0f,
                _ => p.localIntelScale
            };
            return Mathf.Clamp01(a * scale);
        }

        /// <summary>既定パラメータでの取得情報量。</summary>
        public static float IntelYield(SpyRole role, float accessLevel)
            => IntelYield(role, accessLevel, SpyRoleParams.Default);

        /// <summary>
        /// 発覚リスク（0..1）。敵防諜 enemyCounterIntel(0..1) が高いほど上がるが、種類で大きく違う。
        /// 死間（捨て駒）は必ず露見する＝1.0（露見してこそ偽情報が信じられる）。
        /// 生間（熟練）は低リスクで往復する。郷間は住民に紛れて中程度、内間は内部で目立ち高め、反間は中程度。
        /// </summary>
        public static float ExposureRisk(SpyRole role, float enemyCounterIntel)
        {
            float ci = Mathf.Clamp01(enemyCounterIntel);
            float raw = role switch
            {
                // 死間は露見前提（捨て駒）＝防諜に依らず必発覚。
                SpyRole.死間 => 1f,
                // 生間は熟練の往復＝防諜の効きを大きく割り引く。
                SpyRole.生間 => ci * 0.4f,
                // 郷間は住民に紛れる＝中程度。
                SpyRole.郷間 => ci * 0.6f,
                // 内間は敵内部で動く＝足がつきやすい。
                SpyRole.内間 => ci * 0.8f,
                // 反間は敵内部に居続ける＝中〜やや高。
                SpyRole.反間 => ci * 0.7f,
                _ => ci * 0.6f
            };
            return Mathf.Clamp01(raw);
        }

        /// <summary>このとき発覚するか（roll が発覚リスクを下回れば発覚＝死間は常に true）。</summary>
        public static bool IsExposed(SpyRole role, float enemyCounterIntel, float roll)
            => roll < ExposureRisk(role, enemyCounterIntel);

        /// <summary>
        /// 偽情報の注入量（0..1）。死間（捨て駒に偽報を持たせる）が主役で、敵に信じさせる信憑性
        /// credibility(0..1) に比例する。DeceptionRules（欺瞞）へ渡す毒の量。
        /// 反間も二重スパイ経由で偽情報を流せる（死間の半分）。他の種類は偽情報を注入しない。
        /// </summary>
        public static float Disinformation(SpyRole role, float credibility, SpyRoleParams p)
        {
            float c = Mathf.Clamp01(credibility);
            float baseStrength = role switch
            {
                SpyRole.死間 => p.deadIntelDisinfo,
                // 反間は逆用で偽情報を流すが死間ほど露骨にはできない。
                SpyRole.反間 => p.deadIntelDisinfo * 0.5f,
                _ => 0f
            };
            return Mathf.Clamp01(baseStrength * c);
        }

        /// <summary>既定パラメータでの偽情報注入量。</summary>
        public static float Disinformation(SpyRole role, float credibility)
            => Disinformation(role, credibility, SpyRoleParams.Default);

        /// <summary>
        /// 反間の価値（0..1）。寝返らせた敵スパイがまだ敵に信頼されている enemyTrust(0..1) ほど高い
        /// （信頼が残るうちは二重スパイとして使える＝CounterIntelligenceRules の転向資産）。
        /// 反間以外は二重スパイ運用ではない＝0。
        /// </summary>
        public static float TurncoatValue(SpyRole role, float enemyTrust, SpyRoleParams p)
        {
            if (role != SpyRole.反間) return 0f;
            return Mathf.Clamp01(p.turncoatScale * Mathf.Clamp01(enemyTrust));
        }

        /// <summary>既定パラメータでの反間価値。</summary>
        public static float TurncoatValue(SpyRole role, float enemyTrust)
            => TurncoatValue(role, enemyTrust, SpyRoleParams.Default);

        /// <summary>
        /// 運用コスト（0..1）。内間（高位官吏の買収）が最も高く、郷間（住民）は安い。
        /// 反間は寝返り工作が要り中〜高、死間は使い捨ての段取りで中、生間は熟練の維持で中。
        /// </summary>
        public static float RoleCost(SpyRole role, SpyRoleParams p)
        {
            float c = role switch
            {
                SpyRole.内間 => p.insiderCost,
                // 郷間は住民＝最安。
                SpyRole.郷間 => p.insiderCost * 0.2f,
                // 反間は寝返り工作の対価＝内間に次ぐ。
                SpyRole.反間 => p.insiderCost * 0.7f,
                // 死間は仕込みと犠牲＝中。
                SpyRole.死間 => p.insiderCost * 0.5f,
                // 生間は熟練の維持費＝中。
                SpyRole.生間 => p.insiderCost * 0.5f,
                _ => p.insiderCost * 0.5f
            };
            return Mathf.Clamp01(c);
        }

        /// <summary>既定パラメータでの運用コスト。</summary>
        public static float RoleCost(SpyRole role) => RoleCost(role, SpyRoleParams.Default);

        /// <summary>
        /// 目的に応じた最適間者。objectiveType(0..1)＝0で「深い情報収集」、1で「偽情報の注入」。
        /// budget(0..1)＝使える予算（コストが予算超過の種類は外す）。
        /// 深い情報が要るなら内間（予算不足なら生間→郷間へ妥協）、偽情報なら死間。中間は反間。
        /// </summary>
        public static SpyRole BestRoleForObjective(float objectiveType, float budget, SpyRoleParams p)
        {
            float obj = Mathf.Clamp01(objectiveType);
            float b = Mathf.Clamp01(budget);

            // 偽情報寄りの目的＝死間（予算が乏しければ反間）。
            if (obj >= 0.66f)
                return b >= RoleCost(SpyRole.死間, p) ? SpyRole.死間 : SpyRole.反間;

            // 中間の目的＝逆用の反間（予算が足りねば生間）。
            if (obj >= 0.33f)
                return b >= RoleCost(SpyRole.反間, p) ? SpyRole.反間 : SpyRole.生間;

            // 深い情報収集＝内間（予算で生間→郷間へ妥協）。
            if (b >= RoleCost(SpyRole.内間, p)) return SpyRole.内間;
            if (b >= RoleCost(SpyRole.生間, p)) return SpyRole.生間;
            return SpyRole.郷間;
        }

        /// <summary>既定パラメータでの最適間者。</summary>
        public static SpyRole BestRoleForObjective(float objectiveType, float budget)
            => BestRoleForObjective(objectiveType, budget, SpyRoleParams.Default);

        /// <summary>
        /// 「神紀」係数（0..1）。孫子「五間倶に起こりて、其の道を知る莫し、是を神紀と謂う」＝
        /// 五種の間を同時に起こすと敵は全体像を掴めず、各間の発覚を相互に隠蔽し合う。
        /// 稼働している種類数 rolesActive(0..5) が多いほど発覚リスクを割り引く倍率（5種で最小）を返す。
        /// 1種以下では割引なし（1.0）。<see cref="ExposureRisk"/> へ乗算して使う想定（死間の必発覚は別格）。
        /// </summary>
        public static float GodlikeWebFactor(int rolesActive)
        {
            int n = Mathf.Clamp(rolesActive, 0, 5);
            // 5種で最大割引（0.5倍まで）。1種以下は割引なし。
            float reduction = Mathf.Max(0, n - 1) / 4f * 0.5f;
            return Mathf.Clamp01(1f - reduction);
        }
    }
}
