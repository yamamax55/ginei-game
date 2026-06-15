using UnityEngine;

namespace Ginei
{
    /// <summary>醜聞の調整係数（要人スキャンダルの力学）。</summary>
    public readonly struct ScandalParams
    {
        /// <summary>露見の基礎係数（不行跡最大・嗅ぎ回り最大のときの露見率）。</summary>
        public readonly float baseExposure;
        /// <summary>失脚ダメージの基礎幅（偽善ゼロ・不行跡最大のときのダメージ）。</summary>
        public readonly float damageScale;
        /// <summary>偽善プレミアム＝清廉を売る者の汚職がダメージに上乗せされる倍率幅（1で倍打撃）。</summary>
        public readonly float hypocrisyPremium;
        /// <summary>もみ消しの基礎成功率（資金力最大・微罪のときの上限）。</summary>
        public readonly float coverupBase;
        /// <summary>もみ消し失敗の倍返し係数（罪＋隠蔽罪。1以上＝必ず元の醜聞より重い）。</summary>
        public readonly float backfireMultiplier;
        /// <summary>醜聞慣れの減衰率（直近1件ごとに世論の反応がこの割合ぶん鈍る）。</summary>
        public readonly float fatigueRate;
        /// <summary>政治兵器化の上乗せ幅（選挙前夜に撃つと兵器価値がこの倍率ぶん増す）。</summary>
        public readonly float timingScale;

        public ScandalParams(float baseExposure, float damageScale, float hypocrisyPremium,
                             float coverupBase, float backfireMultiplier,
                             float fatigueRate, float timingScale)
        {
            this.baseExposure = Mathf.Clamp01(baseExposure);
            this.damageScale = Mathf.Clamp01(damageScale);
            this.hypocrisyPremium = Mathf.Max(0f, hypocrisyPremium);
            this.coverupBase = Mathf.Clamp01(coverupBase);
            this.backfireMultiplier = Mathf.Max(1f, backfireMultiplier);
            this.fatigueRate = Mathf.Max(0f, fatigueRate);
            this.timingScale = Mathf.Max(0f, timingScale);
        }

        /// <summary>既定＝露見0.8・ダメージ幅0.5・偽善プレミアム1.0(倍打撃)・もみ消し0.9・倍返し2.0・醜聞慣れ0.5・選挙前倍率1.0。</summary>
        public static ScandalParams Default => new ScandalParams(0.8f, 0.5f, 1f, 0.9f, 2f, 0.5f, 1f);
    }

    /// <summary>
    /// 醜聞の純ロジック。汚職・私行の露見が要人を失脚させる個人の失脚力学＝
    /// 「醜聞の致死性は罪の重さでなく、普段の建前との落差（偽善プレミアム）と隠蔽（もみ消し失敗の倍返し）で決まる」。
    /// もみ消しは成功すれば無傷・失敗すれば罪＋隠蔽罪で元の醜聞より重く返る。続発すれば世論が麻痺して効かなくなり
    /// （醜聞慣れ）、同じ醜聞でも選挙前に撃つのが最も効く（政治兵器化）。
    /// <see cref="SecurityRules"/>（体制側の監視・言論抑圧＝組織の統制）とは別レイヤー＝こちらは個人の失脚。
    /// 報道自由度の出所はバックログの FreePressRules 側が出す想定（ここは 0..1 で受けるだけ）。
    /// 乱数は外から roll∈[0,1) を渡す決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ScandalRules
    {
        /// <summary>
        /// 露見確率（0..1）＝基礎係数×不行跡(0..1)×嗅ぎ回り度。嗅ぎ回り度は自由な報道(0..1)と
        /// 政敵の多さ(0..1)の独立な合成＝1−(1−press)(1−enemies)＝どちらか一方でも嗅ぎつける。
        /// 報道も政敵もなければ露見しない（もみ消す必要すらない）。
        /// </summary>
        public static float ExposureChance(float misconductScale, float pressFreedom, float enemies, ScandalParams p)
        {
            float sniff = 1f - (1f - Mathf.Clamp01(pressFreedom)) * (1f - Mathf.Clamp01(enemies));
            return Mathf.Clamp01(p.baseExposure * Mathf.Clamp01(misconductScale) * sniff);
        }

        public static float ExposureChance(float misconductScale, float pressFreedom, float enemies)
            => ExposureChance(misconductScale, pressFreedom, enemies, ScandalParams.Default);

        /// <summary>露見判定（決定論）＝roll∈[0,1) が露見確率未満なら露見。</summary>
        public static bool Exposed(float chance, float roll)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 失脚ダメージ（0..1）＝不行跡(0..1)×基礎幅×（1＋偽善プレミアム×落差(0..1)）。
        /// 清廉を売る者の汚職は倍打撃＝致死性は罪の重さでなく普段の建前との落差で決まる
        /// （軽い罪でも偽善者は、重い罪の野人より深手を負う）。
        /// </summary>
        public static float ReputationDamage(float misconductScale, float hypocrisy, ScandalParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(misconductScale) * p.damageScale
                                 * (1f + p.hypocrisyPremium * Mathf.Clamp01(hypocrisy)));
        }

        public static float ReputationDamage(float misconductScale, float hypocrisy)
            => ReputationDamage(misconductScale, hypocrisy, ScandalParams.Default);

        /// <summary>
        /// もみ消しの成功率（0..1）＝基礎成功率×資金力(0..1)×（1−不行跡(0..1)）。
        /// 微罪は金で消えるが、大罪は隠しきれない。成功すれば無傷、失敗の代償は
        /// <see cref="CoverupBackfire(float, ScandalParams)"/>。判定は <see cref="Exposed"/> を流用する。
        /// </summary>
        public static float CoverupGamble(float misconductScale, float resources, ScandalParams p)
        {
            return Mathf.Clamp01(p.coverupBase * Mathf.Clamp01(resources) * (1f - Mathf.Clamp01(misconductScale)));
        }

        public static float CoverupGamble(float misconductScale, float resources)
            => CoverupGamble(misconductScale, resources, ScandalParams.Default);

        /// <summary>
        /// もみ消し失敗の倍返しダメージ（0..1）＝素の失脚ダメージ（偽善ゼロ）×倍返し係数。
        /// 罪＋隠蔽罪＝必ず元の醜聞より重い（backfireMultiplier は1以上にクランプ済み）。
        /// </summary>
        public static float CoverupBackfire(float misconductScale, ScandalParams p)
        {
            return Mathf.Clamp01(ReputationDamage(misconductScale, 0f, p) * p.backfireMultiplier);
        }

        public static float CoverupBackfire(float misconductScale)
            => CoverupBackfire(misconductScale, ScandalParams.Default);

        /// <summary>
        /// 醜聞慣れ（0..1のダメージ倍率）＝1/（1＋減衰率×直近の醜聞件数）。
        /// 続発すると世論が麻痺し、N件目は効かない（0件で1.0＝満額、負数は0件扱い）。
        /// </summary>
        public static float ScandalFatigue(int recentScandals, ScandalParams p)
        {
            int n = Mathf.Max(0, recentScandals);
            return 1f / (1f + p.fatigueRate * n);
        }

        public static float ScandalFatigue(int recentScandals)
            => ScandalFatigue(recentScandals, ScandalParams.Default);

        /// <summary>
        /// 醜聞の政治兵器価値＝ダメージ(0..1)×（1＋選挙近接度(0..1)×上乗せ幅）。
        /// 同じ醜聞でも選挙前夜に撃つのが最も効く（既定で前夜は平時の2倍）。
        /// ダメージそのものではなく「いつ撃つか」の価値指標なので上限クランプしない。
        /// </summary>
        public static float TimingWeaponValue(float damage, float electionProximity, ScandalParams p)
        {
            return Mathf.Clamp01(damage) * (1f + p.timingScale * Mathf.Clamp01(electionProximity));
        }

        public static float TimingWeaponValue(float damage, float electionProximity)
            => TimingWeaponValue(damage, electionProximity, ScandalParams.Default);
    }
}
