using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class WireframeDisplay : MonoBehaviour
{
    [SerializeField] GameObject AxisX;
    [SerializeField] GameObject AxisY;
    [SerializeField] GameObject AxisZ;
    [SerializeField] GameObject Rotation;
    // Start is called before the first frame update
    void Start()
    {
        ShowAxes(new bool3(false,false,false), quaternion.identity);
        gameObject.SetActive(false);
    }

    public void ShowAxes(bool3 axes, quaternion rotation)
    {
        AxisX.SetActive(axes.x);
        AxisY.SetActive(axes.y);
        AxisZ.SetActive(axes.z);
        Rotation.transform.localRotation = rotation;
    }
}
