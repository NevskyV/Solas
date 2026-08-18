using System.Numerics;
using Silk.NET.Assimp;
using Solas.Assets;

namespace Solas.Render.Components;

public class Mesh : Asset
{
    public Vertex[] Vertices { get; private set; }
    public uint[] Indices { get; private set; }
    public Vector3 BoundingCenter { get; }
    public float BoundingRadius { get; private set; }

    public unsafe Mesh(string path)
    {
        using var assimp = Assimp.GetApi();
        var scene = assimp.ImportFile(path, (uint)PostProcessPreset.TargetRealTimeMaximumQuality);

        var vertexMap = new Dictionary<Vertex, uint>();
        var vertices = new List<Vertex>();
        var indices = new List<uint>();

        VisitSceneNode(scene->MRootNode);

        assimp.ReleaseImport(scene);

        Vertices = vertices.ToArray();
        Indices = indices.ToArray();

        if (Vertices is { Length: > 0 })
        {
            var min = Vertices[0].Pos;
            var max = Vertices[0].Pos;
            for (var i = 1; i < Vertices.Length; i++)
            {
                min = Vector3.Min(min, Vertices[i].Pos);
                max = Vector3.Max(max, Vertices[i].Pos);
            }

            BoundingCenter = (min + max) * 0.5f;
            var maxSq = 0.0f;
            for (var i = 0; i < Vertices.Length; i++)
            {
                var sq = Vector3.DistanceSquared(Vertices[i].Pos, BoundingCenter);
                if (sq > maxSq) maxSq = sq;
            }

            BoundingRadius = MathF.Sqrt(maxSq);
        }
        else
        {
            BoundingCenter = Vector3.Zero;
            BoundingRadius = 5.0f;
        }

        void VisitSceneNode(Node* node)
        {
            for (var m = 0; m < node->MNumMeshes; m++)
            {
                var mesh = scene->MMeshes[node->MMeshes[m]];

                for (var f = 0; f < mesh->MNumFaces; f++)
                {
                    var face = mesh->MFaces[f];

                    for (var i = 0; i < face.MNumIndices; i++)
                    {
                        var index = face.MIndices[i];

                        var position = mesh->MVertices[index];

                        var normal = Vector3.UnitY;
                        if (mesh->MNormals != null)
                        {
                            var norm = mesh->MNormals[index];
                            normal = new Vector3(norm.X, norm.Y, norm.Z);
                        }

                        var uv = Vector2.Zero;
                        if (mesh->MTextureCoords[0] != null)
                        {
                            var texture = mesh->MTextureCoords[0][(int)index];
                            uv = new Vector2(texture.X, 1.0f - texture.Y);
                        }

                        Vertex vertex = new(
                            new Vector3(position.X, position.Y, position.Z),
                            normal,
                            uv
                        );

                        if (vertexMap.TryGetValue(vertex, out var meshIndex))
                        {
                            indices.Add(meshIndex);
                        }
                        else
                        {
                            indices.Add((uint)vertices.Count);
                            vertexMap[vertex] = (uint)vertices.Count;
                            vertices.Add(vertex);
                        }
                    }
                }
            }

            for (var c = 0; c < node->MNumChildren; c++)
            {
                VisitSceneNode(node->MChildren[c]);
            }
        }
    }

    public void FreeCpuData()
    {
        Vertices = null!;
        Indices = null!;
    }
}