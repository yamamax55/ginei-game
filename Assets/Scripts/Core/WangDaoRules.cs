using UnityEngine;

namespace Ginei
{
    /// <summary>王道/覇道の調整係数（孟子の王道vs覇道・#1059）。</summary>
    public readonly struct WangDaoParams
    {
        /// <summary>1秒・行動最大時に道がドリフトする速さ（仁政→王道+／武断→覇道−）。</summary>
        public readonly float driftRate;
        /// <summary>王道（心服）の服従コスト基準（低い＝支配が安い）。</summary>
        public readonly float kinglyCostBase;
        /// <summary>覇道（力服）の服従コスト基準（武力で押さえつける維持費）。</summary>
        public readonly float hegemonCostBase;
        /// <summary>覇道支配が時間で恨みを積もらせる速さ（占領継続あたりの反乱圧）。</summary>
        public readonly float resentmentRate;
        /// <summary>王道の徳が諸侯の自発的帰順を引き寄せる係数。</summary>
        public readonly float attractionScale;

        public WangDaoParams(float driftRate, float kinglyCostBase, float hegemonCostBase, float resentmentRate, float attractionScale)
        {
            this.driftRate = Mathf.Max(0f, driftRate);
            this.kinglyCostBase = Mathf.Max(0f, kinglyCostBase);
            this.hegemonCostBase = Mathf.Max(0f, hegemonCostBase);
            this.resentmentRate = Mathf.Max(0f, resentmentRate);
            this.attractionScale = Mathf.Max(0f, attractionScale);
        }

        /// <summary>既定＝ドリフト0.1/秒・王道コスト0.2・覇道コスト0.8・恨み0.1/秒・帰順係数1.0。</summary>
        public static WangDaoParams Default => new WangDaoParams(0.1f, 0.2f, 0.8f, 0.1f, 1f);
    }

    /// <summary>
    /// 王道/覇道＝統治スタイルの評判メタ層（孟子・#1059）。勢力の統治の「道」を −1..1 の1軸で表す
    /// （王道+1〜覇道−1）。仁政（benevolentActs）は王道へ、武断（coerciveActs）は覇道へ徐々に寄り、
    /// <b>行動の積分が評判</b>になる（DriftTick）。孟子いわく「力を以て人を服する者は心服に非ず・
    /// 力足らざるなり。徳を以て人を服する者は中心悦びて誠に服す」＝<b>覇道は力ずくで従わせる＝
    /// 高コストで離反含み（力服）／王道は心から慕われる＝低コストで安定（心服）</b>。武力は即効だが
    /// 持続せず（CoerciveEfficiency）、覇道支配は時間で恨みが積もり（RebellionPressureFromDao）、
    /// 王道は戦わずして諸侯を集める（AllyAttraction）。徳は天命に適い正統性を生む（LegitimacyFromVirtue）
    /// ＝<b>DynastyRules</b>（天命と易姓革命）へ接続する係数。<b>ReputationRules</b>（個人提督の名声）とは
    /// 別系統＝こちらは<b>勢力の統治の道</b>を解く。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WangDaoRules
    {
        /// <summary>
        /// 行いによる道のドリフト（−1..1を返す）。仁政(0..1)は王道(+)へ・武断(0..1)は覇道(−)へ寄せる。
        /// 変化量＝(仁政−武断)×ドリフト速度×dt＝行動の積分が評判になる（一度の善悪でなく履歴で決まる）。
        /// </summary>
        public static float DriftTick(float daoValue, float benevolentActs, float coerciveActs, float dt, WangDaoParams p)
        {
            float net = Mathf.Clamp01(benevolentActs) - Mathf.Clamp01(coerciveActs);
            float delta = net * p.driftRate * Mathf.Max(0f, dt);
            return Mathf.Clamp(Mathf.Clamp(daoValue, -1f, 1f) + delta, -1f, 1f);
        }

        public static float DriftTick(float daoValue, float benevolentActs, float coerciveActs, float dt)
            => DriftTick(daoValue, benevolentActs, coerciveActs, dt, WangDaoParams.Default);

        /// <summary>
        /// 服従の質＝維持コスト（0..1、低いほど安定した支配）。道を 0..1 の王道度へ写し、
        /// 王道コスト基準と覇道コスト基準を線形補間する＝<b>王道（心服）は低コストで安定し、
        /// 覇道（力服）は高コストで武力に比例して跳ね上がる</b>（力で押さえるほど維持費がかさむ）。
        /// militaryPower は覇道側のコストにのみ重く効く（徳のある支配は兵を要しない）。
        /// </summary>
        public static float SubmissionQuality(float daoValue, float militaryPower, WangDaoParams p)
        {
            float kingly = Mathf.Clamp01((Mathf.Clamp(daoValue, -1f, 1f) + 1f) * 0.5f); // -1..1 → 0..1（王道度）
            float hegemonShare = 1f - kingly;
            float baseCost = Mathf.Lerp(p.hegemonCostBase, p.kinglyCostBase, kingly);
            // 覇道分は兵力で維持費が増す（力ずくの統治は高くつく）。
            float forceCost = hegemonShare * Mathf.Clamp01(militaryPower) * p.hegemonCostBase;
            return Mathf.Clamp01(baseCost + forceCost * (1f - kingly));
        }

        public static float SubmissionQuality(float daoValue, float militaryPower)
            => SubmissionQuality(daoValue, militaryPower, WangDaoParams.Default);

        /// <summary>
        /// 道による反乱圧（0..1）。覇道（daoValue&lt;0）支配は占領継続(0..1)で恨みが積もり、
        /// 王道（daoValue&gt;0）は時間とともに民が懐いて圧が下がる＝<b>覇道は時の経過が敵・王道は味方</b>。
        /// 圧＝覇道度×占領継続×恨み速度（王道度のぶんは差し引く）。
        /// </summary>
        public static float RebellionPressureFromDao(float daoValue, float occupationDuration, WangDaoParams p)
        {
            float hegemon = Mathf.Clamp01(-Mathf.Clamp(daoValue, -1f, 1f)); // 覇道度（0..1）
            float kingly = Mathf.Clamp01(Mathf.Clamp(daoValue, -1f, 1f));   // 王道度（0..1）
            float pressure = hegemon * Mathf.Clamp01(occupationDuration) * p.resentmentRate;
            // 王道はむしろ反乱圧を時間で和らげる（懐く）。
            float pacify = kingly * Mathf.Clamp01(occupationDuration) * p.resentmentRate;
            return Mathf.Clamp01(pressure - pacify);
        }

        public static float RebellionPressureFromDao(float daoValue, float occupationDuration)
            => RebellionPressureFromDao(daoValue, occupationDuration, WangDaoParams.Default);

        /// <summary>
        /// 諸侯の自発的帰順（0..1）。王道（daoValue&gt;0）の徳のみが人を集める＝
        /// <b>戦わずして人を集める＝徳のある者に天下が集まる</b>。覇道は帰順を生まない（0）。
        /// 帰順＝王道度×帰順係数。
        /// </summary>
        public static float AllyAttraction(float daoValue, WangDaoParams p)
        {
            float kingly = Mathf.Clamp01(Mathf.Clamp(daoValue, -1f, 1f));
            return Mathf.Clamp01(kingly * p.attractionScale);
        }

        public static float AllyAttraction(float daoValue)
            => AllyAttraction(daoValue, WangDaoParams.Default);

        /// <summary>
        /// 覇道の短期効率（0..1）。覇道（daoValue&lt;0）は武力に比例して即座に従わせる＝
        /// <b>武力は即効だが持続しない</b>（短期の制圧力。長期は SubmissionQuality/RebellionPressure が蝕む）。
        /// 効率＝覇道度×兵力。王道は力で押さえる効率を持たない（0）。
        /// </summary>
        public static float CoerciveEfficiency(float daoValue, float militaryPower, WangDaoParams p)
        {
            float hegemon = Mathf.Clamp01(-Mathf.Clamp(daoValue, -1f, 1f));
            return Mathf.Clamp01(hegemon * Mathf.Clamp01(militaryPower));
        }

        public static float CoerciveEfficiency(float daoValue, float militaryPower)
            => CoerciveEfficiency(daoValue, militaryPower, WangDaoParams.Default);

        /// <summary>
        /// 徳による正統性（0..1）。王道（daoValue&gt;0）の徳は天命に適い正統性を生む＝
        /// <b>DynastyRules</b>（天命・易姓革命）の virtue/legitimacy へ流し込む係数。覇道は正統性を生まない（0）。
        /// 正統性＝王道度（−1..1の王道側のみ）。
        /// </summary>
        public static float LegitimacyFromVirtue(float daoValue)
        {
            return Mathf.Clamp01(Mathf.Clamp(daoValue, -1f, 1f));
        }
    }
}
