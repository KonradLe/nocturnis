using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Krypta.Editor
{
    public static class NocturnisCryptPaletteSetup
    {
        const string Models = "Assets/Generated/KayKit2026/Dungeon/Models/";
        const string ResourcePath = "Assets/Resources/CryptPalette.asset";

        // Uses only already prepared prefabs. No ZIP import, scene edits or changes
        // to the shared KryptaAssets configuration are needed.
        [MenuItem("Nocturnis/Prepare crypt palette")]
        public static void Prepare()
        {
            var prepared = ScriptableObject.CreateInstance<NocturnisCryptPalette>();
            prepared.name = "CryptPalette";
            try
            {
                prepared.floorPlain = Load("floor_tile_small");
                prepared.floorDecorated = Load("floor_tile_small_decorated");
                prepared.floorBrokenA = Load("floor_tile_small_broken_A");
                prepared.floorBrokenB = Load("floor_tile_small_broken_B");
                prepared.floorWeedsA = Load("floor_tile_small_weeds_A");
                prepared.floorWeedsB = Load("floor_tile_small_weeds_B");
                prepared.floorGrate = Load("floor_tile_grate");
                prepared.wall = Load("wall");
                prepared.wallCracked = Load("wall_cracked");
                prepared.wallLow = Load("wall_half");
                prepared.pillar = Load("pillar");
                prepared.pillarDecorated = Load("pillar_decorated");
                prepared.wallShelves = Load("wall_shelves");
                prepared.barrel = Load("barrel_large");
                prepared.barrels = Load("barrel_small_stack");
                prepared.crates = Load("crates_stacked");
                prepared.shelfCandles = Load("shelf_small_candles");
                prepared.tableBroken = Load("table_medium_broken");
                prepared.bannerBlue = Load("banner_blue");
                prepared.bannerRed = Load("banner_red");
                prepared.bannerGold = Load("banner_yellow");
                prepared.torchMounted = Load("torch_mounted");

                var palette = AssetDatabase.LoadAssetAtPath<NocturnisCryptPalette>(ResourcePath);
                if (palette)
                {
                    EditorUtility.CopySerialized(prepared, palette);
                    EditorUtility.SetDirty(palette);
                    AssetDatabase.SaveAssetIfDirty(palette);
                }
                else
                {
                    if (AssetDatabase.LoadMainAssetAtPath(ResourcePath))
                        throw new InvalidOperationException("An unrelated asset occupies " + ResourcePath);
                    Directory.CreateDirectory("Assets/Resources");
                    AssetDatabase.CreateAsset(prepared, ResourcePath);
                    prepared = null; // The asset database now owns this object.
                }
                Debug.Log("Crypt palette ready: 22 existing KayKit prefabs, " + ResourcePath);
            }
            finally
            {
                if (prepared) UnityEngine.Object.DestroyImmediate(prepared);
            }
        }

        static GameObject Load(string name)
        {
            string path = Models + name + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new InvalidOperationException("Missing prepared crypt model: " + path);
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh && filter.sharedMesh.vertexCount > 0) return prefab;
            throw new InvalidOperationException("Crypt model has no usable mesh: " + path);
        }
    }
}
