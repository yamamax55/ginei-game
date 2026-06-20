using UnityEngine;

namespace Ginei
{
    /// <summary>建国功臣の処遇（厚遇＝領地を与える／転封＝力を削ぐ／粛清＝物理的に消す）。</summary>
    public enum MeritDisposition
    {
        厚遇,
        転封,
        粛清,
    }

    /// <summary>功臣処遇ジレンマの調整係数（劉邦と建国功臣＝飛鳥尽きて良弓蔵る）。</summary>
    public readonly struct MeritRetentionParams
    {
        /// <summary>軍功×地方基盤が中央への潜在脅威に転じる基準倍率（強すぎる功臣は危険）。</summary>
        public readonly float threatScale;
        /// <summary>厚遇で買える当面の忠誠の基準効果。</summary>
        public readonly float rewardLoyaltyScale;
        /// <summary>厚遇が独立勢力化リスクとして残す基準割合（恩を売っても力は削れない）。</summary>
        public readonly float independenceRiskScale;
        /// <summary>転封・降格で力を削いだ分が不満に変わる基準倍率（鳥尽きて弓蔵る）。</summary>
        public readonly float discontentScale;
        /// <summary>粛清が残る功臣に与える恐怖の基準倍率（次は自分かと恐れる）。</summary>
        public readonly float fearScale;
        /// <summary>粛清逆効果（残る功臣の先制反乱）と判定する恐怖の閾値。</summary>
        public readonly float backlashThreshold;

        public MeritRetentionParams(float threatScale, float rewardLoyaltyScale, float independenceRiskScale,
            float discontentScale, float fearScale, float backlashThreshold)
        {
            this.threatScale = Mathf.Max(0f, threatScale);
            this.rewardLoyaltyScale = Mathf.Clamp01(rewardLoyaltyScale);
            this.independenceRiskScale = Mathf.Clamp01(independenceRiskScale);
            this.discontentScale = Mathf.Clamp01(discontentScale);
            this.fearScale = Mathf.Clamp01(fearScale);
            this.backlashThreshold = Mathf.Clamp01(backlashThreshold);
        }

        /// <summary>既定＝脅威1.0・厚遇忠誠0.5・独立リスク0.6・転封不満0.5・粛清恐怖0.7・逆効果閾値0.6。</summary>
        public static MeritRetentionParams Default =>
            new MeritRetentionParams(1.0f, 0.5f, 0.6f, 0.5f, 0.7f, 0.6f);
    }

    /// <summary>
    /// 功臣処遇ジレンマの純ロジック（KORY-6 #1422・項羽と劉邦＝劉邦と韓信・彭越ら建国功臣）。
    /// **勝利・建国後、強大な軍功を持つ功臣をどう処遇するかのジレンマ＝①厚遇して領地を与える
    /// （が独立勢力化の脅威が残る）②転封・降格で力を削ぐ（が不満を生む）③粛清する（が他の功臣を
    /// 恐れさせ離反・先制反乱を招く）**。劉邦は功臣を次々粛清して中央集権を固めたが信を失った
    /// ＝飛鳥尽きて良弓蔵る・狡兎死して走狗烹らる。
    /// 政策としての粛清の損得（人材毀損・萎縮）は <see cref="PurgeRules"/>、報酬の士気/忠誠効果は
    /// <see cref="CompensationRules"/>、君主・功臣の器量は <see cref="CapacityRules"/>、反乱の生起は
    /// <see cref="MutinyRules"/> が担い、ここは**建国功臣の処遇ジレンマ（処遇ごとの安定化帰結と
    /// 他功臣への波及）に特化**する。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MeritRetentionRules
    {
        /// <summary>
        /// 功臣の中央への潜在的脅威（0..1）＝軍功 meritPower(0..1)×地方基盤 regionalBase(0..1)。
        /// 軍功が高くても地方基盤が無ければ脅威は小さく、両方そろう功臣（韓信）が最も危険＝
        /// 強すぎる功臣は中央集権の障害になる。
        /// </summary>
        public static float VassalThreat(float meritPower, float regionalBase, MeritRetentionParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(meritPower) * Mathf.Clamp01(regionalBase) * p.threatScale);
        }

        public static float VassalThreat(float meritPower, float regionalBase)
            => VassalThreat(meritPower, regionalBase, MeritRetentionParams.Default);

        /// <summary>
        /// 厚遇で買える当面の忠誠（0..1）＝厚遇のみ有効で generosity(0..1)×rewardLoyaltyScale。
        /// out で残る独立勢力化リスク（generosity×independenceRiskScale）も返す＝恩を売っても
        /// 力そのものは削れないので、厚遇された功臣は領地で独立勢力に育ちうる。
        /// </summary>
        public static float RewardLoyalty(MeritDisposition disposition, float generosity,
            MeritRetentionParams p, out float independenceRisk)
        {
            float g = Mathf.Clamp01(generosity);
            if (disposition != MeritDisposition.厚遇)
            {
                independenceRisk = 0f;
                return 0f;
            }
            independenceRisk = Mathf.Clamp01(g * p.independenceRiskScale);
            return Mathf.Clamp01(g * p.rewardLoyaltyScale);
        }

        public static float RewardLoyalty(MeritDisposition disposition, float generosity, out float independenceRisk)
            => RewardLoyalty(disposition, generosity, MeritRetentionParams.Default, out independenceRisk);

        /// <summary>
        /// 転封・降格による不満（0..1）＝転封のみ有効で powerReduction(0..1)×discontentScale。
        /// 力を削ぐほど不満が募る（鳥尽きて弓蔵る）＝功に報いず力だけ奪う処遇への怨み。
        /// </summary>
        public static float ReassignmentDiscontent(MeritDisposition disposition, float powerReduction, MeritRetentionParams p)
        {
            if (disposition != MeritDisposition.転封) return 0f;
            return Mathf.Clamp01(Mathf.Clamp01(powerReduction) * p.discontentScale);
        }

        public static float ReassignmentDiscontent(MeritDisposition disposition, float powerReduction)
            => ReassignmentDiscontent(disposition, powerReduction, MeritRetentionParams.Default);

        /// <summary>
        /// 粛清による安定化（純利得 −1..1）＝粛清のみ有効。脅威の除去 threatRemoved(0..1) は中央の
        /// 安定を即座に上げるが、同じ規模が他の功臣の恐怖 threatRemoved×fearScale として差し引かれる
        /// ＝脅威を消すほど残る功臣が震える。純安定 = threatRemoved − 恐怖。
        /// </summary>
        public static float PurgeStabilization(MeritDisposition disposition, float threatRemoved, MeritRetentionParams p)
        {
            if (disposition != MeritDisposition.粛清) return 0f;
            float removed = Mathf.Clamp01(threatRemoved);
            float fear = removed * p.fearScale;
            return Mathf.Clamp(removed - fear, -1f, 1f);
        }

        public static float PurgeStabilization(MeritDisposition disposition, float threatRemoved)
            => PurgeStabilization(disposition, threatRemoved, MeritRetentionParams.Default);

        /// <summary>
        /// 残る功臣の恐怖（0..1）＝粛清の苛烈さ purgeIntensity(0..1)×功臣間の連帯 vassalSolidarity(0..1)。
        /// 連帯が高い功臣集団ほど「次は自分か」と恐れを共有し、離反・先制反乱へ傾く
        /// （韓信の粛清が彭越・英布の反乱を呼ぶ）。連帯ゼロの孤立した功臣は恐れを連鎖させない。
        /// </summary>
        public static float OtherVassalsFear(float purgeIntensity, float vassalSolidarity, MeritRetentionParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(purgeIntensity) * Mathf.Clamp01(vassalSolidarity) * (1f + p.fearScale));
        }

        public static float OtherVassalsFear(float purgeIntensity, float vassalSolidarity)
            => OtherVassalsFear(purgeIntensity, vassalSolidarity, MeritRetentionParams.Default);

        /// <summary>
        /// 裏切りからの信の喪失（0..1）＝忠実な功臣まで粛清した度合い purgeOfLoyal(0..1)に非線形に膨らむ
        /// （信なくば立たず）。疑わしきを罰する程度なら信は保たれるが、明白に忠実な功臣を消すと
        /// 政権は信頼の地盤を失う＝二乗的に効かせ、深い裏切りほど取り返しがつかない。
        /// </summary>
        public static float TrustErosionFromBetrayal(float purgeOfLoyal)
        {
            float x = Mathf.Clamp01(purgeOfLoyal);
            return Mathf.Clamp01(x * (1f + x)); // x + x^2 ＝忠臣を消すほど加速して信が崩れる
        }

        /// <summary>
        /// 処遇ごとの安定化への帰結（−1..1）＝短期の安定 vs 長期の信頼を統合した正味効果。
        /// 厚遇＝当面安定だが脅威ぶん独立化リスクで割り引く／転封＝脅威を一部削るが不満を差し引く／
        /// 粛清＝脅威を消すが恐怖と信の喪失を差し引く。regimeSecurity(0..1)が高いほど反動を吸収できる。
        /// </summary>
        public static float DispositionOutcome(MeritDisposition disposition, float vassalThreat, float regimeSecurity, MeritRetentionParams p)
        {
            float threat = Mathf.Clamp01(vassalThreat);
            float security = Mathf.Clamp01(regimeSecurity);
            float absorb = 0.5f + 0.5f * security; // 強い政権ほど反動を吸収（0.5..1.0）
            switch (disposition)
            {
                case MeritDisposition.厚遇:
                    // 当面は安定だが脅威を温存＝独立勢力化のリスクが残る。
                    return Mathf.Clamp(0.4f - threat * p.independenceRiskScale, -1f, 1f);
                case MeritDisposition.転封:
                    // 力を削ぎ脅威は下がるが、削った分の不満が反動として残る（強い政権は吸収）。
                    return Mathf.Clamp(threat * 0.6f - threat * p.discontentScale * (1f - security), -1f, 1f);
                case MeritDisposition.粛清:
                    // 脅威は即座に消えるが、恐怖と信の喪失が長期の安定を蝕む。
                    float fear = threat * p.fearScale;
                    return Mathf.Clamp(threat - fear * (2f - absorb), -1f, 1f);
                default:
                    return 0f;
            }
        }

        public static float DispositionOutcome(MeritDisposition disposition, float vassalThreat, float regimeSecurity)
            => DispositionOutcome(disposition, vassalThreat, regimeSecurity, MeritRetentionParams.Default);

        /// <summary>
        /// 最適な処遇の推奨。脅威 vassalThreat・政権の強さ regimeStrength(0..1)・功臣の忠誠 vassalLoyalty(0..1)
        /// に応じて：脅威が低いか忠誠が高ければ厚遇（信のある功臣は活かす）、脅威が高く政権が弱ければ
        /// 転封で穏便に力を削ぐ、脅威が極めて高く政権が強い時のみ粛清を辞さない（が信を失う賭け）。
        /// </summary>
        public static MeritDisposition OptimalDisposition(float vassalThreat, float regimeStrength, float vassalLoyalty, MeritRetentionParams p)
        {
            float threat = Mathf.Clamp01(vassalThreat);
            float strength = Mathf.Clamp01(regimeStrength);
            float loyalty = Mathf.Clamp01(vassalLoyalty);

            // 忠誠が高い、または脅威が小さい功臣は厚遇して活かすのが上策。
            if (loyalty >= 0.6f || threat < 0.4f) return MeritDisposition.厚遇;

            // 脅威が極めて高く政権が十分強く、忠誠が低い功臣のみ粛清を選びうる。
            if (threat >= 0.75f && strength >= 0.7f && loyalty < 0.3f) return MeritDisposition.粛清;

            // それ以外（脅威はあるが政権が万全でない）は転封で穏便に力を削ぐ。
            return MeritDisposition.転封;
        }

        public static MeritDisposition OptimalDisposition(float vassalThreat, float regimeStrength, float vassalLoyalty)
            => OptimalDisposition(vassalThreat, regimeStrength, vassalLoyalty, MeritRetentionParams.Default);

        /// <summary>
        /// 粛清の逆効果判定＝残る功臣の恐怖 otherVassalsFear が閾値を超えたら反乱（先制離反）を誘発する。
        /// 恐怖が積もった功臣集団は、自分が消される前に立ち上がる（彭越・英布の反乱）。
        /// </summary>
        public static bool IsPurgeBacklash(float otherVassalsFear, float threshold)
        {
            return Mathf.Clamp01(otherVassalsFear) > Mathf.Clamp01(threshold);
        }

        public static bool IsPurgeBacklash(float otherVassalsFear)
            => IsPurgeBacklash(otherVassalsFear, MeritRetentionParams.Default.backlashThreshold);
    }
}
