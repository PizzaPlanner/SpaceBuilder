using System;
using Unity.Mathematics;

namespace Grid.Essentials
{
    public struct Cell : IEquatable<Cell>
    {
        public short x, y, z;
        public static implicit operator Cell(int3 data) => new Cell
        {
            x = (short)data.x, 
            y = (short)data.y, 
            z = (short)data.z
        };
        
        public static implicit operator int3(Cell data) => new int3
        {
            x = data.x, 
            y = data.y, 
            z = data.z
        };

        public bool Equals(Cell other)
        {
            return x == other.x && y == other.y && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is Cell other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(x, y, z);
        }
    }
}