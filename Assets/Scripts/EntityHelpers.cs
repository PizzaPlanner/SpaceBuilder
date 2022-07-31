using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class EntityHelpers
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int mod(this int i1, int i2)
    {
        return (i1 < 0 ? (i1 + i2) : i1) % i2;
    }

    public static Entity GetInChildren<T>(this EntityManager manager, Entity root) where T: IComponentData
    {
        if (manager.HasComponent<T>(root))
            return root;

        if(!manager.HasComponent<Child>(root))
            return Entity.Null;

        var buffer = manager.GetBuffer<Child>(root);
        if (buffer.IsEmpty)
            return Entity.Null;
        for (int i = 0; i < buffer.Length; i++)
        {
            var res = GetInChildren<T>(manager, buffer[i].Value);
            if (res != Entity.Null)
                return res;
        }

        return Entity.Null;
    }

    public static IEnumerable<Entity> GetAllChildren(this BufferFromEntity<Child> buffer, Entity root)
    {
        yield return root;
        if (buffer.HasComponent(root))
            foreach (var child in buffer[root])
            foreach (var item in GetAllChildren(buffer, child.Value))
                yield return item;
    }

    public static Entity FindParentWith<T>(this SystemBase system, Entity root) where T: IComponentData
    {
        var cdfe = system.GetComponentDataFromEntity<Parent>();
        while (root != Entity.Null && cdfe.HasComponent(root))
        {
            if (system.EntityManager.HasComponent<T>(root))
                return root;
            root = cdfe[root].Value;
        }

        return Entity.Null;
    }

    [BurstCompile]
    public static GridItem.Side Rotate(this GridItem.Side side, quaternion rotation)
    {
        var f = math.mul(rotation, math.forward());
        var fmax = math.abs(f);
        var forward = (int3)math.sign(new float3(
            fmax.x > fmax.y && fmax.x > fmax.z? f.x : 0,
            fmax.y > fmax.x && fmax.y > fmax.z? f.y : 0,
            fmax.z > fmax.y && fmax.z > fmax.x? f.z : 0
        ));
        var r = math.mul(rotation, math.right());
        var rmax = math.abs(r);
        var right = (int3)math.sign(new float3(
            rmax.x > rmax.y && rmax.x > rmax.z? r.x : 0,
            rmax.y > rmax.x && rmax.y > rmax.z? r.y : 0,
            rmax.z > rmax.y && rmax.z > rmax.x? r.z : 0
        ));
        var u = math.mul(rotation, math.up());
        var umax = math.abs(u);
        var up = (int3)math.sign(new float3(
            umax.x > umax.y && umax.x > umax.z? u.x : 0,
            umax.y > umax.x && umax.y > umax.z? u.y : 0,
            umax.z > umax.y && umax.z > umax.x? u.z : 0
        ));

        var sFront = (GridItem.Side.Front & side) != 0;
        var sBack  = (GridItem.Side.Back & side)  != 0;
        var sUp    = (GridItem.Side.Up & side)    != 0;
        var sDown  = (GridItem.Side.Down & side)  != 0;
        var sLeft  = (GridItem.Side.Left & side)  != 0;
        var sRight = (GridItem.Side.Right & side) != 0;

        GridItem.Side newside = GridItem.Side.Center & side;

        if (sFront)
        {
            if (forward.x == 1)       newside |= GridItem.Side.Right;
            else if (forward.x == -1) newside |= GridItem.Side.Left;
            else if (forward.y == 1)  newside |= GridItem.Side.Up;
            else if (forward.y == -1) newside |= GridItem.Side.Down;
            else if (forward.z == 1)  newside |= GridItem.Side.Front;
            else if (forward.z == -1) newside |= GridItem.Side.Back;
        }
        if (sBack)
        {
            if (forward.x == -1)      newside |= GridItem.Side.Right;
            else if (forward.x == 1)  newside |= GridItem.Side.Left;
            else if (forward.y == -1) newside |= GridItem.Side.Up;
            else if (forward.y == 1)  newside |= GridItem.Side.Down;
            else if (forward.z == -1) newside |= GridItem.Side.Front;
            else if (forward.z == 1)  newside |= GridItem.Side.Back;
        }
        if (sUp)
        {
            if (up.x == 1)       newside |= GridItem.Side.Right;
            else if (up.x == -1) newside |= GridItem.Side.Left;
            else if (up.y == 1)  newside |= GridItem.Side.Up;
            else if (up.y == -1) newside |= GridItem.Side.Down;
            else if (up.z == 1)  newside |= GridItem.Side.Front;
            else if (up.z == -1) newside |= GridItem.Side.Back;
        }
        if (sDown)
        {
            if (up.x == -1)      newside |= GridItem.Side.Right;
            else if (up.x == 1)  newside |= GridItem.Side.Left;
            else if (up.y == -1) newside |= GridItem.Side.Up;
            else if (up.y == 1)  newside |= GridItem.Side.Down;
            else if (up.z == -1) newside |= GridItem.Side.Front;
            else if (up.z == 1)  newside |= GridItem.Side.Back;
        }
        if (sRight)
        {
            if (right.x == 1)       newside |= GridItem.Side.Right;
            else if (right.x == -1) newside |= GridItem.Side.Left;
            else if (right.y == 1)  newside |= GridItem.Side.Up;
            else if (right.y == -1) newside |= GridItem.Side.Down;
            else if (right.z == 1)  newside |= GridItem.Side.Front;
            else if (right.z == -1) newside |= GridItem.Side.Back;
        }
        if (sLeft)
        {
            if (right.x == -1)      newside |= GridItem.Side.Right;
            else if (right.x == 1)  newside |= GridItem.Side.Left;
            else if (right.y == -1) newside |= GridItem.Side.Up;
            else if (right.y == 1)  newside |= GridItem.Side.Down;
            else if (right.z == -1) newside |= GridItem.Side.Front;
            else if (right.z == 1)  newside |= GridItem.Side.Back;
        }
        return newside;
    }
}
