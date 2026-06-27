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

    [Header("AI Camera Setup")]
    [SerializeField] Camera aiCamera; // AI neural network camera
    [SerializeField] RenderTexture highResRT; // To eventually downsample if needed

    [SerializeField] private SpellAIReader aiReader;
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
        bool check = finalCircleIdentifier.IsCircle(masterPointCloud, out circleCenter, out circleRadius, out circleAccuracy);

        if (!check)
        {
            Debug.LogWarning("The drawn shape is not a circle, the spell waits for one.");
            return;
        }
        Debug.Log("Valid outer circle! Isolating content...");

        // Hide outer circle in "Default" layer
        List<LineRenderer> circleLines = new List<LineRenderer>();
        int inkLayerIndex = LayerMask.NameToLayer("Ink");
        int defaultLayerIndex = LayerMask.NameToLayer("Default");

        foreach (var lr in allLineRenderers)
        {
            if (lr.positionCount > 0)
            {
                Vector3 worldPos = lr.transform.TransformPoint(lr.GetPosition(0));
                Vector2 pos2D = new Vector2(worldPos.x, worldPos.z);
                float distFromCenter = Vector2.Distance(pos2D, circleCenter);

                if (distFromCenter > circleRadius * 0.9f)
                {
                    circleLines.Add(lr);
                    lr.gameObject.layer = defaultLayerIndex;
                }
            }
        }

        // GROUP INNER SYMBOLS
        // If two lines' dots have a lesser tolerance distance than a threshold, they are considered part of the same symbol
        float connectionTolerance = 0.2f;
        List<List<LineRenderer>> internalSymbols = GroupInternalSymbols(allLineRenderers, connectionTolerance);

        Debug.Log($"Found {internalSymbols.Count} distinct internal symbols inside the circle.");

        if (internalSymbols.Count == 0)
        {
            Debug.LogWarning("The circle is empty inside! No magic performed.");
            // Restore the circle before exiting
            foreach (var cl in circleLines) cl.gameObject.layer = inkLayerIndex;
            return;
        }

        // CAPTURE INTERNAL SYMBOLS cycle
        foreach (List<LineRenderer> symbolGroup in internalSymbols)
        {
            // Calculate the bounding box of the symbol
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var lr in symbolGroup)
            {
                Vector3[] pts = new Vector3[lr.positionCount];
                lr.GetPositions(pts);
                foreach (var p in pts)
                {
                    Vector3 wPos = lr.transform.TransformPoint(p);
                    if (wPos.x < minX) minX = wPos.x; if (wPos.x > maxX) maxX = wPos.x;
                    if (wPos.z < minZ) minZ = wPos.z; if (wPos.z > maxZ) maxZ = wPos.z;
                }
            }

            Vector3 symbolCenter3D = new Vector3((minX + maxX) / 2f, aiCamera.transform.position.y, (minZ + maxZ) / 2f);
            float symbolWidth = maxX - minX;
            float symbolHeight = maxZ - minZ;

            // Temporarily hide all other symbols and the outer circle
            foreach (var lr in allLineRenderers)
            {
                if (!symbolGroup.Contains(lr) && !circleLines.Contains(lr))
                {
                    lr.gameObject.layer = defaultLayerIndex;
                }
            }

            // Calculate the max dimension of the symbol
            float maxDim = Mathf.Max(symbolWidth, symbolHeight);

            // Change thickness of brush so size variations don't affect the AI's perception
            float idealizedThickness = maxDim * 0.05f;
            float previousThickness = 100f;
            // Apply it to only the current symbol's lines
            foreach (var lr in symbolGroup)
            {
                previousThickness = lr.startWidth;
                lr.startWidth = idealizedThickness;
                lr.endWidth = idealizedThickness;
            }

            // Zoom the AI camera to focus on the symbol
            aiCamera.transform.position = symbolCenter3D;
            aiCamera.orthographicSize = (Mathf.Max(symbolWidth, symbolHeight) / 2f) * 1.15f;

            aiCamera.Render();
            // Convert RenderTexture to Texture2D to feed it to the AI
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = renderTexture;

            Texture2D singleSymbolTex = new Texture2D(
                renderTexture.width,
                renderTexture.height,
                TextureFormat.RGBA32,
                false);

            singleSymbolTex.ReadPixels(
                new Rect(0, 0, renderTexture.width, renderTexture.height),
                0,
                0);

            singleSymbolTex.Apply();

            // Save: TextureSaver.SaveTextureAsPNG(singleSymbolTex, "symbol_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + UnityEngine.Random.Range(0000, 9999));

            RenderTexture.active = currentRT;

            // Send the symbol to the AI for analysis
            if (!IsTextureEmpty(singleSymbolTex))
            {
                Debug.Log($"Isolated symbol sent to AI (Dimensions: {symbolWidth}x{symbolHeight})");

                // INNER PHASE 2: Execute the internal phase 2 inference on 'singleSymbolTex'
                string classLabel = aiReader.RecognizeSymbol(singleSymbolTex, out float aiConfidence);

                Debug.LogWarning($"[IA RESULT]: Found '{classLabel}' with confidence of {aiConfidence * 100f}%");

                if (classLabel == "Garbage" || aiConfidence < 0.65f)
                {
                    Debug.LogWarning("The isolated symbol is garbage or unstable. Spell failed.");
                    // Logica di interruzione (es. interrompi il loop ed esci)
                    break;
                }
                else if (classLabel == "Fire Sigil")
                {
                    Debug.Log("Fire Sigil Confirmed!");
                    // Save the information about the magical element
                }
                else if (classLabel == "Column")
                {
                    Debug.Log("Column Confirmed!");
                    // Calculate the direction of the column based on the symbol's orientation
                    //Vector2 arrowDir = CalculateColumnDirection(symbolGroup);
                    //float arrowAngle = Mathf.Atan2(arrowDir.y, arrowDir.x) * Mathf.Rad2Deg;
                    //Debug.Log($"Modificatore Direzione Confermato! Angolo: {arrowAngle}°");
                }
            }

            Destroy(singleSymbolTex); // Free memory after use

            foreach (var lr in symbolGroup)
            {
                lr.startWidth = previousThickness; // Restore original thickness
                lr.endWidth = previousThickness;
            }

            // Restore all symbols to the "Ink" layer
            foreach (var lr in allLineRenderers)
            {
                if (!circleLines.Contains(lr))
                {
                    lr.gameObject.layer = inkLayerIndex;
                }
            }
        }

        // Restore the outer circle to the "Ink" layer
        foreach (var cl in circleLines)
        {
            cl.gameObject.layer = inkLayerIndex;
        }

        Debug.Log("Multicomponent analysis completed successfully.");
    }

    bool IsTextureEmpty(Texture2D tex)
    {
        Color[] pixels = tex.GetPixels();
        foreach (Color c in pixels)
        {
            // White pixel = drawn, Black pixel = empty
            if (c.r > 0.2f)
            {
                return false;
            }
        }
        return true; // All black pixels => texture is empty
    }
    // Method to group close points into clusters based on a distance threshold
    List<List<LineRenderer>> GroupInternalSymbols(List<LineRenderer> allRenderers, float maxDistanceToConnect)
    {
        // Filter lines that are not part of the circle border (i.e., those that are on the "Default" layer)
        List<LineRenderer> internalLines = new List<LineRenderer>();
        int inkLayerIndex = LayerMask.NameToLayer("Ink");

        foreach (var lr in allRenderers)
        {
            if (lr != null && lr.gameObject.layer == inkLayerIndex && lr.positionCount > 0)
            {
                internalLines.Add(lr);
            }
        }

        // List to hold the grouped clusters of LineRenderers
        List<List<LineRenderer>> symbolsList = new List<List<LineRenderer>>();

        // Set to keep track of which LineRenderers have already been grouped
        HashSet<LineRenderer> visitedLines = new HashSet<LineRenderer>();

        // 2. Grouping logic: iterate through each internal line and group them based on proximity
        foreach (var startLine in internalLines)
        {
            if (visitedLines.Contains(startLine)) continue;

            // Found a new symbol! Create a list for its strokes
            List<LineRenderer> currentSymbolGroup = new List<LineRenderer>();
            Queue<LineRenderer> queue = new Queue<LineRenderer>();

            queue.Enqueue(startLine);
            visitedLines.Add(startLine);

            while (queue.Count > 0)
            {
                LineRenderer currentLine = queue.Dequeue();
                currentSymbolGroup.Add(currentLine);

                // Check for neighboring lines that are close enough to connect
                foreach (var potentialNeighbor in internalLines)
                {
                    if (visitedLines.Contains(potentialNeighbor)) continue;

                    if (AreLinesClose(currentLine, potentialNeighbor, maxDistanceToConnect))
                    {
                        queue.Enqueue(potentialNeighbor);
                        visitedLines.Add(potentialNeighbor);
                    }
                }
            }

            symbolsList.Add(currentSymbolGroup);
        }

        return symbolsList;
    }

    // Method to check if two LineRenderers have close points
    bool AreLinesClose(LineRenderer lineA, LineRenderer lineB, float threshold)
    {
        // Extract the points in local space
        Vector3[] pointsA = new Vector3[lineA.positionCount];
        Vector3[] pointsB = new Vector3[lineB.positionCount];

        lineA.GetPositions(pointsA);
        lineB.GetPositions(pointsB);

        // Double loop to verify if there exists AT LEAST one pair of close points
        foreach (Vector3 pA in pointsA)
        {
            Vector3 worldPA = lineA.transform.TransformPoint(pA);
            foreach (Vector3 pB in pointsB)
            {
                Vector3 worldPB = lineB.transform.TransformPoint(pB);

                // Calculate distance in 2D (XZ plane)
                float dist = Vector2.Distance(new Vector2(worldPA.x, worldPA.z), new Vector2(worldPB.x, worldPB.z));
                if (dist <= threshold)
                {
                    return true; // The lines are connected
                }
            }
        }
        return false;
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
