using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 六政体類型＝ポリュビオスのアナキュクローシス（政体循環論）の6形態。
    /// 正しい3形態（王政・貴族政・民主政＝善政）と、それぞれが堕落した3形態（僭主政・寡頭政・衆愚政）が
    /// 王政→僭主政→貴族政→寡頭政→民主政→衆愚政→王政 と循環する。
    /// </summary>
    public enum RegimeForm
    {
        /// <summary>王政（一者の善政）＝正しい形態。徳を失えば僭主政へ腐落する。</summary>
        王政,
        /// <summary>僭主政（一者の暴政）＝王政の堕落形態。打倒されて貴族政へ。</summary>
        僭主政,
        /// <summary>貴族政（少数の善政）＝正しい形態。徳を失えば寡頭政へ腐落する。</summary>
        貴族政,
        /// <summary>寡頭政（少数の私利支配）＝貴族政の堕落形態。打倒されて民主政へ。</summary>
        寡頭政,
        /// <summary>民主政（多数の善政）＝正しい形態。徳を失えば衆愚政へ腐落する。</summary>
        民主政,
        /// <summary>衆愚政（多数の暴民支配）＝民主政の堕落形態。混乱から再び王政へ。</summary>
        衆愚政
    }

    /// <summary>政体循環（アナキュクローシス）の調整係数（POLY-1 #1442）。</summary>
    public readonly struct AnacyclosisParams
    {
        /// <summary>徳0のときの腐落進行/秒（正しい形態が堕落形態へ堕ちる基本速度）。</summary>
        public readonly float degenerationRate;
        /// <summary>堕落形態が打倒されうる打倒圧力の閾値（これ以上で打倒の機が熟す）。</summary>
        public readonly float overthrowThreshold;
        /// <summary>循環速度の基準値（徳が速く失われ制度的歯止めが無いほどこれに近づく）。</summary>
        public readonly float cycleBaseSpeed;
        /// <summary>堕落形態の固有不安定度（徳に依らず常に抱える脆さ）。</summary>
        public readonly float corruptInstability;

        public AnacyclosisParams(float degenerationRate, float overthrowThreshold,
            float cycleBaseSpeed, float corruptInstability)
        {
            this.degenerationRate = Mathf.Max(0f, degenerationRate);
            this.overthrowThreshold = Mathf.Clamp01(overthrowThreshold);
            this.cycleBaseSpeed = Mathf.Max(0f, cycleBaseSpeed);
            this.corruptInstability = Mathf.Clamp01(corruptInstability);
        }

        /// <summary>
        /// 既定＝腐落率0.1・打倒閾値0.6・循環基準速度0.5・堕落形態固有不安定度0.4。
        /// </summary>
        public static AnacyclosisParams Default =>
            new AnacyclosisParams(0.1f, 0.6f, 0.5f, 0.4f);
    }

    /// <summary>
    /// 政体循環論（アナキュクローシス）の純ロジック（POLY-1 #1442・ポリュビオス『歴史』参考）。
    /// 政体は6つの形態を循環する：①王政（君主の善政）→堕落して②僭主政→打倒して③貴族政（少数の善政）
    /// →堕落して④寡頭政→打倒して⑤民主政（多数の善政）→堕落して⑥衆愚政（暴民支配）→混乱から再び①王政へ。
    /// 正しい3形態（王政・貴族政・民主政）は徳を失えば必ず対応する堕落形態へ腐落し、堕落形態は不満が溜まると
    /// 打倒される＝「正→堕落→打倒の輪」を式に出す（<see cref="NextForm"/> の循環が核）。
    /// <see cref="DynastyRules"/>（天命と王朝サイクル＝腐敗と正統性の単線）とは別＝こちらは六政体の循環。
    /// <see cref="RegimeRules"/>（腐敗の一般進行）とも別＝こちらは形態の遷移輪。混合政体が循環を止める仕組みは
    /// <see cref="MixedConstitutionRules"/>（同EPIC POLY）、政体そのものの腐化は <see cref="PolityCorruptionRules"/>
    /// （別EPIC MONT）へ委譲する。全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AnacyclosisRules
    {
        /// <summary>
        /// 循環の決定論遷移＝王政→僭主政→貴族政→寡頭政→民主政→衆愚政→王政（衆愚政の次は王政＝循環）。
        /// 正しい形態の次は対応する堕落形態（腐落）、堕落形態の次は次代の正しい形態（打倒）と交互に進む輪。
        /// </summary>
        public static RegimeForm NextForm(RegimeForm form)
        {
            switch (form)
            {
                case RegimeForm.王政: return RegimeForm.僭主政;   // 腐落
                case RegimeForm.僭主政: return RegimeForm.貴族政; // 打倒
                case RegimeForm.貴族政: return RegimeForm.寡頭政; // 腐落
                case RegimeForm.寡頭政: return RegimeForm.民主政; // 打倒
                case RegimeForm.民主政: return RegimeForm.衆愚政; // 腐落
                default: return RegimeForm.王政;                  // 衆愚政→王政＝循環
            }
        }

        /// <summary>
        /// 正しい形態（王政・貴族政・民主政＝善政）か。これら3形態が enum の偶数位置＝徳がある間は安定だが、
        /// 徳を失えば対応する堕落形態（僭主政・寡頭政・衆愚政＝奇数位置）へ必ず腐落する。
        /// </summary>
        public static bool IsLegitimateForm(RegimeForm form)
        {
            return form == RegimeForm.王政 || form == RegimeForm.貴族政 || form == RegimeForm.民主政;
        }

        /// <summary>
        /// 正しい形態が腐落して堕ちる対応する堕落形態を返す（王政→僭主政・貴族政→寡頭政・民主政→衆愚政）。
        /// 既に堕落形態ならそのまま返す（堕落形態の次は腐落でなく打倒＝<see cref="NextForm"/>）。
        /// </summary>
        public static RegimeForm CorruptedForm(RegimeForm form)
        {
            switch (form)
            {
                case RegimeForm.王政: return RegimeForm.僭主政;
                case RegimeForm.貴族政: return RegimeForm.寡頭政;
                case RegimeForm.民主政: return RegimeForm.衆愚政;
                default: return form; // 既に堕落形態
            }
        }

        /// <summary>
        /// 腐落の進行（0..1）を dt 進める。正しい形態だけが腐落しうる＝徳が低いほど速く対応する堕落形態へ
        /// 近づく（徳の喪失が善政を私利支配へ蝕む）。堕落形態は腐落しない（次は打倒＝0を返す）。
        /// 1.0 で腐落完了＝<see cref="CorruptedForm"/> へ移る機が熟す。
        /// </summary>
        public static float DegenerationTick(float virtue, RegimeForm form, float progress, float dt, AnacyclosisParams p)
        {
            if (!IsLegitimateForm(form)) return 0f; // 堕落形態は腐落しない
            float prog = Mathf.Clamp01(progress);
            float step = Mathf.Max(0f, dt);
            float rise = p.degenerationRate * (1f - Mathf.Clamp01(virtue)) * step;
            return Mathf.Clamp01(prog + rise);
        }

        public static float DegenerationTick(float virtue, RegimeForm form, float progress, float dt)
            => DegenerationTick(virtue, form, progress, dt, AnacyclosisParams.Default);

        /// <summary>
        /// 堕落形態が打倒される圧力（0..1）。僭主政・寡頭政・衆愚政は抑圧と不満が溜まるほど打倒されやすい
        /// （抑圧×不満＝暴政が反発を呼ぶ）。正しい形態には打倒圧力が掛からない（0を返す＝倒すべき堕落でない）。
        /// </summary>
        public static float OverthrowPressure(RegimeForm form, float oppression, float discontent)
        {
            if (IsLegitimateForm(form)) return 0f; // 正しい形態は打倒対象でない
            float opp = Mathf.Clamp01(oppression);
            float dis = Mathf.Clamp01(discontent);
            return Mathf.Clamp01(opp * dis);
        }

        /// <summary>
        /// 打倒の機が熟したか（打倒圧力が閾値 overthrowThreshold 以上）。堕落形態は不満が溜まると倒され、
        /// <see cref="NextForm"/> で次代の正しい形態（僭主政→貴族政等）へ移る。
        /// </summary>
        public static bool IsOverthrowReady(RegimeForm form, float oppression, float discontent, AnacyclosisParams p)
            => OverthrowPressure(form, oppression, discontent) >= p.overthrowThreshold;

        public static bool IsOverthrowReady(RegimeForm form, float oppression, float discontent)
            => IsOverthrowReady(form, oppression, discontent, AnacyclosisParams.Default);

        /// <summary>
        /// 各形態の安定度（0..1）。正しい形態は徳がある間は安定（徳×制度的歯止め）、堕落形態は徳に依らず
        /// 固有の不安定さ（corruptInstability ぶん安定が削られる＝暴政・私利支配はそもそも脆い）。
        /// 制度的歯止め institutionalCheck は混合政体的な抑制（<see cref="MixedConstitutionRules"/> 委譲）の度合い。
        /// </summary>
        public static float FormStability(RegimeForm form, float virtue, float institutionalCheck, AnacyclosisParams p)
        {
            float v = Mathf.Clamp01(virtue);
            float check = Mathf.Clamp01(institutionalCheck);
            if (IsLegitimateForm(form))
            {
                // 正しい形態＝徳と制度的歯止めが安定を支える
                return Mathf.Clamp01(v * Mathf.Lerp(0.5f, 1f, check));
            }
            // 堕落形態＝固有不安定度ぶん削られ、制度的歯止めでわずかに緩和される
            float baseStab = Mathf.Clamp01(v * (1f - p.corruptInstability));
            return Mathf.Clamp01(baseStab * Mathf.Lerp(0.6f, 1f, check));
        }

        public static float FormStability(RegimeForm form, float virtue, float institutionalCheck)
            => FormStability(form, virtue, institutionalCheck, AnacyclosisParams.Default);

        /// <summary>
        /// 循環の速さ（0..1スケール）。徳が速く失われ（virtueDecay 高）、制度的歯止め（institutionalBrake）が
        /// 無いほど循環が速い＝政体が次々と腐落・打倒される。混合政体的なブレーキは循環を遅らせる（止めはしない）。
        /// </summary>
        public static float CycleVelocity(float virtueDecay, float institutionalBrake, AnacyclosisParams p)
        {
            float decay = Mathf.Clamp01(virtueDecay);
            float brake = Mathf.Clamp01(institutionalBrake);
            return Mathf.Clamp01(p.cycleBaseSpeed * decay * (1f - brake) * 2f);
        }

        public static float CycleVelocity(float virtueDecay, float institutionalBrake)
            => CycleVelocity(virtueDecay, institutionalBrake, AnacyclosisParams.Default);

        /// <summary>
        /// 循環位置（0..1）を6形態へ写す。位置が進むほど 王政→僭主政→貴族政→寡頭政→民主政→衆愚政 と
        /// 段階的に切り替わる（円環の弧を6等分してたどる）。1.0は衆愚政の末端＝次の王政へ巻き戻る直前。
        /// </summary>
        public static RegimeForm PhaseOf(float cyclePosition)
        {
            float x = Mathf.Clamp01(cyclePosition);
            int idx = (int)(x * 6f);
            if (idx > 5) idx = 5; // x==1.0 のクランプ
            switch (idx)
            {
                case 0: return RegimeForm.王政;
                case 1: return RegimeForm.僭主政;
                case 2: return RegimeForm.貴族政;
                case 3: return RegimeForm.寡頭政;
                case 4: return RegimeForm.民主政;
                default: return RegimeForm.衆愚政;
            }
        }

        /// <summary>
        /// アナキュクローシスが一巡したか＝現在の形態が起点の形態に戻った（円環を一周した）。
        /// 6形態を <see cref="NextForm"/> でたどり、衆愚政→王政を経て起点へ回帰すれば循環完了。
        /// </summary>
        public static bool IsAnacyclosisComplete(RegimeForm form, RegimeForm startForm)
        {
            return form == startForm;
        }
    }
}
