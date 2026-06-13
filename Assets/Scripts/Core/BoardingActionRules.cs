using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 接舷白兵戦の調整パラメータ（接舷→艦内白兵→拿捕のプロセス専用）。
    /// すべて ctor で Clamp。基準値は非破壊（実効値パターン）。
    /// </summary>
    public readonly struct BoardingActionParams
    {
        /// <summary>接舷成功の鋭さ（速度差の効きの強さ）。大きいほど速度差が成否を分ける。</summary>
        public readonly float grappleSharpness;
        /// <summary>陸戦隊の練度の効き（質が戦力に乗る割合）。</summary>
        public readonly float qualityWeight;
        /// <summary>艦内白兵の優劣カーブの指数（兵力比の効き）。</summary>
        public readonly float combatExponent;
        /// <summary>制圧の進行速度（1秒あたりの掌握量の基準）。</summary>
        public readonly float captureRate;
        /// <summary>撃退側の踏ん張りの効き（守備の士気の重み）。</summary>
        public readonly float repelWeight;

        public BoardingActionParams(float grappleSharpness, float qualityWeight, float combatExponent, float captureRate, float repelWeight)
        {
            this.grappleSharpness = Mathf.Clamp(grappleSharpness, 0.1f, 10f);
            this.qualityWeight = Mathf.Clamp01(qualityWeight);
            this.combatExponent = Mathf.Clamp(combatExponent, 0.1f, 4f);
            this.captureRate = Mathf.Clamp(captureRate, 0.001f, 10f);
            this.repelWeight = Mathf.Clamp01(repelWeight);
        }

        public static BoardingActionParams Default => new BoardingActionParams(2f, 0.6f, 1f, 0.2f, 0.5f);
    }

    /// <summary>
    /// 接舷白兵戦＝敵艦に乗り込んで制圧・拿捕する純ロジック（ローゼンリッター型・盤面非依存）。
    /// 責務分担：兵力対決・損害・決着判定は既存 <see cref="BoardingRules"/> が担う。
    /// 本ルールは「接舷（組み付き）→艦内白兵→制圧進行→拿捕か撃沈かの選択」という戦闘プロセスに特化し、
    /// 数式を重複させない。特殊部隊の運用（<c>SpecialForcesRules</c>）とは別＝こちらは艦対艦の白兵。
    /// 撃沈と違い、制圧すれば艦を無傷で奪える（拿捕）。符号付き出力は -1..1。
    /// </summary>
    public static class BoardingActionRules
    {
        /// <summary>
        /// 接舷（組み付き）成功度 0..1。接舷側の接近速度が高く、対象の回避が低いほど成功しやすい。
        /// 速度差を sharpness でならし、0.5 を拮抗とする飽和カーブ。
        /// </summary>
        public static float GrappleSuccess(float approachSpeed, float targetEvasion, BoardingActionParams p)
        {
            float speed = Mathf.Max(0f, approachSpeed);
            float evade = Mathf.Max(0f, targetEvasion);
            float diff = (speed - evade) * p.grappleSharpness;
            // ロジスティック近似（Exp 不可）＝ x/(1+|x|) を 0..1 へ写す。
            float s = diff / (1f + Mathf.Abs(diff)); // -1..1
            return Mathf.Clamp01(0.5f + 0.5f * s);
        }

        public static float GrappleSuccess(float approachSpeed, float targetEvasion)
            => GrappleSuccess(approachSpeed, targetEvasion, BoardingActionParams.Default);

        /// <summary>
        /// 乗り込む陸戦隊の戦力＝頭数×（基礎＋練度の重み×質）。質は 0..1 で受ける。
        /// </summary>
        public static float BoardingForce(float marines, float marineQuality, BoardingActionParams p)
        {
            float n = Mathf.Max(0f, marines);
            float q = Mathf.Clamp01(marineQuality);
            return n * ((1f - p.qualityWeight) + p.qualityWeight * (1f + q));
        }

        public static float BoardingForce(float marines, float marineQuality)
            => BoardingForce(marines, marineQuality, BoardingActionParams.Default);

        /// <summary>
        /// 艦内白兵戦の優劣 -1..1。乗り込み戦力 vs 艦内防御兵。+1 で乗り込み側が圧倒、-1 で守備が圧倒。
        /// 兵力比を exponent で効かせ、(a-d)/(a+d) で正規化。
        /// </summary>
        public static float ShipboardCombat(float boardingForce, float defenderGarrison, BoardingActionParams p)
        {
            float a = Mathf.Pow(Mathf.Max(0f, boardingForce), p.combatExponent);
            float d = Mathf.Pow(Mathf.Max(0f, defenderGarrison), p.combatExponent);
            float sum = a + d;
            if (sum <= 0f) return 0f;
            return Mathf.Clamp((a - d) / sum, -1f, 1f);
        }

        public static float ShipboardCombat(float boardingForce, float defenderGarrison)
            => ShipboardCombat(boardingForce, defenderGarrison, BoardingActionParams.Default);

        /// <summary>
        /// 制圧の進行量（この dt ぶん艦内を掌握する増分・非負）。白兵優劣が正のときだけ前進する。
        /// 守備優勢（負）なら 0＝掌握は進まない（撃退は RepelBoarders 側で評価）。
        /// </summary>
        public static float CaptureProgress(float shipboardCombat, float dt, BoardingActionParams p)
        {
            float adv = Mathf.Clamp(shipboardCombat, -1f, 1f);
            if (adv <= 0f) return 0f;
            float t = Mathf.Max(0f, dt);
            return adv * p.captureRate * t;
        }

        public static float CaptureProgress(float shipboardCombat, float dt)
            => CaptureProgress(shipboardCombat, dt, BoardingActionParams.Default);

        /// <summary>
        /// 拿捕（無傷で奪う）か撃沈かの価値比較 -1..1。+1 で拿捕優位、-1 で撃沈優位、0 で互角。
        /// </summary>
        public static float CaptureVsDestroy(float captureValue, float destroyValue)
        {
            float c = Mathf.Max(0f, captureValue);
            float x = Mathf.Max(0f, destroyValue);
            float sum = c + x;
            if (sum <= 0f) return 0f;
            return Mathf.Clamp((c - x) / sum, -1f, 1f);
        }

        /// <summary>
        /// 乗り込まれた側が撃退する度合い 0..1。守備兵の数と覚悟（士気）が高いほど押し返す。
        /// </summary>
        public static float RepelBoarders(float defenderGarrison, float defenderResolve, BoardingActionParams p)
        {
            float g = Mathf.Max(0f, defenderGarrison);
            float resolve = Mathf.Clamp01(defenderResolve);
            // 守備力を覚悟で底上げした飽和カーブ。garrison 0 なら撃退不能。
            float effective = g * ((1f - p.repelWeight) + p.repelWeight * (1f + resolve));
            return Mathf.Clamp01(effective / (1f + effective));
        }

        public static float RepelBoarders(float defenderGarrison, float defenderResolve)
            => RepelBoarders(defenderGarrison, defenderResolve, BoardingActionParams.Default);

        /// <summary>
        /// 拿捕した艦の戦利品価値＝健全度（無傷なほど高い）×艦種価値。
        /// capturedShipIntegrity は 0..1、shipClass は艦種の基準価値（大型艦ほど高い）。
        /// </summary>
        public static float PrizeValue(float capturedShipIntegrity, float shipClass)
        {
            float integrity = Mathf.Clamp01(capturedShipIntegrity);
            float worth = Mathf.Max(0f, shipClass);
            return integrity * worth;
        }

        /// <summary>制圧進行が閾値に達したら艦を拿捕（true）。</summary>
        public static bool IsShipCaptured(float captureProgress, float threshold)
        {
            return Mathf.Max(0f, captureProgress) >= Mathf.Max(0f, threshold);
        }
    }
}
