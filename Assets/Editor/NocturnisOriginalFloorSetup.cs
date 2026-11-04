using System;
using UnityEditor;
using UnityEngine;

namespace Krypta.Editor
{
    // Run once before integration QA/build. Uses the already reviewed imports;
    // it does not regenerate scenes, character assets or the old crypt palette.
    public static class NocturnisOriginalFloorSetup
    {
        const string Folder = "Assets/Art/Nocturnis/CryptFloor-v01/";
        const string ResourcePath = "Assets/Resources/OriginalCryptFloor.asset";
        static readonly string[] Names = { "Floor_Slabs_A", "Floor_Slabs_B", "Floor_Slabs_C", "Floor_Cracked", "Floor_Drain", "Floor_Rosette" };

        [MenuItem("Nocturnis/Prepare original crypt floor")]
        public static void Prepare()
        {
            var modules = new GameObject[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                modules[i] = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + Names[i] + ".prefab");
                ValidateModule(modules[i], Names[i]);
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "CryptFloor-DarkFantasy.mat");
            if (!material || !material.shader || material.shader.name != "Standard" || material.color != Color.white)
                throw new InvalidOperationException("Original crypt floor requires the reviewed white-tinted Standard DarkFantasy material.");
            ValidateTexture(material.mainTexture, "CryptFloor_DarkFantasy_BaseColor.png", false, true);
            ValidateTexture(material.GetTexture("_BumpMap"), "CryptFloor_Normal.png", true, false);
            ValidateTexture(material.GetTexture("_MetallicGlossMap"), "CryptFloor_MetallicSmoothness.png", false, false);
            if (!material.IsKeywordEnabled("_NORMALMAP") || !material.IsKeywordEnabled("_METALLICGLOSSMAP"))
                throw new InvalidOperationException("Original crypt floor PBR material keywords are missing.");

            var kit = AssetDatabase.LoadAssetAtPath<NocturnisOriginalFloorKit>(ResourcePath);
            if (!kit)
            {
                if (AssetDatabase.LoadMainAssetAtPath(ResourcePath))
                    throw new InvalidOperationException("Unrelated asset occupies " + ResourcePath);
                kit = ScriptableObject.CreateInstance<NocturnisOriginalFloorKit>();
                kit.name = NocturnisOriginalFloorKit.ResourceName;
                AssetDatabase.CreateAsset(kit, ResourcePath);
            }
            kit.slabsA = modules[0]; kit.slabsB = modules[1]; kit.slabsC = modules[2];
            kit.cracked = modules[3]; kit.drain = modules[4]; kit.rosette = modules[5];
            kit.darkFantasy = material;
            EditorUtility.SetDirty(kit); AssetDatabase.SaveAssetIfDirty(kit);
            Debug.Log("Original crypt floor ready: six 1.8 m closed modules, DarkFantasy PBR, resource " + NocturnisOriginalFloorKit.ResourceName + ".");
        }

        static void ValidateTexture(Texture texture, string file, bool normal, bool srgb)
        {
            string expected = Folder + "Textures/" + file;
            if (!texture || AssetDatabase.GetAssetPath(texture) != expected)
                throw new InvalidOperationException("Wrong original floor texture: " + expected);
            var importer = AssetImporter.GetAtPath(expected) as TextureImporter;
            if (!importer || importer.sRGBTexture != srgb ||
                (importer.textureType == TextureImporterType.NormalMap) != normal || !importer.mipmapEnabled)
                throw new InvalidOperationException("Incorrect original floor texture import: " + expected);
        }

        static void ValidateModule(GameObject module, string name)
        {
            if (!module) throw new InvalidOperationException("Reviewed original floor prefab is missing: " + name);
            bool found = false; var bounds = new Bounds();
            foreach (var filter in module.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh || !mesh.isReadable || mesh.vertexCount == 0 || mesh.uv.Length != mesh.vertexCount || mesh.normals.Length != mesh.vertexCount || mesh.tangents.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Original floor needs readable geometry, UVs, normals and tangents for batching: " + name);
                Matrix4x4 local = module.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (var vertex in mesh.vertices)
                {
                    Vector3 point = local.MultiplyPoint3x4(vertex);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!found || Mathf.Abs(bounds.size.x - 1.8f) > .005f || Mathf.Abs(bounds.size.z - 1.8f) > .005f ||
                Mathf.Abs(bounds.center.x) > .002f || Mathf.Abs(bounds.center.z) > .002f ||
                Mathf.Abs(bounds.max.y) > .002f || Mathf.Abs(bounds.min.y + .12f) > .005f)
                throw new InvalidOperationException("Original floor must be centered 1.8 x 1.8 m, with top Y0 and bottom Y-0.12: " + name + " " + bounds);
        }
    }
}
