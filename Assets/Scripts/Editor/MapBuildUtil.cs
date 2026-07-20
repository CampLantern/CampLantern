#if UNITY_EDITOR
using CampLantern.Player;
using CampLantern.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CampLantern.EditorTools
{
    /// <summary>
    /// 공간 씬 맵 구성 공용 헬퍼 — LobbyMapFactory/RoomMapsFactory가 공유한다.
    /// 프리미티브 파트 조립(PlaceholderArtFactory 방식: 색상별 공유 머티리얼 Mat_PH_*),
    /// 포탈 관문(PortalPanelController 배선), 스폰포인트/EventSystem 보장, 라이팅 프리셋.
    /// RULE-02: 전부 Unity API로만 생성 — 에셋 파일 직접 작성 없음.
    /// </summary>
    internal static class MapBuildUtil
    {
        private const string k_materialFolder        = "Assets/Prefabs/Materials";
        private const string k_panelPrefabPath       = "Assets/Prefabs/UI/VRUIPanel.prefab";
        private const string k_buttonPrefabPath      = "Assets/Prefabs/UI/VRUIButton.prefab";
        private const string k_eventSystemPrefabPath = "Assets/Prefabs/UI/VRUIEventSystem.prefab";

        // 공통 팔레트 (PlaceholderArtFactory 톤과 맞춤)
        public const string Wood      = "#9a6a3a";
        public const string WoodDark  = "#6a4a25";
        public const string WoodDark2 = "#8a5a2f";
        public const string Trunk     = "#6a4a2a";
        public const string LeafDark  = "#4a6b3a";
        public const string LeafLight = "#567a44";
        public const string Stone     = "#84848a";
        public const string Flame     = "#e8641a";
        public const string FlameCore = "#ffd23a";
        public const string Canvas    = "#c98a4a";
        public const string CanvasDk  = "#8a5a2a";
        public const string Glow      = "#fff3b0";
        public const string Gold      = "#c9a53a";
        public const string PathGray  = "#8d8d90";
        public const string LobbyGray = "#7a7a7e"; // 그림 1 로비 색 — 귀환 관문 현판용

        // ── 실사 환경 에셋 (FantasyEnvironments / CFXR 파티클) ────────
        // 팩이 임포트돼 있으면 프리팹·파티클을 쓰고, 없으면 기존 프리미티브로 폴백한다.

        private const string k_envRoot  = "Assets/FantasyEnvironments";
        private const string k_cfxrRoot = "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs";

        // 위치 해시 변주용 프리팹 세트 (결정적 — 재생성해도 동일한 맵)
        private static readonly string[] k_treeSet = { "Pine_tree1", "Oak_tree1", "Pine_tree2", "Birch_tree1",
                                                       "Deciduous_tree2", "Pine_tree3", "Oak_tree3", "Birch_tree3" };
        private static readonly string[] k_rockSet = { "Rock1", "Rock2", "Rock3", "Stone1", "Stone2", "Stone3" };
        private static readonly string[] k_bushSet = { "Bush1", "Fern1", "Plant2", "Fern3" };
        private static readonly string[] k_coverSet = { "Grass1", "Grass2", "Grass3", "Grass4", "Fern1", "Fern2",
                                                        "Flower1", "Flower3", "Flower5", "Flower8", "Plant1",
                                                        "Mushroom1", "Mushroom3" };

        // 루트별 프리팹 basename→경로 인덱스 — 이름에 괄호가 있어도(CFXR) 안전한 조회
        private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> s_prefabIndex = new();

        public static bool HasEnvAssets => AssetDatabase.IsValidFolder(k_envRoot);

        private static GameObject FindPrefab(string root, string name)
        {
            if (!s_prefabIndex.TryGetValue(root, out var index))
            {
                index = new System.Collections.Generic.Dictionary<string, string>();
                foreach (string guid in AssetDatabase.FindAssets("t:prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    index[System.IO.Path.GetFileNameWithoutExtension(path)] = path;
                }
                s_prefabIndex[root] = index;
            }
            return index.TryGetValue(name, out string prefabPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)
                : null;
        }

        /// <summary>환경 프리팹 배치 — 팩 미임포트/이름 불일치면 null (호출부가 폴백 판단).</summary>
        public static GameObject PlaceEnv(Transform parent, string prefabName, Vector3 pos,
                                          float yaw = 0f, float scale = 1f, string rename = null)
        {
            if (!HasEnvAssets) return null;
            var prefab = FindPrefab(k_envRoot, prefabName);
            if (prefab == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale    = Vector3.one * scale;
            if (rename != null) go.name = rename;

            // 정적 배칭만 — Part()와 동일 컨벤션 (ContributeGI 제외)
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            return go;
        }

        /// <summary>CFXR 파티클 프리팹 배치 (모닥불 화염 등) — 없으면 null.</summary>
        public static GameObject PlaceCfxr(Transform parent, string prefabName, Vector3 pos, float scale = 1f)
        {
            if (!AssetDatabase.IsValidFolder(k_cfxrRoot)) return null;
            var prefab = FindPrefab(k_cfxrRoot, prefabName);
            if (prefab == null) return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = pos;
            go.transform.localScale    = Vector3.one * scale;
            return go;
        }

        // 위치 기반 결정적 해시 — 프리팹 종류·회전 변주에 사용
        private static int PosHash(Vector3 pos, int salt = 0) =>
            Mathf.Abs(Mathf.RoundToInt(pos.x * 73.7f + pos.z * 31.3f) + salt);

        // Rock/Stone 프리팹은 원본이 수 미터급 거석 — 소품 바위 스케일로 내리는 보정 계수 (스크린샷 검증으로 확정)
        private const float k_rockPrefabScale = 0.35f;

        /// <summary>바위 배치 — Rock/Stone 프리팹 변주, 폴백은 반매몰 구체.</summary>
        public static void PlaceRock(Transform parent, Vector3 pos, float scale)
        {
            int h = PosHash(pos, 11);
            var real = PlaceEnv(parent, k_rockSet[h % k_rockSet.Length], pos, h % 360, scale * k_rockPrefabScale, "Rock");
            if (real != null) { AddRockCollider(real); return; }
            var fallback = Part(parent, PrimitiveType.Sphere, pos + new Vector3(0f, 0.12f * scale, 0f),
                 new Vector3(0.5f, 0.3f, 0.45f) * scale, Stone, name: "Rock");
            AddRockCollider(fallback);
        }

        /// <summary>수풀 배치 — Bush/Fern/Plant 프리팹 변주, 폴백은 잎색 구체.</summary>
        public static void PlaceBush(Transform parent, Vector3 pos, float scale)
        {
            int h = PosHash(pos, 23);
            if (PlaceEnv(parent, k_bushSet[h % k_bushSet.Length], pos, h % 360, scale, "Bush") != null) return;
            Part(parent, PrimitiveType.Sphere, pos + new Vector3(0f, 0.25f * scale, 0f),
                 new Vector3(0.8f, 0.5f, 0.8f) * scale, h % 2 == 0 ? LeafDark : LeafLight, name: "Bush");
        }

        /// <summary>
        /// 지피식물 스캐터 — 풀·양치·꽃·버섯을 원형 영역에 흩뿌린다 (고정 시드 = 결정적 배치).
        /// 팩 미임포트 시 아무것도 안 함 (프리미티브 잔풀은 오히려 지저분해서 폴백 없음).
        /// </summary>
        public static void ScatterGroundCover(Transform parent, Vector3 center, float radius, int count, int seed)
        {
            if (!HasEnvAssets) return;
            Transform group = Group(parent, "GroundCover", center);
            var rng = new System.Random(seed);
            for (int i = 0; i < count; i++)
            {
                double angle = rng.NextDouble() * Mathf.PI * 2.0;
                float  dist  = Mathf.Sqrt((float)rng.NextDouble()) * radius; // 면적 균등 분포
                var pos = new Vector3(Mathf.Cos((float)angle) * dist, 0f, Mathf.Sin((float)angle) * dist);
                PlaceEnv(group, k_coverSet[rng.Next(k_coverSet.Length)], pos,
                         (float)(rng.NextDouble() * 360.0), 0.8f + (float)rng.NextDouble() * 0.5f);
            }
        }

        /// <summary>
        /// Ground에 타일링 텍스처 머티리얼 적용 — 텍스처 없으면 fallbackHex 단색 폴백.
        /// tintHex는 텍스처에 곱해지는 무드 틴트 (null이면 원색).
        /// </summary>
        public static void TextureGround(string texName, string fallbackHex, float tiling = 20f, string tintHex = null)
        {
            var ground = GameObject.Find("Ground");
            if (ground == null) return;

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{k_envRoot}/Environments/Textures/{texName}.png");
            if (tex == null) { RecolorGround(fallbackHex); return; }

            Color tint = tintHex != null ? Hex(tintHex) : Color.white;
            string safe = tintHex != null ? tintHex.Replace("#", "") : "ffffff";
            string path = $"{k_materialFolder}/Mat_Ground_{texName}_{safe}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureMaterialFolder();
                mat = new Material(GroundShader())
                {
                    mainTexture = tex,
                    mainTextureScale = new Vector2(tiling, tiling),
                    color = tint,
                };
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(mat, path);
            }
            ground.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Pond를 투명·고광택 수면 머티리얼로 교체 (낚시터 물 실사화).</summary>
        public static void PolishPondWater()
        {
            var pond = GameObject.Find("Pond");
            if (pond == null) return;

            string path = $"{k_materialFolder}/Mat_Water.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                EnsureMaterialFolder();
                mat = new Material(GroundShader()) { color = new Color(0.16f, 0.34f, 0.45f, 0.78f) };
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.95f);
                if (mat.HasProperty("_Surface")) // URP Lit 투명 설정
                {
                    mat.SetFloat("_Surface", 1f);
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                AssetDatabase.CreateAsset(mat, path);
            }
            pond.GetComponent<Renderer>().sharedMaterial = mat;
        }

        private static Shader GroundShader()
        {
            Shader shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");
            return shader != null ? shader : Shader.Find("Standard");
        }

        // ── 기본 조립 ────────────────────────────────────────────────

        public static Transform Group(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        public static GameObject Part(Transform parent, PrimitiveType shape, Vector3 pos, Vector3 scale,
                                      string hex, Vector3 euler = default, string name = "Part")
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // 장식용 — 콜라이더 불필요
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale    = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = Mat(hex);
            // 정적 배칭만 — ContributeGI까지 켜면 라이트맵 베이크 대상이 되므로 제외
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        public static void AddPointLight(Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None; // Quest 성능 — 포인트 그림자 금지
        }

        public static void DestroyRootIfExists(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) Object.DestroyImmediate(root);
        }

        /// <summary>씬의 "Ground" 오브젝트 머티리얼을 교체한다 (그레이박스 → 테마색).</summary>
        public static void RecolorGround(string hex)
        {
            var ground = GameObject.Find("Ground");
            if (ground != null) ground.GetComponent<Renderer>().sharedMaterial = Mat(hex);
        }

        /// <summary>
        /// 씬의 이름이 <paramref name="floorName"/>인 오브젝트를 텔레포트 목적지로 등록한다 —
        /// Meta Interaction SDK의 <see cref="Oculus.Interaction.Locomotion.TeleportInteractable"/>은
        /// <see cref="Oculus.Interaction.Surfaces.ColliderSurface"/>로 감싼 콜라이더가 있어야 아크가
        /// 유효한 착지 지점을 찾는다 — 텔레포트 아크 자체는 뜨는데 "이동 가능한 영역이 없다"는 증상은
        /// 이 배선이 빠졌을 때 정확히 나타난다(2026-07-20 사용자 재현). idempotent — 이미 있으면 참조만 보정.
        ///
        /// <see cref="Oculus.Interaction.DistanceReticles.ReticleDataTeleport"/>도 같은 오브젝트에 필요하다 —
        /// SDK의 InteractorReticle.InteractableSet()이 interactable.TryGetComponent&lt;ReticleDataTeleport&gt;()로
        /// 데이터를 가져오는데 이게 없으면 리티클의 Draw()/Align()이 아예 호출되지 않는다(2026-07-20 실측 —
        /// 리티클 GameObject·머티리얼을 다 맞게 배선해도 이 컴포넌트 하나가 빠지면 영원히 안 보임).
        /// </summary>
        public static void EnsureTeleportSurface(string floorName = "Ground")
        {
            var floor = GameObject.Find(floorName);
            if (floor == null)
            {
                Debug.LogWarning($"[MakeAssets] EnsureTeleportSurface: '{floorName}' 오브젝트 없음");
                return;
            }
            if (floor.GetComponent<Collider>() == null)
            {
                Debug.LogWarning($"[MakeAssets] EnsureTeleportSurface: '{floorName}'에 Collider 없음");
                return;
            }

            WireTeleportInteractable(floor, allowTeleport: true);

            if (floor.GetComponent<Oculus.Interaction.DistanceReticles.ReticleDataTeleport>() == null)
                floor.AddComponent<Oculus.Interaction.DistanceReticles.ReticleDataTeleport>();
        }

        /// <summary>
        /// <paramref name="go"/>를 <see cref="Oculus.Interaction.Locomotion.TeleportInteractable"/>로 등록한다
        /// (콜라이더는 이미 있어야 함). <paramref name="allowTeleport"/>=false면 "장애물"로만 등록돼 착지는
        /// 안 되지만 아크 후보 계산엔 참여한다 — 이게 핵심이다: TeleportCandidateComputer(SDK 소스 직접
        /// 확인, 2026-07-20)는 물리 레이캐스트/레이어마스크가 아니라 **등록된 TeleportInteractable 목록만**
        /// 훑어서 아크와 부딪히는지 검사한다. 나무·바위가 콜라이더는 있어도 이 컴포넌트가 없으면 애초에
        /// 검사 대상이 아니라서 100% 통과한다 — "레이어 문제"로 보였던 관통 현상의 진짜 원인.
        /// idempotent — 이미 있으면 참조/플래그만 보정.
        /// </summary>
        private static void WireTeleportInteractable(GameObject go, bool allowTeleport)
        {
            var collider = go.GetComponent<Collider>();
            if (collider == null) return;

            var surface = go.GetComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            if (surface == null) surface = go.AddComponent<Oculus.Interaction.Surfaces.ColliderSurface>();
            var surfaceSo = new SerializedObject(surface);
            surfaceSo.FindProperty("_collider").objectReferenceValue = collider;
            surfaceSo.ApplyModifiedPropertiesWithoutUndo();

            var teleportable = go.GetComponent<Oculus.Interaction.Locomotion.TeleportInteractable>();
            if (teleportable == null) teleportable = go.AddComponent<Oculus.Interaction.Locomotion.TeleportInteractable>();
            var teleportSo = new SerializedObject(teleportable);
            teleportSo.FindProperty("_surface").objectReferenceValue = surface;
            teleportSo.FindProperty("_allowTeleport").boolValue = allowTeleport;
            teleportSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 공통 소품 ────────────────────────────────────────────────

        public static void BuildTree(Transform parent, Vector3 pos, float scale)
        {
            // 실물 나무 (AO-Trees) — 위치 해시로 종류·방향 결정적 변주
            int h = PosHash(pos);
            var real = PlaceEnv(parent, k_treeSet[h % k_treeSet.Length], pos, h % 360, scale, "Tree");
            if (real != null) { AddTrunkCollider(real); return; }

            Transform tree = Group(parent, "Tree", pos);
            tree.localScale = Vector3.one * scale;
            Part(tree, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f),  new Vector3(0.35f, 0.9f, 0.35f),  Trunk);
            Part(tree, PrimitiveType.Sphere,   new Vector3(0f, 2.1f, 0f),  new Vector3(1.7f, 1.5f, 1.7f),    LeafDark);
            Part(tree, PrimitiveType.Sphere,   new Vector3(0f, 3.05f, 0f), new Vector3(1.15f, 1.05f, 1.15f), LeafLight);
            AddTrunkCollider(tree.gameObject);
        }

        /// <summary>
        /// 나무/바위 관통 방지용 콜라이더 — MeshCollider 대신 렌더러 바운즈로 크기를 잰 프리미티브
        /// 콜라이더를 붙인다(성능·안정성 — Quest에서 비볼록 MeshCollider는 물리 비용이 크고, 이 프로젝트
        /// 소품 상당수가 실물 에셋 프리팹이라 원본 메시 형태를 알 수 없어 바운즈 기반 근사가 유일한 범용
        /// 해법). 텔레포트 아크가 소품을 뚫고 지나가던 문제(tech-stack-decisions.md §텔레포트 이동)의
        /// 후속 수정 — Ground만 유일한 TeleportInteractable이라 콜라이더 없는 소품은 아크가 그냥 통과했다.
        /// idempotent. 실물 에셋 프리팹 상당수가 자체 콜라이더를 이미 갖고 있다(2026-07-20 실측 —
        /// FantasyEnvironments 나무는 전부 CapsuleCollider 내장, 바위 일부는 MeshCollider 내장). 이미
        /// 프리미티브(비-Mesh) 콜라이더가 있으면 그대로 두고, MeshCollider면 지우고 교체한다.
        /// </summary>
        private static void AddTrunkCollider(GameObject go) => ReplaceWithPrimitiveCollider(go, isRock: false);

        /// <summary>바위 전용 콜라이더 — 실루엣 전체를 감싸는 구체. AddTrunkCollider 주석 참조.</summary>
        private static void AddRockCollider(GameObject go) => ReplaceWithPrimitiveCollider(go, isRock: true);

        private static void ReplaceWithPrimitiveCollider(GameObject go, bool isRock)
        {
            var existing = go.GetComponent<Collider>();
            if (existing != null && existing is MeshCollider) Object.DestroyImmediate(existing);

            if (go.GetComponent<Collider>() == null)
            {
                Bounds b = GetLocalBounds(go);
                if (b.size == Vector3.zero) return;

                if (isRock)
                {
                    var sphere = go.AddComponent<SphereCollider>();
                    sphere.center = b.center;
                    sphere.radius = Mathf.Max(b.extents.x, b.extents.z) * 0.85f;
                }
                else
                {
                    var capsule = go.AddComponent<CapsuleCollider>();
                    capsule.direction = 1; // Y축
                    // 캐노피 전체 폭이 아니라 줄기만 — 바운즈 XZ의 15%. 높이는 지면(로컬 Y=0, 이 프로젝트
                    // 나무 에셋 공통 피벗 관례)에서 캐노피 아래쪽까지(전체 높이 75%)만 막아 위쪽은 그대로 둔다.
                    float height = b.size.y * 0.75f;
                    capsule.height = height;
                    capsule.center = new Vector3(b.center.x, height * 0.5f, b.center.z);
                    capsule.radius = Mathf.Min(b.size.x, b.size.z) * 0.15f;
                }
            }

            // 콜라이더(기존 프리미티브 유지분 포함)를 텔레포트 장애물로도 등록 — 그냥 콜라이더만
            // 있으면 부족하다, WireTeleportInteractable 주석 참조.
            WireTeleportInteractable(go, allowTeleport: false);
        }

        /// <summary>자식 렌더러들을 합친 바운즈를 <paramref name="go"/>의 로컬 좌표로 변환해 반환한다.</summary>
        private static Bounds GetLocalBounds(GameObject go)
        {
            // includeInactive: true — 실물 에셋 트리는 LOD 자식 중 일부가 기본 비활성일 수 있다.
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);

            Bounds world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

            Vector3 scale = go.transform.lossyScale;
            Vector3 localCenter = go.transform.InverseTransformPoint(world.center);
            Vector3 localSize = new Vector3(
                world.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                world.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                world.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
            return new Bounds(localCenter, localSize);
        }

        /// <summary>모닥불 — 돌 링 + 장작 + 불꽃 + 웜 라이트. withSeats면 통나무 의자 4개 추가.</summary>
        public static Transform BuildCampfire(Transform parent, Vector3 pos, bool withSeats)
        {
            Transform fire = Group(parent, "Campfire", pos);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                Part(fire, PrimitiveType.Sphere,
                     new Vector3(Mathf.Cos(a) * 0.55f, 0.08f, Mathf.Sin(a) * 0.55f),
                     new Vector3(0.28f, 0.18f, 0.28f), "#7f7f83", name: "Stone");
            }
            Part(fire, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.1f, 0.5f, 0.1f), WoodDark,  new Vector3(90f, 25f, 0f));
            Part(fire, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.1f, 0.5f, 0.1f), WoodDark2, new Vector3(90f, -25f, 0f));

            // 화염 — CFXR 파티클 우선, 없으면 캡슐 폴백
            if (PlaceCfxr(fire, "CFXR Fire", new Vector3(0f, 0.12f, 0f)) == null)
            {
                Part(fire, PrimitiveType.Capsule, new Vector3(0f, 0.42f, 0f), new Vector3(0.28f, 0.3f, 0.28f), Flame,     name: "Flame");
                Part(fire, PrimitiveType.Capsule, new Vector3(0f, 0.38f, 0f), new Vector3(0.16f, 0.2f, 0.16f), FlameCore, name: "FlameCore");
            }
            AddPointLight(fire, new Vector3(0f, 0.8f, 0f), Hex("#ffa052"), 2.0f, 9f);

            if (withSeats)
            {
                float[] seatAngles = { 40f, 140f, 220f, 320f };
                foreach (float deg in seatAngles)
                {
                    float a = deg * Mathf.Deg2Rad;
                    Part(fire, PrimitiveType.Cylinder,
                         new Vector3(Mathf.Cos(a) * 1.5f, 0.22f, Mathf.Sin(a) * 1.5f),
                         new Vector3(0.38f, 0.22f, 0.38f), "#7a5a34", name: "Stump");
                }
            }
            return fire;
        }

        public static Transform BuildTent(Transform parent, Vector3 pos, float yaw, float scale)
        {
            // A프레임 캔버스 텐트 — 실물 에셋 옆에서도 무너져 보이지 않게 능선·뒷면·바닥까지 닫은 형태
            const string canvas   = "#8f7f5e"; // 바랜 올리브 캔버스
            const string canvasDk = "#6b5d43";

            Transform tent = Group(parent, "Tent", pos);
            tent.localRotation = Quaternion.Euler(0f, yaw, 0f);
            tent.localScale = Vector3.one * scale;

            // 경사 패널 2장 (35°) — 꼭대기 y≈1.03에서 만나는 Λ자.
            // z+회전은 윗부분을 -x로 눕히므로 왼쪽(-x) 패널이 -35°, 오른쪽이 +35° (부호 반대면 V자로 무너져 보임)
            Part(tent, PrimitiveType.Cube, new Vector3(-0.36f, 0.52f, 0f), new Vector3(0.04f, 1.25f, 1.4f), canvas, new Vector3(0f, 0f, -35f));
            Part(tent, PrimitiveType.Cube, new Vector3(0.36f, 0.52f, 0f),  new Vector3(0.04f, 1.25f, 1.4f), canvas, new Vector3(0f, 0f, 35f));
            // 능선 폴 + 앞뒤 받침 폴
            Part(tent, PrimitiveType.Cylinder, new Vector3(0f, 1.03f, 0f), new Vector3(0.05f, 0.74f, 0.05f), WoodDark, new Vector3(90f, 0f, 0f), name: "Ridge");
            Part(tent, PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0.7f),  new Vector3(0.04f, 0.52f, 0.04f), WoodDark, name: "Pole");
            Part(tent, PrimitiveType.Cylinder, new Vector3(0f, 0.5f, -0.7f), new Vector3(0.04f, 0.52f, 0.04f), WoodDark, name: "Pole");
            // 뒷면 마감 + 바닥 시트
            Part(tent, PrimitiveType.Cube, new Vector3(0f, 0.45f, -0.66f), new Vector3(0.64f, 0.92f, 0.035f), canvasDk, name: "Back");
            Part(tent, PrimitiveType.Cube, new Vector3(0f, 0.012f, 0f),    new Vector3(0.8f, 0.024f, 1.5f),   canvasDk, name: "GroundSheet");
            return tent;
        }

        public static void BuildLanternPost(Transform parent, Vector3 pos)
        {
            Transform post = Group(parent, "LanternPost", pos);
            Part(post, PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(0.07f, 0.85f, 0.07f), "#4a3a28");
            var glow = Part(post, PrimitiveType.Cube, new Vector3(0f, 1.80f, 0f), new Vector3(0.13f, 0.17f, 0.13f), Glow, name: "Glow");
            glow.GetComponent<Renderer>().sharedMaterial = MatEmissive(Glow); // 밤에도 빛나 보이게 에미션
            Part(post, PrimitiveType.Cube,     new Vector3(0f, 1.905f, 0f), new Vector3(0.17f, 0.04f, 0.17f), Gold, name: "Cap");
            PlaceCfxr(post, "CFXR3 LightGlow A (Loop)", new Vector3(0f, 1.78f, 0f), 0.35f); // 은은한 글로우 (없으면 생략)
            AddPointLight(post, new Vector3(0f, 1.78f, 0f), Hex("#ffe2a8"), 1.4f, 6f);
        }

        /// <summary>분기점→목적지 디딤돌 길 (지그재그 오프셋).</summary>
        public static void LayPathStones(Transform parent, Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            dir.y = 0f;
            float length = dir.magnitude;
            dir /= length;
            Vector3 side = Vector3.Cross(Vector3.up, dir);

            int i = 0;
            for (float d = 1.0f; d < length - 1.0f; d += 1.1f, i++)
            {
                Vector3 p = from + dir * d + side * (i % 2 == 0 ? 0.18f : -0.18f);
                Part(parent, PrimitiveType.Cylinder, new Vector3(p.x, 0.015f, p.z),
                     new Vector3(0.38f, 0.02f, 0.38f), PathGray, name: "PathStone");
            }
        }

        // ── 스폰포인트 / EventSystem ─────────────────────────────────

        /// <summary>씬에 PlayerSpawnPoint가 없으면 추가 (컴포넌트 존재 기준 — GO 이름에 의존하지 않음).</summary>
        public static void EnsureSpawnPoint(Vector3 position, Quaternion rotation)
        {
            if (Object.FindFirstObjectByType<PlayerSpawnPoint>() != null) return;

            var spawn = new GameObject("PlayerSpawn");
            spawn.AddComponent<PlayerSpawnPoint>();
            spawn.transform.SetPositionAndRotation(position, rotation);
        }

        /// <summary>씬당 EventSystem 1개 보장 (PointableCanvasModule 포함 프리팹 — VR UI 클릭 필수).</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var esPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_eventSystemPrefabPath);
            if (esPrefab != null) PrefabUtility.InstantiatePrefab(esPrefab);
            else Debug.LogWarning($"[MakeAssets] {k_eventSystemPrefabPath} 없음 — VR UI 클릭 불가. " +
                                  "Tools > Make Assets > VR UI (Create All) 먼저 실행 후 재빌드.");
        }

        // ── 포탈 관문 (GDD 그림 1 색 코딩) ───────────────────────────

        /// <summary>
        /// 관문 하나 — 기둥+보+테마색 현판 프레임에 단일 버튼 패널(PortalPanelController)을 건다.
        /// facingFrom(보통 스폰)을 바라보게 회전. VR UI 프리팹이 없으면 경고 후 null.
        /// </summary>
        public static Transform BuildPortalGate(Transform parent, string title, string sceneName, Vector3 position,
                                                Vector3 facingFrom, string bannerHex, Color panelTint,
                                                string buttonLabel = "이동",
                                                VRUISkin.PillColor pill = VRUISkin.PillColor.Purple)
        {
            var panelPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(k_panelPrefabPath);
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_buttonPrefabPath);
            if (panelPrefab == null || buttonPrefab == null)
            {
                Debug.LogWarning("[MakeAssets] VR UI 프리팹 없음 — 포탈 관문 생략. " +
                                 "Tools > Make Assets > VR UI (Create All) 실행 후 맵 재빌드.");
                return null;
            }

            Transform gate = Group(parent, $"Gate_{sceneName}", position);
            // 관문 정면이 facingFrom을 향하게 — +Z를 반대로 두면 패널(-Z에서 읽힘)이 그쪽을 본다
            Vector3 away = position - facingFrom;
            away.y = 0f;
            gate.rotation = Quaternion.LookRotation(away.normalized);

            Part(gate, PrimitiveType.Cylinder, new Vector3(0.5f, 1.0f, 0.05f),  new Vector3(0.09f, 1.0f, 0.09f), Wood);
            Part(gate, PrimitiveType.Cylinder, new Vector3(-0.5f, 1.0f, 0.05f), new Vector3(0.09f, 1.0f, 0.09f), Wood);
            Part(gate, PrimitiveType.Cube,     new Vector3(0f, 2.02f, 0.05f),   new Vector3(1.3f, 0.1f, 0.12f),  WoodDark);
            Part(gate, PrimitiveType.Cube,     new Vector3(0f, 2.28f, 0.05f),   new Vector3(1.15f, 0.3f, 0.06f), bannerHex, name: "Banner");

            // 패널 — 씬 전용 구성이라 완전 언팩(크기·버튼 치환·컨트롤러 추가 자유)
            var panelGo = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab);
            PrefabUtility.UnpackPrefabInstance(panelGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            panelGo.name = "PortalPanel";
            panelGo.transform.SetParent(gate, false);
            panelGo.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            panelGo.transform.localScale = Vector3.one * 0.0018f;

            // 단일 버튼용 크기 — 흰 카드 + 헤더 걸침 + 버튼 하나 (0.65 x 0.59 m).
            // 인터랙션 자식은 anchors 0-1이라 따라온다.
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.sizeDelta = new Vector2(360f, 330f);
            // 색 코딩: 헤더 필 + 버튼을 목적지 색으로 (흰 카드 본체는 원색 유지). 스킨 없으면 기존 틴트 방식.
            var panelImg = panelGo.GetComponent<Image>();
            if (panelImg.sprite == null) panelImg.color = panelTint;
            var headerChild = panelGo.transform.Find("Header");
            if (headerChild != null)
                VRUISkin.TryApplyPill(headerChild.GetComponent<Image>(), pill);

            var panel = panelGo.GetComponent<VRUIPanel>();

            RectTransform content = panel.Content;
            for (int i = content.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(content.GetChild(i).gameObject);

            var btnGo = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab);
            PrefabUtility.UnpackPrefabInstance(btnGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            btnGo.transform.SetParent(content, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 1f);
            btnRt.anchorMax = new Vector2(0.5f, 1f);
            btnRt.pivot     = new Vector2(0.5f, 1f);
            btnRt.anchoredPosition = new Vector2(0f, -60f); // 헤더 아래 본문 중앙 (콘텐츠 기준)

            var btnLabel = btnGo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (btnLabel != null) btnLabel.text = buttonLabel; // 에디터에서도 보이게 굽기 (런타임 Push와 별개)
            var btnImg = btnGo.GetComponent<Image>();
            bool pillApplied = btnImg != null && btnImg.sprite != null && VRUISkin.TryApplyPill(btnImg, pill); // 헤더와 동색
            if (pillApplied && btnLabel != null) btnLabel.color = VRUISkin.PillText(pill);
            var button = btnGo.GetComponent<VRUIButton>();

            // 제목도 에디터 시점에 굽는다 (버튼 제거 후 남은 TMP = Title)
            var title2 = panelGo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (title2 != null && title2 != btnLabel)
            {
                title2.text = title;
                if (pillApplied) title2.color = VRUISkin.PillText(pill); // 헤더 필 밝기에 맞는 라벨색
            }

            var controller = panelGo.AddComponent<PortalPanelController>();
            var so = new SerializedObject(controller);
            so.FindProperty("m_panel").objectReferenceValue = panel;
            so.FindProperty("m_title").stringValue = title;

            var btnProp = so.FindProperty("m_buttons");
            btnProp.arraySize = 1;
            btnProp.GetArrayElementAtIndex(0).objectReferenceValue = button;

            var destProp = so.FindProperty("m_destinations");
            destProp.arraySize = 1;
            var element = destProp.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("label").stringValue = buttonLabel;
            element.FindPropertyRelative("sceneName").stringValue = sceneName;
            so.ApplyModifiedPropertiesWithoutUndo();

            return gate;
        }

        /// <summary>로비 귀환 관문 — 활동 공간 공통 (그림 1의 로비=회색 코딩).</summary>
        public static Transform BuildLobbyReturnGate(Transform parent, Vector3 position, Vector3 facingFrom)
        {
            return BuildPortalGate(parent, "로비", "Lobby", position, facingFrom,
                                   LobbyGray, new Color(0.13f, 0.13f, 0.15f, 0.92f), "귀환");
        }

        // ── 라이팅 프리셋 ────────────────────────────────────────────

        public static void ConfigureSun(Vector3 euler, string colorHex, float intensity)
        {
            var sunGo = GameObject.Find("Directional Light");
            Light sun = sunGo != null ? sunGo.GetComponent<Light>() : null;
            if (sun == null)
            {
                sunGo = new GameObject("Directional Light");
                sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sunGo.transform.rotation = Quaternion.Euler(euler);
            sun.color = Hex(colorHex);
            sun.intensity = intensity;
            RenderSettings.sun = sun;
        }

        public static void ConfigureLinearFog(string colorHex, float start, float end)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = start;
            RenderSettings.fogEndDistance = end;
            RenderSettings.fogColor = Hex(colorHex);
        }

        /// <summary>절차적 스카이박스 머티리얼 생성/로드 후 씬에 적용 (씬 무드별 이름 분리).</summary>
        public static void ApplySkybox(string assetName, float atmosphere, float exposure,
                                       string skyTintHex, string groundHex)
        {
            string path = $"{k_materialFolder}/{assetName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Skybox/Procedural");
                if (shader == null)
                {
                    Debug.LogWarning("[MakeAssets] Skybox/Procedural 셰이더 없음 — 스카이박스 생략");
                    return;
                }
                EnsureMaterialFolder();
                mat = new Material(shader);
                mat.SetFloat("_AtmosphereThickness", atmosphere);
                mat.SetFloat("_Exposure", exposure);
                mat.SetColor("_SkyTint", Hex(skyTintHex));
                mat.SetColor("_GroundColor", Hex(groundHex));
                AssetDatabase.CreateAsset(mat, path);
            }
            RenderSettings.skybox = mat;
        }

        // 데스크톱(비-VR) 프리뷰용 — VR에선 리그 카메라가 사용됨
        public static void PlacePreviewCamera(Vector3 pos, Vector3 euler)
        {
            var camera = GameObject.Find("Main Camera");
            if (camera != null)
                camera.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
        }

        // ── 머티리얼 ─────────────────────────────────────────────────

        // 색상별 공유 머티리얼 캐시 — PlaceholderArtFactory와 동일 경로(Mat_PH_*) 재사용, 드로우콜 절약
        public static Material Mat(string hex)
        {
            string safe = hex.Replace("#", "");
            string path = $"{k_materialFolder}/Mat_PH_{safe}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            EnsureMaterialFolder();
            Shader shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Standard");

            mat = new Material(shader) { color = Hex(hex) };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>발광 머티리얼 (랜턴 갓 등) — 베이스색 + 에미션, 색상별 공유 캐시.</summary>
        public static Material MatEmissive(string hex, float intensity = 1.6f)
        {
            string safe = hex.Replace("#", "");
            string path = $"{k_materialFolder}/Mat_PH_E_{safe}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            EnsureMaterialFolder();
            mat = new Material(GroundShader()) { color = Hex(hex) };
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.SetColor("_EmissionColor", Hex(hex) * intensity);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(k_materialFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Materials");
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
#endif
