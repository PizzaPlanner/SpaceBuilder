using System;
using NUnit.Framework;
using Unity.Mathematics;
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
        [TestCase(new[]{0, 0, 0}, new[]{0, 0, 0}, new[]{0f, 0f, 0f}, TestName = "All Zero")]
        [TestCase(new[]{1, 1, 2}, new[]{1, 1, 2}, new[]{1f, 1f, 2f}, TestName = "1+1=2")]
        [TestCase(new[]{1, -1, 0}, new[]{1, -1, 0}, new[]{1f, -1f, 0f}, TestName = "1-1=0")]
        [TestCase(new[]{0, 0, 0}, new[]{0, 0, 1}, new[]{0f, MAX_POSITION, 0}, TestName = "Overflow in LocalPosition")]
        [TestCase(new[]{0, 0, -1}, new[]{0, 0, MAX_SUBSECTOR - 1}, new[]{0f, -1f, MAX_POSITION - 1}, TestName = "Double Underflow")]
        [TestCase(new[]{0, 0, 0}, new[]{0, 0, 0}, new[]{-1f, MAX_POSITION, MAX_POSITION - 1}, TestName = "Barely overflow")]
        [TestCase(new[]{0, 0, 0}, new[]{0, 0, 5}, new[]{5f * MAX_POSITION, 0, 0}, TestName = "Multiple Overflow in LocalPosition")]
        [TestCase(new[]{0, 0, 1}, new[]{MAX_SUBSECTOR, 1, 1}, new[]{0f, 0f, 0f}, TestName = "Overflow in SubSector")]
        [TestCase(new[]{int.MinValue, 0, int.MaxValue}, new[]{0, 0, MAX_SUBSECTOR - 1}, new[]{0f, -1f, MAX_POSITION - 1}, TestName = "Triple Underflow")]
        [TestCase(new[]{int.MaxValue, 0, int.MinValue}, new[]{0, MAX_POSITION, 1}, new[]{MAX_POSITION, 0f, 0}, TestName = "Triple Overflow")]
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
            Assert.AreEqual(expectedResult.LocalPosition, sum.LocalPosition);
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
            Assert.AreEqual(origin.LocalPosition.x - dLocal, LocalToGalaxy.RelativePosition(origin, offset).x);
        }

        [Test]
        [TestCase(17713)]
        [TestCase(-4492)]
        [TestCase(4812)]
        [TestCase(0)]
        [TestCase(-89123)]
        public void TestRelativePosition2(int dSub)
        {
            var origin = new LocalToGalaxy {SubSector = MAX_SUBSECTOR / 2};
            var offset = new LocalToGalaxy {SubSector = dSub};

            VerifyValid(offset);
            Assert.AreEqual(origin.SubSector.x - dSub * (double)MAX_POSITION, LocalToGalaxy.RelativePosition(origin, offset).x);
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
            Assert.AreEqual(dSec * (double)MAX_POSITION * 2 * MAX_SUBSECTOR * 2,
                LocalToGalaxy.RelativePosition(origin, offset).x);
        }

        [Test]
        [TestCase(17281321557136781423.2615)]
        [TestCase(-178231789412.21)]
        [TestCase(0)]
        [TestCase(0.00003)]
        [TestCase(-72717772)]
        [TestCase(6e+21d)]
        public void TestLocalToGalaxyTransformation(double offset)
        {
            const double MAX_POSITION = (double) int.MaxValue *
                                        MAX_SUBSECTOR *
                                        Constants.MAX_POSITION;

            var origin = new LocalToGalaxy();
            Assert.Less(offset, MAX_POSITION);
            var target = LocalToGalaxy.OffsetBy(origin, offset);

            VerifyValid(target);
            Assert.AreEqual(offset, LocalToGalaxy.RelativePosition(origin, target).x);
        }
    }
}
