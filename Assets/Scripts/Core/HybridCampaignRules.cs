using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ハイブリッド戦のドメイン（領域・#1374）。正規軍＝従来の艦隊戦力／サイバー＝通信・指揮系統への攻撃／
    /// 情報戦＝世論・心理の揺さぶり／経済圧力＝制裁・通商破壊／代理勢力＝否認可能な第三勢力の動員。
    /// </summary>
    public enum HybridDomain
    {
        正規軍,
        サイバー,
        情報戦,
        経済圧力,
        代理勢力,
    }

    /// <summary>
    /// 複合打撃ドクトリン＝ハイブリッド戦（限定戦争・ULW-1 #1374）。純ロジック。
    /// 軍事力だけでなく、複数のドメイン（正規軍・サイバー・情報戦・経済圧力・代理勢力）を「同時に」発動して
    /// 相乗効果を生む＝単独では決定的でない各手段を組み合わせ、敵を多方面から同時に揺さぶる。各ドメインが
    /// 互いを補強し、全体が部分の和を超える（DomainSynergy が核）。守る側は全方面に資源を割けず、攻撃の帰属も曖昧になる。
    /// 分担：作戦計画の質→効果の写像は <see cref="OperationPlanRules"/>、戦略手段の上策→下策の選好（謀略＞外交＞野戦＞攻城）は
    /// <see cref="SunziDoctrineRules"/>。本ルールは「複数領域の同時発動の多ドメイン相乗」を担う。
    /// グレーゾーン（平戦の境界の曖昧化）は <c>GreyZoneRules</c>、心理戦（個別の心理操作）は <c>PsyOpRules</c>（同EPIC ULW）が担い、
    /// ここはドメイン横断の相乗効果のみ。全入力クランプ・配列null/空安全・乱数なし決定論・test-first。
    /// </summary>
    public static class HybridCampaignRules
    {
        /// <summary>
        /// ドメイン相乗（核）＝複数ドメインの同時発動が部分の和を超える効果。
        /// 各ドメインの強度配列(0..1)から、線形和（部分の和）に「同時に立っているドメインの掛け合わせ」ぶんの
        /// 相乗ボーナスを上乗せして返す。単独（1ドメインのみ）なら相乗ゼロ＝和そのまま。2つ以上が同時に立つほど相乗が増す。
        /// 手書きループ・null/空は 0。
        /// </summary>
        public static float DomainSynergy(float[] domainIntensities, HybridCampaignParams p)
        {
            if (domainIntensities == null || domainIntensities.Length == 0) return 0f;

            float sum = 0f;      // 部分の和
            float product = 1f;  // 立っているドメインの積（相乗の素）
            int active = 0;      // 実効的に立っているドメイン数
            for (int i = 0; i < domainIntensities.Length; i++)
            {
                float v = Mathf.Clamp01(domainIntensities[i]);
                sum += v;
                if (v > p.ActiveThreshold)
                {
                    product *= v;
                    active++;
                }
            }

            // 相乗は「2つ以上が同時に立つ」ときだけ生じる＝全体が部分の和を超える源泉
            float synergyBonus = 0f;
            if (active >= 2)
            {
                synergyBonus = p.SynergyGain * product * (active - 1);
            }
            return sum + synergyBonus;
        }

        public static float DomainSynergy(float[] domainIntensities)
            => DomainSynergy(domainIntensities, HybridCampaignParams.Default);

        /// <summary>
        /// 5ドメインの複合圧力（多方面同時の揺さぶり・0..1）。各ドメインを <see cref="DomainSynergy"/> へ束ね、
        /// 飽和カーブで 0..1 へ写す＝多くのドメインを同時に立てるほど相乗で圧力が高まる（が 1 を超えない）。
        /// </summary>
        public static float MultiVectorPressure(float militaryForce, float cyberAttack, float infoWar, float economicCoercion, float proxyForce, HybridCampaignParams p)
        {
            float[] domains =
            {
                Mathf.Clamp01(militaryForce),
                Mathf.Clamp01(cyberAttack),
                Mathf.Clamp01(infoWar),
                Mathf.Clamp01(economicCoercion),
                Mathf.Clamp01(proxyForce),
            };
            float raw = DomainSynergy(domains, p);
            // 飽和：raw/(raw+half) で 0..1 へ（相乗を含む合計が大きいほど 1 に漸近）
            return Mathf.Clamp01(raw / (raw + p.PressureHalf));
        }

        public static float MultiVectorPressure(float militaryForce, float cyberAttack, float infoWar, float economicCoercion, float proxyForce)
            => MultiVectorPressure(militaryForce, cyberAttack, infoWar, economicCoercion, proxyForce, HybridCampaignParams.Default);

        /// <summary>
        /// 単独ドメインの不足（各手段は単体では決定的にならない・0..1）。
        /// 単一ドメインの強度を、決定打に届かない上限 <see cref="HybridCampaignParams.SingleDomainCap"/> で頭打ちにして返す＝
        /// どれほど強くても単独では限定的（ゆえに組み合わせが要る＝ハイブリッド戦の前提）。
        /// </summary>
        public static float SingleDomainInsufficiency(float domainIntensity, HybridCampaignParams p)
        {
            return Mathf.Clamp01(domainIntensity) * p.SingleDomainCap;
        }

        public static float SingleDomainInsufficiency(float domainIntensity)
            => SingleDomainInsufficiency(domainIntensity, HybridCampaignParams.Default);

        /// <summary>
        /// 二つのドメインの相互補強（情報戦が経済圧力の効果を増す等・0..1）。
        /// 一方の和と、両者の積に補強係数を掛けたぶんを上乗せ＝両方が立っているときだけ相乗が乗る。
        /// 片方が 0 なら補強ゼロ（互いを補強するには両方が要る）。
        /// </summary>
        public static float CrossDomainReinforcement(float domainA, float domainB, HybridCampaignParams p)
        {
            float a = Mathf.Clamp01(domainA);
            float b = Mathf.Clamp01(domainB);
            float baseEffect = Mathf.Max(a, b);
            float reinforce = p.ReinforcementGain * a * b; // 両立で相乗
            return Mathf.Clamp01(baseEffect + reinforce);
        }

        public static float CrossDomainReinforcement(float domainA, float domainB)
            => CrossDomainReinforcement(domainA, domainB, HybridCampaignParams.Default);

        /// <summary>
        /// 敵の混乱（多方面同時攻撃が敵の対応を乱す・0..1）。activeDomainCount＝立っているドメインの規模(0..1)、
        /// enemyAdaptability＝敵の適応力(0..1)。多くのドメインが同時に立つほど「どこから守るか分からず」混乱が増し、
        /// 敵の適応力が高いほど混乱は抑えられる。
        /// </summary>
        public static float EnemyConfusion(float activeDomainCount, float enemyAdaptability, HybridCampaignParams p)
        {
            float vectors = Mathf.Clamp01(activeDomainCount);
            float adapt = Mathf.Clamp01(enemyAdaptability);
            return Mathf.Clamp01(vectors * p.ConfusionGain * (1f - adapt));
        }

        public static float EnemyConfusion(float activeDomainCount, float enemyAdaptability)
            => EnemyConfusion(activeDomainCount, enemyAdaptability, HybridCampaignParams.Default);

        /// <summary>
        /// 帰属の曖昧化（誰がやったか分からない・0..1）。proxyForce＝代理勢力の関与(0..1)、deniability＝否認可能性(0..1)。
        /// 代理勢力・否認可能な手段で攻撃の帰属を曖昧にする＝両者が高いほど「自国の行為」と立証しにくくなる。
        /// 平戦の境界の曖昧化（グレーゾーン）として <c>GreyZoneRules</c> と接続する入口。
        /// </summary>
        public static float AttributionAmbiguity(float proxyForce, float deniability, HybridCampaignParams p)
        {
            float proxy = Mathf.Clamp01(proxyForce);
            float deny = Mathf.Clamp01(deniability);
            return Mathf.Clamp01(proxy * deny * p.AmbiguityGain);
        }

        public static float AttributionAmbiguity(float proxyForce, float deniability)
            => AttributionAmbiguity(proxyForce, deniability, HybridCampaignParams.Default);

        /// <summary>
        /// 守備側の資源ジレンマ（全部は守れない・0..1）。multiVectorPressure＝攻撃側の複合圧力(0..1)、
        /// defenderResources＝守備側が割ける資源(0..1)。多方面の圧力に対して資源が乏しいほどジレンマが深い＝
        /// どこかを手薄にせざるを得ない。圧力が資源を上回るぶんが守りきれない隙となる。
        /// </summary>
        public static float DefenderResourceDilemma(float multiVectorPressure, float defenderResources, HybridCampaignParams p)
        {
            float pressure = Mathf.Clamp01(multiVectorPressure);
            float resources = Mathf.Clamp01(defenderResources);
            // 資源で割り引いた残り＝守りきれない隙
            float gap = pressure - resources * p.DefenseCoverage;
            return Mathf.Clamp01(Mathf.Max(0f, gap));
        }

        public static float DefenderResourceDilemma(float multiVectorPressure, float defenderResources)
            => DefenderResourceDilemma(multiVectorPressure, defenderResources, HybridCampaignParams.Default);

        /// <summary>
        /// ハイブリッド攻勢の判定＝複数ドメインが連動した相乗が閾値を超え、かつ立っているドメインが複数か。
        /// domainSynergy＝<see cref="DomainSynergy"/> の出力、activeDomainCount＝立っているドメイン規模(0..1)。
        /// 単独ドメインの強打（多くても1領域）はハイブリッド攻勢とは見なさない＝多ドメイン同時発動が要件。
        /// </summary>
        public static bool IsHybridOffensive(float domainSynergy, float activeDomainCount, float threshold)
        {
            float vectors = Mathf.Clamp01(activeDomainCount);
            return domainSynergy >= threshold && vectors > 0.5f;
        }
    }

    /// <summary>
    /// HybridCampaignRules の調整値（#1374・マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// </summary>
    public readonly struct HybridCampaignParams
    {
        /// <summary>ドメインが「立っている」とみなす強度の下限（これ以下は相乗に寄与しない）。</summary>
        public readonly float ActiveThreshold;
        /// <summary>ドメイン相乗の係数（同時発動が部分の和を超える上乗せ幅）。</summary>
        public readonly float SynergyGain;
        /// <summary>複合圧力の飽和半値（相乗込み合計がこの値で圧力0.5）。</summary>
        public readonly float PressureHalf;
        /// <summary>単独ドメインの決定打上限（単体では限定的＝頭打ち）。</summary>
        public readonly float SingleDomainCap;
        /// <summary>二領域の相互補強の係数（両立で乗る相乗幅）。</summary>
        public readonly float ReinforcementGain;
        /// <summary>敵の混乱の係数（多方面同時攻撃が乱す度合い）。</summary>
        public readonly float ConfusionGain;
        /// <summary>帰属曖昧化の係数（代理・否認で帰属を曖昧にする度合い）。</summary>
        public readonly float AmbiguityGain;
        /// <summary>守備資源のカバー係数（資源1あたり守れる圧力ぶん）。</summary>
        public readonly float DefenseCoverage;

        public HybridCampaignParams(
            float activeThreshold, float synergyGain, float pressureHalf,
            float singleDomainCap, float reinforcementGain, float confusionGain,
            float ambiguityGain, float defenseCoverage)
        {
            ActiveThreshold = activeThreshold;
            SynergyGain = synergyGain;
            PressureHalf = pressureHalf;
            SingleDomainCap = singleDomainCap;
            ReinforcementGain = reinforcementGain;
            ConfusionGain = confusionGain;
            AmbiguityGain = ambiguityGain;
            DefenseCoverage = defenseCoverage;
        }

        /// <summary>
        /// 既定。立つ閾値0.2・相乗係数0.5・圧力半値2.0・単独上限0.6・補強係数0.5・混乱係数0.8・曖昧化係数1.0・守備カバー1.0。
        /// </summary>
        public static HybridCampaignParams Default => new HybridCampaignParams(
            activeThreshold: 0.2f, synergyGain: 0.5f, pressureHalf: 2.0f,
            singleDomainCap: 0.6f, reinforcementGain: 0.5f, confusionGain: 0.8f,
            ambiguityGain: 1.0f, defenseCoverage: 1.0f);
    }
}
