using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 指揮風土（部隊の組織文化＝指揮官と部下の信頼・結束・率先垂範の土壌）のパラメータ。
    /// readonly struct・全フィールド ctor でクランプ・盤面非依存の plain データ。
    /// 能力値・苦楽の度合いは 0..100 で受け、内部で 0..1 に正規化する。
    /// </summary>
    public readonly struct CommandClimateParams
    {
        /// <summary>信頼に占める指揮官の誠実さの重み（0..1）。</summary>
        public readonly float integrityWeight;
        /// <summary>信頼に占める苦楽を共にする度合いの重み（0..1）。integrityWeight と合わせて 1 を想定。</summary>
        public readonly float hardshipWeight;
        /// <summary>結束が満ちるまでに要する「共に過ごした時間」（月相当・これで割って 0..1 化）。</summary>
        public readonly float timeToBond;
        /// <summary>自発性の下限係数（信頼があっても失敗への寛容が無いと出せる最低限・0..1）。</summary>
        public readonly float initiativeFloor;
        /// <summary>恐怖支配が生む脆さの最大値（恐怖一色でこれだけ崩れやすい・0..1）。</summary>
        public readonly float maxBrittleness;
        /// <summary>士気の土壌の下限（風土が最悪でもこれだけは支える・0..1）。</summary>
        public readonly float moraleFoundationFloor;
        /// <summary>有害な指揮が組織を蝕む最大割合（高圧×不誠実でこれだけ削る・0..1）。</summary>
        public readonly float maxToxicPenalty;

        public CommandClimateParams(
            float integrityWeight,
            float hardshipWeight,
            float timeToBond,
            float initiativeFloor,
            float maxBrittleness,
            float moraleFoundationFloor,
            float maxToxicPenalty)
        {
            this.integrityWeight = Mathf.Clamp01(integrityWeight);
            this.hardshipWeight = Mathf.Clamp01(hardshipWeight);
            this.timeToBond = Mathf.Max(0.001f, timeToBond);
            this.initiativeFloor = Mathf.Clamp01(initiativeFloor);
            this.maxBrittleness = Mathf.Clamp01(maxBrittleness);
            this.moraleFoundationFloor = Mathf.Clamp01(moraleFoundationFloor);
            this.maxToxicPenalty = Mathf.Clamp01(maxToxicPenalty);
        }

        /// <summary>
        /// 既定値。誠実0.6/苦楽0.4・結束完成24（月）・自発性下限0.5・
        /// 恐怖の脆さ最大0.8・士気の土壌下限0.3・有害指揮の上限0.6。
        /// </summary>
        public static CommandClimateParams Default => new CommandClimateParams(
            integrityWeight: 0.6f,
            hardshipWeight: 0.4f,
            timeToBond: 24f,
            initiativeFloor: 0.5f,
            maxBrittleness: 0.8f,
            moraleFoundationFloor: 0.3f,
            maxToxicPenalty: 0.6f);
    }

    /// <summary>
    /// 指揮風土（部隊の組織文化＝信頼と士気の土壌）の純ロジック。すべて static・純関数。
    /// 良い風土（信頼・苦楽を共にする・率先垂範・失敗への寛容）は結束・自発性・粘りを生み、
    /// 悪い風土（恐怖支配・不信）は形だけの服従と、逆境で崩れる脆さを生む。
    /// 実効値パターン（基準値は非破壊）＝倍率・係数を返すだけで状態は持たない。
    ///
    /// 責務分担：
    /// - <see cref="FleetMorale"/>（士気の実体）は read-only 相当＝本ルールが返す「士気の土壌」が
    ///   士気の上限・回復しやすさを支える側で、士気値そのものは持たない。
    /// - <see cref="CommandStaffRules"/>（指揮班の能力＝提督/副提督/参謀の実効能力）とは別物。
    ///   こちらは能力でなく「部隊の空気＝組織文化」を扱う。
    /// - <see cref="Organization"/>（結束/制度化＝英雄死後の組織存続）とも別レイヤー。
    ///   あちらは勢力・組織の存続、本ルールは個々の部隊の指揮風土に特化する。
    /// 盤面非依存の plain 引数のみ・乱数なし（必要箇所は float roll を受ける）。
    /// 能力値・度合いは 0..100、符号付きの指標は -1..1 を返す。
    /// </summary>
    public static class CommandClimateRules
    {
        // --- 信頼・結束・自発性 -------------------------------------------

        /// <summary>
        /// 指揮官への信頼（0..1）。指揮官の誠実さ（0..100）と、苦楽を共にする度合い（0..100）の加重和。
        /// 誠実で部下と苦楽を共にするほど信頼が厚い。
        /// </summary>
        public static float TrustLevel(float leaderIntegrity, float sharedHardship, CommandClimateParams p)
        {
            float integrity = Mathf.Clamp01(leaderIntegrity / 100f);
            float hardship = Mathf.Clamp01(sharedHardship / 100f);
            float trust = integrity * p.integrityWeight + hardship * p.hardshipWeight;
            return Mathf.Clamp01(trust);
        }

        public static float TrustLevel(float leaderIntegrity, float sharedHardship)
            => TrustLevel(leaderIntegrity, sharedHardship, CommandClimateParams.Default);

        /// <summary>
        /// 部隊の結束（0..1）。信頼（0..1）に、共に過ごした時間で育つ要素を掛ける。
        /// 信頼が厚く、長く共に在った部隊ほど結束が固い（timeToBond で頭打ち）。
        /// </summary>
        public static float Cohesion(float trustLevel, float timeServedTogether, CommandClimateParams p)
        {
            float trust = Mathf.Clamp01(trustLevel);
            float timeFactor = Mathf.Clamp01(Mathf.Max(0f, timeServedTogether) / p.timeToBond);
            return Mathf.Clamp01(trust * timeFactor);
        }

        public static float Cohesion(float trustLevel, float timeServedTogether)
            => Cohesion(trustLevel, timeServedTogether, CommandClimateParams.Default);

        /// <summary>
        /// 部下の自発性（0..1）。信頼（0..1）と、失敗への寛容（0..100）で育つ。
        /// 信頼があり失敗を許す風土ほど、部下は自ら動く。寛容が無くても信頼ぶんは下限（initiativeFloor）まで出る。
        /// </summary>
        public static float Initiative(float trustLevel, float toleranceForMistakes, CommandClimateParams p)
        {
            float trust = Mathf.Clamp01(trustLevel);
            float tolerance = Mathf.Clamp01(toleranceForMistakes / 100f);
            float scale = p.initiativeFloor + (1f - p.initiativeFloor) * tolerance;
            return Mathf.Clamp01(trust * scale);
        }

        public static float Initiative(float trustLevel, float toleranceForMistakes)
            => Initiative(trustLevel, toleranceForMistakes, CommandClimateParams.Default);

        // --- 恐怖支配か敬服か ---------------------------------------------

        /// <summary>
        /// 恐怖支配か敬服か（-1=純然たる恐怖支配／+1=純然たる敬服）。
        /// 強制・威圧（coercion 0..100）と敬意・人望（respect 0..100）の差。
        /// </summary>
        public static float FearVsRespect(float coercion, float respect)
        {
            float c = Mathf.Clamp01(coercion / 100f);
            float r = Mathf.Clamp01(respect / 100f);
            return Mathf.Clamp(r - c, -1f, 1f);
        }

        /// <summary>
        /// 恐怖支配が生む脆さ（0..1）。恐怖一色（fearVsRespect=-1）ほど、逆境で崩れる「形だけの服従」になる。
        /// 敬服（+1）なら脆さ 0。fearVsRespect は -1..1。
        /// </summary>
        public static float BrittlenessFromFear(float fearVsRespect, CommandClimateParams p)
        {
            float fvr = Mathf.Clamp(fearVsRespect, -1f, 1f);
            // -1→1.0, +1→0.0 に写像してから最大値を掛ける。
            float fearAmount = (1f - fvr) * 0.5f;
            return Mathf.Clamp01(fearAmount * p.maxBrittleness);
        }

        public static float BrittlenessFromFear(float fearVsRespect)
            => BrittlenessFromFear(fearVsRespect, CommandClimateParams.Default);

        // --- 士気の土壌・有害な指揮 ---------------------------------------

        /// <summary>
        /// 士気の土壌（0..1）。風土（信頼・結束）が士気の上限・回復しやすさを支える。
        /// 信頼と結束が満ちれば 1.0、最悪でも下限（moraleFoundationFloor）は支える。
        /// 呼び出し側（<see cref="FleetMorale"/>）がこの係数を士気の基準に乗じる。
        /// </summary>
        public static float MoraleFoundation(float trustLevel, float cohesion, CommandClimateParams p)
        {
            float trust = Mathf.Clamp01(trustLevel);
            float coh = Mathf.Clamp01(cohesion);
            float climate = trust * 0.5f + coh * 0.5f;
            float foundation = p.moraleFoundationFloor + (1f - p.moraleFoundationFloor) * climate;
            return Mathf.Clamp01(foundation);
        }

        public static float MoraleFoundation(float trustLevel, float cohesion)
            => MoraleFoundation(trustLevel, cohesion, CommandClimateParams.Default);

        /// <summary>
        /// 有害な指揮のペナルティ（0..1）。高圧（coercion 0..100）×不誠実（誠実 leaderIntegrity が低い）ほど
        /// 組織を蝕む。誠実な指揮官なら強く律しても蝕まれない（integrity が高いほど 0 に近づく）。
        /// 呼び出し側がこの割合ぶん風土・士気の土壌から差し引く。
        /// </summary>
        public static float ToxicLeadershipPenalty(float coercion, float leaderIntegrity, CommandClimateParams p)
        {
            float c = Mathf.Clamp01(coercion / 100f);
            float integrity = Mathf.Clamp01(leaderIntegrity / 100f);
            float toxicity = c * (1f - integrity);
            return Mathf.Clamp01(toxicity * p.maxToxicPenalty);
        }

        public static float ToxicLeadershipPenalty(float coercion, float leaderIntegrity)
            => ToxicLeadershipPenalty(coercion, leaderIntegrity, CommandClimateParams.Default);

        // --- 健全性判定 ---------------------------------------------------

        /// <summary>健全な指揮風土か（信頼が割合しきい値 0..1 以上）。</summary>
        public static bool IsHealthyClimate(float trustLevel, float threshold)
        {
            float trust = Mathf.Clamp01(trustLevel);
            float t = Mathf.Clamp01(threshold);
            return trust >= t;
        }
    }
}
