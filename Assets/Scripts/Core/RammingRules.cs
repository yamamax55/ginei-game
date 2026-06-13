using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 衝角・特攻（体当たり攻撃＝捨て身戦法）の調整値（#特攻）。
    /// 相対速度・質量から打撃を導く係数と、意志/回避/盾の重みをまとめる。
    /// </summary>
    public readonly struct RammingParams
    {
        /// <summary>衝突打撃のスケール（相対速度×質量に掛ける）。</summary>
        public readonly float impactScale;
        /// <summary>意志（特攻に踏み切る覚悟）における絶望の重み。</summary>
        public readonly float desperationWeight;
        /// <summary>意志における決死の士気（高士気＝死兵）の重み。</summary>
        public readonly float moraleWeight;
        /// <summary>正面衝突（closingAngle=0）でも残る最低回避率の下限。</summary>
        public readonly float minEvasion;
        /// <summary>盾としての価値で守る対象（味方/要地）の重み。</summary>
        public readonly float protectionWeight;

        public RammingParams(float impactScale, float desperationWeight, float moraleWeight,
            float minEvasion, float protectionWeight)
        {
            this.impactScale = Mathf.Max(0f, impactScale);
            this.desperationWeight = Mathf.Clamp01(desperationWeight);
            this.moraleWeight = Mathf.Clamp01(moraleWeight);
            this.minEvasion = Mathf.Clamp01(minEvasion);
            this.protectionWeight = Mathf.Clamp01(protectionWeight);
        }

        /// <summary>既定：打撃スケール1.0／意志は絶望0.6＋士気0.4／最低回避0.0／盾の対象重み0.5。</summary>
        public static RammingParams Default => new RammingParams(
            DefaultImpactScale, DefaultDesperationWeight, DefaultMoraleWeight,
            DefaultMinEvasion, DefaultProtectionWeight);

        public const float DefaultImpactScale = 1.0f;
        public const float DefaultDesperationWeight = 0.6f;
        public const float DefaultMoraleWeight = 0.4f;
        public const float DefaultMinEvasion = 0.0f;
        public const float DefaultProtectionWeight = 0.5f;
    }

    /// <summary>
    /// 衝角・特攻（体当たり攻撃＝捨て身戦法）の純ロジック（#特攻）。
    /// 追い詰められた、または決死の局面で自艦を敵にぶつけて<b>道連れ</b>にする。
    /// 打撃は<b>相対速度×質量（運動エネルギー的）</b>で決まり、衝突した自艦は基本全損（装甲で稀に生還）。
    /// <b>絶望度</b>と<b>決死の士気</b>が高いほど踏み切りやすい。狙われた側は機動と接近角で<b>回避</b>できる。
    ///
    /// <b>分担</b>：これは<b>体当たりの物理打撃（衝突＝運動エネルギー）</b>に特化する。
    /// <see cref="SutegamariRules"/>（捨てがまり＝殿〔しんがり〕で主君を逃がす関係性の戦術）とは<b>別物</b>
    /// ＝あちらは献身で旗艦を退却させる頭数判定、こちらは敵を物理的に道連れにする衝突モデル。
    /// 既存に SuicideAttack 系の純ロジックが無いため新規＝捨て身戦法（衝突）の唯一の窓口。
    /// 盤面非依存の plain 引数・乱数なし（必要なら roll を渡す）・入力クランプ・実効値パターン（基準値非破壊）。
    /// 各メソッドは Params 明示版＋Default 委譲版を持つ。test-first。
    /// </summary>
    public static class RammingRules
    {
        /// <summary>特攻が成立する意志/収支の既定閾値。</summary>
        public const float DefaultViableThreshold = 0.5f;

        // --- 衝突打撃（相対速度×質量） ---

        /// <summary>既定Paramsで衝突打撃を返す。</summary>
        public static float ImpactDamage(float closingSpeed, float ownMass)
            => ImpactDamage(closingSpeed, ownMass, RammingParams.Default);

        /// <summary>
        /// 相対接近速度（closingSpeed≥0）と自艦の質量（ownMass≥0）から衝突打撃を返す。
        /// `impact = scale × closingSpeed × ownMass`。高速・大質量ほど重い（運動エネルギー的な打撃）。
        /// </summary>
        public static float ImpactDamage(float closingSpeed, float ownMass, RammingParams p)
        {
            float speed = Mathf.Max(0f, closingSpeed);
            float mass = Mathf.Max(0f, ownMass);
            return p.impactScale * speed * mass;
        }

        // --- 道連れ（相手への破壊） ---

        /// <summary>
        /// 衝突打撃が相手をどれだけ道連れにできるか（0..1）＝打撃／相手耐久。
        /// 1.0で相手を撃沈（道連れ成立）、0.5なら半壊。耐久0以下は最大1.0。
        /// </summary>
        public static float MutualDestruction(float impactDamage, float targetDurability)
        {
            float dmg = Mathf.Max(0f, impactDamage);
            if (targetDurability <= 0f) return 1f;
            return Mathf.Clamp01(dmg / targetDurability);
        }

        // --- 自艦の損失 ---

        /// <summary>
        /// 衝突した自艦の損失割合（0..1）。基本は全損（1.0）だが、衝突生存性（装甲・構造強度 0..1）が
        /// 高いほど稀に生還して損失が減る。`loss = 1 - survivability`。
        /// </summary>
        public static float SelfLoss(float impactSurvivability)
            => Mathf.Clamp01(1f - Mathf.Clamp01(impactSurvivability));

        // --- 特攻の意志 ---

        /// <summary>既定Paramsで特攻に踏み切る意志を返す。</summary>
        public static float RammingWillingness(float desperation, float morale)
            => RammingWillingness(desperation, morale, RammingParams.Default);

        /// <summary>
        /// 特攻に踏み切る意志（0..1）＝<b>絶望度</b>と<b>決死の士気</b>の重み付き和。
        /// 追い詰められ（絶望高）かつ崩れていない（士気高＝死兵）ほど踏み切る。入力は 0..1。
        /// 重みは合計が0なら均等扱いにフォールバック。
        /// </summary>
        public static float RammingWillingness(float desperation, float morale, RammingParams p)
        {
            float d = Mathf.Clamp01(desperation);
            float m = Mathf.Clamp01(morale);
            float wd = p.desperationWeight;
            float wm = p.moraleWeight;
            float sum = wd + wm;
            if (sum <= 0f) return Mathf.Clamp01((d + m) * 0.5f);
            return Mathf.Clamp01((d * wd + m * wm) / sum);
        }

        // --- 回避（狙われた側） ---

        /// <summary>既定Paramsで回避度合いを返す。</summary>
        public static float InterceptEvasion(float targetAgility, float closingAngle)
            => InterceptEvasion(targetAgility, closingAngle, RammingParams.Default);

        /// <summary>
        /// 狙われた側が体当たりを回避する度合い（0..1）。機動（targetAgility 0..1）が高く、
        /// 接近角（closingAngle・度）が大きい（横/斜めから来る＝かわしやすい）ほど回避しやすい。
        /// 真正面（0度）は最も避けにくく `minEvasion` まで下がる。180度で機動どおり。
        /// </summary>
        public static float InterceptEvasion(float targetAgility, float closingAngle, RammingParams p)
        {
            float agility = Mathf.Clamp01(targetAgility);
            // 接近角を 0..1 に正規化（0度=正面=0、180度=真横以上=1）。負角は絶対値。
            float angleNorm = Mathf.Clamp01(Mathf.Abs(closingAngle) / 180f);
            float byAngle = Mathf.Lerp(p.minEvasion, agility, angleNorm);
            return Mathf.Clamp01(byAngle);
        }

        // --- 収支 ---

        /// <summary>
        /// 道連れの収支（-1..1）＝相手への破壊（mutualDestruction 0..1）から自艦の損失（selfLoss 0..1）を引く。
        /// 正なら割に合う（相手の方が大きい＝大型艦を小型艦で道連れ）、負なら損。
        /// </summary>
        public static float NetExchange(float mutualDestruction, float selfLoss)
            => Mathf.Clamp(Mathf.Clamp01(mutualDestruction) - Mathf.Clamp01(selfLoss), -1f, 1f);

        // --- 盾としての犠牲 ---

        /// <summary>既定Paramsで盾としての価値を返す。</summary>
        public static float BlockingSacrifice(float rammingTarget, float protectedAsset)
            => BlockingSacrifice(rammingTarget, protectedAsset, RammingParams.Default);

        /// <summary>
        /// 身を挺して味方/要地を守る盾としての価値（0..1）。体当たりで止める脅威（rammingTarget の危険度 0..1）と
        /// 守る対象の価値（protectedAsset 0..1）から、`脅威 × (守対象×weight + (1-weight))` で算出。
        /// 脅威が大きく守る対象が貴いほど、捨て身で割り込む価値が高い。
        /// </summary>
        public static float BlockingSacrifice(float rammingTarget, float protectedAsset, RammingParams p)
        {
            float threat = Mathf.Clamp01(rammingTarget);
            float asset = Mathf.Clamp01(protectedAsset);
            float w = p.protectionWeight;
            float assetTerm = asset * w + (1f - w);
            return Mathf.Clamp01(threat * assetTerm);
        }

        // --- 成立判定 ---

        /// <summary>
        /// 特攻が成立するか（bool）＝意志が閾値以上、かつ収支が割に合う（≥0）。
        /// 決死の意志があり、道連れが損でないときだけ踏み切る。
        /// </summary>
        public static bool IsRammingViable(float rammingWillingness, float netExchange,
            float threshold = DefaultViableThreshold)
        {
            float will = Mathf.Clamp01(rammingWillingness);
            float net = Mathf.Clamp(netExchange, -1f, 1f);
            return will >= threshold && net >= 0f;
        }
    }
}
