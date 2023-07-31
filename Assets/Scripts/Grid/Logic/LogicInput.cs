using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Grid.Logic
{
    public struct LogicInput
    {
        public byte FromPort;
        public Entity FromEntity;
    }
}