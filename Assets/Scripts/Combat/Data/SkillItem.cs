using System;
using UnityEngine;

namespace CampLantern.Combat.Data
{
    /// <summary>스킬 판정 타입 (§4-5).</summary>
    public enum SkillType
    {
        Point,    // 지점 폭발 — 단일 시점 범위 판정
        SelfAoe,  // 자기 중심 폭발 — 단일 시점 범위 판정
        Melee,    // 근접 — hit window 구간 판정 (부채꼴)
        Ranged,   // 원거리 — 발사체/레이저 서브타입
    }

    /// <summary>Ranged 스킬 서브타입 — 발사체(경로 판정)와 레이저(빔 판정) 구분 (§4-5).</summary>
    public enum RangedSubtype
    {
        Projectile,
        Beam,
    }

    /// <summary>
    /// 몬스터 스킬 1개 정의 — MonsterData에 임베디드되는 [Serializable] flat 클래스.
    ///
    /// SO 분리 대신 임베디드: P0 몬스터 수가 적고 몬스터별 독립 튜닝이 기본이라 공유 이점이 없다.
    /// 타입별 서브클래스+SerializeReference 대신 flat: 다형 직렬화 함정 회피
    /// (knowledge/unity-scripting-gotchas.md). 타입별 필드는 Header로 구분하고 해당 타입만 읽는다.
    /// Spec: 지시서 1단계 (§4-5), 판정 규칙은 §4-6 (step-06)
    /// </summary>
    [Serializable]
    public class SkillItem
    {
        public string displayName;

        public SkillType skillType;

        public int damage;

        [Tooltip("재사용 대기시간(초)")]
        public float cooldown;

        [Tooltip("시전 종료 후 무방비(Recover) 시간(초)")]
        public float recoverTime;

        [Tooltip("시전 시간(초) — 애니메이션 없이도 타이머로 동작. 클립 확정 후 step-07 헬퍼가 클립 길이로 동기화")]
        public float animDuration;

        [Tooltip("판정 창 시작 — animDuration 대비 0~1 정규화 비율")]
        [Range(0f, 1f)] public float hitWindowStart;

        [Tooltip("판정 창 끝 — Melee만 start<end 구간, 나머지 타입은 start==end 단일 시점")]
        [Range(0f, 1f)] public float hitWindowEnd;

        [Tooltip("이 거리 이상일 때만 시전 후보 (0 = 제한 없음)")]
        public float minEngage;

        [Tooltip("미사용 — 스킬 선택은 균등 랜덤 유지 (지시서 5단계 명시. 필드만 읽고 로직 금지)")]
        public float weight = 1f;

        [Header("Point")]
        [Tooltip("시전 가능 최대 거리")]
        public float maxCastRange;

        [Header("Point/SelfAoe")]
        [Tooltip("폭발 반경")]
        public float aoeRadius;

        [Header("Melee")]
        [Tooltip("근접 판정 사거리")]
        public float range;
        [Tooltip("전방 부채꼴 각도(deg)")]
        public float arcAngle;

        [Header("Ranged")]
        public RangedSubtype rangedSubtype;
        public int projectileCount;
        public float projectileSpeed;
        [Tooltip("레이저 시전 높이")]
        public float castHeight;
        [Tooltip("레이저 빔 폭")]
        public float beamWidth;

        [Header("미사용 - 추후")]
        [Tooltip("상태이상 부여 — TODO(SPEC): 스펙 미정, 자리만")]
        public string statusEffectId;
    }
}
