using System.Runtime.CompilerServices;
using Unity.Collections;

public static partial class GalaxyTools
{
    public static FixedString32Bytes ToRomanNumeral(byte b)
    {
        return b switch
        {
            0 => "I",
            1 => "II",
            2 => "III",
            3 => "IV",
            4 => "V",
            5 => "VI",
            6 => "VII",
            7 => "VIII",
            8 => "IX",
            9 => "X",
            _ => "0"
        };
    }

    public static string ToGalacticDistanceWithUnit(double distance)
    {
        var unit = "m";
        if (distance >= 0.01 * LocalToGalaxy.DISTANCE_PARSEC)
        {
            distance /= LocalToGalaxy.DISTANCE_PARSEC;
            unit = "pc";
        }
        else if (distance >= LocalToGalaxy.DISTANCE_ASTRONOMICAL_UNIT)
        {
            distance /= LocalToGalaxy.DISTANCE_ASTRONOMICAL_UNIT;
            unit = "au";
        }
        else if (distance > 1000_000_000)
        {
            distance *= 0.000_000_001f;
            unit = "Gm";
        }
        else if (distance > 1000_000)
        {
            distance *= 0.000_001f;
            unit = "Mm";
        }
        else if (distance > 1000)
        {
            distance *= 0.001f;
            unit = "km";
        }

        return $"{distance:F1}{unit}";
    }

    public static string ToTimeUnit(double time)
    {
        var unit = "s";
        if (time > 3600 * 24 * 30 * 12)
        {
            return $"> 1y";
        }
        else if (time > 3600 * 24)
        {
            return $"{time / (3600 * 24):F1}d";
        }
        else if (time > 3600)
        {
            return $"{time / 3600:F1}h";
        }
        else if (time > 60)
        {
            return $"{time / 60:F1}m";
        }
        else return $"{time:F1}s";
    }

    static FixedString32Bytes iToS(in uint x, int rank)
    {
        var y = SubHex(x, rank);
        return StarNameParts[y % StarNameParts.Length];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint SubHex(uint i, int part)
    {
        return (i >> (part * 8)) & 0xFF;
    }

    public static FixedString64Bytes GetStarName(uint seed)
    {
        var seed1 = seed;
        byte bonus = 0;
        for(int i = 0; i < 3; i++)
            if (SubHex(seed1, i) == SubHex(seed1, i + 1) && SubHex(seed1, i) == SubHex(seed1, i + 2))
            {
                bonus++;
                seed1 ^= (uint)(0xFF << (i * 8));
            }
        FixedString64Bytes it1 = $"{iToS(seed1, 0)}{iToS(seed1, 1)}{iToS(seed1, 2)}{iToS(seed1, 3)}";
        if(bonus > 0)
            it1.Append((FixedString64Bytes)$" {bonus + 1}");
        unsafe
        {
            it1.GetUnsafePtr()[0] -= 32;
        }

        return it1;
    }

    static readonly FixedString32Bytes[] StarNameParts = {
        "adex",
        "bama",
        "gis",
        "bura",
        "sugu",
        "gabura",
        "cymex",
        "bara",
        "ten",
        "ara",
        "tesenc",
        "rapara",
        "toro",
        "rosc",
        "loxu",
        "udedama",
        "garu",
        "pona",
        "xi",
        "nux",
        "pom"
    };
}