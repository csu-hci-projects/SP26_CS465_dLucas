using UnityEngine;

namespace AerialNav.Gauntlet
{
    /// <summary>
    /// Generates a procedural torus mesh for GauntletRing.
    /// Static utility — no MonoBehaviour dependency.
    /// </summary>
    public static class RingMeshBuilder
    {
        public static Mesh Build(
            float majorRadius,
            float minorRadius,
            int   majorSegments = 48,
            int   minorSegments = 16)
        {
            int vertCount = (majorSegments + 1) * (minorSegments + 1);
            var verts     = new Vector3[vertCount];
            var normals   = new Vector3[vertCount];
            var uvs       = new Vector2[vertCount];
            var tris      = new int[majorSegments * minorSegments * 6];

            int vi = 0;
            for (int i = 0; i <= majorSegments; i++)
            {
                float u      = (float)i / majorSegments;
                float phi    = u * Mathf.PI * 2f;
                float cosPhi = Mathf.Cos(phi);
                float sinPhi = Mathf.Sin(phi);

                for (int j = 0; j <= minorSegments; j++)
                {
                    float v         = (float)j / minorSegments;
                    float theta     = v * Mathf.PI * 2f;
                    float cosTheta  = Mathf.Cos(theta);
                    float sinTheta  = Mathf.Sin(theta);

                    verts[vi]   = new Vector3(
                        (majorRadius + minorRadius * cosTheta) * cosPhi,
                        minorRadius * sinTheta,
                        (majorRadius + minorRadius * cosTheta) * sinPhi);

                    normals[vi] = new Vector3(
                        cosTheta * cosPhi,
                        sinTheta,
                        cosTheta * sinPhi);

                    uvs[vi] = new Vector2(u, v);
                    vi++;
                }
            }

            int ti = 0;
            for (int i = 0; i < majorSegments; i++)
            {
                for (int j = 0; j < minorSegments; j++)
                {
                    int a = i * (minorSegments + 1) + j;
                    int b = a + minorSegments + 1;

                    tris[ti++] = a;
                    tris[ti++] = b;
                    tris[ti++] = a + 1;

                    tris[ti++] = b;
                    tris[ti++] = b + 1;
                    tris[ti++] = a + 1;
                }
            }

            var mesh = new Mesh { name = "GauntletRingTorus" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}