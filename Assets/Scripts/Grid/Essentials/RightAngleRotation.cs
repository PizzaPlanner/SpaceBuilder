using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public struct RightAngleRotation
{
    [SerializeField] byte Value;

    public RightAngleRotation(byte b)
    {
        Value = b;
    }

    public RightAngleRotation(quaternion q)
    {
        Value = 0;

        var forward = GridItem.Side.Front.Rotate(q);
        if      (forward == GridItem.Side.Left)  Value = 0b0000_1001;
        else if (forward == GridItem.Side.Right) Value = 0b0000_0001;
        else if (forward == GridItem.Side.Down)  Value = 0b0000_1010;
        else if (forward == GridItem.Side.Up)    Value = 0b0000_0010;
        else if (forward == GridItem.Side.Back)  Value = 0b0000_1100;
        else if (forward == GridItem.Side.Front) Value = 0b0000_0100;

        var up = GridItem.Side.Up.Rotate(q);
        if      (up == GridItem.Side.Left)  Value += 0b1001_0000;
        else if (up == GridItem.Side.Right) Value += 0b0001_0000;
        else if (up == GridItem.Side.Down)  Value += 0b1010_0000;
        else if (up == GridItem.Side.Up)    Value += 0b0010_0000;
        else if (up == GridItem.Side.Back)  Value += 0b1100_0000;
        else if (up == GridItem.Side.Front) Value += 0b0100_0000;
    }

    public GridItem.Side Forward
    {
        get
        {
            if ((Value & 0b0000_1001) != 0) return GridItem.Side.Left;
            if ((Value & 0b0000_0001) != 0) return GridItem.Side.Right;
            if ((Value & 0b0000_1010) != 0) return GridItem.Side.Down;
            if ((Value & 0b0000_0010) != 0) return GridItem.Side.Up;
            if ((Value & 0b0000_1100) != 0) return GridItem.Side.Back;
            if ((Value & 0b0000_0100) != 0) return GridItem.Side.Front;
            return 0;
        }
    }

    public GridItem.Side Up
    {
        get
        {
            if ((Value & 0b1001_0000) != 0) return GridItem.Side.Left;
            if ((Value & 0b0001_0000) != 0) return GridItem.Side.Right;
            if ((Value & 0b1010_0000) != 0) return GridItem.Side.Down;
            if ((Value & 0b0010_0000) != 0) return GridItem.Side.Up;
            if ((Value & 0b1100_0000) != 0) return GridItem.Side.Back;
            if ((Value & 0b0100_0000) != 0) return GridItem.Side.Front;
            return 0;
        }
    }

    public static implicit operator quaternion(RightAngleRotation rar)
    {
        if(rar.Value == 0)
            return quaternion.identity;

        var fsign = (rar.Value & 0b0000_1000) != 0 ? -1 : 1;
        var usign = (rar.Value & 0b1000_0000) != 0 ? -1 : 1;
        var forward = new float3(
            math.sign((rar.Value & 0b0000_0001) * fsign),
            math.sign((rar.Value & 0b0000_0010) * fsign),
            math.sign((rar.Value & 0b0000_0100) * fsign));
        var up = new float3(
            math.sign((rar.Value & 0b0001_0000) * usign),
            math.sign((rar.Value & 0b0010_0000) * usign),
            math.sign((rar.Value & 0b0100_0000) * usign));
        return math.abs(math.dot(forward, up)) < 0.1f ? quaternion.LookRotation(forward, up) : quaternion.identity;
    }

    public static bool operator ==(RightAngleRotation lhs, RightAngleRotation rhs) => lhs.Value == rhs.Value;
    public static bool operator !=(RightAngleRotation lhs, RightAngleRotation rhs) => lhs.Value != rhs.Value;

    public override string ToString()
    {
        return Value.ToString("X2");
    }
}