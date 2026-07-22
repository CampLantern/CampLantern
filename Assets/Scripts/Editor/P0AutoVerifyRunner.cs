#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// P0 플레이 자동 검증 진입점 — 배치모드(-executeMethod)와 에디터 메뉴 겸용.
    /// 저장 파일을 백업·삭제해 신규 유저 상태를 만들고(시작 코인 검증), FishingGround에서
    /// Play를 시작해 P0AutoVerifyDriver(SessionState 플래그로 활성화)가 시나리오를 구동한다.
    /// 결과: Library/P0VerifyReport.json + 배치모드에서는 종료 코드(0=전부 통과).
    /// </summary>
    public static class P0AutoVerifyRunner
    {
        private const string k_sessionFlag = "P0AutoVerify"; // P0AutoVerifyDriver와 공유
        private const string k_startScene = "Assets/Scenes/FishingGround.unity";

        /// <summary>배치모드 진입점. -quit 없이 실행할 것 — 종료는 드라이버의 EditorApplication.Exit가 담당.</summary>
        public static void RunAll()
        {
            if (!Prepare()) { EditorApplication.Exit(2); return; }
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/P0 Play Test/Run Auto Verification (Play Mode)")]
        public static void RunFromMenu()
        {
            if (!Prepare()) return;
            EditorApplication.EnterPlaymode();
        }

        private static bool Prepare()
        {
            var startScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(k_startScene);
            if (startScene == null)
            {
                Debug.LogError($"[P0Verify] 시작 씬 없음: {k_startScene} — Room 씬 팩토리 먼저 실행 필요");
                return false;
            }

            // 신규 유저 상태 재현 — 기존 저장은 .bak으로 백업 (수동 복원 가능)
            string savePath = Path.Combine(Application.persistentDataPath, "player_save.json");
            if (File.Exists(savePath))
            {
                File.Copy(savePath, savePath + ".bak", overwrite: true);
                File.Delete(savePath);
                Debug.Log($"[P0Verify] 기존 저장 백업: {savePath}.bak");
            }

            string reportPath = Path.Combine(Application.dataPath, "..", "Library", "P0VerifyReport.json");
            if (File.Exists(reportPath)) File.Delete(reportPath);

            EditorSceneManager.playModeStartScene = startScene;
            SessionState.SetBool(k_sessionFlag, true);
            Debug.Log("[P0Verify] 준비 완료 — FishingGround에서 Play 진입");
            return true;
        }
    }
}
#endif
