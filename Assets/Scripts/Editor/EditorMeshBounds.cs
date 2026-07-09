#if UNITY_EDITOR
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 렌더러 메시 바운즈 합산 헬퍼 — 로컬 메시 바운즈를 localToWorldMatrix로 변환해 합산한다.
    /// Renderer.bounds 캐시에 의존하지 않아 프리팹 콘텐츠(격리 씬)에서도 즉시 정확하다
    /// (WeaponPrefabFactory.AddGrabPhysics와 동일 취지 — 공용화해 팩토리 간 복제 제거).
    /// 프리팹 콘텐츠 루트는 원점·identity이므로 결과는 루트 로컬 좌표와 같다.
    /// </summary>
    internal static class EditorMeshBounds
    {
        /// <summary>root 하위 전체 렌더러의 합산 바운즈. 렌더러가 없으면 false.</summary>
        public static bool TryComputeCombined(GameObject root, out Bounds combined)
        {
            bool hasBounds = false;
            combined = new Bounds();
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!TryGetLocalMeshBounds(r, out var localBounds)) continue;
                EncapsulateLocalBounds(ref combined, ref hasBounds, r.transform, localBounds);
            }
            return hasBounds;
        }

        public static bool TryGetLocalMeshBounds(Renderer r, out Bounds bounds)
        {
            switch (r)
            {
                case MeshRenderer _ when r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null:
                    bounds = mf.sharedMesh.bounds;
                    return true;
                case SkinnedMeshRenderer smr when smr.sharedMesh != null:
                    bounds = smr.sharedMesh.bounds;
                    return true;
                default:
                    bounds = default;
                    return false;
            }
        }

        public static void EncapsulateLocalBounds(ref Bounds combined, ref bool hasBounds, Transform transform, Bounds localBounds)
        {
            var matrix = transform.localToWorldMatrix;
            var center = localBounds.center;
            var extents = localBounds.extents;
            for (int xi = -1; xi <= 1; xi += 2)
            for (int yi = -1; yi <= 1; yi += 2)
            for (int zi = -1; zi <= 1; zi += 2)
            {
                var corner = center + Vector3.Scale(extents, new Vector3(xi, yi, zi));
                var world  = matrix.MultiplyPoint3x4(corner);
                if (!hasBounds) { combined = new Bounds(world, Vector3.zero); hasBounds = true; }
                else combined.Encapsulate(world);
            }
        }
    }
}
#endif
