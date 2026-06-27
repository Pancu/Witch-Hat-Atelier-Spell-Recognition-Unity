using System;
using System.Collections.Generic;
using UnityEngine;

public class FinalCircleIdentifier : MonoBehaviour
{
    private const int NUMBER_OF_SECTORS = 72;
    private const float MIN_COVERAGE = 0.90f;          // At least 90% of sectors occupied
    private const int MAX_CONSECUTIVE_EMPTY = 3;       // Small gaps allowed
    private const float MAX_RADIUS_DEVIATION = 0.15f;  // 15%

    public bool IsCircle(
        List<Vector2> points,
        out Vector2 circleCenter,
        out float circleRadius,
        out float circleAccuracy)
    {
        circleCenter = Vector2.zero;
        circleRadius = 0;
        circleAccuracy = 0;

        if (points == null || points.Count < 20)
            return false;

        // Estimate center (centroid)
        foreach (Vector2 p in points)
            circleCenter += p;

        circleCenter /= points.Count;

        // Furthest point in each sector
        float[] sectorRadius = new float[NUMBER_OF_SECTORS];

        for (int i = 0; i < NUMBER_OF_SECTORS; i++)
            sectorRadius[i] = -1f;

        float sectorSize = Mathf.PI * 2f / NUMBER_OF_SECTORS;

        foreach (Vector2 p in points)
        {
            Vector2 d = p - circleCenter;

            float angle = Mathf.Atan2(d.y, d.x);

            if (angle < 0)
                angle += Mathf.PI * 2f;

            int sector = Mathf.FloorToInt(angle / sectorSize);

            if (sector >= NUMBER_OF_SECTORS)
                sector = NUMBER_OF_SECTORS - 1;

            float dist = d.magnitude;

            if (dist > sectorRadius[sector])
                sectorRadius[sector] = dist;
        }

        // Check coverage
        int occupied = 0;
        float radiusSum = 0;

        foreach (float r in sectorRadius)
        {
            if (r > 0)
            {
                occupied++;
                radiusSum += r;
            }
        }

        float coverage = occupied / (float)NUMBER_OF_SECTORS;

        if (coverage < MIN_COVERAGE)
        {
            Debug.Log("Not enough circle coverage.");
            return false;
        }

        // Calculate average radius
        circleRadius = radiusSum / occupied;

        // Calculate accuracy based on radius deviation
        float deviation = 0;

        foreach (float r in sectorRadius)
        {
            if (r > 0)
                deviation += Mathf.Abs(r - circleRadius);
        }

        deviation /= occupied;

        if (deviation > circleRadius * MAX_RADIUS_DEVIATION)
        {
            Debug.Log("Radius varies too much.");
            return false;
        }

        // Gap check
        int currentGap = 0;
        int largestGap = 0;

        for (int i = 0; i < NUMBER_OF_SECTORS * 2; i++)
        {
            int s = i % NUMBER_OF_SECTORS;

            if (sectorRadius[s] < 0)
            {
                currentGap++;
                largestGap = Mathf.Max(largestGap, currentGap);
            }
            else
            {
                currentGap = 0;
            }
        }

        if (largestGap > MAX_CONSECUTIVE_EMPTY)
        {
            Debug.Log("Circle has a large opening.");
            return false;
        }

        // Overall accuracy
        float radialScore =
            Mathf.Clamp01(1f - deviation / (circleRadius * MAX_RADIUS_DEVIATION));

        circleAccuracy = radialScore * coverage;

        Debug.Log($"Circle detected! Accuracy: {circleAccuracy:P1}");

        return true;
    }
}
