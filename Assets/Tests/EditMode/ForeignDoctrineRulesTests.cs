using NUnit.Framework;

namespace Ginei.Tests
{
    public class ForeignDoctrineRulesTests
    {
        [Test]
        public void Affinity_ConservativeAndReform_DriftApart()
        {
            Assert.AreEqual(-1f,
                ForeignDoctrineRules.Affinity(ForeignDoctrine.保守, ForeignDoctrine.改革));
        }

        [Test]
        public void Affinity_ModerateBridgesConservativeAndReform()
        {
            Assert.Greater(ForeignDoctrineRules.Affinity(ForeignDoctrine.中道, ForeignDoctrine.保守), 0f);
            Assert.Greater(ForeignDoctrineRules.Affinity(ForeignDoctrine.中道, ForeignDoctrine.改革), 0f);
        }

        [Test]
        public void Affinity_IsSymmetricForEveryPair()
        {
            foreach (ForeignDoctrine a in System.Enum.GetValues(typeof(ForeignDoctrine)))
                foreach (ForeignDoctrine b in System.Enum.GetValues(typeof(ForeignDoctrine)))
                    Assert.AreEqual(ForeignDoctrineRules.Affinity(a, b), ForeignDoctrineRules.Affinity(b, a));
        }

        [Test]
        public void Neutral_DoesNotMoveOpinionByDoctrine()
        {
            Assert.AreEqual(0f,
                ForeignDoctrineRules.Affinity(ForeignDoctrine.中立, ForeignDoctrine.改革));
        }

        [Test]
        public void Isolationists_TolerateEachOtherButDistanceFromOthers()
        {
            Assert.Greater(ForeignDoctrineRules.Affinity(ForeignDoctrine.孤立, ForeignDoctrine.孤立), 0f);
            Assert.Less(ForeignDoctrineRules.Affinity(ForeignDoctrine.孤立, ForeignDoctrine.中道), 0f);
        }

        [Test]
        public void BlendWithIdeology_PreservesLegacyWhenShareIsZero()
        {
            Assert.AreEqual(0.6f,
                ForeignDoctrineRules.BlendWithIdeology(0.6f, ForeignDoctrine.保守, ForeignDoctrine.改革, 0f),
                0.0001f);
        }

        [Test]
        public void BlendWithIdeology_CanDriveExistingOpinionTarget()
        {
            float affinity = ForeignDoctrineRules.BlendWithIdeology(
                0f, ForeignDoctrine.保守, ForeignDoctrine.改革, 1f);
            var factors = new DiplomacyRules.OpinionFactors(affinity, 0f, false, 0f, false);

            Assert.Less(DiplomacyRules.TargetOpinion(factors, DiplomacyRules.DiplomacyParams.Default), 0f);
        }
    }
}
