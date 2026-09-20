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
    }
}
