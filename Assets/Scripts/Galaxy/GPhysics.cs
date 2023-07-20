using Unity.Mathematics;

public static class GPhysics
{
    // https://tavianator.com/2011/ray_box.html
    public static bool Intersects(this AABB aabb, float3 rayOrigin, float3 rayDirectionInverseNormalized)
    {
        var t1 = (aabb.Min - rayOrigin) * rayDirectionInverseNormalized;
        var t2 = (aabb.Max - rayOrigin) * rayDirectionInverseNormalized;

        var tmin = math.min(t1, t2);
        var tmax = math.max(t1, t2);

        return math.max(math.max(tmin.x, tmin.y), tmin.z) >= math.min(math.min(tmax.x, tmax.y), tmax.z);
    }
}
