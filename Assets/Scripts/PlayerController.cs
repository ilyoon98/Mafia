using UnityEngine;

namespace NoirRoulette
{
    // 플레이어 행동 처리 — 카드 사용, 발사, 턴 종료
    public class PlayerController : MonoBehaviour
    {
        // 플레이어 턴 활성 여부
        private bool isPlayerTurn = false;

        // 턴 종료 가능 여부 — 발사/허공쏘기 중 하나 충족 시 true
        private bool canEndTurn = false;

        // 탄 확인 카드 사용 후 슬롯 선택 대기 중인 카드
        private CardData pendingTanHwakInCard = null;

        // ─────────────────────────────────────────
        // 플레이어 턴 활성/비활성 (GameManager에서 호출)
        // ─────────────────────────────────────────
        public void SetPlayerTurn(bool active)
        {
            isPlayerTurn = active;
            var ui = GameManager.Instance.uiManager;

            if (active)
            {
                // 턴 시작: 발사 가능, 턴 종료 불가
                canEndTurn = false;
                pendingTanHwakInCard = null;
                ui.SetPlayerInputActive(true);
                ui.SetEndTurnButtonActive(false);
            }
            else
            {
                // 턴 종료: 모든 입력 비활성
                canEndTurn = false;
                pendingTanHwakInCard = null;
                ui.SetPlayerInputActive(false);
                ui.HideSlotSelectionPanel();
            }
        }

        // ─────────────────────────────────────────
        // 카드 사용 (UIManager에서 카드 버튼 클릭 시 호출)
        // ─────────────────────────────────────────
        public void UseCard(CardData card)
        {
            if (!isPlayerTurn)
            {
                Debug.Log("[플레이어] 플레이어 턴이 아님. 카드 사용 불가.");
                return;
            }

            var gm = GameManager.Instance;
            var playerDeck = gm.playerDeckManager;

            // 조롱 카드: 조건 사전 확인
            if (card.cardType == CardType.조롱 && !gm.villainLastTurnMistake)
            {
                gm.uiManager.AppendLog("조롱: 사용 불가 — 빌런 직전 실수 없음.");
                Debug.Log("[플레이어] 조롱 조건 불충족. 취소.");
                return;
            }

            // 탄 확인 카드: 슬롯 선택 UI 표시 (카드 즉시 버리지 않음)
            if (card.cardType == CardType.탄확인)
            {
                pendingTanHwakInCard = card;
                gm.uiManager.ShowSlotSelectionPanel();
                return;
            }

            // 카드 효과 실행
            bool success = CardEffect.Execute(card, true);

            // 조준/급소/허공쏘기 성공 → 발사 완료, ShootButton 비활성 + EndTurn 활성
            // (행동 기록은 CardEffect 내부에서 추가)
            if (success && (card.cardType == CardType.조준
                         || card.cardType == CardType.급소
                         || card.cardType == CardType.허공쏘기))
            {
                canEndTurn = true;
                gm.uiManager.SetShootButtonActive(false);
                gm.uiManager.SetEndTurnButtonActive(true);
            }
            // 나머지 카드는 여기서 행동 기록 추가 (조준/급소/허공쏘기/탄확인 제외)
            else if (card.cardType != CardType.탄확인)
            {
                gm.uiManager.AddPlayerAction($"[{card.cardName}]");
            }

            // 핸드에서 제거
            playerDeck.DiscardCard(card);

            // 전체 UI 갱신 — 모든 카드 사용 후 공통 (실린더·HP·빌런 상태 등 즉시 반영)
            gm.uiManager.UpdateAll();

            // 핸드 UI 갱신
            gm.uiManager.UpdatePlayerHand(playerDeck.hand);
        }

        // ─────────────────────────────────────────
        // 탄 확인 슬롯 선택 (UIManager의 슬롯 버튼에 연결)
        // ─────────────────────────────────────────
        public void OnSlotSelected(int slotIndex)
        {
            if (!isPlayerTurn || pendingTanHwakInCard == null)
            {
                Debug.Log("[플레이어] 슬롯 선택 조건 불충족.");
                return;
            }

            var gm = GameManager.Instance;
            var cyl = gm.cylinderSystem;
            var playerDeck = gm.playerDeckManager;

            // 소모된 칸 선택 방지
            if (cyl.slotStates[slotIndex] == CylinderSlotState.Consumed)
            {
                gm.uiManager.AppendLog($"탄 확인: {slotIndex + 1}번 칸은 이미 소모된 칸입니다.");
                return;
            }

            // 선택한 칸 공개
            cyl.PeekSlot(slotIndex, true);

            // 행동 기록
            bool isLive = cyl.slots[slotIndex];
            string slotType = isLive ? "실탄" : "공탄";
            gm.uiManager.AddPlayerAction($"[탄확인] {slotIndex + 1}번 칸 → [{slotType}]");

            // 카드 버리기
            playerDeck.DiscardCard(pendingTanHwakInCard);
            pendingTanHwakInCard = null;

            // UI 정리
            gm.uiManager.HideSlotSelectionPanel();
            gm.uiManager.UpdatePlayerHand(playerDeck.hand);
            gm.uiManager.UpdateAll();
        }

        // ─────────────────────────────────────────
        // 발사 버튼 클릭 — 자기 자신에게 발사 전담
        // ─────────────────────────────────────────
        public void OnShootButtonClicked()
        {
            if (!isPlayerTurn)
            {
                Debug.Log("[플레이어] 플레이어 턴이 아님. 발사 불가.");
                return;
            }

            var gm = GameManager.Instance;
            var cyl = gm.cylinderSystem;

            // 발사 전 타겟 및 급소 플래그 저장 (Fire() 후 리셋되므로)
            ShootTarget targetBefore = cyl.shootTarget;
            bool guksoActive = gm.isGuksoPending;

            // 발사
            bool isLive = cyl.Fire();

            // ── 결과 처리 ──
            if (targetBefore == ShootTarget.Opponent)
            {
                // 빌런에게 발사한 경우 (ShootButton으로는 통상 Self이지만 예외 처리 유지)
                if (isLive)
                {
                    int dmg = guksoActive ? 2 : 1;
                    gm.DamageVillain(dmg, DamageSource.Bullet);
                    string msg = guksoActive ? $"급소 명중! 빌런 HP -{dmg}" : $"빌런 명중! HP -{dmg}";
                    gm.uiManager.AppendLog(msg);
                }
                else
                {
                    if (guksoActive)
                    {
                        gm.DamagePlayer(1);
                        gm.uiManager.AppendLog("급소 공탄... 내 HP -1.");
                    }
                    else
                    {
                        gm.uiManager.AppendLog("공탄. 빌런 안전.");
                    }
                }
                gm.isGuksoPending = false;
            }
            else
            {
                // 자기 자신에게 발사
                if (isLive)
                {
                    gm.DamagePlayer(1);
                }
                else
                {
                    gm.uiManager.AppendLog("공탄! 살았다...");
                }
            }

            // 행동 기록
            string shotResult;
            if (targetBefore == ShootTarget.Opponent)
                shotResult = isLive ? (guksoActive ? "급소 명중! 빌런 HP-2" : "명중! 빌런 HP-1")
                                    : (guksoActive ? "공탄 (내 HP-1)" : "공탄");
            else
                shotResult = isLive ? "자신에게 발사 → 명중! HP-1" : "자신에게 발사 → 공탄 (안전)";
            gm.uiManager.AddPlayerAction($"[발사] → {shotResult}");

            // ShootButton 비활성 + 턴 종료 활성
            canEndTurn = true;
            gm.uiManager.SetShootButtonActive(false);
            gm.uiManager.SetEndTurnButtonActive(true);

            // 전체 UI 갱신
            gm.uiManager.UpdateAll();
        }

        // ─────────────────────────────────────────
        // 턴 종료 버튼 클릭 (UIManager의 EndTurnButton에 연결)
        // ─────────────────────────────────────────
        public void OnEndTurnButtonClicked()
        {
            if (!isPlayerTurn || !canEndTurn)
            {
                Debug.Log("[플레이어] 턴 종료 조건 미충족.");
                return;
            }
            GameManager.Instance.EndPlayerTurn();
        }
    }
}
