using UnityEngine;

namespace Krypta
{
    // Authored body dimensions exclude weapons and animation culling bounds.
    public sealed class ActorModelProfile : MonoBehaviour
    {
        public float bodyHeight=1.8f;
        public float groundOffset;
        // The new rig's complete actions are fitted to the existing gameplay windows.
        public bool fitActionDuration;
        // Distance covered by one full in-place gait cycle, relative to body height.
        // Existing procedural actors retain their authored stride by default.
        public float walkStridePerHeight=.60f;
        public float runStridePerHeight=1.45f;
        // Empty preserves the legacy whole-body wardrobe tint.
        public string garbMaterialPrefix="";
    }
}
