using System.Runtime.CompilerServices;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class GridTools
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int mod(int x, int m) {
        return (x%m + m)%m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int2 mod(int2 x, int2 m) {
        return (x%m + m)%m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int3 mod(int3 x, int3 m) {
        return (x%m + m)%m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float mod(float x, int m) {
        return (x%m + m)%m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 mod(float2 x, int2 m) {
        return (x%m + m)%m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 mod(float3 x, int3 m) {
        return (x%m + m)%m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double mod(double x, int m) {
        return (x%m + m)%m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double2 mod(double2 x, int2 m) {
        return (x%m + m)%m;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double3 mod(double3 x, int3 m) {
        return (x%m + m)%m;
    }

    public static void Rotate(this ref int3 value, quaternion rotation) =>
        value = (int3)math.round(math.mul(rotation, value));

    public static int3 Rotated(this int3 value, quaternion rotation) =>
        (int3)math.round(math.mul(rotation, value));

    public static GridItem Rotated(this GridItem original, quaternion rotation)
    {
        original.Airtight = original.Airtight.Rotate(rotation);
        original.Occupied = original.Occupied.Rotate(rotation);
        return original;
    }

    public static BoundingBox Rotated(this BoundingBox boundingBox, quaternion rotation)
    {
        var center = boundingBox.Center;
        boundingBox.Origin = (int3)math.round(math.rotate(rotation, boundingBox.Origin - center) + center);
        boundingBox.Extents = (int3)math.round(math.rotate(rotation, boundingBox.Extents));
        return boundingBox;
    }

    // Source: https://www.scratchapixel.com/lessons/3d-basic-rendering/minimal-ray-tracer-rendering-simple-shapes/ray-plane-and-ray-disk-intersection
    public static bool RayIntersectsPlane(float3 ray, float3 rayOrigin, float3 planeOrigin, float3 planeNormal, float maxRange, out float3 hit)
    {
        hit = 0;
        var denom = math.dot(planeNormal, ray);
        if (denom > 1e-6)
        {
                var p0l0 = planeOrigin - rayOrigin;
                var t = math.dot(p0l0, planeNormal) / denom;
                hit = rayOrigin + ray * t;
                return t >= 0 && t <= maxRange;
        }
        return false;
    }

    public static bool GetLookedAtPointOnPlanesFromObject(LocalToWorld cameraLtw, LocalToWorld gridLtw, int3 targetObjectPosition, float maxRange, out int3 position)
    {
        var planeOrigin = Grid.PositionFromCell(gridLtw, targetObjectPosition);
        var rPos = float3.zero;

        var f = math.abs(math.dot(cameraLtw.Forward, gridLtw.Forward));
        var r = math.abs(math.dot(cameraLtw.Forward, gridLtw.Right));
        var u = math.abs(math.dot(cameraLtw.Forward, gridLtw.Up));

        var first = f >= r && f >= u ? gridLtw.Forward : r >= f && r >= u ? gridLtw.Right : gridLtw.Up;
        var second =
            math.all(first == gridLtw.Forward) ? (r >= u ? gridLtw.Right : gridLtw.Up) :
            math.all(first == gridLtw.Right) ? (f >= u ? gridLtw.Forward : gridLtw.Up) :
            f >= r ? gridLtw.Forward : gridLtw.Right;
        var third =
            math.all(first != gridLtw.Forward) && math.all(second != gridLtw.Forward) ? gridLtw.Forward :
            math.all(first != gridLtw.Right) && math.all(second != gridLtw.Right) ? gridLtw.Right :
            gridLtw.Up;

        var hit =
            RayIntersectsPlane(
                cameraLtw.Forward, cameraLtw.Position,
                planeOrigin, first,
                maxRange, out rPos) ||
            RayIntersectsPlane(
                cameraLtw.Forward, cameraLtw.Position,
                planeOrigin, -first,
                maxRange, out rPos) ||
            RayIntersectsPlane(
                cameraLtw.Forward, cameraLtw.Position,
                planeOrigin, second,
                maxRange, out rPos) ||
            RayIntersectsPlane(
                cameraLtw.Forward, cameraLtw.Position,
                planeOrigin, -second,
                maxRange, out rPos) ||
            RayIntersectsPlane(
                cameraLtw.Forward, cameraLtw.Position,
                planeOrigin, third,
                maxRange, out rPos);
            RayIntersectsPlane(
                cameraLtw.Forward, cameraLtw.Position,
                planeOrigin, -third,
                maxRange, out rPos);

        position = hit ? Grid.CellFromPosition(gridLtw, rPos) : 0;
        return hit;
    }
}
