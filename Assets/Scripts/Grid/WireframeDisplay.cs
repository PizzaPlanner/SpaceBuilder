using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class WireframeDisplay : MonoBehaviour
{
    [SerializeField] GameObject AxisX;
    [SerializeField] GameObject AxisY;
    [SerializeField] GameObject AxisZ;
    [SerializeField] GameObject Rotation;

    [SerializeField] public Transform BaseGrid;
    [SerializeField] public Transform ObjectGrid;

    [SerializeField] GameObject ScaleAxisX;
    [SerializeField] GameObject ScaleAxisY;
    [SerializeField] GameObject ScaleAxisZ;
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

    public void ShowScaleAxes(bool3 axes)
    {
        ScaleAxisX.SetActive(axes.x);
        ScaleAxisY.SetActive(axes.y);
        ScaleAxisZ.SetActive(axes.z);
    }
}
