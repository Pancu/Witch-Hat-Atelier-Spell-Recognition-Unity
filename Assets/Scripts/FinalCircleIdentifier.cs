using System;
using System.Collections.Generic;
using UnityEngine;

public class FinalCircleIdentifier : MonoBehaviour
{
    //identifies if the shape drawn is a circle with a bigger radius than 50% of the mesh drawn on.
    public bool IsCircle(List<Vector2> points)
    {
        if (points.Count < 3)
            return false;
        float maxX = float.MaxValue;
        float maxY = float.MaxValue;
        float minX = float.MinValue;
        float minY = float.MinValue;
        foreach (Vector2 point in points)
        {
            if (point.x < maxX)
                maxX = point.x;
            if (point.y < maxY)
                maxY = point.y;
            if (point.x > minX)
                minX = point.x;
            if (point.y > minY)
                minY = point.y;
        }
        float centerX = (maxX + minX) / 2f;
        float centerY = (maxY + minY) / 2f;
        Vector2 center = new Vector2(centerX, centerY);
        Debug.Log("Center of circle: " + center);
        return true; //temp
        /*
        foreach (var point in points)
        {
            center += point;
        }
        center /= points.Count;
        float radius = 0f;
        foreach (var point in points)
        {
            radius += Vector2.Distance(center, point);
        }
        radius /= points.Count;
        return radius > meshRadius * 0.5f;*/
    }
}
