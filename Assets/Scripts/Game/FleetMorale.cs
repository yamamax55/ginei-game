using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊の士気を管理するクラス。
    /// 被弾や交戦で低下し、能力に影響を与えます。士気が0になると敗走状態になります。
    /// </summary>
    [RequireComponent(typeof(FleetStrength))]
    public class FleetMorale : MonoBehaviour
    {
        [Header("士気設定")]
        public float morale = 100f;
        public float maxMorale = 100f;

        [Tooltip("非交戦時の自然回復速度 (ポイント/秒)")]
        public float recoveryRate = 0.7f;

        [Tooltip("交戦中の自然低下速度 (ポイント/秒)")]
        public float combatDrainRate = 0.1f;

        [Tooltip("ダメージ100あたりの士気低下量")]
        public float damageDrainFactor = 0.05f;

        [Tooltip("敗走から回復を始めるまでの『交戦が無い』継続時間（秒）")]
        public float routedRecoveryDelay = 4f;

        [Tooltip("交戦の自然低下が下げ止まる士気の床（最大士気に対する割合）＝交戦だけでは敗走しない・崩れるのは被弾のみ #会戦改善")]
        public float combatMoraleFloor = 0.2f;

        [Tooltip("1回の被弾で減る士気の上限（最大士気に対する割合）＝一撃で即敗走させない #会戦改善")]
        public float maxSingleHitMoraleFraction = 0.08f;

        [Header("武名の威圧（ADM-3 #2304）")]
        [Tooltip("近傍の高武名の敵将が味方士気を削る（威圧）。RenownRules.IntimidationFactor を士気の押し下げに使う")]
        public bool enableIntimidation = true;
        [Tooltip("威圧が届く範囲")]
        public float intimidationRange = 25f;
        [Tooltip("威圧による士気低下の速さ（×武名係数×最大士気×dt）。押し下げの下限は (1-威圧係数)×最大士気")]
        public float intimidationDrainRate = 0.6f;
        // 威圧の再計算は間引き（毎フレーム全旗艦走査を避ける＝終盤ラグ回避）。
        private const float IntimidationInterval = 0.5f;
        private float intimidationCheckTimer;
        private float currentIntimidation;

        // #2263 名誉→士気の底上げ（勲章の名誉点スケール・上限）。
        private const float PrestigeMoraleScale = 250f;     // 名誉点÷これ＝士気倍率の加算（50点で+20%）
        private const float MaxPrestigeMoraleBonus = 0.20f; // 名誉による士気底上げの上限

        /// <summary>
        /// 敗走しているか。<b>不退転（#2175）が効いている間は敗走にしない</b>
        /// ＝被弾のタイミングや Update の順に関係なく、いつ読んでも同じ答えになる
        /// （判定は <see cref="MoraleLockRules"/> に集約＝読む側ごとに解釈しない）。
        /// </summary>
        public bool IsRouted => MoraleLockRules.IsRouted(morale, MoraleLocked);

        /// <summary>不退転が効いているか（士気の下限と敗走判定に効く）。</summary>
        public bool MoraleLocked => strength != null && strength.activeMoraleLock;

        /// <summary>
        /// 士気を増減する（士気の連鎖崩壊／高揚 #2176）。下限〜maxMorale にクランプ。負で衝撃、正で高揚。
        /// 下限は不退転中だけ <see cref="MoraleLockRules.LockedFloor"/>（平時は0＝従来どおり）。
        /// </summary>
        public void ApplyMoraleDelta(float delta)
            => ApplyMoraleDelta(delta, MoraleChangeSource.その他, null);

        /// <summary>
        /// 士気を増減する（原因を名乗る版）。<b>増減の計算は上と同一</b>で、
        /// 観測台帳 <see cref="MoraleAuditLog"/> へ原因を1件残すだけ違う（既定は無効＝何もしない）。
        /// 自然回復・会戦イベント・撃墜高揚はどれも士気を上げるので、
        /// 値と時刻だけから原因を言い当てると推測になる＝<b>書いた側に名乗らせる</b>。
        /// </summary>
        public void ApplyMoraleDelta(float delta, MoraleChangeSource source, string detail)
        {
            ApplyWithAudit(delta, source, detail);
        }

        /// <summary>
        /// 士気を <see cref="MoraleLockRules.Clamp"/> で増減し、観測台帳へ原因を残す。
        /// ★台帳が無効なら <see cref="MoraleAuditLog.Record"/> は即 return するので、
        ///   通常プレイでは前後の読み取り以外に何もしない（挙動は従来どおり）。
        /// </summary>
        private void ApplyWithAudit(float delta, MoraleChangeSource source, string detail)
        {
            bool locked = MoraleLocked;
            float before = morale;
            bool routedBefore = MoraleLockRules.IsRouted(before, locked);

            morale = MoraleLockRules.Clamp(before, delta, maxMorale, locked);

            MoraleAuditLog.Record(Time.time, AuditName, source, detail,
                before, morale, routedBefore, MoraleLockRules.IsRouted(morale, locked),
                Time.time - lastCombatTime, locked);
        }

        /// <summary>観測台帳に出す艦隊名（提督名があればそれ、無ければ GameObject 名）。</summary>
        private string AuditName
            => (strength != null && !string.IsNullOrEmpty(strength.admiralName))
                ? strength.admiralName : gameObject.name;

        private FleetStrength strength;
        private FleetWeapon weapon;
        private FleetSustainment sustainment; // 継戦（ORBAT-4・任意・既定で挙動不変）
        private TextMesh moraleLabel;
        private float lastCombatTime;   // 直近に交戦していた時刻（敗走回復の待機判定用）

        private void Awake()
        {
            strength = GetComponent<FleetStrength>();
            weapon = GetComponent<FleetWeapon>();
        }

        private void Start()
        {
            // 継戦（ORBAT-4）は Squadron.Awake が付与する＝Awake 順に依存しないよう Start で取得（無ければ null＝挙動不変）。
            sustainment = GetComponent<FleetSustainment>();
            InitializeMorale();
            CreateMoraleLabel();
        }

        private void Update()
        {
            UpdateMorale();
            UpdateMoraleLabel();
        }

        private void CreateMoraleLabel()
        {
            // 日本語フォントは FontProvider に集約（Unity6 の Arial.ttf 禁止対応も一元化）
            Font jaFont = FontProvider.JapaneseFont;
            // プレハブに焼き込まれた既存 "MoraleLabel" があれば再利用（二重生成を防ぐ）
            Transform existingLabel = transform.Find("MoraleLabel");
            GameObject go;
            if (existingLabel != null)
            {
                go = existingLabel.gameObject;
            }
            else
            {
                go = new GameObject("MoraleLabel");
                go.transform.SetParent(transform);
                go.transform.localPosition = new Vector3(-0.6f, 0.6f, 0f);
                go.transform.localScale = Vector3.one * 0.15f;
            }

            moraleLabel = go.GetComponent<TextMesh>();
            if (moraleLabel == null) moraleLabel = go.AddComponent<TextMesh>();
            moraleLabel.font = jaFont;
            moraleLabel.anchor = TextAnchor.LowerCenter;
            moraleLabel.alignment = TextAlignment.Center;
            moraleLabel.fontSize = 60;
            moraleLabel.characterSize = 0.4f;

            // 兵力ラベルと同じくズームに追従し、引き／寄せで極端な縮小・肥大を防ぐ（FSH-5）。
            LabelZoomScaler labelScaler = go.GetComponent<LabelZoomScaler>();
            if (labelScaler == null) labelScaler = go.AddComponent<LabelZoomScaler>();
            labelScaler.Configure(go.transform.localScale, 16f);

            var mr = go.GetComponent<MeshRenderer>();
            if (jaFont != null) mr.sharedMaterial = jaFont.material;

            moraleLabel.text = "";
        }

        private void UpdateMoraleLabel()
        {
            if (moraleLabel == null) return;

            if (IsRouted)
            {
                moraleLabel.text = "敗走";
                moraleLabel.color = new Color(1f, 0.2f, 0.2f);
            }
            else if (GetMoraleFactor() < 1f)
            {
                moraleLabel.text = "士気低下";
                moraleLabel.color = new Color(1f, 0.85f, 0.1f);
            }
            else
            {
                moraleLabel.text = "";
            }
        }

        private void InitializeMorale()
        {
            if (strength != null && strength.admiralData != null)
            {
                // 最大士気は提督の統率力に依存 (例: 統率と同じ値)。0以下にはしない（ゼロ除算防止）
                // 参謀補完を反映した実効統率を使用（基準値は非破壊）
                maxMorale = Mathf.Max(1f, strength.admiralData.EffectiveLeadership);

                // #2263 名誉：勲章を持つ提督は名望で士気が底上げされる（前戦の叙勲が次戦に効く）。実効値パターン。
                float prestige = MedalRegistry.Prestige(EntityKey.Of(strength.admiralData));
                if (prestige > 0f) maxMorale *= 1f + Mathf.Min(prestige / PrestigeMoraleScale, MaxPrestigeMoraleBonus);

                float beforeInit = morale;
                morale = maxMorale;
                MoraleAuditLog.Record(Time.time, AuditName, MoraleChangeSource.初期化, null,
                    beforeInit, morale, false, false, 0f, false);
            }
        }

        private void UpdateMorale()
        {
            // 特殊指揮『不退転』（#2175）：効果中は敗走しない＝士気を下限で踏みとどまらせる。
            // ★下限は ChangeMorale / ApplyMoraleDelta 側でも守っているので、ここは保険
            //   （直接 morale を書いた経路があっても1フレームで整う）。判定は MoraleLockRules に集約。
            if (MoraleLocked && morale < MoraleLockRules.LockedFloor)
            {
                float beforeFloor = morale;
                morale = MoraleLockRules.LockedFloor;
                MoraleAuditLog.Record(Time.time, AuditName, MoraleChangeSource.不退転下限, null,
                    beforeFloor, morale, MoraleLockRules.IsRouted(beforeFloor, true), false,
                    Time.time - lastCombatTime, true);
            }

            bool inCombat = (weapon != null && weapon.IsInCombat);
            if (inCombat) lastCombatTime = Time.time;

            if (IsRouted)
            {
                // 敗走中：交戦が routedRecoveryDelay 秒途切れたら回復を開始（士気>0で敗走解除）。
                // 判定は Core に集約（被弾も交戦として数え直す＝撃たれている間は立ち直らない）。
                if (RoutRecoveryRules.CanRecover(inCombat, Time.time - lastCombatTime, routedRecoveryDelay))
                {
                    ChangeMorale(recoveryRate * Time.deltaTime, MoraleChangeSource.自然回復);
                }
                return;
            }

            if (inCombat)
            {
                // 交戦中による低下。ただし床（combatMoraleFloor）までで止まる＝交戦だけでは敗走しない（崩れるのは被弾のみ）。
                float floor = maxMorale * Mathf.Clamp01(combatMoraleFloor);
                if (morale > floor)
                {
                    float beforeDrain = morale;
                    morale = Mathf.Max(floor, morale - combatDrainRate * Time.deltaTime);
                    MoraleAuditLog.Record(Time.time, AuditName, MoraleChangeSource.交戦低下, null,
                        beforeDrain, morale, MoraleLockRules.IsRouted(beforeDrain, MoraleLocked),
                        IsRouted, Time.time - lastCombatTime, MoraleLocked);
                }
            }
            else
            {
                // 非交戦中による回復
                ChangeMorale(recoveryRate * Time.deltaTime, MoraleChangeSource.自然回復);
            }

            ApplyIntimidation(); // 武名の威圧（ADM-3）：近傍の高武名の敵将が士気を押し下げる
        }

        // 武名の威圧（ADM-3 #2304）：近傍に高武名の敵将がいると士気が (1-威圧係数)×最大士気 まで押し下げられる。
        private void ApplyIntimidation()
        {
            if (!enableIntimidation || strength == null) return;
            intimidationCheckTimer -= Time.deltaTime;
            if (intimidationCheckTimer <= 0f)
            {
                currentIntimidation = ComputeEnemyIntimidation();
                intimidationCheckTimer = IntimidationInterval;
            }
            if (currentIntimidation <= 0f) return;
            float floor = maxMorale * (1f - Mathf.Clamp01(currentIntimidation));
            if (morale > floor)
            {
                float beforeIntim = morale;
                morale = Mathf.Max(floor, morale - currentIntimidation * intimidationDrainRate * maxMorale * Time.deltaTime);
                MoraleAuditLog.Record(Time.time, AuditName, MoraleChangeSource.威圧, null,
                    beforeIntim, morale, MoraleLockRules.IsRouted(beforeIntim, MoraleLocked),
                    IsRouted, Time.time - lastCombatTime, MoraleLocked);
            }
        }

        // 範囲内の敵旗艦の提督の実効武名から最大の威圧係数を返す（RenownRules.IntimidationFactor・平時 heroism=0）。
        private float ComputeEnemyIntimidation()
        {
            if (strength == null) return 0f;
            Vector3 pos = transform.position;
            float r2 = intimidationRange * intimidationRange;
            float maxF = 0f;
            var flags = FleetRegistry.AllFlagships;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength f = flags[i];
                if (f == null || !f.IsAlive || f.admiralData == null) continue;
                if (!FactionRelations.IsHostile(strength.factionData, strength.faction, f)) continue;
                if (((Vector2)(f.transform.position - pos)).sqrMagnitude > r2) continue;
                int effFame = Mathf.Max(f.admiralData.fame, FameRegistry.Get(EntityKey.Of(f.admiralData)));
                float fac = RenownRules.IntimidationFactor(effFame, 0f);
                if (fac > maxF) maxF = fac;
            }
            return maxF;
        }

        /// <summary>
        /// ダメージを受けた際の士気低下処理。
        /// </summary>
        public void OnTakeDamage(int damageAmount)
        {
            // ★被弾も「交戦」のうち（敗走の立ち直り待ちを数え直す）。
            //   IsInCombat は「自分が撃った／自分の射界に敵がいる」だけなので、
            //   射界の外から叩かれている・敗走して背を向けている部隊は非交戦と見なされ、
            //   待ち時間を飛ばして次のフレームに立ち直ってしまう（実機の敗走/解除の反復）。
            lastCombatTime = Time.time;

            float drain = damageAmount * damageDrainFactor;
            // 一撃で即敗走させない＝1回の被弾で減る士気を上限でクランプ（#会戦改善）。
            float cap = maxMorale * Mathf.Clamp01(maxSingleHitMoraleFraction);
            if (cap > 0f) drain = Mathf.Min(drain, cap);
            ChangeMorale(-drain, MoraleChangeSource.被弾);
        }

        private void ChangeMorale(float amount) => ChangeMorale(amount, MoraleChangeSource.その他);

        private void ChangeMorale(float amount, MoraleChangeSource source)
        {
            // ★下限は不退転中だけ 1（平時は0＝従来どおり）。被弾はフレーム中の任意の時点で来るので、
            //   ここで下限を守らないと「次の Update で1へ戻るまでのあいだだけ敗走」になる。
            ApplyWithAudit(amount, source, null);
        }

        /// <summary>
        /// 現在の士気による能力補正倍率を取得します。
        /// </summary>
        /// <returns>1.0 (正常) 〜 0.5 (低士気) 等</returns>
        public float GetMoraleFactor()
        {
            // 最大士気が未設定(0以下)なら補正なし（ゼロ除算によるNaN防止）
            if (maxMorale <= 0) return ApplySustainment(1.0f);

            // 士気が低いほど(閾値以下で)ペナルティが発生する簡易モデル
            // 例: 士気が最大値の30%以下から低下し始め、0で0.5倍になる
            float ratio = morale / maxMorale;
            float factor = (ratio > 0.3f) ? 1.0f : Mathf.Lerp(0.5f, 1.0f, ratio / 0.3f);
            return ApplySustainment(factor);
        }

        /// <summary>継戦ペナルティ（ORBAT-4・任意）＋軍団長の士気バフ/デバフ（CSG）を乗せる。既定1.0で挙動不変。</summary>
        private float ApplySustainment(float factor)
        {
            if (sustainment != null) factor *= sustainment.EffectiveFactor;
            if (strength != null) factor *= Mathf.Max(0.1f, strength.corpsMoraleFactor); // 軍団長のバフ/デバフ
            if (strength != null) factor *= AdmiralArchetypeModifiers.MoraleFactor(strength.admiralData); // 黄金の獅子#覇王（フラグ無しは1.0）
            return factor;
        }
    }
}
