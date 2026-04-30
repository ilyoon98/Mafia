using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace NoirRoulette
{
    // 빌런 멘탈 상태
    public enum MentalState { CALM, NERVOUS, PANIC, BROKEN }

    public class VillainAI : MonoBehaviour
    {
        // 블러핑 활성 여부 (눈치채기 카드로 확인 가능)
        [HideInInspector] public bool isBluffing = false;

        // 빌런 급소 카드 대기 플래그
        [HideInInspector] public bool isGuksoPendingForVillain = false;

        // 탄 확인 블러핑 디버그용 — 실제 결과 vs 보고 결과
        [HideInInspector] public string debug_lastTanHwakIn = "없음";

        // 빌런 행동 간 딜레이 (초)
        [Header("AI 딜레이 설정")]
        public float actionDelay = 0.8f;
        public float shootDelay = 1.0f;

        // ─────────────────────────────────────────
        // 멘탈 상태 반환 (빌런 멘탈 수치 → enum 변환)
        // ─────────────────────────────────────────
        public MentalState GetMentalState()
        {
            int mental = GameManager.Instance.villainMental;
            if (mental >= 3) return MentalState.CALM;
            if (mental == 2) return MentalState.NERVOUS;
            if (mental == 1) return MentalState.PANIC;
            return MentalState.BROKEN;
        }

        // ─────────────────────────────────────────
        // 빌런 턴 실행 코루틴 (GameManager.StartVillainTurn에서 호출)
        // ─────────────────────────────────────────
        public IEnumerator ExecuteTurn()
        {
            var gm = GameManager.Instance;
            MentalState state = GetMentalState();

            // BROKEN: 항복 → 즉시 멘탈 승리 처리
            if (state == MentalState.BROKEN)
            {
                Debug.Log("[빌런] 멘탈 BROKEN — 항복.");
                gm.uiManager.AppendLog("빌런이 총을 내려놓는다...");
                gm.EndGame(true, WinType.Mental);
                yield break;
            }

            yield return new WaitForSeconds(actionDelay);

            // 1. 블러핑 결정
            DecideBluff(state);

            // 2. 카드 사용
            yield return StartCoroutine(SelectAndUseCards(state));

            yield return new WaitForSeconds(actionDelay);

            // 3. 발사
            VillainShoot(state);

            yield return new WaitForSeconds(shootDelay);

            // 4. 턴 종료
            isGuksoPendingForVillain = false;
            gm.EndVillainTurn();
        }

        // ─────────────────────────────────────────
        // 블러핑 결정 (멘탈 상태별 확률)
        // ─────────────────────────────────────────
        private void DecideBluff(MentalState state)
        {
            float chance = state switch
            {
                MentalState.CALM    => 0.20f,
                MentalState.NERVOUS => 0.40f,
                MentalState.PANIC   => 0.60f,
                _                  => 0f
            };

            isBluffing = Random.value < chance;
            string bluffStr = isBluffing ? "블러핑 발동!" : "블러핑 없음";
            Debug.Log($"[빌런 AI] 블러핑 결정: {bluffStr} (확률 {chance * 100}%)");
            GameManager.Instance.uiManager.AppendLog($"빌런: {bluffStr}");
        }

        // ─────────────────────────────────────────
        // 카드 선택 및 사용 (멘탈 상태별 전략)
        // ─────────────────────────────────────────
        private IEnumerator SelectAndUseCards(MentalState state)
        {
            var gm = GameManager.Instance;
            var deck = gm.villainDeckManager;

            if (deck.hand.Count == 0)
            {
                Debug.Log("[빌런 AI] 핸드 없음. 카드 사용 건너뜀.");
                yield break;
            }

            // 공격 카드(조준/급소)는 발사 단계에서 별도 처리 — 여기선 비공격 카드만 선택
            List<CardData> nonAttackCards = deck.hand.FindAll(c =>
                c.cardType != CardType.조준 && c.cardType != CardType.급소);

            CardData chosen = null;

            switch (state)
            {
                case MentalState.CALM:
                    // 전략적 선택: 멘탈 공격 우선, 없으면 정보계
                    chosen = FindCardOfType(nonAttackCards, CardType.협박)
                          ?? FindCardOfType(nonAttackCards, CardType.탄확인)
                          ?? FindCardOfType(nonAttackCards, CardType.허공쏘기);
                    break;

                case MentalState.NERVOUS:
                    // 50% 확률로 랜덤 카드 1장
                    if (Random.value < 0.5f && nonAttackCards.Count > 0)
                        chosen = nonAttackCards[Random.Range(0, nonAttackCards.Count)];
                    break;

                case MentalState.PANIC:
                    // 랜덤 카드 1장 (조건 없이)
                    if (nonAttackCards.Count > 0)
                        chosen = nonAttackCards[Random.Range(0, nonAttackCards.Count)];
                    break;
            }

            if (chosen != null)
            {
                // 조롱 카드 조건 확인 (빌런은 플레이어 실수 조건이므로 현재 미구현)
                if (chosen.cardType == CardType.조롱) { yield break; }

                bool success = CardEffect.Execute(chosen, false);
                deck.DiscardCard(chosen);

                // ③카드 효과 실패 → 빌런 실수 판정
                if (!success)
                    gm.villainLastTurnMistake = true;

                gm.uiManager.UpdateAll();
                yield return new WaitForSeconds(actionDelay);
            }
        }

        // ─────────────────────────────────────────
        // 발사 처리 (공격 카드 + 실수 확률 포함)
        // ─────────────────────────────────────────
        private void VillainShoot(MentalState state)
        {
            var gm = GameManager.Instance;
            var cyl = gm.cylinderSystem;
            var deck = gm.villainDeckManager;

            // 실수 확률 (실탄인 걸 알면서도 자기에게 쏨)
            float mistakeChance = state switch
            {
                MentalState.NERVOUS => 0.10f,
                MentalState.PANIC   => 0.35f,
                _                  => 0f
            };
            bool isMistake = Random.value < mistakeChance;

            if (isMistake)
            {
                Debug.Log($"[빌런 AI] 실수 발생! (확률 {mistakeChance * 100}%) → 자기 자신에게 발사.");
                gm.uiManager.AppendLog($"빌런 실수! 자기 자신에게 발사...");
                cyl.ResetTarget();
                isGuksoPendingForVillain = false;
            }
            else
            {
                // 전략적 판단: 조준 또는 급소 카드가 있으면 공격 시도
                CardData attackCard = FindCardOfType(deck.hand, CardType.조준)
                                   ?? FindCardOfType(deck.hand, CardType.급소);

                if (attackCard != null && state != MentalState.PANIC)
                {
                    CardEffect.Execute(attackCard, false);
                    deck.DiscardCard(attackCard);
                }
                // 공격 카드 없거나 PANIC 상태면 자기에게 발사 (기본)
            }

            // ── 발사 ──
            ShootTarget targetBefore = cyl.shootTarget;
            bool guksoActive = isGuksoPendingForVillain;
            bool isLive = cyl.Fire();

            // 결과 처리
            if (targetBefore == ShootTarget.Opponent)
            {
                // 플레이어에게 발사
                if (isLive)
                {
                    int dmg = guksoActive ? 2 : 1;
                    gm.DamagePlayer(dmg);
                    gm.uiManager.AppendLog($"빌런 공격 명중! 플레이어 HP -{dmg}");
                }
                else
                {
                    if (guksoActive)
                    {
                        // 빌런 급소 공탄 → 빌런 HP -1
                        gm.DamageVillain(1, DamageSource.Bullet);
                        gm.uiManager.AppendLog("빌런 급소 공탄! 빌런 HP -1");
                        gm.villainLastTurnMistake = true;
                    }
                    else
                    {
                        gm.uiManager.AppendLog("빌런 공격 공탄. 플레이어 안전.");
                    }
                }
            }
            else
            {
                // 자기 자신에게 발사 (기본 또는 실수)
                if (isLive)
                {
                    gm.DamageVillain(1, DamageSource.Bullet);
                    gm.uiManager.AppendLog("빌런 자기 자신에게 실탄 명중! HP -1");
                }
                else
                {
                    // ②나한테 쐈는데 공탄 → 빌런 실수 판정
                    gm.villainLastTurnMistake = true;
                    gm.uiManager.AppendLog("빌런 자기 자신에게 공탄... (실수 기록)");
                }
            }

            gm.uiManager.UpdateAll();
        }

        // 특정 타입의 카드 찾기 (없으면 null)
        private CardData FindCardOfType(List<CardData> hand, CardType type)
        {
            return hand.Find(c => c.cardType == type);
        }
    }
}
