using System;
using System.Collections.Generic;
using CampLantern.Core;
using UnityEngine;

namespace CampLantern.Cooking
{
    /// <summary>
    /// 냄비 — 재료 투입·조리 실행. 레시피는 사전 공개하지 않으며 조합 시도로 발견한다
    /// (domain/resource-loop.md). P0에서는 Cook() 즉시 판정 — 30~60초 수동 조리 인터랙션은 P1.
    /// 힌트 시스템·숙련도도 P1 — 여기서는 구현하지 않는다.
    /// </summary>
    public class CookingPot : MonoBehaviour
    {
        [Tooltip("매칭 대상 레시피 목록 — 데이터 에셋 참조")]
        [SerializeField] private RecipeDef[] m_recipes;

        [Tooltip("매칭 실패 시 지급할 실패작 아이템 (저가 판매/개그 요소 — resource-loop.md)")]
        [SerializeField] private ItemDef m_failResult;

        /// <summary>조리 완료 시 발화 — 성공 요리 또는 실패작. 구독자는 OnDestroy/OnDisable에서 반드시 해제할 것 (rules/scripts.md).</summary>
        public event Action<ItemDef> Cooked;

        /// <summary>재료 투입 성공 시 발화 — 물리 투입(PotInteractionZone)·IMGUI 공통 피드백 훅. 구독 해제 규칙 동일.</summary>
        public event Action<ItemDef> IngredientAdded;

        private Inventory m_inventory;
        private RecipeMatcher m_matcher;
        private readonly List<ItemDef> m_ingredients = new List<ItemDef>();

        /// <summary>현재 투입된 재료 (읽기 전용).</summary>
        public IReadOnlyList<ItemDef> Ingredients => m_ingredients;

        /// <summary>
        /// 소유 Manager가 준비된 시점에 주입한다 (rules/scripts.md — Push 초기화 원칙).
        /// 레시피는 인스펙터 세팅(m_recipes)을 사용한다.
        /// </summary>
        public void Initialize(Inventory inventory)
        {
            Initialize(inventory, m_recipes, m_failResult);
        }

        /// <summary>레시피·실패작까지 코드로 주입하는 오버로드 (인스펙터 세팅 무관 — 테스트/코드 구성용).</summary>
        public void Initialize(Inventory inventory, IReadOnlyList<RecipeDef> recipes, ItemDef failResult)
        {
            m_inventory  = inventory;
            m_failResult = failResult;
            m_matcher    = new RecipeMatcher(recipes);
        }

        /// <summary>Inventory에서 재료를 꺼내 냄비에 투입한다. 보유하지 않은 재료면 false.</summary>
        public bool TryAddIngredient(ItemDef item)
        {
            if (item == null || m_inventory == null) return false;
            if (!m_inventory.TryRemove(item)) return false;

            m_ingredients.Add(item);
            IngredientAdded?.Invoke(item);
            return true;
        }

        /// <summary>
        /// 매칭 → 성공/실패 결과 생성.
        /// - 성공: 투입 재료 전부 소모, RecipeDef.Result 지급.
        /// - 실패: Rare 이상 재료는 Inventory로 반환, Common만 소모 + 실패작 1개 지급
        ///   (resource-loop.md — 실패해도 희귀 재료는 소모 안 됨).
        /// </summary>
        public void Cook()
        {
            if (m_inventory == null || m_matcher == null) return;
            if (m_ingredients.Count == 0) return;

            RecipeDef matched = m_matcher.Match(m_ingredients);

            ItemDef output;
            if (matched != null)
            {
                // 성공 — 재료는 투입 시점에 이미 Inventory에서 빠져 있으므로 그대로 소모
                output = matched.Result;
            }
            else
            {
                // 실패 — Rare 이상 재료만 Inventory로 반환, Common은 소모
                for (int i = 0; i < m_ingredients.Count; i++)
                {
                    ItemDef ingredient = m_ingredients[i];
                    if (ingredient != null && ingredient.Rarity >= Rarity.Rare)
                        m_inventory.Add(ingredient);
                }

                output = m_failResult;
            }

            m_ingredients.Clear();

            if (output != null)
            {
                m_inventory.Add(output);
                Cooked?.Invoke(output);
            }
            else
            {
                Debug.LogWarning("[CookingPot] 조리 결과 아이템이 비어 있음 — RecipeDef.Result 또는 실패작(m_failResult) 데이터 확인 필요", this);
            }
        }

        /// <summary>조리하지 않고 투입 재료를 전부 Inventory로 반환한다.</summary>
        public void Clear()
        {
            if (m_inventory != null)
            {
                for (int i = 0; i < m_ingredients.Count; i++)
                {
                    if (m_ingredients[i] != null)
                        m_inventory.Add(m_ingredients[i]);
                }
            }

            m_ingredients.Clear();
        }
    }
}
