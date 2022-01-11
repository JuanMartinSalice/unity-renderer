using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkyboxOrbital3DManager : MonoBehaviour
{
    public int segments;
    public SkyboxOrbit orbit;


    public GameObject orbitObject;
    public Vector3 initialScale = new Vector3(1,1,1);
    public Vector3 initialRotation;

    public Vector3 rotationSpeed;
    public RotationType rotationT;

    LineRenderer lr;
    Vector3[] points;
    Transform targetTransform;
    float orbitProgress;
    public float orbitPeriod;
    bool moving;
    public enum RotationType
    {
        Fixed, Automatic, Orbit
    }

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        moving = true;
    }

    private void Start()
    {
        if(orbitObject)
        {
            SpawnObject();
            SetObjectPosition();
            StartCoroutine(MoveObject());
        }
    }
    /////////////////////////////////////////////////////////////////////////////////////////////////////ORBIT
    void CalculateOrbit()
    {
        points = new Vector3[segments + 1];
        
        for (int i = 0; i < segments; i++)
        {
            Vector2 tPos = orbit.Evaluate((float)i / (float)segments);
            points[i] = new Vector3(tPos.x, tPos.y, 0f);
        }

        points[segments] = points[0]; //EQUALS THE LAST POINT TO THE FIRST TO HAVE A FULL ELLIPSE

        if(lr)
        { 
            lr.positionCount = segments + 1;
            lr.SetPositions(points);
            lr.widthMultiplier = ((orbit.xAxis + orbit.yAxis) * 0.5f) * 0.005f;
        }
    }
    private void FixedUpdate()
    {
        CalculateOrbit();
    }

    /////////////////////////////////////////////////////////////////////////////////////////////////////OBJECT MOVEMENT
    private void Update()
    {
        if (orbitPeriod == 0f)
        {
            orbitPeriod = 0.01f;
        }
        targetTransform.localScale = initialScale;

        if (rotationT == RotationType.Automatic)
        {
            targetTransform.transform.Rotate(rotationSpeed * Time.deltaTime);
        }
        else if(rotationT == RotationType.Fixed)
        {
            targetTransform.rotation = Quaternion.Euler(initialRotation);
        }

    }


    void SpawnObject()
    {
        GameObject newObj = Instantiate(orbitObject, transform);
        newObj.transform.localScale = initialScale;
        newObj.transform.rotation = Quaternion.Euler(initialRotation);
        targetTransform = newObj.transform;
    }

    void SetObjectPosition()
    {
        float orbitSpeed = 1f / orbitPeriod;
        orbitProgress += orbitSpeed * Time.deltaTime;
        orbitProgress %= 1f;


        Vector2 orbitPos = orbit.Evaluate(orbitProgress);

        Vector3 finalPos = new Vector3(orbitPos.x, orbitPos.y, 0);

        if (rotationT == RotationType.Orbit)
        {
            targetTransform.LookAt(finalPos - targetTransform.transform.localPosition, targetTransform.up);
        }

        targetTransform.localPosition = finalPos;
    }

    IEnumerator MoveObject()
    {
        if(orbitPeriod == 0f)
        {
            orbitPeriod = 0.01f;
        }


        while(moving)
        {
            SetObjectPosition();
            yield return null;
        }
    }

    /*private void OnDrawGizmos()
    {
        CalculateOrbit();

        for (int i = 0; i < points.Length; i++)
        {
            Vector3 pointA = transform.position + points[i];
            
            if (i != points.Length - 1)
            {
                Vector3 pointB = transform.position + points[i + 1];

                Gizmos.DrawLine(pointA, pointB);
            }
            else
            {
                Vector3 pointB = transform.position + points[0];

                Gizmos.DrawLine(pointA, pointB);
            }
        }
    }*/
}
