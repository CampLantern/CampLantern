using System.Collections.Generic;
using CampLantern.Estate;
using UnityEngine;

namespace CampLantern.Core.Persistence
{
    /// <summary>
    /// Wallet/Inventory/EstateShop을 소유하고 로컬 JSON과 동기화한다.
    /// 씬(공간)마다 새로 만들어지지만 Load()가 디스크의 마지막 저장 상태를 복원하므로
    /// 로비→낚시터→영지 이동에도 코인·아이템이 이어진다.
    /// 오프라인 영지 방문(다른 유저가 내 영지를 읽는 것)은 이걸로 해결되지 않는다 — 그건 서버가 필요
    /// (room-architecture.md, tech-stack-decisions.md "영구 저장 백엔드" 항목 참조).
    /// </summary>
    public class PlayerState
    {
        // 시작 코인 — 최초 실행(저장 파일 없음)에만 지급. 하네스별 지급은 첫 진입 씬이
        // 영지가 아니면(예: 로비→낚시터 동선) 첫 포획 Save가 파일을 먼저 만들어 영영 미지급되는
        // 결함이 있어, 어느 씬이 최초든 동일하게 적용되도록 여기서 중앙 처리한다.
        private const int k_startingCoins = 100;

        public Wallet Wallet { get; }
        public Inventory Inventory { get; }
        public EstateShop Shop { get; }

        /// <summary>Load() 직후 채워지는 배치 복원 목록 — 영지 씬이 EstateManager.Place로 적용한다.</summary>
        public IReadOnlyList<PlacedObjectSave> PendingPlacements { get; private set; } = new List<PlacedObjectSave>();

        /// <summary>
        /// 낚싯대 소모품 (미끼·내구도). Load가 디스크 값을 채우고, 낚싯대가 있는 씬의 하네스가
        /// 낚싯대에 Push한 뒤 저장 전에 최신 값을 되써 넣는다. -1 = 기록 없음 —
        /// 배치 목록과 같은 원칙으로, 낚싯대를 모르는 씬은 -1을 유지해 디스크 값을 건드리지 않는다.
        /// </summary>
        public int BaitCount { get; set; } = -1;
        public int RodDurability { get; set; } = -1;

        public PlayerState()
        {
            Wallet    = new Wallet();
            Inventory = new Inventory();
            Shop      = new EstateShop(Wallet, Inventory);
        }

        /// <summary>디스크에서 로드해 Wallet/Inventory/Shop에 반영한다. 씬 시작 시 1회 호출.</summary>
        public void Load(ContentRegistry registry)
        {
            bool isNewSave = !SaveService.Exists();

            PlayerSaveData data = SaveService.Load();

            if (isNewSave) Wallet.Add(k_startingCoins);
            if (data.Coins > 0) Wallet.Add(data.Coins);

            foreach (ItemStackSave stack in data.Inventory)
            {
                if (registry.TryGetItem(stack.Id, out ItemDef def))
                    Inventory.Add(def, stack.Count);
                else
                    Debug.LogWarning($"[PlayerState] 저장된 아이템 Id를 찾을 수 없음(ContentRegistry 갱신 필요): {stack.Id}");
            }

            foreach (ItemStackSave stack in data.OwnedEstateDefs)
            {
                if (registry.TryGetEstateObject(stack.Id, out EstateObjectDef def))
                    for (int i = 0; i < stack.Count; i++) Shop.ReturnOwned(def); // 기존 공개 API로 보유 목록 복원
                else
                    Debug.LogWarning($"[PlayerState] 저장된 영지 오브젝트 Id를 찾을 수 없음(ContentRegistry 갱신 필요): {stack.Id}");
            }

            PendingPlacements = data.PlacedObjects;
            BaitCount         = data.BaitCount;
            RodDurability     = data.RodDurability;
        }

        /// <summary>
        /// 현재 상태를 디스크에 저장한다. estateManager가 있으면(영지 씬) 배치 목록도 함께 갱신하고,
        /// 없으면(낚시터/사냥터 씬) 디스크에 있던 배치 목록을 그대로 보존한다 — 이 씬은 배치를 모르므로
        /// 건드리면 안 됨.
        /// pendingPotIngredients: 냄비에 투입됐지만 아직 조리되지 않은 재료. 메모리 인벤토리에서는
        /// 투입 시점에 이미 빠져 있지만, 냄비는 씬 소속이라 조리 전에 씬을 떠나면 내용물이 사라지므로
        /// 디스크에는 인벤토리 보유분으로 합산 기록한다 — 조리 후의 Save가 정확한 상태로 다시 덮어쓴다.
        /// </summary>
        public void Save(EstateManager estateManager = null, IReadOnlyList<ItemDef> pendingPotIngredients = null)
        {
            PlayerSaveData data = SaveService.Load(); // baseline — 이 씬이 모르는 필드(예: 배치 목록) 보존

            data.Coins = Wallet.Coins;

            // -1(기록 없음)은 쓰지 않는다 — 구 저장 파일의 값을 실수로 지우지 않기 위한 가드
            if (BaitCount >= 0) data.BaitCount = BaitCount;
            if (RodDurability >= 0) data.RodDurability = RodDurability;

            data.Inventory.Clear();
            foreach (KeyValuePair<ItemDef, int> entry in Inventory.Items)
                data.Inventory.Add(new ItemStackSave { Id = entry.Key.Id, Count = entry.Value });

            if (pendingPotIngredients != null)
            {
                foreach (ItemDef ingredient in pendingPotIngredients)
                {
                    if (ingredient == null) continue;
                    AddToStackList(data.Inventory, ingredient.Id, 1);
                }
            }

            data.OwnedEstateDefs.Clear();
            foreach (KeyValuePair<EstateObjectDef, int> entry in Shop.OwnedDefs)
                data.OwnedEstateDefs.Add(new ItemStackSave { Id = entry.Key.Id, Count = entry.Value });

            if (estateManager != null)
            {
                data.PlacedObjects.Clear();
                foreach (PlacedObject placed in estateManager.PlacedObjects)
                {
                    // 씬 종료/티어다운 순서에 따라 배치 오브젝트가 먼저 파괴됐을 수 있다
                    // (예: Play 종료 시 P0Harness.OnDestroy에서 Save 호출). Unity의 == null은
                    // 파괴된 오브젝트도 잡아내므로 여기서 걸러 MissingReferenceException을 막는다.
                    if (placed == null) continue;

                    data.PlacedObjects.Add(new PlacedObjectSave
                    {
                        DefId    = placed.Def.Id,
                        Position = placed.transform.position,
                        Rotation = placed.transform.rotation,
                    });
                }
            }

            SaveService.Save(data);
        }

        private static void AddToStackList(List<ItemStackSave> stacks, string id, int count)
        {
            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].Id != id) continue;
                stacks[i].Count += count;
                return;
            }
            stacks.Add(new ItemStackSave { Id = id, Count = count });
        }
    }
}
