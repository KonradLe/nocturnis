using UnityEngine;

namespace Krypta
{
    // Renderer.bounds can still be empty immediately after Instantiate, before
    // the renderer has processed the imported hierarchy. Mesh bounds are ready.
    public static class ModelBounds
    {
        public static bool TryGet(GameObject model,out Bounds result)
        {
            result=new Bounds();
            bool found=false;
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh) Include(GeometryBounds(filter.sharedMesh),filter.transform,ref result,ref found);
            foreach (SkinnedMeshRenderer renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                if (renderer.sharedMesh)
                {
                    Bounds local=renderer.localBounds;
                    if (local.size.sqrMagnitude < .0001f) local=GeometryBounds(renderer.sharedMesh);
                    Include(local,renderer.transform,ref result,ref found);
                }
            return found;
        }

        static Bounds GeometryBounds(Mesh mesh)
        {
            // Some glTFast/Unity combinations import static meshes with an empty
            // bounds box. Rebuild it once from the retained vertex positions;
            // this also repairs renderer culling for every instance of the mesh.
            if (mesh.bounds.size.sqrMagnitude < .0001f && mesh.isReadable) mesh.RecalculateBounds();
            return mesh.bounds;
        }

        static void Include(Bounds local,Transform transform,ref Bounds result,ref bool found)
        {
            if (local.size.sqrMagnitude < .0001f) return;
            for (int corner=0;corner<8;corner++)
            {
                Vector3 point=transform.TransformPoint(new Vector3(
                    (corner&1)==0 ? local.min.x : local.max.x,
                    (corner&2)==0 ? local.min.y : local.max.y,
                    (corner&4)==0 ? local.min.z : local.max.z));
                if (!found) { result=new Bounds(point,Vector3.zero); found=true; }
                else result.Encapsulate(point);
            }
        }
    }
}
