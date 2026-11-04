using UnityEngine;

namespace Krypta
{
    public sealed class KryptaAssets : ScriptableObject
    {
        [System.Serializable]
        public sealed class Character
        {
            public GameObject model, weapon;
            public AnimationClip[] animations;
        }
        public GameObject knight, skeleton, floor, pillar, chest, torch, sword;
        public AnimationClip[] knightAnimations, skeletonAnimations;
        public Material floorMaterial, wallMaterial, goldMaterial, tealMaterial, redMaterial;
        public Character[] heroes;
        // Ordered Minion, Warrior, Rogue, Mage; optional for older asset configurations.
        public Character[] skeletonVariants;
        public Character zombie, bishop;
        public GameObject[] trees, rocks, buildings, graves;
        public GameObject church, well, tent, fence, skull, crypt, bridge;
        public Material earthMaterial, violetMaterial, greenMaterial;
        public Material effectMaterial, particleMaterial;
        public GameObject[] cryptFloors;
        public GameObject cryptWall, coffin, altar, candles, rubble, banner;
        public int version;
        [System.Serializable]
        public sealed class Surface { public string source; public Material material; }
        public Surface[] surfaces;
    }
}
