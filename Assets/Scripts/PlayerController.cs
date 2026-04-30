using UnityEngine;

namespace NoirRoulette
{
    // 플레이어 행동 처리 — 카드 사용, 발사, 턴 종료
    public class PlayerController : MonoBehaviour
    {
        // 플레이어 턴 활성 여부
        private bool isPlayerTurn = false;

        // ─────────────────────────────────────────
        // 플레이어 턴 활성/비활성 (GameManager에서 호출)
        // ─────────────────────────────────────────
        public void SetPlayerTurn(bool active)
        {
            isPlayerTurn = active;
            GameManager.Instance.uiManager.SetPlayerInputActive(active);
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

            // 카드 효과 실행
            CardEffect.Execute(card, true);

            // 핸드에서 제거
            playerDeck.DiscardCard(card);

            // 핸드 UI 갱신
            gm.uiManager.UpdatePlayerHand(playerDeck.hand);
        }

        // ─────────────────────────────────────────
        // 발사 버튼 클릭 (UIManager의 ShootButton에 연결)
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
                // 빌런에게 발사한 경우
                if (isLive)
                {
                    // 명중
                    int dmg = guksoActive ? 2 : 1;
                    gm.DamageVillain(dmg, DamageSource.Bullet);
                    string msg = guksoActive ? $"급소 명중! 빌런 HP -{dmg}" : $"빌런 명중! HP -{dmg}";
                    gm.uiManager.AppendLog(msg);
                }
                else
                {
                    // 공탄
                    if (guksoActive)
                    {
                        // 급소 공탄 → 내 HP -1
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
                // 자기 자신에게 발사한 경우
                if (isLive)
                {
                    gm.DamagePlayer(1);
                }
                else
                {
                    gm.uiManager.AppendLog("공탄! 살았다...");
                }
            }

            // 전체 UI 갱신
            gm.uiManager.UpdateAll();
        }

        // ─────────────────────────────────────────
        // 턴 종료 버튼 클릭 (UIManager의 EndTurnButton에 연결)
        // ─────────────────────────────────────────
        public void OnEndTurnButtonClicked()
        {
            if (!isPlayerTurn)
            {
                Debug.Log("[플레이어] 플레이어 턴이 아님.");
                return;
            }
            GameManager.Instance.EndPlayerTurn();
        }
    }
}
