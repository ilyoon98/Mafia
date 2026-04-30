using UnityEngine;
using UnityEngine.UI;

namespace NoirRoulette
{
    // 디버그 패널 — 실린더/덱/핸드 실시간 표시 + 디버그 버튼
    public class DebugPanel : MonoBehaviour
    {
        // ─────────────────────────────────────────
        // Inspector 연결 — 시스템 참조
        // ─────────────────────────────────────────
        [Header("시스템 참조")]
        public CylinderSystem cylinderSystem;
        public DeckManager playerDeckManager;
        public DeckManager villainDeckManager;
        public VillainAI villainAI;
        public GameManager gameManager;

        // ─────────────────────────────────────────
        // Inspector 연결 — 디버그 텍스트
        // ─────────────────────────────────────────
        [Header("디버그 텍스트")]
        public Text debug_CylinderText;     // 실린더 전체 상태
        public Text debug_PlayerDeckText;   // 플레이어 덱/핸드 정보
        public Text debug_VillainDeckText;  // 빌런 덱/핸드 정보
        public Text debug_BluffText;        // 빌런 블러핑 여부
        public Text debug_TurnText;         // 현재 턴/라운드

        // ─────────────────────────────────────────
        // Inspector 연결 — 디버그 버튼
        // ─────────────────────────────────────────
        [Header("디버그 버튼")]
        public Button reloadCylinderButton;  // 실린더 강제 재장전
        public Button showDeckButton;        // 덱 전체 Console 출력

        private void Start()
        {
            // 버튼 리스너 연결
            if (reloadCylinderButton != null)
                reloadCylinderButton.onClick.AddListener(OnReloadCylinder);
            if (showDeckButton != null)
                showDeckButton.onClick.AddListener(OnShowDeck);
        }

        // ─────────────────────────────────────────
        // 매 프레임 디버그 정보 갱신
        // ─────────────────────────────────────────
        private void Update()
        {
            if (cylinderSystem == null || playerDeckManager == null
                || villainDeckManager == null || villainAI == null || gameManager == null)
                return;

            // 실린더 상태
            if (debug_CylinderText != null)
            {
                string cylStr = cylinderSystem.GetCylinderDisplayString();
                string jamStr = cylinderSystem.isJammed ? " [잼 예약]" : "";
                string targetStr = cylinderSystem.shootTarget == ShootTarget.Opponent ? " [→상대]" : " [→자신]";
                debug_CylinderText.text = $"[실린더]\n{cylStr}{jamStr}{targetStr}";
            }

            // 플레이어 덱/핸드
            if (debug_PlayerDeckText != null)
            {
                debug_PlayerDeckText.text =
                    $"[플레이어 덱]\n" +
                    $"덱: {playerDeckManager.deck.Count}장  핸드: {playerDeckManager.hand.Count}장\n" +
                    $"{playerDeckManager.GetHandDisplayString()}";
            }

            // 빌런 덱/핸드
            if (debug_VillainDeckText != null)
            {
                debug_VillainDeckText.text =
                    $"[빌런 덱]\n" +
                    $"덱: {villainDeckManager.deck.Count}장  핸드: {villainDeckManager.hand.Count}장\n" +
                    $"{villainDeckManager.GetHandDisplayString()}";
            }

            // 블러핑 여부 + 탄확인 블러핑 결과
            if (debug_BluffText != null)
            {
                string bluff = villainAI.isBluffing ? "▶ 블러핑 중!" : "블러핑 없음";
                debug_BluffText.text = $"[블러핑] {bluff}\n[탄확인] {villainAI.debug_lastTanHwakIn}";
            }

            // 턴/라운드 정보
            if (debug_TurnText != null)
            {
                string stateStr = gameManager.currentState switch
                {
                    TurnState.PlayerTurn => "플레이어 턴",
                    TurnState.VillainTurn => "빌런 턴",
                    TurnState.GameOver => "게임 종료",
                    _ => "-"
                };
                debug_TurnText.text =
                    $"[상태]\n" +
                    $"턴: {stateStr}\n" +
                    $"라운드: {gameManager.roundCount}\n" +
                    $"스테이지: {gameManager.currentStage}";
            }
        }

        // ─────────────────────────────────────────
        // 디버그 버튼: 실린더 강제 재장전
        // ─────────────────────────────────────────
        private void OnReloadCylinder()
        {
            cylinderSystem.Load(gameManager.currentStage);
            Debug.Log("[디버그] 실린더 강제 재장전 완료.");
            GameManager.Instance?.uiManager.AppendLog("[디버그] 실린더 강제 재장전.");
            GameManager.Instance?.uiManager.UpdateAll();
        }

        // ─────────────────────────────────────────
        // 디버그 버튼: 덱 전체 Console 출력
        // ─────────────────────────────────────────
        private void OnShowDeck()
        {
            Debug.Log("=== [디버그] 덱 전체 확인 ===");
            playerDeckManager.LogDeckContents();
            villainDeckManager.LogDeckContents();
            playerDeckManager.LogHandContents();
            villainDeckManager.LogHandContents();
            Debug.Log("=== [디버그] 덱 확인 완료 ===");
        }
    }
}
