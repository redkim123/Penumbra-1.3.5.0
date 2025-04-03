using Penumbra.GameData.Files;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Transforms;

namespace Penumbra.Import.Models.Export;

public class ModelExporter
{
    public class Model(List<MeshExporter.Mesh> meshes, GltfSkeleton? skeleton)
    {
        public void AddToScene(SceneBuilder scene)
        {
            // If there's a skeleton, the root node should be added before we add any potentially skinned meshes.
            var skeletonRoot = skeleton?.Root;
            if (skeletonRoot != null)
                scene.AddNode(skeletonRoot);

            // Add all the meshes to the scene.
            foreach (var mesh in meshes)
                mesh.AddToScene(scene);
        }
    }

    /// <summary> Export a model in preparation for usage in a glTF file. If provided, skeleton will be used to skin the resulting meshes where appropriate. </summary>
    public static Model Export(in ExportConfig config, MdlFile mdl, IEnumerable<XivSkeleton> xivSkeletons, Dictionary<string, MaterialExporter.Material> rawMaterials, IoNotifier notifier)
    {
        var gltfSkeleton = ConvertSkeleton(xivSkeletons);
        var materials = ConvertMaterials(mdl, rawMaterials, notifier);
        var meshes = ConvertMeshes(config, mdl, materials, gltfSkeleton, notifier);
        return new Model(meshes, gltfSkeleton);
    }

    /// <summary> Convert a .mdl to a mesh (group) per LoD. </summary>
    private static List<MeshExporter.Mesh> ConvertMeshes(in ExportConfig config, MdlFile mdl, MaterialBuilder[] materials, GltfSkeleton? skeleton, IoNotifier notifier)
    {
        var meshes = new List<MeshExporter.Mesh>();

        for (byte lodIndex = 0; lodIndex < mdl.LodCount; lodIndex++)
        {
            var lod = mdl.Lods[lodIndex];

            // TODO: consider other types of mesh?
            for (ushort meshOffset = 0; meshOffset < lod.MeshCount; meshOffset++)
            {
                var meshIndex = (ushort)(lod.MeshIndex + meshOffset);
                var mesh = MeshExporter.Export(config, mdl, lodIndex, meshIndex, materials, skeleton, notifier.WithContext($"Mesh {meshIndex}"));
                meshes.Add(mesh);
            }
        }

        return meshes;
    }

    /// <summary> Build materials for each of the material slots in the .mdl. </summary>
    private static MaterialBuilder[] ConvertMaterials(MdlFile mdl, Dictionary<string, MaterialExporter.Material> rawMaterials, IoNotifier notifier)
        => mdl.Materials
            .Select(name => 
            {
                if (rawMaterials.TryGetValue(name, out var rawMaterial))
                    return MaterialExporter.Export(rawMaterial, name, notifier.WithContext($"Material {name}"));

                notifier.Warning($"Material \"{name}\" missing, using blank fallback.");
                return MaterialExporter.Unknown;
            })
            .ToArray();

    /// <summary> Convert XIV skeleton data into a glTF-compatible node tree, with mappings. </summary>
    private static GltfSkeleton? ConvertSkeleton(IEnumerable<XivSkeleton> skeletons)
    {
        NodeBuilder? root = null;
        var names = new Dictionary<string, int>();
        var joints = new List<NodeBuilder>();

        // Flatten out the bones across all the received skeletons, but retain a reference to the parent skeleton for lookups.
        var iterator = skeletons.SelectMany(skeleton => skeleton.Bones.Select(bone => (skeleton, bone)));
        foreach (var (skeleton, bone) in iterator)
        {
            if (names.ContainsKey(bone.Name))
                continue;

            var node = new NodeBuilder(bone.Name);
            names[bone.Name] = joints.Count;
            joints.Add(node);

            node.SetLocalTransform(new AffineTransform(
                bone.Transform.Scale,
                bone.Transform.Rotation,
                bone.Transform.Translation
            ), false);

            if (bone.ParentIndex == -1)
            {
                root = node;
                continue;
            }

            var parent = joints[names[skeleton.Bones[bone.ParentIndex].Name]];
            parent.AddNode(node);
        }

        if (root == null)
            return null;

        return new GltfSkeleton
        {
            Root = root,
            Joints = joints,
            Names = names,
        };
    }
}
