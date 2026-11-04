using UnityEngine;

namespace Krypta
{
    // Optional environment palette; the shared KryptaAssets v8 config stays intact.
    public sealed class NocturnisCryptPalette : ScriptableObject
    {
        [Header("Stone floor modules (2 x 2 source units)")]
        public GameObject floorPlain;
        [Tooltip("Full tile with raised debris up to Y 0.673; keep clear of tight walking routes.")]
        public GameObject floorDecorated;
        public GameObject floorBrokenA, floorBrokenB;
        public GameObject floorWeedsA, floorWeedsB;

        [Tooltip("Closed grate module, 4 x 2 source units; not a square 2 x 2 tile. Its well extends down to Y -0.98.")]
        public GameObject floorGrate;

        [Header("Masonry")]
        public GameObject wall, wallCracked;
        [Tooltip("KayKit wall_half is half WIDTH, not half height: 2 x 4 x 1 source units, off-center (source X 0 to 2). Use imported bounds to center and scale it for a low wall.")]
        public GameObject wallLow;
        public GameObject pillar, pillarDecorated, wallShelves;

        [Header("Room details")]
        public GameObject barrel, barrels, crates;
        [Tooltip("Wall shelf with candles, about 1 x 0.994 x 0.5 units; its origin is at shelf level, not its bottom.")]
        public GameObject shelfCandles;
        public GameObject tableBroken;
        public GameObject bannerBlue, bannerRed, bannerGold;
        [Tooltip("Wall-mounted torch; source mesh extends from Y -0.381 to 0.682 and projects along positive source Z.")]
        public GameObject torchMounted;
    }
}
