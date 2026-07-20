using CampLantern.Core;
using CampLantern.Player;
using UnityEngine;

namespace CampLantern.Cooking
{
    /// <summary>
    /// 물리 요리 세트 런타임 조립기 — 냄비에 투입구(PotInteractionZone)를 붙이고, 옆에 재료 선반과 국자를 세운다.
    /// HUD류와 같은 "코드 생성" 패턴: 하네스가 Start에서 호출하며 씬/프리팹 수정이 필요 없다(RULE-02 무관).
    /// 단, 그랩이 붙는 프리팹(IngredientPickup/CookingLadle)은 에디터 팩토리 산출물(Resources)을 쓴다 —
    /// Meta Interaction 타입은 에디터 팩토리에서만 리플렉션으로 배선하는 프로젝트 컨벤션(컴파일 격리) 유지.
    /// 선행 조건: Tools > Make Assets > Physical Cooking (Create All) 실행(프리팹 2종 생성).
    /// </summary>
    public static class PhysicalCookingBuilder
    {
        private const string k_pickupPrefabName = "IngredientPickup";
        private const string k_ladlePrefabName  = "CookingLadle";

        private static readonly int k_baseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// 냄비 기준으로 세트를 조립한다. 반환된 존으로 Rejected 피드백을 구독할 수 있다.
        /// 프리팹 미생성 등 선행 조건 미충족이면 에러 로그 후 null.
        /// </summary>
        public static PotInteractionZone Build(CookingPot pot, Inventory inventory)
        {
            if (pot == null || inventory == null) return null;

            var pickupPrefab = Resources.Load<IngredientPickup>(k_pickupPrefabName);
            var ladlePrefab  = Resources.Load<StirTool>(k_ladlePrefabName);
            if (pickupPrefab == null || ladlePrefab == null)
            {
                Debug.LogError("[PhysicalCooking] 프리팹 없음 — Tools > Make Assets > Physical Cooking (Create All) 실행 필요");
                return null;
            }

            PotInteractionZone zone = BuildIntakeZone(pot);
            Transform shelf = BuildShelf(pot, inventory, pickupPrefab, out Transform ladleAnchor);
            SpawnLadle(ladlePrefab, ladleAnchor);

            return zone;
        }

        // ── 냄비 림 트리거 ───────────────────────────────────────────
        private static PotInteractionZone BuildIntakeZone(CookingPot pot)
        {
            var go = new GameObject("IntakeZone");
            go.transform.SetParent(pot.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.0f, 0f); // 냄비 비주얼(높이 0.8) 바로 위

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.35f;

            var zone = go.AddComponent<PotInteractionZone>();
            zone.Initialize(pot);
            return zone;
        }

        // ── 재료 선반 (그레이박스 프리미티브) ────────────────────────
        private static Transform BuildShelf(CookingPot pot, Inventory inventory,
                                            IngredientPickup pickupPrefab, out Transform ladleAnchor)
        {
            // 냄비 오른편 1.1m — 영지 씬 기준 요리·판매 패널(왼편)과 반대쪽이라 동선이 겹치지 않는다
            var root = new GameObject("IngredientShelf");
            root.transform.position = pot.transform.TransformPoint(new Vector3(1.1f, 0f, 0f));
            root.transform.rotation = pot.transform.rotation;

            Color wood = new Color(0.45f, 0.32f, 0.2f);

            // 상판·받침 — 프리미티브 기본 콜라이더를 그대로 둔다(정적 트레이, Rigidbody 없음 — RULE-03 무관)
            AddBox(root.transform, "Top",      new Vector3(0f, 0.775f, 0f),  new Vector3(0.9f, 0.05f, 0.5f),  wood);
            AddBox(root.transform, "Pedestal", new Vector3(0f, 0.375f, 0f),  new Vector3(0.18f, 0.75f, 0.18f), wood);

            // 가장자리 턱 — 프록시가 굴러 떨어지는 것 방지
            Color rim = new Color(0.35f, 0.24f, 0.15f);
            AddBox(root.transform, "RimFront", new Vector3(0f, 0.825f, -0.24f), new Vector3(0.9f, 0.05f, 0.02f), rim);
            AddBox(root.transform, "RimBack",  new Vector3(0f, 0.825f,  0.24f), new Vector3(0.9f, 0.05f, 0.02f), rim);
            AddBox(root.transform, "RimLeft",  new Vector3(-0.44f, 0.825f, 0f), new Vector3(0.02f, 0.05f, 0.5f), rim);
            AddBox(root.transform, "RimRight", new Vector3( 0.44f, 0.825f, 0f), new Vector3(0.02f, 0.05f, 0.5f), rim);

            // 슬롯 앵커 2×4 — 상판 위
            float[] xs = { -0.32f, -0.11f, 0.11f, 0.32f };
            float[] zs = { -0.12f, 0.12f };
            var slots = new Transform[xs.Length * zs.Length];
            int index = 0;
            foreach (float z in zs)
            {
                foreach (float x in xs)
                {
                    var slot = new GameObject($"Slot_{index}");
                    slot.transform.SetParent(root.transform, false);
                    slot.transform.localPosition = new Vector3(x, 0.87f, z);
                    slots[index++] = slot.transform;
                }
            }

            var shelf = root.AddComponent<IngredientShelf>();
            shelf.Initialize(inventory, pickupPrefab, slots);

            // 국자 낙사 복귀 지점 — 상판 오른쪽 끝 위
            var anchor = new GameObject("LadleAnchor");
            anchor.transform.SetParent(root.transform, false);
            anchor.transform.localPosition = new Vector3(0.36f, 0.95f, 0f);
            anchor.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); // 눕혀서 상판에 안착
            ladleAnchor = anchor.transform;

            return root.transform;
        }

        private static void SpawnLadle(StirTool prefab, Transform anchor)
        {
            StirTool ladle = Object.Instantiate(prefab, anchor.position, anchor.rotation);
            ladle.SetAnchor(anchor.position, anchor.rotation);

            // 커스텀 손 부착(낚싯대와 동일 방식). 국자 손잡이 실린더는 rod-local y=0~0.36, 스쿱=0.38 →
            // 하단 손잡이 (0, 0.13, 0)를 손에 쥔다. 놓으면 거치대(스폰 앵커)로 복귀. TODO(TUNING): 실기 확인
            ladle.gameObject.AddComponent<HeldItemGrabber>().Configure(
                gripLocalPoint: new Vector3(0f, 0.13f, 0f),
                heldEuler: new Vector3(10f, 0f, 0f),
                palmOffset: new Vector3(0f, -0.02f, 0.03f),
                hand: HeldItemGrabber.HandSide.Auto,
                releaseMode: HeldItemGrabber.ReleaseMode.ReturnToDock);
        }

        // 프리미티브 큐브 생성 — 기본 URP 머티리얼에 MPB로 색만 입힌다(머티리얼 인스턴스 누수 없음)
        private static void AddBox(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localScale = localScale;

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(k_baseColorId, color);
            box.GetComponent<Renderer>().SetPropertyBlock(mpb);
        }
    }
}
