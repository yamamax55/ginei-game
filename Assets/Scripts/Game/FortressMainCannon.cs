using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦術要塞の主砲（#77・トールハンマー型）。超長射程・広範囲の一撃必殺砲を1門。
    /// チャージ→予告線（発射方向の警告ライン）→発射で射線上の全敵に大ダメージ→長いクールダウン。
    /// **最小射程あり＝懐に潜れば撃たれない**（攻城側のリズム＝主砲を避けて距離を詰める）。
    /// 目標は射界帯で最も艦が密集した方向を自動選定。倍速・ポーズに追従（Time.time＝scaled）。
    /// 射界帯の判定は <see cref="FortressBatteryRules.InFiringBand"/> に委譲（純ロジック・test-first）。
    /// </summary>
    [RequireComponent(typeof(FortressUnit))]
    public class FortressMainCannon : MonoBehaviour
    {
        [Header("射程・射界")]
        [Tooltip("最大射程（ワールド単位・超長射程）")]
        public float maxRange = 48f;
        [Tooltip("最小射程（これより内＝懐に入られると撃てない）")]
        public float minRange = 8f;
        [Tooltip("射線の半幅（この幅内の敵が射線上＝被弾）")]
        public float halfWidth = 1.6f;

        [Header("威力・テンポ")]
        [Tooltip("直撃ダメージ（艦の防御をほぼ無視する一撃必殺の威力）")]
        public int damage = 6000;
        [Tooltip("チャージ時間（秒・game-time）。この間に予告線が出る＝散開で回避可能")]
        public float chargeTime = 3.0f;
        [Tooltip("発射後のクールダウン（秒・game-time）。この間は懐に踏み込める")]
        public float cooldown = 9.0f;
        [Tooltip("待機中の目標再探索間隔（秒）")]
        public float targetingInterval = 0.5f;

        [Header("演出")]
        [Tooltip("予告線の色（警告）")]
        public Color warningColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        [Tooltip("発射ビームの色")]
        public Color fireColor = new Color(1f, 0.95f, 0.7f, 1f);
        [Tooltip("予告線の幅")]
        public float warningWidth = 0.18f;
        [Tooltip("発射ビームの幅（太い）")]
        public float beamWidth = 0.9f;
        [Tooltip("発射ビームの表示時間（秒）")]
        public float beamDuration = 0.45f;

        private enum Phase { 待機, チャージ }
        private Phase phase = Phase.待機;

        // ── 危険域の公開（#641 AI ハメ防止）：チャージ中だけ射線が固定＝AIが横へ散開して回避できる窓 ──
        /// <summary>チャージ中（予告線が出て射線が固定されている）か。AI回避が効く窓。</summary>
        public bool IsCharging => phase == Phase.チャージ && owner != null && owner.IsAlive;
        /// <summary>チャージ中に固定された射線方向（正規化）。</summary>
        public Vector2 AimDirection => aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : Vector2.up;
        /// <summary>主砲の発射原点（要塞中心）。</summary>
        public Vector2 Origin => (Vector2)transform.position;
        public float MinRange => minRange;
        public float MaxRange => maxRange;
        public float HalfWidth => halfWidth;

        private FortressUnit owner;
        private float chargeEndTime;
        private float readyTime;          // この時刻まではクールダウン中で撃てない
        private float nextAcquireTime;
        private Vector2 aimDir = Vector2.up; // チャージ開始で固定（プレイヤーが回避できるよう発射まで動かさない）

        // 予告線（自前 LineRenderer）。発射ビームは BeamFx を別の子で使う。
        private LineRenderer warningLine;
        private LineRenderer beamLine;
        private Material beamMaterial;

        // 列挙中の TakeDamage で在庫が変わらないようスナップショットを使う
        private readonly List<IShipTarget> snapshot = new List<IShipTarget>();

        private void Awake()
        {
            owner = GetComponent<FortressUnit>();
            SetupWarningLine();
            SetupBeamLine();
            // 開幕直後の即撃ちを避け、最初の1発まで少し溜める。
            readyTime = Time.time + Mathf.Min(cooldown, 2f);
        }

        private void Update()
        {
            if (owner == null || !owner.IsAlive) { HideWarning(); return; }

            switch (phase)
            {
                case Phase.待機:
                    if (Time.time < readyTime) { HideWarning(); return; }
                    if (Time.time < nextAcquireTime) return;
                    nextAcquireTime = Time.time + targetingInterval;
                    if (TryAcquireAim(out aimDir))
                    {
                        phase = Phase.チャージ;
                        chargeEndTime = Time.time + Mathf.Max(0.1f, chargeTime);
                        ShowWarning();
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayBeam(); // 警告音（代用）
                    }
                    break;

                case Phase.チャージ:
                    UpdateWarningPulse();
                    if (Time.time >= chargeEndTime)
                    {
                        Fire();
                        phase = Phase.待機;
                        readyTime = Time.time + Mathf.Max(0.5f, cooldown);
                        HideWarning();
                    }
                    break;
            }
        }

        /// <summary>
        /// 射界帯（minRange..maxRange）の敵が最も密集する方向を選ぶ。各敵方向を候補に、その射線上に
        /// 収まる敵数が最大の向きを返す。帯内に敵がいなければ false（撃たない）。
        /// </summary>
        private bool TryAcquireAim(out Vector2 dir)
        {
            dir = Vector2.up;
            Vector2 origin = transform.position;

            // 帯内の敵を収集
            List<Vector2> enemies = new List<Vector2>();
            IReadOnlyList<IShipTarget> all = FleetRegistry.AllTargets;
            for (int i = 0; i < all.Count; i++)
            {
                IShipTarget t = all[i];
                if (!ShipCombat.IsValidTarget(t)) continue;
                if (!FactionRelations.IsHostile(owner.FactionData, owner.Faction, t)) continue;
                Vector2 v = (Vector2)t.Transform.position - origin;
                float d = v.magnitude;
                if (!FortressBatteryRules.InFiringBand(d, minRange, maxRange)) continue;
                enemies.Add(v);
            }
            if (enemies.Count == 0) return false;

            // 各敵方向を候補に、その射線（半幅 halfWidth）に乗る敵数で評価
            int bestScore = -1;
            Vector2 best = enemies[0].normalized;
            for (int i = 0; i < enemies.Count; i++)
            {
                Vector2 cand = enemies[i].normalized;
                int score = 0;
                for (int j = 0; j < enemies.Count; j++)
                {
                    float along = Vector2.Dot(enemies[j], cand);
                    if (along <= 0f) continue;
                    float perp = (enemies[j] - cand * along).magnitude;
                    if (perp <= halfWidth) score++;
                }
                if (score > bestScore) { bestScore = score; best = cand; }
            }
            dir = best;
            return true;
        }

        /// <summary>射線上（minRange..maxRange・半幅 halfWidth・前方）の全敵に直撃ダメージ。</summary>
        private void Fire()
        {
            Vector2 origin = transform.position;
            Vector2 dir = aimDir.sqrMagnitude > 1e-6f ? aimDir.normalized : Vector2.up;

            // 在庫のスナップショット（TakeDamage で Unregister されても安全に列挙）
            snapshot.Clear();
            IReadOnlyList<IShipTarget> all = FleetRegistry.AllTargets;
            for (int i = 0; i < all.Count; i++) snapshot.Add(all[i]);

            for (int i = 0; i < snapshot.Count; i++)
            {
                IShipTarget t = snapshot[i];
                if (!ShipCombat.IsValidTarget(t)) continue;
                if (!FactionRelations.IsHostile(owner.FactionData, owner.Faction, t)) continue;
                Vector2 v = (Vector2)t.Transform.position - origin;
                float d = v.magnitude;
                if (!FortressBatteryRules.InFiringBand(d, minRange, maxRange)) continue; // 懐/射程外は無傷
                float along = Vector2.Dot(v, dir);
                if (along <= 0f) continue;                       // 後方は当たらない
                float perp = (v - dir * along).magnitude;
                if (perp > halfWidth) continue;                  // 射線から外れていれば回避成功

                Vector3 pos = t.Transform.position;
                t.TakeDamage(damage);
                DamageAccumulator.Add(t.Transform, damage, false, pos);
            }

            FireBeam(origin, origin + dir * maxRange);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayExplosion();
            CameraController cam = Object.FindAnyObjectByType<CameraController>();
            if (cam != null) cam.Shake();
        }

        // ── 予告線（警告）──
        private void SetupWarningLine()
        {
            GameObject obj = new GameObject("MainCannonWarning");
            obj.transform.SetParent(transform, false);
            warningLine = obj.AddComponent<LineRenderer>();
            warningLine.useWorldSpace = true;
            warningLine.positionCount = 2;
            warningLine.numCapVertices = 2;
            warningLine.material = new Material(Shader.Find("Sprites/Default"));
            warningLine.startWidth = warningWidth;
            warningLine.endWidth = warningWidth;
            warningLine.sortingOrder = 6;
            warningLine.enabled = false;
        }

        private void ShowWarning()
        {
            if (warningLine == null) return;
            Vector3 origin = transform.position;
            Vector3 end = origin + (Vector3)(aimDir.normalized * maxRange);
            warningLine.SetPosition(0, origin + (Vector3)(aimDir.normalized * minRange)); // 懐(最小射程)は警告外＝そこは安全
            warningLine.SetPosition(1, end);
            warningLine.enabled = true;
        }

        private void UpdateWarningPulse()
        {
            if (warningLine == null || !warningLine.enabled) return;
            // チャージ進行に応じて警告色を濃く・点滅させる（発射直前ほど赤く強く）。
            float p = Mathf.Clamp01(1f - (chargeEndTime - Time.time) / Mathf.Max(0.1f, chargeTime));
            float blink = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * (6f + 10f * p)));
            Color c = Color.Lerp(warningColor, new Color(1f, 0.2f, 0.1f, 1f), p);
            c.a = blink;
            warningLine.startColor = c;
            warningLine.endColor = c;
        }

        private void HideWarning()
        {
            if (warningLine != null) warningLine.enabled = false;
        }

        // ── 発射ビーム（太い・BeamFx）──
        private void SetupBeamLine()
        {
            GameObject obj = new GameObject("MainCannonBeam");
            obj.transform.SetParent(transform, false);
            beamLine = obj.AddComponent<LineRenderer>();
            BeamFx.ConfigureLine(beamLine, beamWidth);
            beamMaterial = BeamFx.CreateMaterial();
            beamLine.material = beamMaterial;
            BeamFx.ApplyGradient(beamLine, fireColor);
            beamLine.sortingOrder = 7;
        }

        private void FireBeam(Vector3 from, Vector3 to)
        {
            if (beamLine == null) return;
            StopAllCoroutines();
            StartCoroutine(BeamFx.Play(beamLine, beamMaterial, beamWidth, beamDuration, from, to));
        }

        private void OnDestroy()
        {
            if (beamMaterial != null) Destroy(beamMaterial);
            if (warningLine != null && warningLine.material != null) Destroy(warningLine.material);
        }
    }
}
