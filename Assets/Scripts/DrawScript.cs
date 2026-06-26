using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class DrawScript : MonoBehaviour
{
    [SerializeField] LayerMask drawingLayer;
    [SerializeField] LayerMask inkLayer;
    [SerializeField] float minDistanceBetweenPoints = 0.1f;
    [SerializeField] GameObject lineRendererPrefab;
    bool canDraw = false;
    bool isDrawing = false;

    LineRenderer lineRenderer; //points to current line renderer
    [SerializeField] List<LineRenderer> allLineRenderers = new List<LineRenderer>();
    Vector3 previousPos = Vector3.zero;
    List<Vector2> pointList = new List<Vector2>();
    GameObject objBelowDrawing;

    List<List<Vector2>> shapes = new List<List<Vector2>>();

    FinalCircleIdentifier finalCircleIdentifier;

    // Contains all points of all shapes
    List<Vector2> masterPointCloud = new List<Vector2>();

    Coroutine analysisCoroutine;
    // Wait before analyzing the drawn shape
    [SerializeField] float analysisDelay = 1.5f;

    [SerializeField] RenderTexture renderTexture;
    private void Start()
    {
        finalCircleIdentifier = gameObject.AddComponent<FinalCircleIdentifier>();
        StartCoroutine(IsMouseOnDrawable());
    }

    void Update()
    {
        if(!canDraw)
            return;

        bool mouseDown = Mouse.current.leftButton.isPressed;
        if (mouseDown && !isDrawing)
        {
            isDrawing = true;
            StartDraw();
        }
        if (mouseDown && isDrawing)
        {
            Draw();
        }
        if (!mouseDown && isDrawing)
        {
            isDrawing = false;
            StopDraw();
        }
    }

    IEnumerator IsMouseOnDrawable()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.01f);
            Vector3 mousePos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if ((drawingLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    canDraw = true;
                    objBelowDrawing = hit.collider.gameObject;
                    yield return null;
                }
            }
            canDraw = false;
        }
    }

    void StartDraw()
    {
        // If the user draws again, do not analyze yet
        if (analysisCoroutine != null)
        {
            StopCoroutine(analysisCoroutine);
            Debug.Log("The user hasn't finished drawing, analysis rescheduled.");
        }

        pointList.Clear();
        lineRenderer = Instantiate(lineRendererPrefab, objBelowDrawing.transform).GetComponent<LineRenderer>();
        allLineRenderers.Add(lineRenderer);
        lineRenderer.positionCount = 0;

        Vector3 hitPoint;
        bool check;
        (hitPoint, check) = GetSurfacePosition();
        if (check)
        {
            AddPoint(hitPoint);
            previousPos = hitPoint;
        }
    }

    void Draw()
    {
        Vector3 surfacePosition;
        bool check;
        (surfacePosition, check) = GetSurfacePosition();
        if (check)
        {
            if (Vector3.Distance(surfacePosition, previousPos) > minDistanceBetweenPoints)
            {
                AddPoint(surfacePosition);
                previousPos = surfacePosition;
            }
        }
    }

    void StopDraw()
    {
        previousPos = Vector3.zero;
        shapes.Add(new List<Vector2>(pointList));
        // Add to global point cloud
        masterPointCloud.AddRange(pointList);

        // If the user doesn't draw after a delay -> analyze the shape
        analysisCoroutine = StartCoroutine(WaitAndAnalyzeSpell());
    }

    IEnumerator WaitAndAnalyzeSpell()
    {
        Debug.Log($"Start analysis countdown: {analysisDelay} seconds...");
        yield return new WaitForSeconds(analysisDelay);

        Debug.LogWarning("Times up, elaborating spell...");
        ProcessMagicalDrawing();
    }

    void ProcessMagicalDrawing()
    {
        Vector2 circleCenter = new Vector2();
        float circleRadius = 0f, circleAccuracy = 0f;
        bool check = false;
        check = finalCircleIdentifier.IsCircle(masterPointCloud, out circleCenter, out circleRadius, out circleAccuracy);
        // FASE 1: Trasformare l'area di disegno in Texture2D o elaborare la masterPointCloud
        if (!check){
            Debug.LogWarning("The drawn shape is not a circle, the spell waits for one.");
        }
        // FASE 2: Passare i dati alla Rete Neurale (Unity Barracuda)
        // FASE 3: Calcolare accuratezza ed eseguire l'effetto visivo

        Debug.Log($"Completed analysis of {allLineRenderers.Count} strokes and {masterPointCloud.Count} points.");

        // Alla fine dell'incantesimo (riuscito o fallito), pulisci l'area
        // ClearCanvas();
    }

    (Vector3,bool) GetSurfacePosition()
    {
        Vector3 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;
        LayerMask actualDrawLayer = drawingLayer - inkLayer;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, actualDrawLayer))
        {
            return (hit.point, true);
        }

        return (Vector3.zero, false);
    }

    void AddPoint(Vector3 position)
    {        
        pointList.Add(new Vector2(position.x, position.z));

        lineRenderer.positionCount = pointList.Count;

        Vector3 localPos = lineRenderer.transform.InverseTransformPoint(position);

        Vector3 visualOffset = new Vector3(0f, 0f, -0.01f);

        lineRenderer.SetPosition(lineRenderer.positionCount - 1, localPos + visualOffset);
    }

    public void Undo()
    {
        if (shapes.Count > 0)
        {
            shapes.RemoveAt(shapes.Count - 1);
            // Remove the last shape from the scene
            if (lineRenderer != null)
            {
                Destroy(lineRenderer.gameObject);
                allLineRenderers.Remove(lineRenderer);
                if(allLineRenderers.Count > 0)
                {
                    lineRenderer = allLineRenderers[allLineRenderers.Count - 1];
                }
                if (analysisCoroutine != null)
                {
                    StopCoroutine(analysisCoroutine);
                    Debug.Log("User did Undo, wait for new input.");
                }
            }
            masterPointCloud.Clear();
            foreach(var lists in shapes)
            {
                masterPointCloud.AddRange(lists);
            }
        }
        Debug.Log(shapes.Count);
    }
}
