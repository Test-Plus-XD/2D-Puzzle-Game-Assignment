using System.Collections.Generic;
using UnityEngine;

// Fills a PolygonCollider2D's interior by generating a mesh that matches its shape.
[RequireComponent(typeof(PolygonCollider2D))]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class ColliderFill : MonoBehaviour
{
    [Tooltip("Colour used to fill the collider.")]
    public Color fillColour = new Color(1f, 0f, 0f, 0.5f); // Semi-transparent red

    private PolygonCollider2D polygonCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private void Awake()
    {
        polygonCollider = GetComponent<PolygonCollider2D>();
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        Mesh mesh = GenerateMeshFromCollider();
        meshFilter.mesh = mesh;

        Material material = new Material(Shader.Find("Sprites/Default"));
        material.color = fillColour;
        meshRenderer.material = material;
    }

    // Creates a triangulated mesh using the collider's path
    Mesh GenerateMeshFromCollider()
    {
        List<Vector2> points = new List<Vector2>();
        polygonCollider.GetPath(0, points);

        Vector3[] vertices = new Vector3[points.Count];
        for(int i = 0; i < points.Count; i++)
        {
            vertices[i] = points[i];
        }

        // Triangulation (simple ear-clipping)
        Triangulator triangulator = new Triangulator(points.ToArray());
        int[] triangles = triangulator.Triangulate();

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}

// Basic ear-clipping triangulation for simple polygons
public class Triangulator
{
    private List<Vector2> points;

    public Triangulator(Vector2[] points)
    {
        this.points = new List<Vector2>(points);
    }

    public int[] Triangulate()
    {
        List<int> indices = new List<int>();

        int n = points.Count;
        if(n < 3)
            return indices.ToArray();

        int[] V = new int[n];
        if(Area() > 0)
        {
            for(int v = 0; v < n; v++)
                V[v] = v;
        } else
        {
            for(int v = 0; v < n; v++)
                V[v] = (n - 1) - v;
        }

        int nv = n;
        int count = 2 * nv;
        for(int m = 0, v = nv - 1; nv > 2;)
        {
            if((count--) <= 0)
                return indices.ToArray();

            int u = v;
            if(nv <= u)
                u = 0;
            v = u + 1;
            if(nv <= v)
                v = 0;
            int w = v + 1;
            if(nv <= w)
                w = 0;

            if(Snip(u, v, w, nv, V))
            {
                int a = V[u];
                int b = V[v];
                int c = V[w];
                indices.Add(a);
                indices.Add(b);
                indices.Add(c);
                for(int s = v, t = v + 1; t < nv; s++, t++)
                    V[s] = V[t];
                nv--;
                count = 2 * nv;
            }
        }

        indices.Reverse();
        return indices.ToArray();
    }

    private float Area()
    {
        int n = points.Count;
        float A = 0f;
        for(int p = n - 1, q = 0; q < n; p = q++)
        {
            Vector2 pval = points[p];
            Vector2 qval = points[q];
            A += pval.x * qval.y - qval.x * pval.y;
        }
        return A * 0.5f;
    }

    private bool Snip(int u, int v, int w, int nv, int[] V)
    {
        Vector2 A = points[V[u]];
        Vector2 B = points[V[v]];
        Vector2 C = points[V[w]];
        if(Mathf.Epsilon > (((B.x - A.x) * (C.y - A.y)) - ((B.y - A.y) * (C.x - A.x))))
            return false;
        for(int p = 0; p < nv; p++)
        {
            if(p == u || p == v || p == w)
                continue;
            Vector2 P = points[V[p]];
            if(IsInsideTriangle(A, B, C, P))
                return false;
        }
        return true;
    }

    private bool IsInsideTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
    {
        float ax = C.x - B.x;
        float ay = C.y - B.y;
        float bx = A.x - C.x;
        float by = A.y - C.y;
        float cx = B.x - A.x;
        float cy = B.y - A.y;
        float apx = P.x - A.x;
        float apy = P.y - A.y;
        float bpx = P.x - B.x;
        float bpy = P.y - B.y;
        float cpx = P.x - C.x;
        float cpy = P.y - C.y;

        float aCROSSbp = (ax * bpy) - (ay * bpx);
        float cCROSSap = (cx * apy) - (cy * apx);
        float bCROSScp = (bx * cpy) - (by * cpx);

        return (aCROSSbp >= 0f) && (bCROSScp >= 0f) && (cCROSSap >= 0f);
    }
}