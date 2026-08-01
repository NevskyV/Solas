using System.Numerics;
using Silk.NET.Assimp;
using Solas.Assets;

namespace Solas.Render.Components;

public class Mesh : Asset
{
    public Vertex[]? Vertices { get; private set; }
    public uint[]? Indices { get; private set; }

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

        void VisitSceneNode(Node* node)
        {
            for (int m = 0; m < node->MNumMeshes; m++)
            {
                var mesh = scene->MMeshes[node->MMeshes[m]];

                for (int f = 0; f < mesh->MNumFaces; f++)
                {
                    var face = mesh->MFaces[f];

                    for (int i = 0; i < face.MNumIndices; i++)
                    {
                        uint index = face.MIndices[i];

                        var position = mesh->MVertices[index];

                        Vector3 normal = Vector3.UnitY;
                        if (mesh->MNormals != null)
                        {
                            var norm = mesh->MNormals[index];
                            normal = new Vector3(norm.X, norm.Y, norm.Z);
                        }

                        Vector2 uv = Vector2.Zero;
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

            for (int c = 0; c < node->MNumChildren; c++)
            {
                VisitSceneNode(node->MChildren[c]);
            }
        }
    }

    public void FreeCpuData()
    {
        Vertices = null;
        Indices = null;
    }
}