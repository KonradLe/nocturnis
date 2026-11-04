using UnityEngine;

namespace Krypta
{
    // A separate resource keeps original 1.8 m modules independent from the
    // older 2 m KayKit floor contract and from the shared character config.
    public sealed class NocturnisOriginalFloorKit : ScriptableObject
    {
        public const string ResourceName = "OriginalCryptFloor";
        public const float ModuleSize = 1.8f;
        public GameObject slabsA, slabsB, slabsC, cracked, drain, rosette;
        public Material darkFantasy;

        public bool IsReady
        {
            get { return slabsA && slabsB && slabsC && cracked && drain && rosette && darkFantasy; }
        }

        public GameObject Module(int index)
        {
            switch (index)
            {
                case 1: return slabsB;
                case 2: return slabsC;
                case 3: return cracked;
                case 4: return drain;
                case 5: return rosette;
                default: return slabsA;
            }
        }
    }
}
