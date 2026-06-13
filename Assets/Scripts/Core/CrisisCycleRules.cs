using UnityEngine;

namespace Ginei
{
    /// <summary>ミンスキー型金融循環の4相（変位→熱狂→恐慌→収縮の弧）。</summary>
    public enum MinskyPhase
    {
        /// <summary>変位（Displacement）＝新技術や信用拡大という外的衝撃が循環の口火を切る相。</summary>
        変位,
        /// <summary>熱狂（Boom/Euphoria）＝信用とレバレッジが積み上がり脆弱性が最も高まる相。</summary>
        熱狂,
        /// <summary>恐慌（Panic/Crisis）＝臨界でミンスキー・モーメントが反転を引き、過剰が一気に崩れる相。</summary>
        恐慌,
        /// <summary>収縮（Contraction/Revulsion）＝レバレッジが剥落し出尽くして次の変位を待つ相。</summary>
        収縮
    }

    /// <summary>危機サイクル（ミンスキー型金融循環）の調整係数。</summary>
    public readonly struct CrisisCycleParams
    {
        /// <summary>循環位置（0..1）で変位→熱狂へ移る閾値。</summary>
        public readonly float boomThreshold;
        /// <summary>循環位置（0..1）で熱狂→恐慌へ移る閾値。</summary>
        public readonly float panicThreshold;
        /// <summary>循環位置（0..1）で恐慌→収縮へ移る閾値。</summary>
        public readonly float contractionThreshold;
        /// <summary>熱狂で1単位時間に積み上がる信用・レバレッジの率（安定が不安定を生む）。</summary>
        public readonly float leverageGrowthRate;
        /// <summary>恐慌・収縮で1単位時間に剥落するレバレッジの率（出尽くし）。</summary>
        public readonly float leverageDecayRate;
        /// <summary>脆弱性に占めるレバレッジの寄与スケール。</summary>
        public readonly float fragilityLeverageScale;
        /// <summary>これ以上の脆弱性でショックが反転を引きうる臨界閾値（ミンスキー・モーメント）。</summary>
        public readonly float reversalThreshold;
        /// <summary>循環の基本進行速度（per 単位時間・勢い1.0のとき）。</summary>
        public readonly float baseProgressRate;
        /// <summary>債務返済比率がこれ未満ならヘッジ金融（元利を自前で返せる）。</summary>
        public readonly float hedgeCeiling;
        /// <summary>債務返済比率がこれ以上ならポンツィ金融（元本どころか利払いも借金で回す）。</summary>
        public readonly float ponziFloor;

        public CrisisCycleParams(float boomThreshold, float panicThreshold, float contractionThreshold,
            float leverageGrowthRate, float leverageDecayRate, float fragilityLeverageScale,
            float reversalThreshold, float baseProgressRate, float hedgeCeiling, float ponziFloor)
        {
            this.boomThreshold = Mathf.Clamp01(boomThreshold);
            this.panicThreshold = Mathf.Clamp01(panicThreshold);
            this.contractionThreshold = Mathf.Clamp01(contractionThreshold);
            this.leverageGrowthRate = Mathf.Max(0f, leverageGrowthRate);
            this.leverageDecayRate = Mathf.Max(0f, leverageDecayRate);
            this.fragilityLeverageScale = Mathf.Max(0f, fragilityLeverageScale);
            this.reversalThreshold = Mathf.Clamp01(reversalThreshold);
            this.baseProgressRate = Mathf.Max(0f, baseProgressRate);
            this.hedgeCeiling = Mathf.Clamp01(hedgeCeiling);
            this.ponziFloor = Mathf.Clamp01(ponziFloor);
        }

        /// <summary>
        /// 既定＝熱狂閾値0.25・恐慌閾値0.6・収縮閾値0.8・蓄積率0.3・剥落率0.5・脆弱性スケール0.7・
        /// 反転閾値0.7・基本進行0.5・ヘッジ上限0.4・ポンツィ下限0.8。
        /// </summary>
        public static CrisisCycleParams Default =>
            new CrisisCycleParams(0.25f, 0.6f, 0.8f, 0.3f, 0.5f, 0.7f, 0.7f, 0.5f, 0.4f, 0.8f);
    }

    /// <summary>
    /// 危機サイクル（ミンスキー型金融循環）の純ロジック（KNDB-1 #1610・キンドルバーガー『熱狂、恐慌、崩壊』
    /// ＋ミンスキー『金融不安定性仮説』参考）。新技術や信用拡大という「変位」が熱狂を生み、熱狂のあいだに
    /// 信用・レバレッジが積み上がって脆弱性が最も高まり、臨界でショックがミンスキー・モーメント＝恐慌への反転を
    /// 引き、収縮でレバレッジが出尽くして次の変位を待つ＝「安定（熱狂）が脆弱性を積み上げ、臨界で反転し、
    /// 収縮して循環する」を式に出す。<see cref="ArmsRaceRules"/>（軍拡の螺旋＝量の自己強化）とは別＝こちらは
    /// 相を巡る金融循環のフェーズ遷移。価格そのものの動きは <see cref="BubblePriceRules"/>（Wave30・価格）、
    /// 収縮局面の債務の自己強化的悪化は <see cref="DebtDeflationRules"/>（Wave30・収縮の債務）へ委譲する。
    /// 全入力クランプ・乱数は roll 引数で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CrisisCycleRules
    {
        /// <summary>レバレッジの下限（収縮で出尽くしてもこれ未満には剥落しない＝種火は残る）。</summary>
        public const float MinLeverage = 0f;

        /// <summary>
        /// 循環位置（0..1）を4相へ写す。閾値で 変位→熱狂→恐慌→収縮 と段階的に切り替わる
        /// （位置が進むほど相も進む＝循環の弧をたどる）。
        /// </summary>
        public static MinskyPhase PhaseOf(float cyclePosition, CrisisCycleParams p)
        {
            float x = Mathf.Clamp01(cyclePosition);
            if (x < p.boomThreshold) return MinskyPhase.変位;
            if (x < p.panicThreshold) return MinskyPhase.熱狂;
            if (x < p.contractionThreshold) return MinskyPhase.恐慌;
            return MinskyPhase.収縮;
        }

        public static MinskyPhase PhaseOf(float cyclePosition)
            => PhaseOf(cyclePosition, CrisisCycleParams.Default);

        /// <summary>
        /// 相の決定論遷移＝変位→熱狂→恐慌→収縮→変位（収縮の次は変位＝循環する）。
        /// 出尽くした収縮の後に次の変位が口火を切る、ミンスキーの円環。
        /// </summary>
        public static MinskyPhase NextPhase(MinskyPhase phase)
        {
            switch (phase)
            {
                case MinskyPhase.変位: return MinskyPhase.熱狂;
                case MinskyPhase.熱狂: return MinskyPhase.恐慌;
                case MinskyPhase.恐慌: return MinskyPhase.収縮;
                default: return MinskyPhase.変位; // 収縮→変位＝循環
            }
        }

        /// <summary>
        /// レバレッジ（信用・借入の積み上がり）の1tick後の値。熱狂では leverageGrowthRate で積み上がり
        /// （安定が不安定を生む＝平穏が借入を誘う）、恐慌・収縮では leverageDecayRate で剥落する（出尽くし）。
        /// 変位ではほぼ横ばい。下限 <see cref="MinLeverage"/> でクランプ。
        /// </summary>
        public static float LeverageBuildup(MinskyPhase phase, float currentLeverage, float dt, CrisisCycleParams p)
        {
            float lev = Mathf.Max(MinLeverage, currentLeverage);
            float step = Mathf.Max(0f, dt);
            switch (phase)
            {
                case MinskyPhase.熱狂:
                    return Mathf.Max(MinLeverage, lev + p.leverageGrowthRate * step);
                case MinskyPhase.恐慌:
                case MinskyPhase.収縮:
                    return Mathf.Max(MinLeverage, lev - p.leverageDecayRate * step);
                default: // 変位＝横ばい（種火は残る）
                    return lev;
            }
        }

        public static float LeverageBuildup(MinskyPhase phase, float currentLeverage, float dt)
            => LeverageBuildup(phase, currentLeverage, dt, CrisisCycleParams.Default);

        /// <summary>
        /// 金融脆弱性指数（0..1）＝レバレッジ×スケールに相の係数を掛ける。熱狂のピークで最も脆く
        /// （積み上がったレバレッジが熱狂で増幅される）、恐慌で高止まり、収縮・変位では低い。
        /// </summary>
        public static float FragilityIndex(float leverage, MinskyPhase phase, CrisisCycleParams p)
        {
            float baseFrag = Mathf.Clamp01(Mathf.Max(0f, leverage) * p.fragilityLeverageScale);
            float phaseFactor;
            switch (phase)
            {
                case MinskyPhase.熱狂: phaseFactor = 1f; break;   // ピークで最も脆い
                case MinskyPhase.恐慌: phaseFactor = 0.8f; break; // 反転後も高い
                case MinskyPhase.収縮: phaseFactor = 0.3f; break; // 出尽くしで低下
                default: phaseFactor = 0.4f; break;               // 変位＝まだ低い
            }
            return Mathf.Clamp01(baseFrag * phaseFactor);
        }

        public static float FragilityIndex(float leverage, MinskyPhase phase)
            => FragilityIndex(leverage, phase, CrisisCycleParams.Default);

        /// <summary>
        /// 反転トリガー＝臨界脆弱性（reversalThreshold 以上）でショックが恐慌への反転を引くか。
        /// 脆弱性が高いほど、ショックが大きいほど起きやすい＝有効反転確率＝脆弱性×ショック。
        /// 脆弱性が臨界未満なら（同じショックでも）反転しない＝積み上がりがあって初めて崩れる。
        /// roll∈[0,1) で決定論判定。
        /// </summary>
        public static bool ReversalTrigger(float fragility, float shock, float roll, CrisisCycleParams p)
        {
            float frag = Mathf.Clamp01(fragility);
            if (frag < p.reversalThreshold) return false;
            float chance = frag * Mathf.Clamp01(shock);
            return Mathf.Clamp01(roll) < chance;
        }

        public static bool ReversalTrigger(float fragility, float shock, float roll)
            => ReversalTrigger(fragility, shock, roll, CrisisCycleParams.Default);

        /// <summary>
        /// 循環位置（0..1）を進める。勢い momentum と基本進行率で前進し、熱狂は速く・収縮は淀む
        /// （相ごとの速度倍率を掛ける）。1.0 を超えたら 0 へ巻き戻る＝循環の弧を一周する。
        /// </summary>
        public static float PhaseProgressTick(float cyclePosition, float momentum, float dt, CrisisCycleParams p)
        {
            float pos = Mathf.Clamp01(cyclePosition);
            MinskyPhase phase = PhaseOf(pos, p);
            float speedFactor;
            switch (phase)
            {
                case MinskyPhase.熱狂: speedFactor = 1.5f; break; // 熱狂は速い
                case MinskyPhase.恐慌: speedFactor = 1.2f; break; // 恐慌も速い
                case MinskyPhase.収縮: speedFactor = 0.5f; break; // 収縮は淀む
                default: speedFactor = 0.8f; break;               // 変位
            }
            float advance = p.baseProgressRate * Mathf.Clamp01(momentum) * speedFactor * Mathf.Max(0f, dt);
            float next = pos + advance;
            if (next >= 1f) next -= 1f; // 循環＝一周したら巻き戻る
            return Mathf.Clamp01(next);
        }

        public static float PhaseProgressTick(float cyclePosition, float momentum, float dt)
            => PhaseProgressTick(cyclePosition, momentum, dt, CrisisCycleParams.Default);

        /// <summary>
        /// ミンスキーの3段階の弁別＝債務返済比率（収入に対する元利返済の割合）で
        /// ヘッジ金融（hedgeCeiling 未満＝元利を自前で返せる健全）／
        /// 投機金融（中間＝利払いはできるが元本は借り換えに頼る）／
        /// ポンツィ金融（ponziFloor 以上＝利払いすら借金で回す＝資産値上がり頼みの自転車操業）を返す。
        /// 0=ヘッジ・1=投機・2=ポンツィ。
        /// </summary>
        public static int HedgeSpeculativePonzi(float debtServiceRatio, CrisisCycleParams p)
        {
            float r = Mathf.Clamp01(debtServiceRatio);
            if (r < p.hedgeCeiling) return 0; // ヘッジ
            if (r < p.ponziFloor) return 1;   // 投機
            return 2;                          // ポンツィ
        }

        public static int HedgeSpeculativePonzi(float debtServiceRatio)
            => HedgeSpeculativePonzi(debtServiceRatio, CrisisCycleParams.Default);

        /// <summary>
        /// ミンスキー・モーメント＝脆弱性が閾値以上に達した瞬間（熱狂が恐慌へ転じる臨界点）。
        /// 積み上がった脆弱性がここを越えると、些細なショックでも反転を引きうる。
        /// </summary>
        public static bool IsMinskyMoment(float fragility, float threshold)
        {
            return Mathf.Clamp01(fragility) >= Mathf.Clamp01(threshold);
        }

        public static bool IsMinskyMoment(float fragility, CrisisCycleParams p)
            => IsMinskyMoment(fragility, p.reversalThreshold);

        public static bool IsMinskyMoment(float fragility)
            => IsMinskyMoment(fragility, CrisisCycleParams.Default);
    }
}
