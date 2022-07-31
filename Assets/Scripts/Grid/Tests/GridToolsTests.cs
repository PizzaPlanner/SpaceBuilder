using NUnit.Framework;
using Unity.Mathematics;

namespace Grid.Tests
{
    public class GridToolsTests
    {

        [Test]
        public void TestBoundingBoxRotatesCorrectly()
        {
            var bbox = new BoundingBox();
            bbox.Origin = new int3(1, 0, 0);
            bbox.Extents = new int3(0, 0, 2);

            /* Pre rotation:
             * .  |_| <- (1,2)
             * .  |_| <- center
             * .  |o| <- origin
             * ^ (0,0)
             */

            Assert.AreEqual(new float3(1, 0, 1), bbox.Center);

            var rotation = quaternion.AxisAngle(math.up(), 0.5f * math.PI);

            var rotatedBbox = bbox.Rotated(rotation);

            /* Expected post rotation:
             * .  .  .
             * |o||_||_|
             * .  .  .
             * ^ (0,0)
             */

            Assert.AreEqual(new float3(1, 0, 1), bbox.Center);
            Assert.AreEqual(new int3(0, 0, 1), rotatedBbox.Origin);
            Assert.AreEqual(new int3(2, 0, 0), rotatedBbox.Extents);
        }
    }
}
