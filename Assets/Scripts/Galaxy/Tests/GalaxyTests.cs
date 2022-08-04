using System;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using static Constants;

namespace Galaxy.Tests
{

    public class GalaxyTests
    {

        public void VerifyValid(LocalToGalaxy ltg)
        {
            Assert.Less(math.abs(ltg.LocalPosition.x), MAX_POSITION);
            Assert.Less(math.abs(ltg.LocalPosition.y), MAX_POSITION);
            Assert.Less(math.abs(ltg.LocalPosition.z), MAX_POSITION);

            Assert.Less(math.abs(ltg.SubSector.x), MAX_SUBSECTOR);
            Assert.Less(math.abs(ltg.SubSector.y), MAX_SUBSECTOR);
            Assert.Less(math.abs(ltg.SubSector.y), MAX_SUBSECTOR);
        }

        [Test]
        public void CheckMath()
        {
            Assert.AreEqual(0, 5%5);
            Assert.AreEqual(0, -5%5);
        }

        [Test]
        public void WtfIsGoingOn()
        {
            // Log Ingame: (6:3A98, 0:0, 0:0)@float3(2.976562f, 0.04003906f, 0.06054688f) + double3(0.0187210395980439, -0.0403389283651856, -0.0737264584656766) = (6:3A98, FFFFFFFF:FFFE, 1:FFFD)@float3(2.995117f, 0f, 10751.99f)
            var from = new LocalToGalaxy
            {
                Sector = new int3(0x6, 0, 0),
                SubSector = new int3(0x3A98, 0, 0),
                LocalPosition = new float3(2.976562f, 0.04003906f, 0.06054688f)
            };
            var delta = new double3(0.0187210395980439, -0.0403389283651856, -0.0737264584656766);

            // Actual: (6:3A98, FFFFFFFF:FFFE, 1:FFFD)@float3(2.995117f, 0f, 10751.99f)
            // Expected: (6:3A98, FFFFFFFF:FFFE, 1:FFFE)@float3(2.xxx, MAX-0.0403389283651856, 10751.99f)
            var result = LocalToGalaxy.OffsetBy(from, delta);
            Assert.AreEqual((int)MAX_SUBSECTOR - 1, result.SubSector.z);
            Assert.AreEqual((int)MAX_POSITION + from.LocalPosition.y + delta.y, result.LocalPosition.y);
        }

        [Test]
        public void GalaxyConstantsCannotCauseOverflow()
        {
            Assert.Less(MAX_POSITION, int.MaxValue * 2L);
            Assert.Less(MAX_SUBSECTOR, int.MaxValue * 2L);
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
            new[]{0f, (int)MAX_POSITION, 0}, TestName = "Overflow in LocalPosition")]
        [TestCase(
            new[]{5, 0, 5},
            new[]{2, 0, 1},
            new[]{0f, -1, (int)MAX_POSITION - 1}, TestName = "Underflow in LocalPosition")]
        [TestCase(
            new[]{0, 0, -1},
            new[]{0, 0, (int)MAX_SUBSECTOR - 1},
            new[]{0f, -1f, (int)MAX_POSITION - 1}, TestName = "Double Underflow")]
        [TestCase(
            new[]{2, 0, 1},
            new[]{0, 0, (int)MAX_SUBSECTOR - 1},
            new[]{0, -0.15f, (int)MAX_POSITION - 0.15f}, TestName = "Double Underflow (small)")]
        [TestCase(
            new[]{0, 0, 0},
            new[]{0, 0, 0},
            new[]{-1f, (int)MAX_POSITION, (int)MAX_POSITION - 1}, TestName = "Barely overflow")]
        [TestCase(
            new[]{0, 0, 0},
            new[]{0, 0, 5},
            new[]{5f * (int)MAX_POSITION, 0, 0}, TestName = "Multiple Overflow in LocalPosition")]
        [TestCase(
            new[]{0, 0, 1},
            new[]{(int)MAX_SUBSECTOR, 1, 1},
            new[]{0f, 0f, 0f}, TestName = "Overflow in SubSector")]
        [TestCase(
            new[]{int.MinValue, 0, int.MaxValue},
            new[]{0, 0, (int)MAX_SUBSECTOR - 1},
            new[]{0f, -1f, (int)MAX_POSITION - 1}, TestName = "Triple Underflow")]
        [TestCase(
            new[]{int.MaxValue, 0, int.MinValue},
            new[]{0, (int)MAX_SUBSECTOR, 1},
            new[]{(int)MAX_POSITION, 0f, 0}, TestName = "Triple Overflow")]
        public void TestLocalToGalaxyAddition(int[] SectorsX, int[] SubSectorsX, float[] PositionsX)
        {
            var lhs = new LocalToGalaxy
            {
                Sector = SectorsX[0],
                SubSector = SubSectorsX[0],
                LocalPosition = PositionsX[0]
            };
            var rhs = new LocalToGalaxy
            {
                Sector = SectorsX[1],
                SubSector = SubSectorsX[1],
                LocalPosition = PositionsX[1]
            };
            var sum = lhs + rhs;
            var expectedResult = new LocalToGalaxy
            {
                Sector = SectorsX[2],
                SubSector = SubSectorsX[2],
                LocalPosition = PositionsX[2]
            };
            Assert.AreEqual(expectedResult.Sector, sum.Sector);
            Assert.AreEqual(expectedResult.SubSector, sum.SubSector);
            Assert.AreEqual(expectedResult.LocalPosition.x, sum.LocalPosition.x, 0.0001f);
            Assert.AreEqual(expectedResult.LocalPosition.y, sum.LocalPosition.z, 0.0001f);
            Assert.AreEqual(expectedResult.LocalPosition.y, sum.LocalPosition.z, 0.0001f);
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
            new[]{(int)MAX_POSITION, 0f}, TestName = "Overflow in LocalPosition")]
        [TestCase(
            new[]{0, 0},
            new[]{0, 1},
            new[]{(int)MAX_POSITION + 0.01f, 0.01f}, TestName = "Overflow in LocalPosition (small)")]
        [TestCase(
            new[]{0, -1},
            new[]{0, (int)MAX_SUBSECTOR - 1},
            new[]{-1f, (int)MAX_POSITION - 1f}, TestName = "Underflow in LocalPosition")]
        [TestCase(
            new[]{0, -1},
            new[]{0, (int)MAX_SUBSECTOR - 1},
            new[]{-0.01f, (int)MAX_POSITION - 0.01f}, TestName = "Underflow in LocalPosition (small)")]
        [TestCase(
            new[]{0, -1},
            new[]{0, (int)MAX_SUBSECTOR - 2},
            new[]{-(int)MAX_POSITION - 1f, (int)MAX_POSITION - 1f}, TestName = "Underflow Two Subsectors")]
        [TestCase(
            new[]{int.MinValue, int.MaxValue},
            new[]{0, (int)MAX_SUBSECTOR - 1},
            new[]{-1f, (int)MAX_POSITION -1f}, TestName = "Triple Underflow")]
        [TestCase(
            new[]{int.MaxValue, int.MinValue},
            new[]{(int)MAX_SUBSECTOR, 1},
            new[]{(int)MAX_POSITION, 0f}, TestName = "Triple Overflow")]
        [TestCase(
            new[]{int.MaxValue, int.MinValue + 6},
            new[]{7 * (int)MAX_SUBSECTOR, 7},
            new[]{7 * (int)MAX_POSITION, 0f}, TestName = "Triple Overflow x7")]
        [TestCase(
            new[]{int.MaxValue, int.MinValue + 2},
            new[]{3 * (int)MAX_SUBSECTOR, 0},
            new[]{0, 0f}, TestName = "Overflow in Subsector x3")]
        public void TestLocalToGalaxyCorrection(int[] SectorsX, int[] SubSectorsX, float[] PositionsX)
        {
            var uncorrected = new LocalToGalaxy
            {
                Sector = SectorsX[0],
                SubSector = SubSectorsX[0],
                LocalPosition = PositionsX[0]
            };
            var expectedResult = new LocalToGalaxy
            {
                Sector = SectorsX[1],
                SubSector = SubSectorsX[1],
                LocalPosition = PositionsX[1]
            };
            var corrected = LocalToGalaxy.Corrected(uncorrected);
            Assert.AreEqual(expectedResult.Sector, corrected.Sector);
            Assert.AreEqual(expectedResult.SubSector, corrected.SubSector);
            Assert.AreEqual(expectedResult.LocalPosition.x, corrected.LocalPosition.x, 0.0001f);
            Assert.AreEqual(expectedResult.LocalPosition.y, corrected.LocalPosition.z, 0.0001f);
            Assert.AreEqual(expectedResult.LocalPosition.y, corrected.LocalPosition.z, 0.0001f);
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
            var origin = new LocalToGalaxy {LocalPosition = MAX_POSITION / 2};
            var offset = new LocalToGalaxy {LocalPosition = dLocal};

            VerifyValid(offset);
            Assert.AreEqual(dLocal- origin.LocalPosition.x, LocalToGalaxy.RelativePosition(origin, offset).x);
        }

        [Test]
        [TestCase(17713)]
        [TestCase(-4492)]
        [TestCase(4812)]
        [TestCase(0)]
        [TestCase(-89123)]
        public void TestRelativePosition2(int dSub)
        {
            var origin = new LocalToGalaxy {SubSector = (int)MAX_SUBSECTOR / 2};
            var offset = new LocalToGalaxy {SubSector = dSub};

            VerifyValid(offset);
            Assert.AreEqual(dSub * MAX_POSITION - origin.SubSector.x, LocalToGalaxy.RelativePosition(origin, offset).x);
        }

        [Test]
        [TestCase(17326713)]
        [TestCase(-4492)]
        [TestCase(4812)]
        [TestCase(0)]
        [TestCase(-89123782)]
        public void TestRelativePosition3(int dSec)
        {
            var origin = new LocalToGalaxy();
            var offset = new LocalToGalaxy {Sector = new int3(dSec, 0, 0)};

            VerifyValid(offset);
            Assert.AreEqual(dSec * DISTANCE_PER_SECTOR,
                LocalToGalaxy.RelativePosition(origin, offset).x);
        }

        [Test]
        // Some values are adapted for floating point imprecision. assert.areequal with delta does not work for some reason
        [TestCase(17281321557136781423.2615)]
        [TestCase(-1782312.2861328125)]
        [TestCase(0)]
        [TestCase(0.025390625)]
        [TestCase(-72717772)]
        [TestCase(2e+21d)]
        public void TestLocalToGalaxyTransformation(double offset)
        {
            const double MAX_ABS_POSITION = (double) int.MaxValue * DISTANCE_PER_SECTOR;

            var origin = new LocalToGalaxy();
            Assert.Less(offset, MAX_ABS_POSITION);
            var target = LocalToGalaxy.OffsetBy(origin, offset);

            VerifyValid(target);
            Assert.AreEqual(offset, LocalToGalaxy.RelativePosition(origin, target).x);
        }

        [Test]
        [TestCase(0, new[]{ 0, 0 }, new[]{ 0, 0 }, new[]{ 0f, 0f }, TestName = "All Zero")]
        [TestCase(0, new[]{ 5, 5 }, new[]{ 24, 24 }, new[]{ 3f, 3f }, TestName = "Identity")]
        [TestCase(0.001953, new[]{ 5, 5 }, new[]{ 24, 24 }, new[]{ 3f, 3.0015f }, TestName = "Small Increase")]
        [TestCase(-1, new[]{ 0, 0 }, new[]{ 25, 24 }, new[]{ 0f, (int)MAX_POSITION - 1 }, TestName = "LocalPosition Underflow")]
        [TestCase(-0.0015, new[]{ 0, 0 }, new[]{ 25, 24 }, new[]{ 0f, (int)MAX_POSITION - 0.0015f }, TestName = "LocalPosition Underflow (small)")]
        [TestCase(1, new[]{ 0, 0 }, new[]{ 24, 25 }, new[]{ (int)MAX_POSITION - 1f, 0 }, TestName = "LocalPosition Overflow")]
        [TestCase(0.0015, new[]{ 0, 0 }, new[]{ 24, 25 }, new[]{ (int)MAX_POSITION - 0.0015f, 0 }, TestName = "LocalPosition Overflow (small)")]
        [TestCase(1, new[]{ 0, 1 }, new[]{ (int)MAX_SUBSECTOR - 1, 0 }, new[]{ (int)MAX_POSITION - 1f, 0 }, TestName = "Double Overflow")]
        [TestCase(0.0015, new[]{ 0, 1 }, new[]{ (int)MAX_SUBSECTOR - 1, 0 }, new[]{ (int)MAX_POSITION - 0.0015f, 0 }, TestName = "Double Overflow (small)")]
        [TestCase(-1, new[]{ 0, -1 }, new[]{ 0, (int)MAX_SUBSECTOR - 1 }, new[]{ 0, (int)MAX_POSITION - 1f }, TestName = "Double Underflow")]
        [TestCase(-0.0015, new[]{ 0, -1 }, new[]{ 0, (int)MAX_SUBSECTOR - 1 }, new[]{ 0, (int)MAX_POSITION - 0.0015f }, TestName = "Double Underflow (small)")]
        public void TestOffsetToLocalToGalaxy(double offset, int[] sector, int[] subsector, float[] position)
        {
            const double MAX_ABS_POSITION = (double) int.MaxValue * DISTANCE_PER_SECTOR;
            var origin = new LocalToGalaxy{Sector = sector[0], SubSector = subsector[0], LocalPosition = position[0]};

            Assert.Less(offset, MAX_ABS_POSITION);
            var target = LocalToGalaxy.OffsetBy(origin, offset);

            Assert.AreEqual(sector[1], target.Sector.x);
            Assert.AreEqual(subsector[1], target.SubSector.x);
            Assert.AreEqual(position[1], target.LocalPosition.x);
        }
    }
}
