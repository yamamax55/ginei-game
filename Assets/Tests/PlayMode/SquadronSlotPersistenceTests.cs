using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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
