using UnityEngine;

namespace Ginei
{
    /// <summary>テロの原理化（アーレント型）の調整係数。</summary>
    public readonly struct TerrorPrincipleParams
    {
        /// <summary>道具的恐怖の最大値（実在脅威を抑える手段としての恐怖の上限）。</summary>
        public readonly float instrumentalScale;
        /// <summary>恐怖が制度に根を張り脅威と無関係に自走する速度（per dt・根付き1のとき）。</summary>
        public readonly float autonomyGrowthRate;
        /// <summary>粛清が脅威減少後も慣性で増殖する係数（自己増殖の燃料）。</summary>
        public readonly float momentumGain;
        /// <summary>運動が止まらないために敵を作り続ける速度（per dt・イデオロギー掌握1のとき）。</summary>
        public readonly float movementMomentumRate;
        /// <summary>恐怖が統治原理へ転化したと見なす自走度の閾値。</summary>
        public readonly float principleThreshold;

        public TerrorPrincipleParams(float instrumentalScale, float autonomyGrowthRate, float momentumGain,
                                     float movementMomentumRate, float principleThreshold)
        {
            this.instrumentalScale = Mathf.Clamp01(instrumentalScale);
            this.autonomyGrowthRate = Mathf.Max(0f, autonomyGrowthRate);
            this.momentumGain = Mathf.Max(0f, momentumGain);
            this.movementMomentumRate = Mathf.Max(0f, movementMomentumRate);
            this.principleThreshold = Mathf.Clamp01(principleThreshold);
        }

        /// <summary>既定＝道具的上限0.8・自走0.1・粛清慣性0.15・運動慣性0.1・原理化閾値0.6。</summary>
        public static TerrorPrincipleParams Default => new TerrorPrincipleParams(0.8f, 0.1f, 0.15f, 0.1f, 0.6f);
    }

    /// <summary>
    /// テロの原理化の純ロジック（TOTL-2 #1519・ハンナ・アーレント参考＝全体主義における恐怖の転化）。
    /// 恐怖（terror）は当初、実在する反対派を排除する**道具**だったが、やがてそれ自体が**目的＝統治の原理**へ
    /// 転化する＝もはや脅威がなくても粛清が慣性で自己増殖し、無実の者すら標的になる（恐怖の本質化）。
    /// 全体主義運動は止まると死ぬので、敵を作り続ける（運動の永久機関）＝全員が常に標的になりうる恒常的不安が
    /// 服従を極大化させる。テロの劇場性で恐怖を媒体増幅する <see cref="TerrorRules"/>、政策としての損得計算の
    /// <see cref="PurgeRules"/>、生成済みの全体主義モデル <c>TotalitarianRules</c>（TerrorLoopGain＝恐怖の自己強化ループ）、
    /// 告発が連鎖する <c>AccusationCascadeRules</c>（告発カスケード）とは別系統＝恐怖が手段から目的へ転化する
    /// 「道具→原理」の転換そのものを式に出す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TerrorPrincipleRules
    {
        /// <summary>
        /// 当初の道具的恐怖（0..instrumentalScale）＝実在する脅威 threat(0..1) を抑える必要 suppressionNeed(0..1) に
        /// 比例する手段としての恐怖。脅威も必要も無ければ恐怖は要らない（＝まだ道具）。
        /// </summary>
        public static float InstrumentalTerror(float threat, float suppressionNeed, TerrorPrincipleParams p)
        {
            return Mathf.Clamp01(threat) * Mathf.Clamp01(suppressionNeed) * p.instrumentalScale;
        }

        public static float InstrumentalTerror(float threat, float suppressionNeed)
            => InstrumentalTerror(threat, suppressionNeed, TerrorPrincipleParams.Default);

        /// <summary>
        /// 恐怖の自走度の1tick後（0..1）＝恐怖が制度に根を張る度合い terrorEntrenchment(0..1) に応じて、脅威と無関係に
        /// 自律的に育つ（道具から目的へ）。根が深いほど速く自走し、根がなければ自走しない。
        /// </summary>
        public static float TerrorAutonomy(float terrorEntrenchment, float dt, TerrorPrincipleParams p)
        {
            float d = Mathf.Max(0f, dt);
            float entrenchment = Mathf.Clamp01(terrorEntrenchment);
            return Mathf.Clamp01(entrenchment + p.autonomyGrowthRate * entrenchment * d);
        }

        public static float TerrorAutonomy(float terrorEntrenchment, float dt)
            => TerrorAutonomy(terrorEntrenchment, dt, TerrorPrincipleParams.Default);

        /// <summary>
        /// 自己増殖する粛清の1tick後の勢い（0..1）＝脅威 threatLevel(0..1) が減っても、粛清の慣性 purgeMomentum が
        /// 自分を燃料に増殖する＝脅威が低いほど（標的が尽きるほど）むしろ自分で標的を作り続けて加速する。
        /// </summary>
        public static float SelfPerpetuatingPurge(float purgeMomentum, float threatLevel, float dt, TerrorPrincipleParams p)
        {
            float d = Mathf.Max(0f, dt);
            float momentum = Mathf.Clamp01(purgeMomentum);
            // 脅威が尽きるほど（1−threat が大きいほど）慣性が標的を捏造して自己増殖する。
            float threatVacuum = 1f - Mathf.Clamp01(threatLevel);
            return Mathf.Clamp01(momentum + p.momentumGain * momentum * threatVacuum * d);
        }

        public static float SelfPerpetuatingPurge(float purgeMomentum, float threatLevel, float dt)
            => SelfPerpetuatingPurge(purgeMomentum, threatLevel, dt, TerrorPrincipleParams.Default);

        /// <summary>
        /// 無実の標的化の度合い（0..1）＝恐怖の自走度 terrorAutonomy(0..1) が高いほど、罪の有無 guiltRelevance(0..1)
        /// （標的選定に罪が関係する度合い）が無関係になる＝原理化した恐怖は誰でも標的にできる。
        /// guiltRelevance=1（罪が完全に関係する）なら無実の標的化なし、自走が極大なら誰でも標的。
        /// </summary>
        public static float InnocentTargeting(float terrorAutonomy, float guiltRelevance, TerrorPrincipleParams p)
        {
            return Mathf.Clamp01(terrorAutonomy) * (1f - Mathf.Clamp01(guiltRelevance));
        }

        public static float InnocentTargeting(float terrorAutonomy, float guiltRelevance)
            => InnocentTargeting(terrorAutonomy, guiltRelevance, TerrorPrincipleParams.Default);

        /// <summary>
        /// 道具的恐怖が統治原理へ転化したか＝自走度 terrorAutonomy が閾値 threshold を超え、かつ自走分が
        /// 当初の道具的恐怖 instrumentalTerror を上回る（手段としての恐怖を、目的としての恐怖が追い越した）。
        /// </summary>
        public static bool PrincipleTransition(float instrumentalTerror, float terrorAutonomy, float threshold)
        {
            float autonomy = Mathf.Clamp01(terrorAutonomy);
            return autonomy >= Mathf.Clamp01(threshold) && autonomy > Mathf.Clamp01(instrumentalTerror);
        }

        /// <summary>
        /// 恒常的不安（0..1）＝恐怖が原理化（自走度 terrorAutonomy 高）するほど、全員が常に標的になりうる
        /// ＝誰も安全でない。これが服従を極大化させる（安全を保証できる者がいない世界）。
        /// </summary>
        public static float PermanentInsecurity(float terrorAutonomy)
        {
            return Mathf.Clamp01(terrorAutonomy);
        }

        /// <summary>
        /// 運動の慣性が要求する敵生産の1tick後（0..1）＝全体主義運動は止まると死ぬので、イデオロギー掌握
        /// ideologyGrip(0..1) が強いほど敵を作り続ける（運動の永久機関）。掌握が弱まれば敵生産も鈍る。
        /// </summary>
        public static float MovementMomentumNeed(float ideologyGrip, float dt, TerrorPrincipleParams p)
        {
            float d = Mathf.Max(0f, dt);
            float grip = Mathf.Clamp01(ideologyGrip);
            return Mathf.Clamp01(grip * (1f + p.movementMomentumRate * d));
        }

        public static float MovementMomentumNeed(float ideologyGrip, float dt)
            => MovementMomentumNeed(ideologyGrip, dt, TerrorPrincipleParams.Default);

        /// <summary>
        /// 恐怖が目的化した（粛清が自己増殖する段階に入った）か＝自走度 terrorAutonomy が閾値 threshold 以上。
        /// 既定 threshold は <see cref="TerrorPrincipleParams.principleThreshold"/>。
        /// </summary>
        public static bool IsTerrorPrincipalized(float terrorAutonomy, float threshold)
        {
            return Mathf.Clamp01(terrorAutonomy) >= Mathf.Clamp01(threshold);
        }

        public static bool IsTerrorPrincipalized(float terrorAutonomy)
            => IsTerrorPrincipalized(terrorAutonomy, TerrorPrincipleParams.Default.principleThreshold);
    }
}
