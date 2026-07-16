#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 맵 실사화 일괄 실행 진입점 — 배치모드(-executeMethod)와 에디터 메뉴 겸용.
    /// URP 머티리얼 변환 → 로비/활동 공간 맵 빌드 → 검증용 스크린샷(Library/MapShots) 순서.
    /// 스크린샷은 각 씬의 프리뷰 카메라(Main Camera) 시점 렌더 — 빌드 결과 눈검증용.
    /// </summary>
    public static class EnvBuildRunner
    {
        private static readonly (string path, string name)[] k_scenes =
        {
            ("Assets/Scenes/Lobby.unity",          "Lobby"),
            ("Assets/Scenes/FishingGround.unity",  "FishingGround"),
            ("Assets/Scenes/HuntZone_A.unity",     "HuntZone_A"),
            ("Assets/Scenes/EstateTemplate.unity", "EstateTemplate"),
        };

        /// <summary>배치모드 진입점: 변환 + 4개 맵 빌드 + 스크린샷.</summary>
        public static void RunAll()
        {
            EnvMaterialUrpConverter.Convert();
            LobbyMapFactory.Build();
            RoomMapsFactory.BuildAll();
            CaptureAll();
            Debug.Log("[MakeAssets] EnvBuildRunner.RunAll 완료");
        }

        [MenuItem("Tools/Make Assets/Capture Map Screenshots")]
        public static void CaptureAll()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Library", "MapShots");
            Directory.CreateDirectory(dir);

            EditorSceneManager.SaveOpenScenes();
            foreach (var (path, name) in k_scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Capture(Path.Combine(dir, name + ".png"));
            }
            Debug.Log($"[MakeAssets] 맵 스크린샷 저장: {dir}");
        }

        private static void Capture(string filePath)
        {
            var camGo = GameObject.Find("Main Camera");
            Camera cam = camGo != null ? camGo.GetComponent<Camera>() : null;
            bool tempCam = cam == null;
            if (tempCam)
            {
                camGo = new GameObject("TempCaptureCamera");
                cam = camGo.AddComponent<Camera>();
                camGo.transform.SetPositionAndRotation(new Vector3(0f, 2f, -8f), Quaternion.Euler(10f, 0f, 0f));
            }

            const int w = 1600, h = 900;
            var rt = new RenderTexture(w, h, 24);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;

            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(filePath, tex.EncodeToPNG());

            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            if (tempCam) Object.DestroyImmediate(camGo);
        }
    }
}
#endif
