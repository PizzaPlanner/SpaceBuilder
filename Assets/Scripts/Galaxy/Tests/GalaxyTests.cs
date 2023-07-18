using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using static LocalToGalaxy;

namespace Galaxy.Tests
{

    public class GalaxyTests
    {

        public void VerifyValid(LocalToGalaxy ltg)
        {
            Assert.Less(math.abs(ltg.LocalPosition.x), MAX_POSITION);
            Assert.Less(math.abs(ltg.LocalPosition.y), MAX_POSITION);
            Assert.Less(math.abs(ltg.LocalPosition.z), MAX_POSITION);

            Assert.Less(math.abs(ltg.PlanetSector.x), MAX_PLANETSECTOR);
            Assert.Less(math.abs(ltg.PlanetSector.y), MAX_PLANETSECTOR);
            Assert.Less(math.abs(ltg.PlanetSector.y), MAX_PLANETSECTOR);
        }

        public void AssertMeasurable(LocalToGalaxy ltg) =>
            Assert.IsTrue(math.all(ltg.DistanceMeasureable), "Distance too big to be measurable");

        [Test]
        public void CheckMath()
        {
            Assert.AreEqual(0, 5%5);
            Assert.AreEqual(0, -5%5);
        }

        [Test]
        public void SpecificUsecaseTest1()
        {
            // Log Ingame: (6:3A98, 0:0, 0:0)@float3(2.976562f, 0.04003906f, 0.06054688f) + double3(0.0187210395980439, -0.0403389283651856, -0.0737264584656766) = (6:3A98, FFFFFFFF:FFFE, 1:FFFD)@float3(2.995117f, 0f, 10751.99f)
            var from = new LocalToGalaxy
            {
                StarSector = new int3(0x6, 0, 0),
                PlanetSector = new int3(0x3A98, 0, 0),
                LocalPosition = new float3(2.976562f, 0.04003906f, 0.06054688f)
            };
            var delta = new double3(0.0187210395980439, -0.0403389283651856, -0.0737264584656766);

            // Actual: (6:3A98, FFFFFFFF:FFFE, 1:FFFD)@float3(2.995117f, 0f, 10751.99f)
            // Expected: (6:3A98, FFFFFFFF:FFFE, 1:FFFE)@float3(2.xxx, MAX-0.0403389283651856, 10751.99f)
            var result = from + delta;
            Assert.AreEqual(MAX_PLANETSECTOR - 1, result.PlanetSector.z);
            Assert.AreEqual(MAX_POSITION + from.LocalPosition.y + delta.y, result.LocalPosition.y, 1f);
        }

        [Test]
        public void LtgDistanceCalculationIsInputOrderAgnostic()
        {
            var player = new LocalToGalaxy
            {
                StarSector = new int3(754224, -1712, 144812),
                PlanetSector = new int3(12273, 188061, 175145),
                LocalPosition = new float3(5728.764f, 5078.215f, 6029.149f)
            };
            var target = new LocalToGalaxy
            {
                StarSector = new int3(754224, -1712, 144812),
                PlanetSector = new int3(93751, 130501, 178877),
                LocalPosition = new float3(0, 0, 0)
            };

            var deltaPT = target - player;
            var deltaTP = player - target;
            var distPT = math.length(deltaPT.Distance);
            var distTP = math.length(deltaTP.Distance);
            Assert.AreEqual(distTP, distPT, .1);
        }

        [Test]
        public void GalaxyConstantsCannotCauseOverflow()
        {
            Assert.Less(MAX_POSITION, int.MaxValue * 2L);
            Assert.Less(MAX_PLANETSECTOR, int.MaxValue * 2L);
        }

        [Test]
        [TestCase(
            new[]{0, 0, 0},
            new[]{0, 0, 0},
            new[]{0f, 0f, 0f}, TestName = "All Zero")]
        [TestCase(
            new[]{1, 1, 2},
            new[]{1, 1, 2},
            new[]{1f, 1f, 2f}, TestName = "1+1=2")]
        [TestCase(
            new[]{1, -1, 0},
            new[]{1, -1, 0},
            new[]{1f, -1f, 0f}, TestName = "1-1=0")]
        [TestCase(
            new[]{0, 0, 0},
            new[]{0, 0, 1},
            new[]{0f, MAX_POSITION, 0}, TestName = "Overflow in LocalPosition")]
        [TestCase(
            new[]{5, 0, 5},
            new[]{2, 0, 1},
            new[]{0f, -1, MAX_POSITION - 1}, TestName = "Underflow in LocalPosition")]
        [TestCase(
            new[]{0, 0, -1},
            new[]{0, 0, MAX_PLANETSECTOR - 1},
            new[]{0f, -1f, MAX_POSITION - 1}, TestName = "Double Underflow")]
        [TestCase(
            new[]{2, 0, 1},
            new[]{0, 0, MAX_PLANETSECTOR - 1},
            new[]{0, -0.15f, MAX_POSITION - 0.15f}, TestName = "Double Underflow (small)")]
        [TestCase(
            new[]{0, 0, 0},
            new[]{0, 0, 0},
            new[]{MAX_POSITION, -1f, MAX_POSITION - 1}, TestName = "Barely overflow")]
        [TestCase(
            new[]{0, 0, 0},
            new[]{0, 0, 5},
            new[]{5f * MAX_POSITION, 0, 0}, TestName = "Multiple Overflow in LocalPosition")]
        [TestCase(
            new[]{0, 0, 1},
            new[]{MAX_PLANETSECTOR, 1, 1},
            new[]{0f, 0f, 0f}, TestName = "Overflow in SubSector")]
        [TestCase(
            new[]{0, 0, MAX_STARSECTOR - 1},
            new[]{0, 0, MAX_PLANETSECTOR - 1},
            new[]{0f, -1f, MAX_POSITION - 1}, TestName = "Triple Underflow")]
        [TestCase(
            new[]{MAX_STARSECTOR - 1, 0, 0},
            new[]{0, MAX_PLANETSECTOR, 1},
            new[]{MAX_POSITION, 0f, 0}, TestName = "Triple Overflow")]
        public void TestLocalToGalaxyAddition(int[] SectorsX, int[] SubSectorsX, float[] PositionsX)
        {
            var lhs = new LocalToGalaxy
            {
                StarSector = SectorsX[0],
                PlanetSector = SubSectorsX[0],
                LocalPosition = PositionsX[0]
            };
            var rhs = new LocalToGalaxy
            {
                StarSector = SectorsX[1],
                PlanetSector = SubSectorsX[1],
                LocalPosition = PositionsX[1]
            };
            var sum = lhs + rhs;
            var expectedResult = new LocalToGalaxy
            {
                StarSector = SectorsX[2],
                PlanetSector = SubSectorsX[2],
                LocalPosition = PositionsX[2]
            };
            Debug.Log($"EXPECTED: {expectedResult} from {SectorsX[0]}/{SectorsX[1]}/{SectorsX[2]}");
            Assert.AreEqual(expectedResult.StarSector, sum.StarSector);
            Assert.AreEqual(expectedResult.PlanetSector, sum.PlanetSector);
            Assert.AreEqual(expectedResult.LocalPosition.x, sum.LocalPosition.x, 0.01f);
            Assert.AreEqual(expectedResult.LocalPosition.y, sum.LocalPosition.z, 0.01f);
            Assert.AreEqual(expectedResult.LocalPosition.y, sum.LocalPosition.z, 0.01f);
        }

        [Test]
        [TestCase(
            new[]{0, 0},
            new[]{0, 0},
            new[]{0f, 0f}, TestName = "All Zero")]
        [TestCase(
            new[]{-42, -42},
            new[]{5331, 5331},
            new[]{774f, 774f}, TestName = "Identity")]
        [TestCase(
            new[]{0, 0},
            new[]{0, 1},
            new[]{MAX_POSITION, 0f}, TestName = "Overflow in LocalPosition")]
        [TestCase(
            new[]{0, 0},
            new[]{0, 1},
            new[]{MAX_POSITION + 0.01f, 0.01f}, TestName = "Overflow in LocalPosition (small)")]
        [TestCase(
            new[]{0, MAX_STARSECTOR - 1},
            new[]{0, MAX_PLANETSECTOR - 1},
            new[]{-1f, MAX_POSITION - 1f}, TestName = "Underflow in LocalPosition")]
        [TestCase(
            new[]{0, -1},
            new[]{0, MAX_PLANETSECTOR - 1},
            new[]{-0.01f, MAX_POSITION - 0.01f}, TestName = "Underflow in LocalPosition (small)")]
        [TestCase(
            new[]{0, -1},
            new[]{0, MAX_PLANETSECTOR - 2},
            new[]{-MAX_POSITION - 1f, MAX_POSITION-1f}, TestName = "Underflow Two Subsectors")]
        [TestCase(
            new[]{0, MAX_STARSECTOR - 1},
            new[]{0, MAX_PLANETSECTOR - 1},
            new[]{-1f, MAX_POSITION -1f}, TestName = "Triple Underflow")]
        [TestCase(
            new[]{MAX_STARSECTOR, 7},
            new[]{7 * MAX_PLANETSECTOR, 7},
            new[]{7 * MAX_POSITION, 0f}, TestName = "Triple Overflow x7")]
        [TestCase(
            new[]{0, 3},
            new[]{3 * MAX_PLANETSECTOR, 0},
            new[]{0, 0f}, TestName = "Overflow in Subsector x3")]
        public void TestLocalToGalaxyCorrection(int[] SectorsX, int[] SubSectorsX, float[] PositionsX)
        {
            var corrected = new LocalToGalaxy
            {
                StarSector = SectorsX[0],
                PlanetSector = SubSectorsX[0],
                LocalPosition = PositionsX[0]
            };
            var expectedResult = new LocalToGalaxy
            {
                StarSector = SectorsX[1],
                PlanetSector = SubSectorsX[1],
                LocalPosition = PositionsX[1]
            };
            Debug.Log($"EXP: {expectedResult}, CORR: {corrected}");
            Assert.AreEqual(expectedResult.StarSector, corrected.StarSector);
            Assert.AreEqual(expectedResult.PlanetSector, corrected.PlanetSector);
            Assert.AreEqual(expectedResult.LocalPosition.x, corrected.LocalPosition.x, 0.01f);
            Assert.AreEqual(expectedResult.LocalPosition.y, corrected.LocalPosition.y, 0.01f);
            Assert.AreEqual(expectedResult.LocalPosition.z, corrected.LocalPosition.z, 0.01f);
        }

        [Test]
        [TestCase(1713)]
        [TestCase(-3292)]
        [TestCase(4094)]
        [TestCase(0)]
        [TestCase(-891.172f)]
        [TestCase(0.933f)]
        public void TestRelativePosition1(float dLocal)
        {
            var origin = Center + new LocalToGalaxy {LocalPosition = MAX_POSITION / 2};
            var offset = Center + new LocalToGalaxy {LocalPosition = dLocal};

            VerifyValid(offset);
            AssertMeasurable(offset);
            Assert.AreEqual(dLocal - MAX_POSITION / 2,
                (offset - origin).Distance.x, 0.01f);
        }

        [Test]
        [TestCase(17713)]
        [TestCase(-4492)]
        [TestCase(4812)]
        [TestCase(0)]
        [TestCase(-89123)]
        public void TestRelativePosition2(int dSub)
        {
            var origin = Center + new LocalToGalaxy {PlanetSector = MAX_PLANETSECTOR / 2};
            var offset = Center + new LocalToGalaxy {PlanetSector = dSub};

            VerifyValid(offset);
            AssertMeasurable(offset);
            var target = (dSub - MAX_PLANETSECTOR / 2) * DISTANCE_PER_PLANETSECTOR;
            Assert.AreEqual(target, 
                (offset - origin).Distance.x, 0.01f);
        }

        [Test]
        [TestCase(17326713)]
        [TestCase(-4492)]
        [TestCase(4812)]
        [TestCase(0)]
        [TestCase(-89123782)]
        public void TestRelativePosition3(int dSec)
        {
            var delta = new LocalToGalaxy { StarSector = new int3(dSec, 0, 0) };
            var origin = Center;
            var offset = Center + delta;
            
            VerifyValid(offset);
            AssertMeasurable(offset);
            AssertMeasurable(offset - origin);
            Assert.AreEqual(dSec * DISTANCE_PER_STARSECTOR,
                (offset - origin).Distance.x, 0.01f);
        }

        [Test]
        // Some values are adapted for floating point imprecision. assert.areequal with delta does not work for some reason
        [TestCase(-1782312.2861328125)]
        [TestCase(0)]
        [TestCase(0.025390625)]
        [TestCase(-0.025390625)]
        [TestCase(-72717772)]
        public void TestLocalToGalaxyTransformation(double offset)
        {
            var origin = Center;
            var expected = offset;
            var target = origin + offset;
            AssertMeasurable(target);
            AssertMeasurable(target - origin);
            Assert.AreEqual(expected, (target - origin).Distance.x);
        }

        [Test]
        [TestCase(0, 
            new[]{ 0, 0 }, new[]{ 0, 0 }, new[]{ 0f, 0f }, 
            TestName = "All Zero")]
        [TestCase(0, 
            new[]{ 5, 5 }, new[]{ 24, 24 }, new[]{ 3f, 3f }, 
            TestName = "Identity")]
        [TestCase(0.001953, 
            new[]{ 5, 5 }, new[]{ 24, 24 }, new[]{ 3f, 3.0015f }, 
            TestName = "Small Increase")]
        [TestCase(-1, 
            new[]{ 0, 0 }, new[]{ 25, 24 }, new[]{ 0f, MAX_POSITION - 1 }, 
            TestName = "LocalPosition Underflow")]
        [TestCase(-0.0015, 
            new[]{ 0, 0 }, new[]{ 25, 24 }, new[]{ 0f, MAX_POSITION - 0.0015f }, 
            TestName = "LocalPosition Underflow (small)")]
        [TestCase(1, 
            new[]{ 0, 0 }, new[]{ 24, 25 }, new[]{ MAX_POSITION - 1f, 0 }, 
            TestName = "LocalPosition Overflow")]
        [TestCase(0.0015, 
            new[]{ 0, 0 }, new[]{ 24, 25 }, new[]{ MAX_POSITION - 0.0015f, 0 }, 
            TestName = "LocalPosition Overflow (small)")]
        [TestCase(1, 
            new[]{ 0, 1 }, new[]{ MAX_PLANETSECTOR - 1, 0 }, new[]{ MAX_POSITION - 1f, 0 }, 
            TestName = "Double Overflow")]
        [TestCase(0.0015, 
            new[]{ 0, 1 }, new[]{ MAX_PLANETSECTOR - 1, 0 }, new[]{ MAX_POSITION - 0.0015f, 0 }, 
            TestName = "Double Overflow (small)")]
        [TestCase(-1, 
            new[]{ 0, MAX_STARSECTOR - 1 }, new[]{ 0, MAX_PLANETSECTOR - 1 }, new[]{ 0, MAX_POSITION - 1f }, 
            TestName = "Double Underflow")]
        [TestCase(-0.0015, 
            new[]{ 0, MAX_STARSECTOR - 1 }, new[]{ 0, MAX_PLANETSECTOR - 1 }, new[]{ 0, MAX_POSITION - 0.0015f }, 
            TestName = "Double Underflow (small)")]
        public void TestOffsetToLocalToGalaxy(double offset, int[] sector, int[] subsector, float[] position)
        {
            const double MAX_ABS_POSITION = int.MaxValue * DISTANCE_PER_STARSECTOR;
            var origin = new LocalToGalaxy
            {
                StarSector = sector[0], 
                PlanetSector = subsector[0], 
                LocalPosition = position[0]
            };

            Assert.Less(offset, MAX_ABS_POSITION);
            var target = origin + offset;

            Assert.AreEqual(sector[1], target.StarSector.x);
            Assert.AreEqual(subsector[1], target.PlanetSector.x);
            Assert.AreEqual(position[1], target.LocalPosition.x, delta: 0.01f);
        }
    }
}
