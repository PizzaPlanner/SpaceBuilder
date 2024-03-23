using NUnit.Framework;
using Unity.Mathematics;

namespace Grid.Tests
{
    public class GridToolsTests
    {
        [Test]
        public void SanityCheck()
        {
            var forward = new float3(5,2, 8);
            forward = math.normalize(forward);
            var targetRotationSafe = quaternion.LookRotationSafe(forward, math.up());
            var targetRotationUnsafe = quaternion.LookRotation(forward, math.up());
            
            var targetUpSafe = math.mul(targetRotationSafe, math.up());
            var targetUpUnsafe = math.mul(targetRotationUnsafe, math.up());
            
            Assert.AreEqual(1, math.length(forward));
            Assert.AreEqual(math.up(), targetUpUnsafe, "Unsafe Check");
            Assert.AreEqual(math.up(), targetUpSafe, "Safe Check");
        }
    }
}
