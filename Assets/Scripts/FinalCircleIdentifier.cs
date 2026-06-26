using System;
using System.Collections.Generic;
using UnityEngine;

public class FinalCircleIdentifier : MonoBehaviour
{
    private const int NUMBER_OF_SECTORS = 64; // Number of sectors to divide the circle into, to check if it's connected
    private const float radiusTolerancePercentage = 0.25f; // Tolerance of circle error

    //identifies if the shape drawn is a circle with a bigger radius than 50% of the mesh drawn on.
    public bool IsCircle(List<Vector2> points, out Vector2 circleCenter, out float circleRadius, out float circleAccuracy)
    {
        circleCenter = Vector2.zero;
        circleRadius = 0f;
        circleAccuracy = 0f;

        if (points.Count < 20) return false;

        // Bounds, center and radius calculation
        float maxX = float.MinValue, maxY = float.MinValue;
        float minX = float.MaxValue, minY = float.MaxValue;
        foreach (Vector2 point in points)
        {
            if (point.x > maxX) maxX = point.x;
            if (point.y > maxY) maxY = point.y;
            if (point.x < minX) minX = point.x;
            if (point.y < minY) minY = point.y;
        }

        circleCenter = new Vector2((maxX + minX) / 2f, (maxY + minY) / 2f);
        float width = maxX - minX;
        float height = maxY - minY;
        circleRadius = (width + height) / 4f;

        // Avoids division by zero
        if (circleRadius < 0.05f) return false;

        // SECTORS: Check if the shape is connected and covers all sectors
        // sectorsOccupied[i] = true if the sector i has at least one point
        bool[] sectorsOccupied = new bool[NUMBER_OF_SECTORS];

        List<float> perimeterDistances = new List<float>();

        foreach (Vector2 point in points)
        {
            float distance = Vector2.Distance(point, circleCenter);

            // Check if point is within the radius tolerance
            float minValidRadius = circleRadius * (1f - radiusTolerancePercentage);
            float maxValidRadius = circleRadius * (1f + radiusTolerancePercentage);

            if (distance >= minValidRadius && distance <= maxValidRadius)
            {
                // Point is within the valid radius range, consider it for sector occupation
                float angle = Mathf.Atan2(point.y - circleCenter.y, point.x - circleCenter.x);

                // Convert angle from radians to degrees and normalize it to [0, 360)
                float angleDegrees = angle * Mathf.Rad2Deg;
                if (angleDegrees < 0) angleDegrees += 360f;

                // Find the sector index for this angle
                int sectorIndex = Mathf.FloorToInt(angleDegrees / (360f / NUMBER_OF_SECTORS));
                sectorIndex = Mathf.Clamp(sectorIndex, 0, NUMBER_OF_SECTORS - 1);

                sectorsOccupied[sectorIndex] = true;
                perimeterDistances.Add(distance);
            }
        }

        // Check if all sectors are occupied
        for (int i = 0; i < NUMBER_OF_SECTORS; i++)
        {
            if (!sectorsOccupied[i])
            {
                Debug.Log($"Incomplete circle, Sector {i} empty (draw a line there).");
                return false; // The circle has a hole
            }
        }

        // Calculate accuracy based on the average distance from the centerco
        float totalDeviation = 0f;
        foreach (float dist in perimeterDistances)
        {
            totalDeviation += Mathf.Abs(dist - circleRadius);
        }

        float averageDeviation = totalDeviation / perimeterDistances.Count;
        // 100% accuracy means averageDeviation = 0, 0% accuracy means averageDeviation = circleRadius
        circleAccuracy = Mathf.Clamp01(1f - (averageDeviation / circleRadius));

        Debug.Log($"Circle completed! Accuracy: {circleAccuracy * 100f}%");
        return true;
    }
}
