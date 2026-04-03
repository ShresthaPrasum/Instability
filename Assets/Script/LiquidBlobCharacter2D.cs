using System.Collections.Generic;
using UnityEngine;

public class LiquidBlobCharacter2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInstabilityController controller;
    [SerializeField] private Rigidbody2D centerBody;
    [SerializeField] private Collider2D centerCollider;
    [SerializeField] private SpriteRenderer mainRenderer;
    [SerializeField] private Transform nodeRoot;

    [Header("Blob Setup")]
    [SerializeField, Min(4)] private int nodeCount = 10;
    [SerializeField, Min(0.1f)] private float blobRadius = 0.55f;
    [SerializeField, Min(0.02f)] private float nodeColliderRadius = 0.14f;
    [SerializeField] private bool generateOnAwake = true;

    [Header("Node Physics")]
    [SerializeField, Min(0f)] private float nodeMass = 0.25f;
    [SerializeField] private bool nodeIsTrigger;
    [SerializeField] private PhysicsMaterial2D nodeMaterial;

    [Header("Node Visuals")]
    [SerializeField] private bool addNodeSprites = true;
    [SerializeField] private Sprite nodeSprite;
    [SerializeField] private Color nodeColor = new Color(0.2f, 0.55f, 1f, 0.9f);
    [SerializeField] private string nodeSortingLayer = "Default";
    [SerializeField] private int nodeSortingOrder = 10;

    [Header("Mode Switching")]
    [SerializeField] private bool hideMainRendererInLiquid = true;
    [SerializeField] private bool disableCenterColliderInLiquid = true;

    [Header("Springs")]
    [SerializeField, Min(0f)] private float centerSpringFrequency = 7.5f;
    [SerializeField, Range(0f, 1f)] private float centerSpringDamping = 0.7f;
    [SerializeField, Min(0f)] private float edgeSpringFrequency = 8.5f;
    [SerializeField, Range(0f, 1f)] private float edgeSpringDamping = 0.55f;

    [Header("Stability")]
    [SerializeField, Min(0f)] private float centeringForce = 12f;
    [SerializeField, Min(0f)] private float maxNodeSpeed = 9f;
    [SerializeField] private bool simulateOnlyInLiquid = true;

    private readonly List<Rigidbody2D> nodes = new List<Rigidbody2D>();
    private readonly List<Vector2> nodeDirections = new List<Vector2>();
    private bool simulationEnabled = true;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<PlayerInstabilityController>();
        }

        if (centerBody == null)
        {
            centerBody = GetComponent<Rigidbody2D>();
        }

        if (centerCollider == null)
        {
            centerCollider = GetComponent<Collider2D>();
        }

        if (mainRenderer == null)
        {
            mainRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (nodeRoot == null)
        {
            GameObject root = new GameObject("LiquidBlobNodes");
            root.transform.SetParent(transform);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            nodeRoot = root.transform;
        }

        if (generateOnAwake)
        {
            GenerateBlobNodes();
        }

        SetSimulationEnabled(!simulateOnlyInLiquid || IsLiquidForm());
    }

    private void FixedUpdate()
    {
        bool shouldSimulate = !simulateOnlyInLiquid || IsLiquidForm();
        if (shouldSimulate != simulationEnabled)
        {
            SetSimulationEnabled(shouldSimulate);
        }

        if (!simulationEnabled || nodes.Count == 0)
        {
            return;
        }

        StabilizeBlob();
    }

    [ContextMenu("Generate Blob Nodes")]
    public void GenerateBlobNodes()
    {
        ClearBlobNodes();

        for (int i = 0; i < nodeCount; i++)
        {
            float angle = i * Mathf.PI * 2f / nodeCount;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector3 worldPos = transform.position + (Vector3)(dir * blobRadius);

            GameObject nodeObj = new GameObject("BlobNode_" + i);
            nodeObj.transform.SetParent(nodeRoot);
            nodeObj.transform.position = worldPos;
            nodeObj.transform.rotation = Quaternion.identity;

            Rigidbody2D nodeBody = nodeObj.AddComponent<Rigidbody2D>();
            nodeBody.mass = nodeMass;
            nodeBody.gravityScale = 1f;
            nodeBody.linearDamping = 1.2f;
            nodeBody.angularDamping = 2f;
            nodeBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            nodeBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D nodeCollider = nodeObj.AddComponent<CircleCollider2D>();
            nodeCollider.radius = nodeColliderRadius;
            nodeCollider.isTrigger = nodeIsTrigger;
            nodeCollider.sharedMaterial = nodeMaterial;

            if (addNodeSprites)
            {
                SpriteRenderer nodeRenderer = nodeObj.AddComponent<SpriteRenderer>();
                nodeRenderer.sprite = nodeSprite;
                nodeRenderer.color = nodeColor;
                nodeRenderer.sortingLayerName = nodeSortingLayer;
                nodeRenderer.sortingOrder = nodeSortingOrder;

                float diameter = nodeColliderRadius * 2f;
                nodeObj.transform.localScale = new Vector3(diameter, diameter, 1f);
            }

            SpringJoint2D centerJoint = nodeObj.AddComponent<SpringJoint2D>();
            centerJoint.autoConfigureConnectedAnchor = false;
            centerJoint.autoConfigureDistance = false;
            centerJoint.connectedBody = centerBody;
            centerJoint.connectedAnchor = Vector2.zero;
            centerJoint.distance = blobRadius;
            centerJoint.frequency = centerSpringFrequency;
            centerJoint.dampingRatio = centerSpringDamping;
            centerJoint.enableCollision = false;

            nodes.Add(nodeBody);
            nodeDirections.Add(dir.normalized);
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            Rigidbody2D current = nodes[i];
            Rigidbody2D next = nodes[(i + 1) % nodes.Count];

            SpringJoint2D edgeJoint = current.gameObject.AddComponent<SpringJoint2D>();
            edgeJoint.autoConfigureDistance = false;
            edgeJoint.connectedBody = next;
            edgeJoint.distance = Vector2.Distance(current.position, next.position);
            edgeJoint.frequency = edgeSpringFrequency;
            edgeJoint.dampingRatio = edgeSpringDamping;
            edgeJoint.enableCollision = false;
        }
    }

    [ContextMenu("Clear Blob Nodes")]
    public void ClearBlobNodes()
    {
        if (nodeRoot == null)
        {
            return;
        }

        for (int i = nodeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = nodeRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        nodes.Clear();
        nodeDirections.Clear();
    }

    public void SetSimulationEnabled(bool enabled)
    {
        simulationEnabled = enabled;

        if (mainRenderer != null && hideMainRendererInLiquid)
        {
            mainRenderer.enabled = !enabled;
        }

        if (centerCollider != null && disableCenterColliderInLiquid)
        {
            centerCollider.enabled = !enabled;
        }

        if (enabled)
        {
            SnapNodesToRing();
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            Rigidbody2D node = nodes[i];
            if (node == null)
            {
                continue;
            }

            node.simulated = enabled;

            if (!enabled)
            {
                node.linearVelocity = Vector2.zero;
                node.angularVelocity = 0f;
            }
            else
            {
                node.linearVelocity = centerBody != null ? centerBody.linearVelocity : Vector2.zero;
            }
        }

        if (nodeRoot != null)
        {
            nodeRoot.gameObject.SetActive(enabled);
        }
    }

    private void SnapNodesToRing()
    {
        if (centerBody == null)
        {
            return;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            Rigidbody2D node = nodes[i];
            if (node == null)
            {
                continue;
            }

            Vector2 dir = i < nodeDirections.Count ? nodeDirections[i] : Vector2.right;
            node.position = centerBody.position + dir * blobRadius;
            node.rotation = 0f;
        }
    }

    private void StabilizeBlob()
    {
        Vector2 center = Vector2.zero;

        for (int i = 0; i < nodes.Count; i++)
        {
            Rigidbody2D node = nodes[i];
            if (node == null)
            {
                continue;
            }

            center += node.position;
        }

        center /= Mathf.Max(1, nodes.Count);

        for (int i = 0; i < nodes.Count; i++)
        {
            Rigidbody2D node = nodes[i];
            if (node == null)
            {
                continue;
            }

            Vector2 towardCenter = center - node.position;
            node.AddForce(towardCenter * centeringForce, ForceMode2D.Force);

            if (maxNodeSpeed > 0f && node.linearVelocity.sqrMagnitude > maxNodeSpeed * maxNodeSpeed)
            {
                node.linearVelocity = node.linearVelocity.normalized * maxNodeSpeed;
            }
        }
    }

    private bool IsLiquidForm()
    {
        return controller != null && controller.CurrentForm == PlayerInstabilityController.FormState.Liquid;
    }
}
