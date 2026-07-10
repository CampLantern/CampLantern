using System.Collections.Generic;
using CampLantern.Core;
using UnityEngine;

namespace CampLantern.Combat.Data
{
    /// <summary>
    /// 몬스터 정의 — 전투 스탯·반경·어그로 계수·스킬 목록.
    /// Id/primary key는 넣지 않는다 (지시서 방침 — 정식 스키마 때 일괄 부여).
    /// ContentRegistry에도 등록하지 않는다 (전투 데이터는 저장 파일에서 참조되지 않음).
    /// 사냥 보상·필요 인원은 Core.HuntTargetDef 소관 — 관계 정리는 step-12에서 결정.
    /// Spec: 지시서 1단계 (§4-1), 어그로 필드는 §4-3 (step-05에서 소비 — 스키마만 선제 확정)
    /// </summary>
    [CreateAssetMenu(menuName = "CampLantern/Combat/Monster", fileName = "Monster_")]
    public class MonsterData : ScriptableObject
    {
        [Tooltip("HP 기준값 — HP 스케일링(§4-2)의 기준. 스케일링 로직은 step-09")]
        public int baseHp;

        [Tooltip("활동 반경 — Idle 배회 범위 (스폰 지점 기준)")]
        public float roamRadius;

        [Tooltip("감지 반경 — 플레이어 진입 시 Chase 전환")]
        public float detectRadius;

        [Tooltip("추격 반경 — 가장 가까운 플레이어가 이 밖이면 Return")]
        public float chaseRadius;

        public float idleMoveSpeed;

        public float chaseMoveSpeed;

        [Tooltip("약점(머리) 적중 데미지 배율 — 수신 측(MonsterHealth)이 적용")]
        public float weakpointMultiplier = 1.5f;

        [Header("어그로 (§4-3)")]
        [Tooltip("히스테리시스 — 현재 타겟보다 이 거리 이상 가까워야 타겟 전환. TODO(TUNING): 임시 2m")]
        public float aggroHysteresisMeters = 2f;

        [Tooltip("약점 유효타 시 어그로 고정 시간(초). TODO(TUNING): 임시 5s")]
        public float weakpointLockSeconds = 5f;

        [Tooltip("고정 중 타 플레이어 약점 타격 시 잔여 고정시간 감쇄량(초). TODO(TUNING): 임시 1.5s")]
        public float weakpointDecaySeconds = 1.5f;

        [Header("스킬 (§4-5)")]
        public List<SkillItem> skills;

        [Header("사냥 통합 (combat-detailed-network)")]
        [Tooltip("보상(RewardMaterials)·필요 인원(RequiredParticipants)·저장 Id는 HuntTargetDef 소관 — " +
                 "전투 스탯만 MonsterData. null이면 순수 로컬 몬스터. " +
                 "HuntTargetDef.MaxHealth는 구 HuntTarget 라인 전용(중복 정리는 정식 스키마 때 — TODO(SPEC))")]
        public HuntTargetDef huntDef;
    }
}
