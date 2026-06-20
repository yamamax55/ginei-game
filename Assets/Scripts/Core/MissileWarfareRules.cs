using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ミサイル戦（誘導兵器の飽和攻撃と迎撃）の調整値。斉射・迎撃・誘導・弾頭・弾薬の各係数。
    /// 全フィールドは ctor で Clamp 済み。<see cref="Default"/> が既定。実効値パターン（基準値非破壊）。
    /// </summary>
    public readonly struct MissileWarfareParams
    {
        /// <summary>1迎撃チャンネルあたりの迎撃可能弾数（迎撃能力の単位換算）。</summary>
        public readonly float interceptPerChannel;
        /// <summary>飽和度の上限（弾数が迎撃能力を大きく上回ってもこれ以上は無限に伸ばさない）。</summary>
        public readonly float maxSaturation;
        /// <summary>飽和していない（飽和度≤1）ときの基本迎撃成功率の上限（0..1）。</summary>
        public readonly float baseInterceptRate;
        /// <summary>飽和したときの迎撃率の減衰の効き（飽和度で割って迎撃率を落とす）。</summary>
        public readonly float saturationDecay;
        /// <summary>弾頭威力の効きの倍率（命中効果の伸び）。</summary>
        public readonly float yieldScale;
        /// <summary>装甲の効きの倍率（被命中効果の減衰）。</summary>
        public readonly float armorScale;

        public MissileWarfareParams(float interceptPerChannel, float maxSaturation, float baseInterceptRate,
            float saturationDecay, float yieldScale, float armorScale)
        {
            this.interceptPerChannel = Mathf.Max(0f, interceptPerChannel);
            this.maxSaturation = Mathf.Max(1f, maxSaturation);
            this.baseInterceptRate = Mathf.Clamp01(baseInterceptRate);
            this.saturationDecay = Mathf.Max(0f, saturationDecay);
            this.yieldScale = Mathf.Max(0f, yieldScale);
            this.armorScale = Mathf.Max(0f, armorScale);
        }

        /// <summary>既定：1チャンネル4弾・最大飽和5・基本迎撃0.9・減衰1.0・威力1.0・装甲1.0。</summary>
        public static MissileWarfareParams Default => new MissileWarfareParams(
            DefaultInterceptPerChannel, DefaultMaxSaturation, DefaultBaseInterceptRate,
            DefaultSaturationDecay, DefaultYieldScale, DefaultArmorScale);

        public const float DefaultInterceptPerChannel = 4f;
        public const float DefaultMaxSaturation = 5f;
        public const float DefaultBaseInterceptRate = 0.9f;
        public const float DefaultSaturationDecay = 1.0f;
        public const float DefaultYieldScale = 1.0f;
        public const float DefaultArmorScale = 1.0f;
    }

    /// <summary>
    /// ミサイル・誘導兵器戦の純ロジック。斉射の飽和攻撃 → 目標の対空迎撃（CIWS）→ 飽和による迎撃の飽和突破 →
    /// 誘導の振り切り（ECM/デコイ）→ 迎撃と妨害を抜けた有効弾（leakers）の到達 → 命中効果 → 弾薬の消耗、を一連で扱う。
    /// 「火力の集中（一斉射の弾数）が迎撃能力を上回ると迎撃が崩れる」飽和攻撃の体感を係数で表す。
    /// 決定論・入力 Clamp・実効値パターン。test-first。
    /// </summary>
    public static class MissileWarfareRules
    {
        // ---- SalvoSize：発射機数×斉射レートで一斉射の弾数 ----
        public static float SalvoSize(float launcherCount, float salvoRate)
            => SalvoSize(launcherCount, salvoRate, MissileWarfareParams.Default);

        public static float SalvoSize(float launcherCount, float salvoRate, MissileWarfareParams p)
        {
            float launchers = Mathf.Max(0f, launcherCount);
            float rate = Mathf.Max(0f, salvoRate);
            return launchers * rate;
        }

        // ---- InterceptCapacity：対空砲×追尾チャンネルで迎撃能力 ----
        public static float InterceptCapacity(float pointDefenseGuns, float trackingChannels)
            => InterceptCapacity(pointDefenseGuns, trackingChannels, MissileWarfareParams.Default);

        public static float InterceptCapacity(float pointDefenseGuns, float trackingChannels, MissileWarfareParams p)
        {
            float guns = Mathf.Max(0f, pointDefenseGuns);
            float channels = Mathf.Max(0f, trackingChannels);
            // 砲の数と同時追尾できるチャンネル数の小さい方が律速＝そのぶんの弾を迎撃できる。
            float effectiveTracks = Mathf.Min(guns, channels * p.interceptPerChannel);
            return effectiveTracks;
        }

        // ---- SaturationFactor：弾数/迎撃能力で飽和度（迎撃を上回る飽和攻撃） ----
        public static float SaturationFactor(float salvoSize, float interceptCapacity)
            => SaturationFactor(salvoSize, interceptCapacity, MissileWarfareParams.Default);

        public static float SaturationFactor(float salvoSize, float interceptCapacity, MissileWarfareParams p)
        {
            float salvo = Mathf.Max(0f, salvoSize);
            float capacity = Mathf.Max(0f, interceptCapacity);
            if (capacity <= 0f) return p.maxSaturation; // 迎撃能力ゼロ＝完全飽和
            float ratio = salvo / capacity;
            return Mathf.Clamp(ratio, 0f, p.maxSaturation);
        }

        // ---- InterceptionRate：迎撃で落とせる割合（飽和すると落ちる）（0..1） ----
        public static float InterceptionRate(float interceptCapacity, float salvoSize, float saturationFactor)
            => InterceptionRate(interceptCapacity, salvoSize, saturationFactor, MissileWarfareParams.Default);

        public static float InterceptionRate(float interceptCapacity, float salvoSize, float saturationFactor, MissileWarfareParams p)
        {
            float salvo = Mathf.Max(0f, salvoSize);
            float capacity = Mathf.Max(0f, interceptCapacity);
            float saturation = Mathf.Max(0f, saturationFactor);

            if (salvo <= 0f) return 0f; // 撃って来なければ迎撃ゼロ

            // 弾数のうち迎撃能力で物理的に落とせる上限割合（弾数を超える迎撃はできない）。
            float coverage = Mathf.Clamp01(capacity / salvo);
            float rate = p.baseInterceptRate * coverage;

            // 飽和（飽和度>1）すると迎撃が破綻し成功率が落ちる。
            if (saturation > 1f)
            {
                float decayed = 1f + p.saturationDecay * (saturation - 1f);
                rate /= decayed;
            }
            return Mathf.Clamp01(rate);
        }

        // ---- GuidanceJamming：敵ECM/(ECM+シーカー精度)で誘導の振り切り（0..1） ----
        public static float GuidanceJamming(float targetEcm, float missileSeekerQuality)
            => GuidanceJamming(targetEcm, missileSeekerQuality, MissileWarfareParams.Default);

        public static float GuidanceJamming(float targetEcm, float missileSeekerQuality, MissileWarfareParams p)
        {
            float ecm = Mathf.Max(0f, targetEcm);
            float seeker = Mathf.Max(0f, missileSeekerQuality);
            float denom = ecm + seeker;
            if (denom <= 0f) return 0f; // 妨害もシーカーも無し＝振り切られない
            return Mathf.Clamp01(ecm / denom);
        }

        // ---- Leakers：迎撃と妨害を抜けて到達する有効弾数 ----
        public static float Leakers(float salvoSize, float interceptionRate, float guidanceJamming)
            => Leakers(salvoSize, interceptionRate, guidanceJamming, MissileWarfareParams.Default);

        public static float Leakers(float salvoSize, float interceptionRate, float guidanceJamming, MissileWarfareParams p)
        {
            float salvo = Mathf.Max(0f, salvoSize);
            float intercept = Mathf.Clamp01(interceptionRate);
            float jam = Mathf.Clamp01(guidanceJamming);
            // 迎撃を抜けた弾のうち、さらに誘導妨害（ECM/デコイ）に振り切られたぶんを引く。
            float survivors = salvo * (1f - intercept) * (1f - jam);
            return Mathf.Max(0f, survivors);
        }

        // ---- MissileEffectiveness：到達弾×弾頭威力/(1+装甲)で命中効果（0..1） ----
        public static float MissileEffectiveness(float leakers, float warheadYield, float targetArmor)
            => MissileEffectiveness(leakers, warheadYield, targetArmor, MissileWarfareParams.Default);

        public static float MissileEffectiveness(float leakers, float warheadYield, float targetArmor, MissileWarfareParams p)
        {
            float hits = Mathf.Max(0f, leakers);
            float yield = Mathf.Max(0f, warheadYield) * p.yieldScale;
            float armor = Mathf.Max(0f, targetArmor) * p.armorScale;
            float raw = hits * yield / (1f + armor);
            return Mathf.Clamp01(raw);
        }

        // ---- AmmoDepletion：斉射で弾薬庫が減る割合（飽和攻撃は弾切れが早い）（0..1） ----
        public static float AmmoDepletion(float salvoSize, float magazineSize)
            => AmmoDepletion(salvoSize, magazineSize, MissileWarfareParams.Default);

        public static float AmmoDepletion(float salvoSize, float magazineSize, MissileWarfareParams p)
        {
            float salvo = Mathf.Max(0f, salvoSize);
            float magazine = Mathf.Max(0f, magazineSize);
            if (magazine <= 0f) return 1f; // 弾薬庫が無い＝撃てば即枯渇
            return Mathf.Clamp01(salvo / magazine);
        }
    }
}
