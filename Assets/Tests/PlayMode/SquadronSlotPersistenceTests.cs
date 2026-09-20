using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ginei.Tests
{
    public class SquadronSlotPersistenceTests
    {
        [Test]
        public void RemoveMember_PreservesSurvivorsAssignedSlots()
        {
            var root = new GameObject("squadron-slot-test");
            var members = new List<GameObject>();
            try
            {
                var squadron = root.AddComponent<Squadron>();
                squadron.spacing = 1f;
                for (int i = 0; i < 4; i++)
                {
                    var member = new GameObject("member-" + i);
                    member.transform.SetParent(root.transform, true);
                    member.transform.position = new Vector3(i - 1.5f, 0f, 0f);
                    members.Add(member);
                    squadron.memberShips.Add(member.transform);
                }

                InvokeEnsureSlots(squadron);
                var before = new List<int>(GetPrivate<List<int>>(squadron, "slotForMember"));

                squadron.RemoveMember(members[1].transform);
                InvokeEnsureSlots(squadron);

                var after = GetPrivate<List<int>>(squadron, "slotForMember");
                Assert.AreEqual(new[] { before[0], before[2], before[3] }, after);
                Assert.AreEqual(4, GetPrivate<List<Vector2>>(squadron, "cachedSlots").Count,
                    "戦死直後はスロットを詰めず、欠員の穴を残す");
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var member in members)
                    if (member != null) Object.DestroyImmediate(member);
            }
        }

        [Test]
        public void ConsecutiveCasualties_KeepVacanciesUntilBelowHalfThenCompact()
        {
            var root = new GameObject("squadron-consecutive-casualties-test");
            var members = new List<GameObject>();
            try
            {
                var squadron = root.AddComponent<Squadron>();
                squadron.spacing = 1f;
                for (int i = 0; i < 8; i++)
                {
                    var member = new GameObject("member-" + i);
                    member.transform.SetParent(root.transform, true);
                    member.transform.position = new Vector3(i - 3.5f, 0f, 0f);
                    members.Add(member);
                    squadron.memberShips.Add(member.transform);
                }

                InvokeEnsureSlots(squadron);
                var initialAssignments = new List<int>(GetPrivate<List<int>>(squadron, "slotForMember"));

                // 8隻から4隻までは生存艦の持ち場を保ち、戦死位置を穴として残す。
                for (int casualty = 7; casualty >= 4; casualty--)
                {
                    squadron.RemoveMember(members[casualty].transform);
                    InvokeEnsureSlots(squadron);
                }

                Assert.AreEqual(8, GetPrivate<List<Vector2>>(squadron, "cachedSlots").Count);
                CollectionAssert.AreEqual(initialAssignments.GetRange(0, 4),
                    GetPrivate<List<int>>(squadron, "slotForMember"));

                // 50%を下回る3隻目で初めて圧縮再編し、全員を重複なしで再割当する。
                squadron.RemoveMember(members[3].transform);
                InvokeEnsureSlots(squadron);

                var compactedSlots = GetPrivate<List<Vector2>>(squadron, "cachedSlots");
                var compactedAssignments = GetPrivate<List<int>>(squadron, "slotForMember");
                Assert.AreEqual(3, compactedSlots.Count);
                Assert.AreEqual(3, compactedAssignments.Count);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, compactedAssignments);
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var member in members)
                    if (member != null) Object.DestroyImmediate(member);
            }
        }

        [Test]
        public void FormationChange_Assigns_NonCrossing_StraightPaths_InRealSquadron()
        {
            var root = new GameObject("squadron-formation-crossing-test");
            var members = new List<GameObject>();
            try
            {
                var squadron = root.AddComponent<Squadron>();
                squadron.escortCount = 0;
                squadron.spacing = 1.4f;
                squadron.currentFormation = Formation.横陣;
                for (int i = 0; i < 10; i++)
                {
                    var member = new GameObject("member-" + i);
                    member.transform.SetParent(root.transform, true);
                    members.Add(member);
                    squadron.memberShips.Add(member.transform);
                }

                InvokeEnsureSlots(squadron);
                var oldSlots = GetPrivate<List<Vector2>>(squadron, "cachedSlots");
                var oldAssignments = GetPrivate<List<int>>(squadron, "slotForMember");
                var starts = new List<Vector2>();
                for (int i = 0; i < members.Count; i++)
                {
                    Vector2 p = root.transform.TransformPoint(oldSlots[oldAssignments[i]]);
                    members[i].transform.position = p;
                    starts.Add(p);
                }

                squadron.currentFormation = Formation.円陣;
                InvokeEnsureSlots(squadron);
                var newSlots = GetPrivate<List<Vector2>>(squadron, "cachedSlots");
                var newAssignments = GetPrivate<List<int>>(squadron, "slotForMember");
                var ends = new List<Vector2>();
                for (int i = 0; i < members.Count; i++)
                    ends.Add(root.transform.TransformPoint(newSlots[newAssignments[i]]));

                int crossings = 0;
                for (int i = 0; i < starts.Count; i++)
                    for (int j = i + 1; j < starts.Count; j++)
                        if (SegmentsProperlyIntersect(starts[i], ends[i], starts[j], ends[j])) crossings++;

                Assert.Zero(crossings, "横陣→円陣の再編経路が交差している");
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, newAssignments,
                    "全艦が重複なく新しい持ち場へ割り当てられる");
            }
            finally
            {
                Object.DestroyImmediate(root);
                foreach (var member in members)
                    if (member != null) Object.DestroyImmediate(member);
            }
        }

        [UnityTest]
        public IEnumerator UpdateShipPositions_AccelerationRampCannotExceedFinalSpeedLimit()
        {
            var root = new GameObject("squadron-speed-limit-test");
            var member = new GameObject("member");
            try
            {
                var squadron = root.AddComponent<Squadron>();
                squadron.escortCount = 0;
                squadron.enableAccelRamp = true;
                squadron.escortAcceleration = 0.1f;
                squadron.catchUpRatio = 1f;
                squadron.minimumCatchUpRatio = 1f;
                member.transform.SetParent(root.transform, true);
                member.transform.position = new Vector3(100f, 0f, 0f);
                squadron.memberShips.Add(member.transform);

                yield return null;

                var velocities = GetPrivate<List<Vector2>>(squadron, "velocities");
                velocities[0] = new Vector2(100f, 0f);
                Vector2 before = member.transform.position;
                float dt = Time.deltaTime;
                typeof(Squadron).GetMethod("UpdateShipPositions", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(squadron, null);

                float distance = ((Vector2)member.transform.position - before).magnitude;
                Assert.LessOrEqual(distance, 6f * dt + 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (member != null) Object.DestroyImmediate(member);
            }
        }

        [UnityTest]
        public IEnumerator FlagshipTranslation_IsFollowedThroughSpeedLimitedWorldMovement()
        {
            var root = new GameObject("squadron-parent-motion-test");
            var member = new GameObject("member");
            try
            {
                var squadron = root.AddComponent<Squadron>();
                squadron.escortCount = 0;
                squadron.catchUpRatio = 1f;
                squadron.enableAccelRamp = false;
                member.transform.SetParent(root.transform, true);
                member.transform.position = Vector3.zero;
                squadron.memberShips.Add(member.transform);

                yield return null;
                Vector2 before = member.transform.position;
                root.transform.position = new Vector3(20f, 0f, 0f);
                yield return null;

                float moved = ((Vector2)member.transform.position - before).magnitude;
                Assert.Less(moved, 1f, "旗艦の20unit移動を子Transformとして瞬間継承しない");
                Assert.Greater(moved, 0f, "旗艦の新しい陣形位置へ速度制限付きで追従する");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (member != null) Object.DestroyImmediate(member);
            }
        }

        [UnityTest]
        public IEnumerator FlagshipTurn_DoesNotSwingOuterEscortInstantly()
        {
            var root = new GameObject("squadron-parent-turn-test");
            var member = new GameObject("member");
            try
            {
                var squadron = root.AddComponent<Squadron>();
                squadron.escortCount = 0;
                squadron.catchUpRatio = 1f;
                squadron.enableAccelRamp = false;
                member.transform.SetParent(root.transform, true);
                member.transform.position = new Vector3(5f, 0f, 0f);
                squadron.memberShips.Add(member.transform);

                yield return null;
                Vector2 before = member.transform.position;
                root.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
                yield return null;

                float moved = ((Vector2)member.transform.position - before).magnitude;
                Assert.Less(moved, 1f, "旗艦の180度回頭で外周艦を反対側へ瞬間移動させない");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (member != null) Object.DestroyImmediate(member);
            }
        }

        [UnityTest]
        public IEnumerator FormationProgress_ReportsReformingWhileEscortIsFarFromSlot()
        {
            var root = new GameObject("squadron-progress-test");
            var member = new GameObject("member");
            try
            {
                var squadron = root.AddComponent<Squadron>();
                squadron.escortCount = 0;
                squadron.formationReadyTolerance = 0.5f;
                member.transform.SetParent(root.transform, true);
                member.transform.position = new Vector3(20f, 0f, 0f);
                squadron.memberShips.Add(member.transform);

                yield return null;

                Assert.IsTrue(squadron.IsReforming);
                Assert.Less(squadron.FormationProgress01, 1f);
                Assert.GreaterOrEqual(squadron.FormationProgress01, 0f);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (member != null) Object.DestroyImmediate(member);
            }
        }

        private static void InvokeEnsureSlots(Squadron squadron)
        {
            typeof(Squadron).GetMethod("EnsureSlots", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(squadron, null);
        }

        private static T GetPrivate<T>(Squadron squadron, string name)
        {
            return (T)typeof(Squadron).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(squadron);
        }

        private static bool SegmentsProperlyIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross(b - a, c - a);
            float abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c);
            float cdB = Cross(d - c, b - c);
            const float epsilon = 1e-5f;
            return abC * abD < -epsilon && cdA * cdB < -epsilon;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
