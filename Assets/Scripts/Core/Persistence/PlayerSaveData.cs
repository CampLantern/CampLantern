using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampLantern.Core.Persistence
{
    /// <summary>
    /// 로컬 JSON 저장 스키마. 나중에 서버 백엔드로 교체할 때 API 응답 모델로 재사용 가능하도록
    /// ScriptableObject 참조가 아닌 Id 문자열로만 구성한다 (ItemDef.Id — "저장/네트워크 동기화 키").
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        public int Coins;
        public List<ItemStackSave> Inventory = new List<ItemStackSave>();
        public List<ItemStackSave> OwnedEstateDefs = new List<ItemStackSave>();
        public List<PlacedObjectSave> PlacedObjects = new List<PlacedObjectSave>();

        // 낚싯대 소모품 — 미끼 구매 코인이 재진입 리필로 무력화되지 않게 영속화.
        // -1 = 기록 없음(구 저장 파일/신규) → 낚싯대 기본값 사용. JsonUtility는 JSON에 없는
        // 필드를 초기값으로 두므로 구 파일 마이그레이션이 자동으로 안전하다.
        public int BaitCount = -1;
        public int RodDurability = -1;
    }

    [Serializable]
    public class ItemStackSave
    {
        public string Id;
        public int Count;
    }

    [Serializable]
    public class PlacedObjectSave
    {
        public string DefId;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
