using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 組織の「空気（くうき）」の純データ＝日本軍型の集合的雰囲気の状態（#1371・SHP-1）。
    /// 論理や事実でなく、その場の情緒的圧力が意思決定を縛る度合いを保持する（可変フィールド）。
    /// </summary>
    public struct AtmosphereState
    {
        /// <summary>空気の圧力（0..1）＝場を支配する情緒的な集合的圧力の強さ。高いほど誰も逆らえない。</summary>
        public float atmosphericPressure;
        /// <summary>同調（0..1）＝空気を読んで皆が足並みを揃える度合い。高いほど異論が出ない。</summary>
        public float groupConformity;
        /// <summary>封じられた異論（0..1）＝内心の疑問が空気に押さえ込まれて表に出ない量。</summary>
        public float silencedDissent;

        public AtmosphereState(float atmosphericPressure, float groupConformity, float silencedDissent)
        {
            this.atmosphericPressure = Mathf.Clamp01(atmosphericPressure);
            this.groupConformity = Mathf.Clamp01(groupConformity);
            this.silencedDissent = Mathf.Clamp01(silencedDissent);
        }
    }

    /// <summary>「空気」支配（山本七平型）の調整係数。</summary>
    public readonly struct AtmosphereParams
    {
        /// <summary>空気の形成に効く情緒的合意の重み（論理でなく雰囲気で固まる）。</summary>
        public readonly float consensusWeight;
        /// <summary>空気の形成に効く階層圧力の重み（上意下達が空気を固める）。</summary>
        public readonly float hierarchyWeight;
        /// <summary>空気が個人の勇気をどれだけ無力化するか（勇気があっても言い出せない強さ）。</summary>
        public readonly float suppressionGain;
        /// <summary>空気が客観的事実・撤退の決断をどれだけ圧倒するか（不利でも空気で決まる強さ）。</summary>
        public readonly float overrideGain;
        /// <summary>空気の自己増幅速度（per dt・一度できた空気は時間で強化される）。</summary>
        public readonly float momentumRate;
        /// <summary>空気支配判定の既定しきい値（圧力×異論封殺がこれ以上で合理的判断を失う）。</summary>
        public readonly float ruleThreshold;

        public AtmosphereParams(float consensusWeight, float hierarchyWeight, float suppressionGain,
                                float overrideGain, float momentumRate, float ruleThreshold)
        {
            this.consensusWeight = Mathf.Max(0f, consensusWeight);
            this.hierarchyWeight = Mathf.Max(0f, hierarchyWeight);
            this.suppressionGain = Mathf.Max(0f, suppressionGain);
            this.overrideGain = Mathf.Max(0f, overrideGain);
            this.momentumRate = Mathf.Max(0f, momentumRate);
            this.ruleThreshold = Mathf.Clamp01(ruleThreshold);
        }

        /// <summary>既定＝合意重み0.6/階層重み0.4/封殺強度0.8/圧倒強度0.8/増幅速度0.1/支配閾0.6。</summary>
        public static AtmosphereParams Default => new AtmosphereParams(0.6f, 0.4f, 0.8f, 0.8f, 0.1f, 0.6f);
    }

    /// <summary>
    /// 組織を支配する「空気（くうき）」の純ロジック＝山本七平『「空気」の研究』『失敗の本質』型（#1371・SHP-1）。
    /// 日本軍型の組織では、論理や客観的事実でなく<b>その場の雰囲気・情緒的な集合的圧力＝「空気」</b>が意思決定を縛る。
    /// 誰も反対できない「空気」が形成されると、客観的に不利でも撤退・中止を言い出せず破滅へ突き進む
    /// （インパール作戦・戦艦大和の沖縄特攻）。集合的沈黙圧力が諫言を封じる＝<b>論理が空気に敗れる</b>。
    /// <see cref="PreferenceFalsificationRules"/>（選好偽装＝抑圧下で表明支持と本音が乖離・観測の虚構性）／
    /// <see cref="AdvisorCandorRules"/>（直言と佞臣＝諫言の質が情報環境を決める・君主への到達）／
    /// <see cref="InformationDistortionRules"/>（情報歪曲・同EPIC SHP＝伝達過程での情報の劣化）／
    /// <see cref="PublicOpinionRules"/>（世論の同調圧力＝多数派収束と沈黙の螺旋）とは別系統＝こちらは
    /// 「日本軍型の『空気』支配＝情緒的圧力が論理を圧倒し撤退を麻痺させる」を <see cref="AtmosphereState"/> 中核で扱う。
    /// 乱数なし決定論・全入力クランプ。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AtmosphereRules
    {
        // ===== 空気の形成（論理でなく雰囲気で固まる） =====

        /// <summary>
        /// 「空気」の形成（0..1の時間発展）＝情緒的な集合的合意(emotionalConsensus 0..1)と
        /// 階層圧力(hierarchyPressure 0..1)が重み付けで「空気」を固める（論理でなく雰囲気で決まる）。
        /// 両者が高いほど空気が濃く、現在の空気圧 atmosphericPressure からその目標へ向かって育つ。
        /// dt で時間積分・0..1にクランプ。
        /// </summary>
        public static float AtmosphereFormation(float atmosphericPressure, float emotionalConsensus, float hierarchyPressure, float dt, AtmosphereParams p)
        {
            float cur = Mathf.Clamp01(atmosphericPressure);
            float consensus = Mathf.Clamp01(emotionalConsensus);
            float hierarchy = Mathf.Clamp01(hierarchyPressure);
            float weightSum = Mathf.Max(0.0001f, p.consensusWeight + p.hierarchyWeight);
            float target = Mathf.Clamp01((consensus * p.consensusWeight + hierarchy * p.hierarchyWeight) / weightSum);
            return Mathf.Clamp01(Mathf.MoveTowards(cur, target, p.momentumRate * Mathf.Max(0f, dt)));
        }

        public static float AtmosphereFormation(float atmosphericPressure, float emotionalConsensus, float hierarchyPressure, float dt)
            => AtmosphereFormation(atmosphericPressure, emotionalConsensus, hierarchyPressure, dt, AtmosphereParams.Default);

        // ===== 異論の封殺（誰も反対できない） =====

        /// <summary>
        /// 異論の封殺度（0..1）＝空気の圧力(atmosphericPressure 0..1)が個人の勇気(individualCourage 0..1)を
        /// 無力化する。圧力が高いほど、勇気があっても言い出せない＝勇気は圧力を suppressionGain の分しか
        /// 押し返せない。圧力1・勇気0で封殺最大、圧力0なら封殺なし。
        /// </summary>
        public static float DissentSuppression(float atmosphericPressure, float individualCourage, AtmosphereParams p)
        {
            float pressure = Mathf.Clamp01(atmosphericPressure);
            float courage = Mathf.Clamp01(individualCourage);
            // 空気の圧力が封殺を生み、勇気がその一部を押し返す（勇気があっても完全には抗えない）。
            return Mathf.Clamp01(pressure * (1f - courage * p.suppressionGain));
        }

        public static float DissentSuppression(float atmosphericPressure, float individualCourage)
            => DissentSuppression(atmosphericPressure, individualCourage, AtmosphereParams.Default);

        // ===== 論理が空気に圧倒される（大和特攻） =====

        /// <summary>
        /// 論理が空気に圧倒される度合い（0..1）＝空気の圧力(atmosphericPressure 0..1)が
        /// 客観的証拠(objectiveEvidence 0..1)を押しのける。圧力が高く証拠の重み（overrideGain）が勝つほど、
        /// 不利を示す事実があっても空気で決まる（戦艦大和の特攻＝勝算なき出撃を空気が決めた）。
        /// 証拠が空気を打ち消せた残りが「論理の勝ち」＝返り値が高いほど論理が敗れている。
        /// </summary>
        public static float LogicOverriddenByMood(float atmosphericPressure, float objectiveEvidence, AtmosphereParams p)
        {
            float pressure = Mathf.Clamp01(atmosphericPressure);
            float evidence = Mathf.Clamp01(objectiveEvidence);
            // 空気の押し（overrideGain で増幅）から、客観的証拠の押し返しを差し引く。
            float moodForce = pressure * p.overrideGain;
            return Mathf.Clamp01(moodForce - evidence);
        }

        public static float LogicOverriddenByMood(float atmosphericPressure, float objectiveEvidence)
            => LogicOverriddenByMood(atmosphericPressure, objectiveEvidence, AtmosphereParams.Default);

        // ===== 撤退・中止の麻痺（インパール） =====

        /// <summary>
        /// 撤退・中止の決断の麻痺度（0..1）＝空気の圧力(atmosphericPressure 0..1)が高いほど、
        /// 状況の悪化(situationDeterioration 0..1)が深刻でも撤退を言い出せない。客観的に不利になるほど
        /// 撤退すべきなのに、空気がその決断を麻痺させて破滅へ突き進む（インパール作戦）。
        /// 状況悪化が空気の圧力と掛かり合い、麻痺が深まる＝悪化しても止められない逆説。
        /// </summary>
        public static float RetreatParalysis(float atmosphericPressure, float situationDeterioration, AtmosphereParams p)
        {
            float pressure = Mathf.Clamp01(atmosphericPressure);
            float deterioration = Mathf.Clamp01(situationDeterioration);
            // 圧力が麻痺の土台。状況が悪化するほど（撤退の必要が増すほど）空気が決断を縛り麻痺が増す。
            return Mathf.Clamp01(pressure * (1f - p.overrideGain * 0.25f) + pressure * deterioration * p.overrideGain * 0.25f);
        }

        public static float RetreatParalysis(float atmosphericPressure, float situationDeterioration)
            => RetreatParalysis(atmosphericPressure, situationDeterioration, AtmosphereParams.Default);

        // ===== 沈黙の多数派（皆が空気を読んで黙る） =====

        /// <summary>
        /// 沈黙の多数派（0..1）＝同調(groupConformity 0..1)が高いほど、皆が内心の疑問(privateDoubt 0..1)を
        /// 抱えていても空気を読んで沈黙する。疑問を持つ者が多くても、同調圧力がそれを声にさせない＝
        /// 「皆が反対と知らないまま全員が反対を黙る」沈黙の多数派。疑問×同調の積で沈黙の量を出す。
        /// </summary>
        public static float SilentMajority(float groupConformity, float privateDoubt)
        {
            float conformity = Mathf.Clamp01(groupConformity);
            float doubt = Mathf.Clamp01(privateDoubt);
            // 内心の疑問が、同調圧力に比例して沈黙へ押し込まれる。
            return Mathf.Clamp01(doubt * conformity);
        }

        // ===== 空気を破る者（誰かが言えば崩れる） =====

        /// <summary>
        /// 空気を破れる度合い（0..1）＝よそ者の存在(outsiderPresence 0..1＝空気を共有しない者)と
        /// 空気に逆らえる権威(authorityToDefy 0..1＝空気を読まない権威者)が論理を取り戻す。
        /// 場の空気に染まらない者が一人でも声を上げれば空気が崩れる＝相補（どちらかが高ければ破れる）。
        /// 1−(1−よそ者)(1−権威) で「少なくともどちらかが効く」を表す。
        /// </summary>
        public static float AtmosphereBreaker(float outsiderPresence, float authorityToDefy)
        {
            float outsider = Mathf.Clamp01(outsiderPresence);
            float authority = Mathf.Clamp01(authorityToDefy);
            // どちらか一方でも空気を破れる＝補集合の積を取って反転（OR 的合成）。
            return Mathf.Clamp01(1f - (1f - outsider) * (1f - authority));
        }

        // ===== 空気の自己増幅（引き返せなくなる） =====

        /// <summary>
        /// 空気の自己増幅（1tick後の atmosphericPressure・0..1）＝一度できた空気は時間とともに強化され、
        /// 引き返せなくなる（空気が空気を呼ぶ）。現在の圧力が高いほど増分が大きい（自己強化）＝
        /// 既存の空気圧 × momentumRate × 残り余地で押し上がり、放置すると 1 へ漸近する。
        /// dt で時間積分・0..1にクランプ。
        /// </summary>
        public static float KutekiMomentum(float atmosphericPressure, float dt, AtmosphereParams p)
        {
            float pressure = Mathf.Clamp01(atmosphericPressure);
            float d = Mathf.Max(0f, dt);
            // 自己強化：今ある空気が残り余地を埋めるように増幅する（強い空気ほど速く固まる）。
            float growth = pressure * (1f - pressure) * p.momentumRate * d;
            return Mathf.Clamp01(pressure + growth);
        }

        public static float KutekiMomentum(float atmosphericPressure, float dt)
            => KutekiMomentum(atmosphericPressure, dt, AtmosphereParams.Default);

        // ===== 空気支配の判定（合理的判断を失う） =====

        /// <summary>
        /// 組織が「空気」に支配され合理的判断を失った判定＝空気の圧力(atmosphericPressure 0..1)と
        /// 異論の封殺(dissentSuppression 0..1)の積（＝圧力が高く異論も封じられた状態）が
        /// しきい値(threshold 0..1)以上なら true。空気が論理を縛り、誰も止められない組織。
        /// </summary>
        public static bool IsRuledByAtmosphere(float atmosphericPressure, float dissentSuppression, float threshold)
        {
            float pressure = Mathf.Clamp01(atmosphericPressure);
            float suppression = Mathf.Clamp01(dissentSuppression);
            return pressure * suppression >= Mathf.Clamp01(threshold);
        }

        /// <summary>空気支配判定（既定しきい値 ruleThreshold を使用）。</summary>
        public static bool IsRuledByAtmosphere(float atmosphericPressure, float dissentSuppression, AtmosphereParams p)
            => IsRuledByAtmosphere(atmosphericPressure, dissentSuppression, p.ruleThreshold);

        public static bool IsRuledByAtmosphere(float atmosphericPressure, float dissentSuppression)
            => IsRuledByAtmosphere(atmosphericPressure, dissentSuppression, AtmosphereParams.Default);
    }
}
