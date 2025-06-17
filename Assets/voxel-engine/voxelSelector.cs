using UnityEngine;

[RequireComponent(typeof(Camera))]
public class VoxelSelector : MonoBehaviour
{
    public Camera mainCamera;
    public float maxSelectionDistance = 8f;
    public Color lightColor = Color.white;
    public Color darkColor = Color.black;

    private LineRenderer lineRenderer;
    private Chunk currentChunk;
    private Vector3[] cubeCorners = new Vector3[8];

    private const float voxelSize = 1f;

    // to destroy blocks
    [SerializeField] private GameObject floatingVoxelPrefab;
    [SerializeField] private float destructionDelay = 0.3f;

    private float holdTime = 0f;
    private bool isHolding = false;
    private Vector3Int lastSelectedVoxel;


    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        // Setup LineRenderer
        GameObject outlineObj = new GameObject("VoxelSelectorOutline");
        lineRenderer = outlineObj.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.positionCount = 16;
        lineRenderer.widthMultiplier = 0.03f;

        // Basic unlit material
        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));

        lineRenderer.enabled = false;
    }

    void Update()
    {
        //Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition); // cursor based selection
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));



        if (Physics.Raycast(ray, out RaycastHit hit, maxSelectionDistance))
        {
            currentChunk = hit.collider.GetComponent<Chunk>();

            if (currentChunk != null)
            {
                //Vector3 localHit = (hit.point - hit.normal * 0.001f); // Slightly push inside the voxel
                //Vector3 localHit = hit.point - currentChunk.transform.position;
                Vector3 localHit = (hit.point - hit.normal * 0.01f) - currentChunk.transform.position;


                int x = Mathf.FloorToInt(localHit.x);
                int y = Mathf.FloorToInt(localHit.y);
                int z = Mathf.FloorToInt(localHit.z);

                if (x >= 0 && x < 16 && y >= 0 && y < 16 && z >= 0 && z < 16)
                {
                    //Vector3Int voxelPos = Vector3Int.FloorToInt(localHit - currentChunk.transform.position);
                    Vector3 voxelPos = currentChunk.transform.position + new Vector3(x, y, z);
                    Vector3Int selected = new Vector3Int(x, y, z);

                    if (selected == lastSelectedVoxel)
                    {
                        if (Input.GetMouseButton(0)) // Holding left click
                        {
                            isHolding = true;
                            holdTime += Time.deltaTime;

                            if (holdTime >= destructionDelay)
                            {
                                RemoveVoxel(currentChunk, selected);
                                holdTime = 0f;
                                isHolding = false;
                            }
                        }
                        else
                        {
                            holdTime = 0f;
                            isHolding = false;
                        }
                    }
                    else
                    {
                        lastSelectedVoxel = selected;
                        holdTime = 0f;
                        isHolding = false;
                    }

                    DrawOutlineBox(voxelPos);
                    lineRenderer.enabled = true;

                }
                else
                {
                    lineRenderer.enabled = false;
                }
            }
            else
            {
                lineRenderer.enabled = false;
            }
        }
        else
        {
            lineRenderer.enabled = false;
        }
    }

    private void RemoveVoxel(Chunk chunk, Vector3Int pos)
    {
        if (chunk == null) return;

        Voxel voxel = chunk.GetVoxel(pos.x, pos.y, pos.z);

        if (voxel.type == Voxel.VoxelType.Air)
            return; // Don't destroy air

        // Replace voxel with air
        chunk.SetVoxel(pos.x, pos.y, pos.z, Voxel.VoxelType.Air);

        // Spawn floating block
        Vector3 worldPos = chunk.transform.position + new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
        Instantiate(floatingVoxelPrefab, worldPos, Quaternion.identity);

        // Regenerate mesh
        chunk.RegenerateMesh();
    }


    void DrawOutlineBox(Vector3 minCorner)
    {
        Vector3 maxCorner = minCorner + Vector3.one;

        // Define 8 corners
        Vector3[] c = cubeCorners;
        c[0] = new Vector3(minCorner.x, minCorner.y, minCorner.z);
        c[1] = new Vector3(maxCorner.x, minCorner.y, minCorner.z);
        c[2] = new Vector3(maxCorner.x, minCorner.y, maxCorner.z);
        c[3] = new Vector3(minCorner.x, minCorner.y, maxCorner.z);

        c[4] = new Vector3(minCorner.x, maxCorner.y, minCorner.z);
        c[5] = new Vector3(maxCorner.x, maxCorner.y, minCorner.z);
        c[6] = new Vector3(maxCorner.x, maxCorner.y, maxCorner.z);
        c[7] = new Vector3(minCorner.x, maxCorner.y, maxCorner.z);

        // 16 edges of the box in correct order
        Vector3[] linePoints = new Vector3[]
        {
            c[0], c[1], c[2], c[3], c[0], // bottom face
            c[4], c[5], c[1], c[5], c[6], c[2], c[6], c[7], c[3], c[7], c[4] // vertical & top face
        };

        lineRenderer.positionCount = linePoints.Length;
        lineRenderer.SetPositions(linePoints);
    }
}
