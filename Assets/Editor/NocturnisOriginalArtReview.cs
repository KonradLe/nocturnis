using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Krypta.Editor
{
    // Floor-only Edit Mode study. The rejected character draft is archived outside Assets.
    // This does not enter Play Mode, change game configuration, or build a player.
    public static class NocturnisOriginalArtReview
    {
        const string Output = "art-direction/original-art-review-20261029";
        const string FloorFolder = "Assets/Art/Nocturnis/CryptFloor-v01";
        const string ScenePath = "Assets/Art/Nocturnis/OriginalArtReview20261029.unity";
        static readonly string[] FloorNames = { "Floor_Slabs_A", "Floor_Slabs_B", "Floor_Slabs_C", "Floor_Cracked", "Floor_Drain", "Floor_Rosette" };
        [Serializable] public sealed class MeshResult { public string name, path; public int vertices, triangles; public Vector3 size, minimum, maximum; }
        [Serializable] public sealed class RenderResult { public string path, lighting, description; public float orthographic_size; public int width, height; }
        [Serializable] public sealed class Report
        {
            public bool passed;
            public int assertions, continuity_samples;
            public string error, scene = ScenePath, scope = "Isolated Edit Mode floor import and Unity renders. Existing KayKit reference only; no new character or gameplay validation.";
            public string[] active_assets;
            public MeshResult[] floor_meshes;
            public RenderResult[] renders;
        }
        static readonly List<MeshResult> floors = new List<MeshResult>();
        static readonly List<RenderResult> renders = new List<RenderResult>();
        static readonly List<string> assets = new List<string>();
        static Report report;
        static GameObject patch, gallery;
        static Camera camera;
        static Material storybook, dark;

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            report = new Report(); floors.Clear(); renders.Clear(); assets.Clear();
            var originalShadows = QualitySettings.shadows;
            float originalShadowDistance = QualitySettings.shadowDistance;
            try
            {
                Require(!EditorApplication.isPlaying, "Review must run in Edit Mode.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Texture(FloorFolder + "/Textures/CryptFloor_Roughness.png", false, false);
                storybook = FloorMaterial("Storybook"); dark = FloorMaterial("DarkFantasy");
                var prefabs = FloorNames.Select(ImportFloor).ToArray();
                patch = new GameObject("Original floor continuity patch - 5 by 5 tiles");
                for (int x = -2; x <= 2; x++)
                    for (int z = -2; z <= 2; z++)
                    {
                        int index = ((x + 2) * 5 + z + 2) % prefabs.Length;
                        var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[index]);
                        tile.transform.SetParent(patch.transform, false);
                        tile.transform.localPosition = new Vector3(x * 1.8f, 0, z * 1.8f);
                        tile.name = FloorNames[index] + " [" + x + "," + z + "]";
                        foreach (var filter in tile.GetComponentsInChildren<MeshFilter>())
                            filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    }
                CheckContinuity();
                foreach (var collider in patch.GetComponentsInChildren<MeshCollider>()) UnityEngine.Object.DestroyImmediate(collider);
                camera = new GameObject("Original floor review camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.045f, .044f, .05f);
                camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 100;
                camera.allowHDR = false; camera.allowMSAA = true;
                Lighting(true);
                Capture("01-floor-storybook-continuous.png", Vector3.zero, 4.8f, true, "Five by five 1.8 m tiles, all six original variants; Storybook palette.");
                SetPatchMaterial(dark);
                Capture("02-floor-dark-continuous.png", Vector3.zero, 4.8f, true, "Identical original patch, camera and lighting; DarkFantasy palette.");
                Lighting(false);
                Capture("03-floor-dark-game-camera9.png", Vector3.zero, 9, false, "DarkFantasy floor with current game lighting, camera offset (9,15,-11), orthographic size 9.");
                SetPatchMaterial(storybook);
                Capture("04-floor-storybook-game-camera9.png", Vector3.zero, 9, false, "Storybook floor with identical current game lighting and camera distance.");
                Lighting(true);
                var config = Resources.Load<KryptaAssets>("KryptaAssets");
                Require(config && config.heroes != null && config.heroes.Length == 5, "Current KayKit reference is unavailable.");
                var current = config.heroes[0];
                Require(current.model && current.weapon && current.animations != null, "Current equipped hero reference is incomplete.");
                var kaykit = new ActorVisual(current.model, current.animations, Vector3.zero, 1.8f, "Temporary existing KayKit reference");
                try
                {
                    kaykit.Equip(current.weapon); kaykit.PreviewIdle(.15f);
                    Capture("05-current-kaykit-original-storybook-floor.png", Vector3.zero, 4, true, "Existing free KayKit Knight, body height 1.8 m, on the new original Storybook floor. The character is not original Nocturnis geometry.");
                }
                finally { UnityEngine.Object.DestroyImmediate(kaykit.Root); }
                gallery = new GameObject("Six original floor variants"); patch.SetActive(false);
                for (int i = 0; i < prefabs.Length; i++)
                {
                    var tile = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]);
                    tile.transform.SetParent(gallery.transform, false);
                    tile.transform.localPosition = new Vector3((i % 3 - 1) * 2.15f, 0, (i / 3 - .5f) * 2.15f);
                }
                Capture("06-floor-six-variants.png", Vector3.zero, 3.6f, true, "Separate 1.8 m samples: Slabs A/B/C, Cracked, Drain, Rosette.");
                gallery.SetActive(false); patch.SetActive(true); SetPatchMaterial(storybook);
                Lighting(false); PositionCamera(Vector3.zero, 4.8f);
                QualitySettings.shadows = originalShadows; QualitySettings.shadowDistance = originalShadowDistance;
                Require(EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath), "Could not save isolated floor review scene.");
                assets.Add(ScenePath); AssetDatabase.SaveAssets();
                string[] dependencies = AssetDatabase.GetDependencies(ScenePath, true);
                Require(!dependencies.Any(path => path.Contains("Inquisitor-v01") || path.Contains("Inquisitor-v02") || path.Contains("KayKit")), "Saved floor scene unexpectedly depends on character assets.");
                report.passed = true;
            }
            catch (Exception error) { report.error = error.ToString(); Debug.LogException(error); }
            finally { QualitySettings.shadows = originalShadows; QualitySettings.shadowDistance = originalShadowDistance; }
            report.floor_meshes = floors.ToArray(); report.renders = renders.ToArray(); report.active_assets = assets.Distinct().ToArray();
            File.WriteAllText(Output + "/import-report.json", JsonUtility.ToJson(report, true));
            File.WriteAllText(Output + "/import-report.txt", (report.passed ? "PASS" : "FAIL") + ": " + report.assertions + " assertions; " + report.continuity_samples + " floor coverage samples; " + renders.Count + " Unity renders.\n" + report.scope + "\n" + report.error);
            Debug.Log("Original floor review " + (report.passed ? "PASS" : "FAIL") + ": " + report.assertions + " assertions.");
            EditorApplication.Exit(report.passed ? 0 : 1);
        }

        static void Require(bool condition, string message)
        {
            report.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        static Texture2D Texture(string path, bool normal, bool srgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer, "Missing texture importer: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb; importer.mipmapEnabled = true; importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Require(texture && texture.width == 2048 && texture.height == 2048, "Expected real 2048x2048 texture: " + path);
            assets.Add(path); return texture;
        }

        static Material FloorMaterial(string variant)
        {
            string path = FloorFolder + "/CryptFloor-" + variant + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Standard"); Require(shader && shader.isSupported, "Standard PBR shader unavailable.");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.color = Color.white;
            material.mainTexture = Texture(FloorFolder + "/Textures/CryptFloor_" + variant + "_BaseColor.png", false, true);
            material.SetTexture("_BumpMap", Texture(FloorFolder + "/Textures/CryptFloor_Normal.png", true, false));
            material.SetTexture("_MetallicGlossMap", Texture(FloorFolder + "/Textures/CryptFloor_MetallicSmoothness.png", false, false));
            material.SetFloat("_BumpScale", .6f); material.SetFloat("_GlossMapScale", 1);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICGLOSSMAP");
            EditorUtility.SetDirty(material); assets.Add(path); return material;
        }

        static GameObject ImportFloor(string name)
        {
            string path = FloorFolder + "/Models/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            Require(importer, "Missing FBX importer: " + path);
            importer.globalScale = 1; importer.useFileScale = true; importer.isReadable = true;
            importer.importAnimation = false; importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.meshCompression = ModelImporterMeshCompression.Off; importer.importNormals = ModelImporterNormals.Import;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path); Require(source, "FBX did not import: " + name);
            var root = UnityEngine.Object.Instantiate(source); root.name = name;
            var filters = root.GetComponentsInChildren<MeshFilter>(); Require(filters.Length > 0, "No floor geometry: " + name);
            int vertices = 0, triangles = 0;
            foreach (var filter in filters)
            {
                CheckMesh(filter.sharedMesh, name); vertices += filter.sharedMesh.vertexCount; triangles += filter.sharedMesh.triangles.Length / 3;
                filter.GetComponent<MeshRenderer>().sharedMaterials = Enumerable.Repeat(storybook, filter.sharedMesh.subMeshCount).ToArray();
            }
            var bounds = BoundsOf(root);
            Require(Mathf.Abs(bounds.size.x - 1.8f) < .012f && Mathf.Abs(bounds.size.z - 1.8f) < .012f, "Floor must be 1.8 x 1.8 m, without Tile/2 scaling: " + name + " " + bounds.size);
            Require(Mathf.Abs(bounds.center.x) < .006f && Mathf.Abs(bounds.center.z) < .006f, "Floor pivot is not centered: " + name);
            Require(bounds.max.y < .025f && bounds.max.y > -.025f && bounds.min.y < -.07f && bounds.min.y > -.2f, "Floor top/backing dimensions: " + name + " " + bounds);
            floors.Add(new MeshResult { name = name, path = path, vertices = vertices, triangles = triangles, size = bounds.size, minimum = bounds.min, maximum = bounds.max });
            string prefabPath = FloorFolder + "/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath); Require(prefab, "Failed to create floor prefab: " + name);
            assets.Add(path); assets.Add(prefabPath); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        static void CheckMesh(Mesh mesh, string name)
        {
            Require(mesh && mesh.vertexCount > 8 && mesh.triangles.Length > 12, "Empty geometry: " + name);
            Require(mesh.uv.Length == mesh.vertexCount && mesh.normals.Length == mesh.vertexCount, "Missing UV/normals: " + name);
            Require(mesh.vertices.All(v => Finite(v.x) && Finite(v.y) && Finite(v.z)), "Nonfinite vertices: " + name);
            Require(mesh.uv.All(v => Finite(v.x) && Finite(v.y)), "Nonfinite UV: " + name);
            Require(mesh.normals.All(v => v.sqrMagnitude > .5f && v.sqrMagnitude < 1.5f), "Invalid normals: " + name);
            var uv = mesh.uv; float minX = uv.Min(v => v.x), maxX = uv.Max(v => v.x), minY = uv.Min(v => v.y), maxY = uv.Max(v => v.y);
            Require(maxX - minX > .02f && maxY - minY > .02f, "Collapsed UV: " + name);
        }

        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        static Bounds BoundsOf(GameObject root)
        {
            bool found = false; var result = new Bounds();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(vertex);
                    if (!found) { result = new Bounds(point, Vector3.zero); found = true; } else result.Encapsulate(point);
                }
            Require(found, "Cannot measure empty floor model."); return result;
        }

        static void CheckContinuity()
        {
            Physics.SyncTransforms();
            // Includes exact seams and samples on both sides; every variant has a closed backing.
            var coordinates = new List<float>();
            for (int i = 0; i <= 88; i++) coordinates.Add(-4.4f + i * .1f);
            for (int i = -2; i <= 1; i++) foreach (float epsilon in new[] { -.002f, 0f, .002f }) coordinates.Add(i * 1.8f + .9f + epsilon);
            foreach (float x in coordinates) foreach (float z in coordinates)
            {
                RaycastHit hit;
                Require(Physics.Raycast(new Vector3(x, 1, z), Vector3.down, out hit, 1.22f), "Missing floor coverage at " + x + "," + z);
                Require(hit.collider.transform.IsChildOf(patch.transform), "Coverage ray hit unrelated geometry.");
                Require(hit.point.y >= -.125f && hit.point.y <= .025f, "Invalid floor surface height.");
                report.continuity_samples++;
            }
        }

        static void SetPatchMaterial(Material material)
        { foreach (var renderer in patch.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray(); }

        static void Lighting(bool warm)
        {
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(light.gameObject);
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = warm ? new Color(.40f, .37f, .32f) : new Color(.37f, .35f, .42f);
            var sun = new GameObject(warm ? "Warm review key" : "Current game sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0); sun.color = warm ? new Color(1, .88f, .72f) : new Color(.67f, .74f, .91f);
            sun.intensity = warm ? 1.05f : .85f; sun.shadows = LightShadows.Soft; sun.shadowStrength = .85f;
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = 50;
        }

        static void PositionCamera(Vector3 target, float size)
        { camera.orthographicSize = size; camera.transform.position = target + new Vector3(9, 15, -11); camera.transform.LookAt(target); }

        static void Capture(string name, Vector3 target, float size, bool warm, string description, int width = 1600, int height = 1000)
        {
            PositionCamera(target, size);
            var texture = new RenderTexture(width, height, 24) { antiAliasing = 4 }; texture.Create();
            var previousTarget = RenderTexture.active;
            try
            {
                camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Output + "/" + name, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            }
            finally { RenderTexture.active = previousTarget; camera.targetTexture = null; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            renders.Add(new RenderResult { path = Output + "/" + name, lighting = warm ? "Warm comparison" : "Current game", orthographic_size = size, description = description, width = width, height = height });
        }
    }
}
