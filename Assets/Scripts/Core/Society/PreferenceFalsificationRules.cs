using UnityEngine;

namespace Ginei
{
    /// <summary>選好偽装（クーラン型）の調整係数。</summary>
    public readonly struct PreferenceFalsificationParams
    {
        /// <summary>抑圧→偽装の強さ（抑圧が表明支持をどれだけ本音より上へ盛るか）。</summary>
        public readonly float falsificationGain;
        /// <summary>可視的反対が個人の表明閾値をどれだけ押し下げるか（閾値カスケードの感度）。</summary>
        public readonly float cascadeSensitivity;
        /// <summary>1tick で可視的反対が動く最大幅。</summary>
        public readonly float maxCascadeShift;
        /// <summary>体制の盲目度に効く抑圧の重み（抑圧が強いほど足元が見えない）。</summary>
        public readonly float blindnessGain;

        public PreferenceFalsificationParams(float falsificationGain, float cascadeSensitivity, float maxCascadeShift, float blindnessGain)
        {
            this.falsificationGain = Mathf.Clamp01(falsificationGain);
            this.cascadeSensitivity = Mathf.Clamp01(cascadeSensitivity);
            this.maxCascadeShift = Mathf.Clamp01(maxCascadeShift);
            this.blindnessGain = Mathf.Clamp01(blindnessGain);
        }

        /// <summary>既定＝偽装強度0.6・カスケード感度0.5・最大カスケード幅0.2・盲目重み0.7。</summary>
        public static PreferenceFalsificationParams Default => new PreferenceFalsificationParams(0.6f, 0.5f, 0.2f, 0.7f);
    }

    /// <summary>
    /// 選好偽装の純ロジック（クーラン『私的真実、公的虚偽』型）。抑圧下では本音（私的選好）が隠され、
    /// 世論調査も体制も「見かけの支持」（公的に表明された支持）しか観測できない＝<b>抑圧は不満を消さず隠す</b>。
    /// 隠された不満は個人ごとの表明閾値を抱え、周りが声を上げ始めると自分も明かす（<b>閾値カスケード</b>）。
    /// ある臨界点（criticalMass）を超えると一気に噴出する（プレオブラジェンスカヤ広場型＝革命が突然に見える理由）。
    /// 強権ほど偽装が深く、体制は自分の足元の不満を測り損ねて突然倒れる。
    /// <see cref="ConsentRules"/> が「実際の協力（本音に基づく統治可能性）」を扱うのに対し、こちらは
    /// 「表明と本音の乖離（観測される支持の虚構性）」を扱う＝別系統。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PreferenceFalsificationRules
    {
        /// <summary>
        /// 表明される（公的な）支持（0..1）。本音の支持 privateSupport(0..1) を抑圧 repression(0..1) が
        /// 残り余地（1−本音）の分だけ上へ盛る＝抑圧が高いほど見かけの支持が本音より高く偽装される。
        /// 抑圧0なら本音がそのまま表に出る。
        /// </summary>
        public static float ExpressedSupport(float privateSupport, float repression, PreferenceFalsificationParams p)
        {
            float priv = Mathf.Clamp01(privateSupport);
            float rep = Mathf.Clamp01(repression);
            float headroom = 1f - priv;
            return Mathf.Clamp01(priv + headroom * rep * p.falsificationGain);
        }

        public static float ExpressedSupport(float privateSupport, float repression)
            => ExpressedSupport(privateSupport, repression, PreferenceFalsificationParams.Default);

        /// <summary>
        /// 選好ギャップ（0..1）＝表明支持と本音支持の乖離＝体制が見えていない隠れた不満の量。
        /// expressed≥private（偽装は上方向）を前提に正の差を返す。大きいほど統計が現実から乖離している。
        /// </summary>
        public static float PreferenceGap(float privateSupport, float expressedSupport)
        {
            float priv = Mathf.Clamp01(privateSupport);
            float exp = Mathf.Clamp01(expressedSupport);
            return Mathf.Clamp01(exp - priv);
        }

        /// <summary>
        /// 個人が本音を明かす（公然と反対する）閾値（0..1）。素の臨値 individualThreshold(0..1) から、
        /// 既に見えている反対 visibleDissent(0..1) が cascadeSensitivity の分だけ閾値を押し下げる
        /// ＝周りが声を上げ始めると自分の閾値も下がる（閾値カスケード）。低いほど明かしやすい。
        /// </summary>
        public static float RevealedThreshold(float individualThreshold, float visibleDissent, PreferenceFalsificationParams p)
        {
            float th = Mathf.Clamp01(individualThreshold);
            float vis = Mathf.Clamp01(visibleDissent);
            return Mathf.Clamp01(th - vis * p.cascadeSensitivity);
        }

        public static float RevealedThreshold(float individualThreshold, float visibleDissent)
            => RevealedThreshold(individualThreshold, visibleDissent, PreferenceFalsificationParams.Default);

        /// <summary>
        /// 可視的反対の連鎖（1tick後の visibleDissent・0..1）。隠れた不満の圧力（本音の不満 1−privateSupport）が
        /// 抑圧 repression の蓋を上回る分だけ、可視的反対をカスケード感度に比例して押し上げる。
        /// 圧力＝(1−private)×(1+visibleDissent×cascadeSensitivity)（既に見えている反対が呼び水になり加速）。
        /// 蓋＝repression。差が正なら噴出、負（蓋が勝つ）なら沈静（やや戻す）。maxCascadeShift で1tickの幅を制限。
        /// 臨界点を超えると一気に噴出する非線形性を、可視反対を呼び水に取り込むことで表す。
        /// </summary>
        public static float CascadeTick(float visibleDissent, float privateSupport, float repression, float dt, PreferenceFalsificationParams p)
        {
            float vis = Mathf.Clamp01(visibleDissent);
            float discontent = 1f - Mathf.Clamp01(privateSupport);
            float rep = Mathf.Clamp01(repression);
            float d = Mathf.Max(0f, dt);

            float pressure = discontent * (1f + vis * p.cascadeSensitivity);
            float net = pressure - rep; // 正＝噴出 / 負＝沈静
            float shift = Mathf.Clamp(net, -1f, 1f) * p.maxCascadeShift * d;
            return Mathf.Clamp01(vis + shift);
        }

        public static float CascadeTick(float visibleDissent, float privateSupport, float repression, float dt)
            => CascadeTick(visibleDissent, privateSupport, repression, dt, PreferenceFalsificationParams.Default);

        /// <summary>
        /// 革命的カスケードの臨界判定＝可視的反対 visibleDissent が臨界質量 criticalMass(0..1) 以上に達したか。
        /// 超えた瞬間に偽装が連鎖崩壊し本音が一斉に表に出る（雪崩）。
        /// </summary>
        public static bool IsPreferenceCascade(float visibleDissent, float criticalMass)
            => Mathf.Clamp01(visibleDissent) >= Mathf.Clamp01(criticalMass);

        /// <summary>
        /// 体制の盲目度（0..1）＝抑圧 repression が強いほど、また選好ギャップ preferenceGap が大きいほど
        /// 体制は自分の足元の不満を測れない（強権ほど突然倒れる）。抑圧は偽装を深め、ギャップは観測を欺く＝
        /// 両者の積で「測り損ね」の深さを出す。
        /// </summary>
        public static float RegimeBlindness(float repression, float preferenceGap, PreferenceFalsificationParams p)
        {
            float rep = Mathf.Clamp01(repression);
            float gap = Mathf.Clamp01(preferenceGap);
            return Mathf.Clamp01(rep * gap * p.blindnessGain + gap * (1f - p.blindnessGain));
        }

        public static float RegimeBlindness(float repression, float preferenceGap)
            => RegimeBlindness(repression, preferenceGap, PreferenceFalsificationParams.Default);
    }
}
