using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    public float Rate;
    public float3 Axis;

    // Update is called once per frame
    void Update()
    {
        transform.Rotate(Axis, Rate);
    }
}
