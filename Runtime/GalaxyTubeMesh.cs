using System.Collections.Generic;
using UnityEngine;

namespace GalaxyToon
{
    /// <summary>毎フレーム作り直す細い管・粒のメッシュ(糸・涙など)。ワールド座標で足し、owner のローカルへ直して入れる。</summary>
    public sealed class GalaxyTubeMesh
    {
        public readonly Mesh Mesh;
        private readonly Transform owner;
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector3> norms = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> tris = new List<int>();

        public GalaxyTubeMesh(Transform owner, string name)
        {
            this.owner = owner;
            Mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave }; // 一時メッシュ(シーン・プレハブに保存しない)
            Mesh.MarkDynamic();
        }

        public void Begin() { verts.Clear(); norms.Clear(); uvs.Clear(); tris.Clear(); }

        public void End()
        {
            Mesh.Clear();
            Mesh.SetVertices(verts);
            Mesh.SetNormals(norms);
            Mesh.SetUVs(0, uvs);
            Mesh.SetTriangles(tris, 0);
            Mesh.RecalculateBounds();
        }

        /// <summary>points[0..count) に沿った管。radii は各点の半径。</summary>
        public void AddTube(Vector3[] points, float[] radii, int count, int sides = 6)
        {
            if (count < 2) return;
            int start = verts.Count, last = count - 1;
            for (int k = 0; k < count; k++)
            {
                Vector3 dir = (k < last ? points[k + 1] - points[k] : points[k] - points[k - 1]);
                dir = dir.sqrMagnitude < 1e-12f ? Vector3.up : dir.normalized;
                Vector3 n1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 n2 = Vector3.Cross(dir, n1);
                for (int j = 0; j <= sides; j++)
                {
                    float ang = j / (float)sides * Mathf.PI * 2f;
                    Vector3 n = n1 * Mathf.Cos(ang) + n2 * Mathf.Sin(ang);
                    verts.Add(owner.InverseTransformPoint(points[k] + n * radii[k]));
                    norms.Add(owner.InverseTransformDirection(n));
                    uvs.Add(new Vector2(j / (float)sides, k / (float)last));
                }
            }
            int ring = sides + 1;
            for (int k = 0; k < last; k++)
                for (int j = 0; j < sides; j++)
                {
                    int i0 = start + k * ring + j, i1 = i0 + 1, i2 = i0 + ring, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i2); tris.Add(i1);
                    tris.Add(i1); tris.Add(i2); tris.Add(i3);
                }
        }

        /// <summary>粒(楕円体)。axis 方向へ stretch 倍に伸ばす(流れる向きに少し伸びたしずく)。</summary>
        public void AddDrop(Vector3 center, float radius, Vector3 axis, float stretch, int rings = 6, int sides = 8)
        {
            axis = axis.sqrMagnitude < 1e-12f ? Vector3.down : axis.normalized;
            Vector3 n1 = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 n2 = Vector3.Cross(axis, n1);
            int start = verts.Count;
            for (int r = 0; r <= rings; r++)
            {
                float lat = Mathf.PI * r / rings;                 // 0=前の極 π=後ろの極
                float along = Mathf.Cos(lat), around = Mathf.Sin(lat);
                for (int j = 0; j <= sides; j++)
                {
                    float ang = j / (float)sides * Mathf.PI * 2f;
                    Vector3 n = axis * along + (n1 * Mathf.Cos(ang) + n2 * Mathf.Sin(ang)) * around;
                    Vector3 p = center + (axis * (along * stretch) + (n1 * Mathf.Cos(ang) + n2 * Mathf.Sin(ang)) * around) * radius;
                    verts.Add(owner.InverseTransformPoint(p));
                    norms.Add(owner.InverseTransformDirection(n));
                    uvs.Add(new Vector2(j / (float)sides, r / (float)rings));
                }
            }
            int ring = sides + 1;
            for (int r = 0; r < rings; r++)
                for (int j = 0; j < sides; j++)
                {
                    int i0 = start + r * ring + j, i1 = i0 + 1, i2 = i0 + ring, i3 = i2 + 1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i2);
                    tris.Add(i1); tris.Add(i3); tris.Add(i2);
                }
        }
    }
}
