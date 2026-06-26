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
        yield return new WaitForSeconds(0.01f);
        Vector3 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            if ((drawingLayer.value & (1 << hit.collider.gameObject.layer)) != 0)
            {
                canDraw = true;
                Debug.Log("Mouse is on drawable plane: " + hit.collider.gameObject.name);
                objBelowDrawing = hit.collider.gameObject;
                yield return null;
                
            }
        }
        canDraw = false;

        StartCoroutine(IsMouseOnDrawable());
    }

    void StartDraw()
    {
        Debug.Log("Started drawing");
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
        }
    }

    void Draw()
    {
        Debug.Log("Drawing");

        // draw on the plane
        Vector3 surfacePosition;
        bool check;
        (surfacePosition, check) = GetSurfacePosition();
        if (check) { 
            if(Vector3.Distance(surfacePosition, previousPos) > minDistanceBetweenPoints)
            {
                // Add a new point to the line renderer
                AddPoint(surfacePosition);
                previousPos = surfacePosition;
            }
        }
    }

    void StopDraw()
    {
        Debug.Log("Stopped drawing");
        previousPos = Vector3.zero;

        shapes.Add(new List<Vector2>(pointList));
        // Save shape created by the user
        bool check = finalCircleIdentifier.IsCircle(pointList);
        if (!check)
        {
            Debug.LogWarning("Line doesn't make a circle");
        }
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
            }
        }
        Debug.Log(shapes.Count);
    }
}
