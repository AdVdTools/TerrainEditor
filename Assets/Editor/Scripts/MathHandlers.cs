using UnityEngine;

interface IMathHandler<T>
{
    T Product(T value1, float value2);
    T Product(T value1, T value2);
    T Sum(T value1, T value2);
    T WeightedSum(T value1, T value2, float weight2);
    T Blend(T value1, T value2, float t);
    T BarycentricInterpolation(T value1, T value2, T value3, Vector3 weights);
}

public class FloatMath : IMathHandler<float>
{
    public static FloatMath sharedHandler = new FloatMath();
    public float Product(float value1, float value2)
    {
        return value1 * value2;
    }

    public float Sum(float value1, float value2)
    {
        return value1 + value2;
    }

    public float WeightedSum(float value1, float value2, float weight2)
    {
        return value1 + value2 * weight2;
    }

    public float Blend(float value1, float value2, float t)
    {
        return value1 + (value2 - value1) * t;
    }
    
    public float BarycentricInterpolation(float value1, float value2, float value3, Vector3 weights)
    {
        return value1 * weights.x + value2 * weights.y + value3 * weights.z;
    }
}


public class Vector4Math : IMathHandler<Vector4>
{
    public static Vector4 mask = new Vector4(1f, 1f, 1f, 0f);
    public static Vector4Math sharedHandler = new Vector4Math();
    public Vector4 Product(Vector4 value1, float value2)
    {
        return value1 * value2;
    }

    public Vector4 Product(Vector4 value1, Vector4 value2)
    {
        return Vector4.Scale(value1, value2);
    }

    public Vector4 Sum(Vector4 value1, Vector4 value2)
    {
        return value1 + value2;
    }

    public Vector4 WeightedSum(Vector4 value1, Vector4 value2, float weight2)
    {
        return value1 + Vector4.Scale(value2, (mask * weight2));
    }

    public Vector4 Blend(Vector4 value1, Vector4 value2, float t)
    {
        return value1 + Vector4.Scale(value2 - value1, mask * t);
    }

    public Vector4 BarycentricInterpolation(Vector4 value1, Vector4 value2, Vector4 value3, Vector3 weights)
    {
        return value1 * weights.x + value2 * weights.y + value3 * weights.z;
    }
}


public class ColorMath : IMathHandler<Color>
{
    public static Color mask = new Color(1f, 1f, 1f, 0f);
    public static ColorMath sharedHandler = new ColorMath();
    public Color Product(Color value1, float value2)
    {
        return value1 * value2;
    }

    public Color Product(Color value1, Color value2)
    {
        return value1 * value2;
    }

    public Color Sum(Color value1, Color value2)
    {
        return value1 + value2;
    }

    public Color WeightedSum(Color value1, Color value2, float weight2)
    {
        return value1 + value2 * (mask * weight2);
    }

    public Color Blend(Color value1, Color value2, float t)
    {
        return value1 + (value2 - value1) * (mask * t);
    }

    public Color BarycentricInterpolation(Color value1, Color value2, Color value3, Vector3 weights)
    {
        return value1 * weights.x + value2 * weights.y + value3 * weights.z;
    }
}
