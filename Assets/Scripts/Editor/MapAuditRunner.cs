#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using CampLantern.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 맵 배치 감사 — 4개 공간 씬을 열어 환경 오브젝트의 실측 바운즈로
    /// 스케일·배치 오류(공중부양, 게임플레이 구역 침범, 비정상 크기)를 수치 검증하고
    /// 탑다운/플레이어 시점 스크린샷(Library/MapShots)을 남긴다. 씬은 수정하지 않는다(읽기 전용).
    /// </summary>
    public static class MapAuditRunner
    {
        private static readonly (string path, string name, string envRoot)[] k_scenes =
        {
            ("Assets/Scenes/Lobby.unity",          "Lobby",          "LobbyEnvironment"),
            ("Assets/Scenes/FishingGround.unity",  "FishingGround",  "FishingEnvironment"),
            ("Assets/Scenes/HuntZone_A.unity",     "HuntZone_A",     "HuntZoneEnvironment"),
            ("Assets/Scenes/EstateTemplate.unity", "EstateTemplate", "EstateEnvironment"),
        };

        /// <summary>배치모드용: 맵 재빌드 + 감사 일괄 실행.</summary>
        public static void BuildAndAudit()
        {
            EnvBuildRunner.RunAll();
            AuditAll();
        }

        [MenuItem("Tools/Make Assets/Audit Map Placement")]
        public static void AuditAll()
        {
            string shotDir = Path.Combine(Directory.GetCurrentDirectory(), "Library", "MapShots");
            Directory.CreateDirectory(shotDir);
            var report = new StringBuilder("[AUDIT] == 맵 배치 감사 ==\n");
            int warnings = 0;

            EditorSceneManager.SaveOpenScenes();
            foreach (var (path, name, envRoot) in k_scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                warnings += AuditScene(name, envRoot, report);
                CaptureView(Path.Combine(shotDir, name + "_top.png"), new Vector3(0f, 34f, 2f), Quaternion.Euler(90f, 0f, 0f));

                var spawn = Object.FindFirstObjectByType<PlayerSpawnPoint>();
                Vector3 eye = (spawn != null ? spawn.transform.position : Vector3.zero) + new Vector3(0f, 1.65f, 0f);
                CaptureView(Path.Combine(shotDir, name + "_pov.png"), eye, Quaternion.Euler(8f, 0f, 0f));
            }

            report.Append($"[AUDIT] == 총 경고 {warnings}건 ==");
            Debug.Log(report.ToString());
        }

        private static int AuditScene(string sceneName, string envRootName, StringBuilder report)
        {
            int warn = 0;
            report.Append($"[AUDIT] ── {sceneName} ──\n");

            var spawn = Object.FindFirstObjectByType<PlayerSpawnPoint>();
            if (spawn == null) { report.Append("[AUDIT] WARN: PlayerSpawnPoint 없음\n"); warn++; }

            // 환경 루트 + 관문 루트의 1단계 자식별 바운즈 수집
            var items = new List<(string name, Vector3 pos, Bounds b)>();
            foreach (string rootName in new[] { envRootName, "PortalGates" })
            {
                var root = GameObject.Find(rootName);
                if (root == null) { report.Append($"[AUDIT] WARN: {rootName} 루트 없음\n"); warn++; continue; }
                foreach (Transform child in root.transform)
                    CollectLeafBounds(child, items);
            }

            // 통짜 바운즈(울타리·로프·스캐터 묶음)와 의도된 형태는 위치성 검사에서 제외
            static bool IsAggregate(string n) => n is "Fence" or "GroundCover" or "Rope" or "ExpansionGround";

            float maxTree = 0f; int treeCount = 0;
            foreach (var (name, pos, b) in items)
            {
                Vector3 size = b.size;
                bool isTree = name == "Tree";
                if (isTree) { treeCount++; maxTree = Mathf.Max(maxTree, size.y); }
                if (IsAggregate(name)) continue;

                // ① 공중부양/지면 뚫림 — 나무 뿌리 융기(-1.2m까지)는 정상으로 본다
                if (b.min.y > 0.4f)
                { report.Append($"[AUDIT] WARN: {name} 공중부양 y={b.min.y:F2} @({pos.x:F1},{pos.z:F1})\n"); warn++; }
                float buryLimit = isTree ? -1.2f : -0.6f;
                if (b.max.y < -0.05f || b.min.y < buryLimit)
                { report.Append($"[AUDIT] WARN: {name} 지면 아래 min.y={b.min.y:F2} @({pos.x:F1},{pos.z:F1})\n"); warn++; }

                // ② 비정상 스케일 — 나무 외 오브젝트가 4m를 넘으면 의심 (풍차·관문 제외)
                if (!isTree && name != "Windmill" && !name.StartsWith("Gate_") && size.y > 4f)
                { report.Append($"[AUDIT] WARN: {name} 과대 크기 {size.x:F1}x{size.y:F1}x{size.z:F1}m @({pos.x:F1},{pos.z:F1})\n"); warn++; }

                // ③ 스폰 지점 압박 — 줄기/본체 위치 기준 반경 1.2m (캐노피 AABB 오탐 방지)
                if (spawn != null && size.y > 1f)
                {
                    Vector3 sp = spawn.transform.position;
                    float dist = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(sp.x, sp.z));
                    if (dist < 1.2f)
                    { report.Append($"[AUDIT] WARN: {name} 스폰 지점 압박 (거리 {dist:F2}m)\n"); warn++; }
                }
            }
            report.Append($"[AUDIT] 나무 {treeCount}그루 (최고 {maxTree:F1}m), 배치물 {items.Count}개\n");

            // ④ 씬별 게임플레이 구역 침범 — 본체 위치(줄기) 기준, 캐노피 드리움은 허용
            switch (sceneName)
            {
                case "FishingGround": // 연못 수면(x±5, z-3..7) 위 — 물가 연출용(갈대·바위·모래톱·부두)만 허용
                    warn += ZoneCheck(items, report, "연못 수면", -5f, 5f, -3f, 7f,
                                      allow: n => n is "Reeds" or "Rock" or "SandRim" or "Dock" or "GroundCover" or "Tree");
                    break;
                case "HuntZone_A": // 사냥감 스폰 공터(x±8, z0..15) — 나무 줄기 금지 (지피식물은 허용)
                    warn += ZoneCheck(items, report, "사냥 공터", -8f, 8f, 0f, 15f,
                                      allow: n => n != "Tree");
                    break;
                case "EstateTemplate": // 입구 길(x±1.2, z-8..0) — 디딤돌·길가 랜턴 외 1m+ 장애물 금지
                    foreach (var (name, pos, b) in items)
                    {
                        if (name is "PathStone" or "LanternPost" or "GroundCover" or "Fence" || b.size.y <= 1f) continue;
                        if (pos.x < -1.6f || pos.x > 1.6f || pos.z < -8.4f || pos.z > 0.4f) continue;
                        report.Append($"[AUDIT] WARN: {name} 입구 길 침범 @({pos.x:F1},{pos.z:F1})\n");
                        warn++;
                    }
                    break;
            }
            return warn;
        }

        /// <summary>본체 위치 XZ가 사각 구역 안인데 허용 목록에 없는 배치물을 경고 (나무는 별도 줄기 검사).</summary>
        private static int ZoneCheck(List<(string name, Vector3 pos, Bounds b)> items, StringBuilder report, string zoneLabel,
                                     float xMin, float xMax, float zMin, float zMax, System.Func<string, bool> allow)
        {
            int warn = 0;
            foreach (var (name, pos, b) in items)
            {
                bool inside = pos.x >= xMin && pos.x <= xMax && pos.z >= zMin && pos.z <= zMax;
                if (!inside) continue;
                if (allow(name) && name != "Tree") continue;
                if (name == "Tree") // 나무는 어느 구역이든 줄기가 안에 있으면 경고
                { report.Append($"[AUDIT] WARN: Tree 줄기 {zoneLabel} 안 @({pos.x:F1},{pos.z:F1})\n"); warn++; continue; }
                report.Append($"[AUDIT] WARN: {name} {zoneLabel} 침범 @({pos.x:F1},{pos.z:F1}) 크기 {b.size.y:F1}m\n");
                warn++;
            }
            return warn;
        }

        /// <summary>1단계 자식 단위: 본체 위치 + 렌더러 합산 바운즈 (파티클 제외).</summary>
        private static void CollectLeafBounds(Transform t, List<(string, Vector3, Bounds)> items)
        {
            var renderers = t.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            items.Add((t.name, t.position, b));
        }

        private static void CaptureView(string filePath, Vector3 pos, Quaternion rot)
        {
            var camGo = new GameObject("TempAuditCamera");
            var cam = camGo.AddComponent<Camera>();
            camGo.transform.SetPositionAndRotation(pos, rot);
            cam.farClipPlane = 120f;

            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            RenderTexture.active = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
        }
    }
}
#endif
