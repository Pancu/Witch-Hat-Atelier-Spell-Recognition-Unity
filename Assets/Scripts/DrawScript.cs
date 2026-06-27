using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public struct AnalyzedSymbolData
{
    public string ClassLabel;     // "Fire Sigil", "Column", etc.
    public float Accuracy;        // The accuracy provided by the ML model
    public float Size;            // maxDim (the scale of the symbol)
    public float RotationAngle;   // The angle in degrees calculated in C#
    public Vector3 CenterPosition;// The central coordinate 3D of the symbol
}

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

    [SerializeField] private SpellVFXMaker vfxMaker;
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

        // This list collects all data of the symbols found inside the circle
        List<AnalyzedSymbolData> collectedSymbols = new List<AnalyzedSymbolData>();

        // Hide outer circle in "Default" layer
        List<LineRenderer> circleLines = new List<LineRenderer>();
        List<LineRenderer> innerLines = new List<LineRenderer>();

        int inkLayerIndex = LayerMask.NameToLayer("Ink");
        int defaultLayerIndex = LayerMask.NameToLayer("Default");

        float bandMin = circleRadius * 0.80f;
        float bandMax = circleRadius * 1.20f;

        foreach (var lr in allLineRenderers)
        {
            if (lr.positionCount < 2)
                continue;

            Vector3[] pts = new Vector3[lr.positionCount];
            lr.GetPositions(pts);

            int inBand = 0;
            int total = pts.Length;

            Vector2 first = Vector2.zero;
            Vector2 last = Vector2.zero;
            bool firstSet = false;

            foreach (var p in pts)
            {
                Vector3 w = lr.transform.TransformPoint(p);
                Vector2 pos2D = new Vector2(w.x, w.z);

                float d = Vector2.Distance(pos2D, circleCenter);

                if (d >= bandMin && d <= bandMax)
                    inBand++;

                if (!firstSet)
                {
                    first = pos2D;
                    firstSet = true;
                }

                last = pos2D;
            }

            float ratio = inBand / (float)total;

            // closure check (is it a loop?)
            float closure = Vector2.Distance(first, last);

            bool isOuterCircle =
                ratio > 0.65f &&
                closure < circleRadius * 0.25f;

            if (isOuterCircle)
            {
                circleLines.Add(lr);
                lr.gameObject.layer = defaultLayerIndex;
            }
            else
            {
                innerLines.Add(lr);
            }
        }

        // GROUP INNER SYMBOLS
        // If two lines' dots have a lesser tolerance distance than a threshold, they are considered part of the same symbol
        float connectionTolerance = 0.2f;
        List<List<LineRenderer>> internalSymbols = GroupInternalSymbols(innerLines, connectionTolerance);

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

            // Save for debug:
            //TextureSaver.SaveTextureAsPNG(singleSymbolTex, "symbol_" + System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + UnityEngine.Random.Range(0000, 9999));

            RenderTexture.active = currentRT;

            // Send the symbol to the AI for analysis
            if (!IsTextureEmpty(singleSymbolTex))
            {
                Debug.Log($"Isolated symbol sent to AI (Dimensions: {symbolWidth}x{symbolHeight})");

                string classLabel = aiReader.RecognizeSymbol(singleSymbolTex, out float aiConfidence);
                Debug.LogWarning($"[IA RESULT]: Found '{classLabel}' with confidence of {aiConfidence * 100f}%");

                if (classLabel == "Garbage" || aiConfidence < 0.5f)
                {
                    Debug.LogError("The isolated symbol is garbage or unstable. This piece failed.");
                }
                else
                {
                    // Generate data structure for the symbol
                    AnalyzedSymbolData data = new AnalyzedSymbolData();
                    data.ClassLabel = classLabel;
                    data.Accuracy = aiConfidence;
                    data.Size = maxDim;
                    data.CenterPosition = symbolCenter3D;
                    data.RotationAngle = 0f; // Default for sigils that are stationary

                    // If it's a column sigil, we need to calculate its rotation angle based on the line's direction
                    if (classLabel == "Column")
                    {
                        Vector2 columnDir = CalculateColumnDirection(symbolGroup);
                        data.RotationAngle = Mathf.Atan2(columnDir.y, columnDir.x) * Mathf.Rad2Deg;
                    }

                    // Save the symbol in the global magic inventory
                    collectedSymbols.Add(data);
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

        if (collectedSymbols.Count > 0 && vfxMaker != null)
        {
            // Convert the 2D circle center to 3D world position for the VFX
            Vector3 circleCenter3D = new Vector3(circleCenter.x, objBelowDrawing.transform.position.y + 0.05f, circleCenter.y);

            // Send all collected data to the VFX creator
            vfxMaker.CreateSpellEffects(collectedSymbols, circleRadius, circleCenter3D);
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

    Vector2 CalculateColumnDirection(List<LineRenderer> lines)
    {
        if (lines == null || lines.Count == 0) return Vector2.up;
        LineRenderer mainLine = lines[0];
        float maxDist = 0f;

        foreach (var lr in lines)
        {
            if (lr.positionCount < 2) continue;
            Vector3 start = lr.GetPosition(0);
            Vector3 end = lr.GetPosition(lr.positionCount - 1);
            float dist = Vector3.Distance(start, end);
            if (dist > maxDist) { maxDist = dist; mainLine = lr; }
        }

        if (mainLine.positionCount >= 2)
        {
            Vector3 startW = mainLine.transform.TransformPoint(mainLine.GetPosition(0));
            Vector3 endW = mainLine.transform.TransformPoint(mainLine.GetPosition(mainLine.positionCount - 1));
            return new Vector2(endW.x - startW.x, endW.z - startW.z).normalized;
        }
        return Vector2.up;
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
