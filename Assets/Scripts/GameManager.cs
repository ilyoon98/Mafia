using UnityEngine;
using System.Collections;

namespace NoirRoulette
{
    // 턴 상태
    public enum TurnState { PlayerTurn, VillainTurn, GameOver }

    // 승리 타입
    public enum WinType { Bullet, Mental }

    // 마지막 데미지 원인 (승리 타입 판별용)
    public enum DamageSource { Bullet, Mental }

    public class GameManager : MonoBehaviour
    {
        // ─────────────────────────────────────────
        // 싱글톤
        // ─────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ─────────────────────────────────────────
        // Inspector 연결 참조
        // ─────────────────────────────────────────
        [Header("시스템 참조")]
        public CylinderSystem cylinderSystem;
        public DeckManager playerDeckManager;
        public DeckManager villainDeckManager;
        public PlayerController playerController;
        public VillainAI villainAI;
        public UIManager uiManager;

        // ─────────────────────────────────────────
        // 플레이어 스탯
        // ─────────────────────────────────────────
        [Header("플레이어 스탯")]
        public int playerHP = 2;        // 시작 체력 (조직원1 + 본인)
        public int playerMaxHP = 3;     // 최대 체력 (조직원2 + 본인)

        // ─────────────────────────────────────────
        // 빌런 스탯 (HP = 멘탈 항상 연동)
        // ─────────────────────────────────────────
        [Header("빌런 스탯 (HP=멘탈 연동)")]
        public int villainHP = 3;
        public int villainMental = 3;

        // ─────────────────────────────────────────
        // 게임 상태
        // ─────────────────────────────────────────
        [Header("게임 상태")]
        public TurnState currentState = TurnState.PlayerTurn;
        public int currentStage = 1;    // 1~3 스테이지 (실탄 수 = 스테이지 번호)
        public int roundCount = 0;

        // 조롱 카드 조건: 빌런 직전 행동 실수 여부
        // 실수 3가지: ①블러핑 들킴 ②나한테 쐈는데 공탄 ③카드 효과 실패
        [HideInInspector] public bool villainLastTurnMistake = false;

        // 마지막 빌런 데미지 원인 (승리 타입 판별)
        [HideInInspector] public DamageSource lastVillainDamageSource = DamageSource.Bullet;

        // 급소 카드 발동 대기 플래그 (카드 사용 → 발사 시점에 처리)
        [HideInInspector] public bool isGuksoPending = false;

        // ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            StartGame();
        }

        // ─────────────────────────────────────────
        // 게임 시작
        // ─────────────────────────────────────────
        public void StartGame()
        {
            // Inspector 참조 누락 시 명확한 오류 출력 후 중단
            if (cylinderSystem == null)    { Debug.LogError("[GameManager] CylinderSystem이 연결되지 않았습니다. Unity 메뉴 → NoirRoulette → 씬 자동 설정 ▶ 를 실행하세요."); return; }
            if (playerDeckManager == null) { Debug.LogError("[GameManager] PlayerDeckManager가 연결되지 않았습니다."); return; }
            if (villainDeckManager == null){ Debug.LogError("[GameManager] VillainDeckManager가 연결되지 않았습니다."); return; }
            if (uiManager == null)         { Debug.LogError("[GameManager] UIManager가 연결되지 않았습니다."); return; }
            if (playerController == null)  { Debug.LogError("[GameManager] PlayerController가 연결되지 않았습니다."); return; }
            if (villainAI == null)         { Debug.LogError("[GameManager] VillainAI가 연결되지 않았습니다."); return; }

            // 게임오버 패널 숨기기 (재시작 시)
            if (uiManager.gameOverPanel != null)
                uiManager.gameOverPanel.SetActive(false);

            // 스탯 초기화
            playerHP = 2;
            SetVillainStat(3);
            roundCount = 0;
            villainLastTurnMistake = false;
            isGuksoPending = false;

            // 스테이지별 실탄 수 (1스테이지=1발, 2=2발, 3=3발)
            cylinderSystem.Load(currentStage);

            // 덱 초기화
            playerDeckManager.BuildDeck();
            villainDeckManager.BuildDeck();

            Debug.Log($"[게임 시작] 스테이지 {currentStage} / 실탄 {currentStage}발 / 플레이어HP={playerHP} / 빌런HP={villainHP}");

            uiManager.UpdateAll();
            StartPlayerTurn();
        }

        // ─────────────────────────────────────────
        // 플레이어 턴 시작
        // ─────────────────────────────────────────
        public void StartPlayerTurn()
        {
            currentState = TurnState.PlayerTurn;
            roundCount++;
            villainLastTurnMistake = false;

            Debug.Log($"─── [플레이어 턴 시작] 라운드 {roundCount} ───");

            // 핸드 보충 (3장이 될 때까지)
            playerDeckManager.DrawToHand();

            uiManager.SetTurnText($"플레이어 턴  (라운드 {roundCount})");
            uiManager.UpdatePlayerHand(playerDeckManager.hand);
            uiManager.UpdateAll();

            playerController.SetPlayerTurn(true);
        }

        // ─────────────────────────────────────────
        // 플레이어 턴 종료 (턴 종료 버튼 클릭 시 호출)
        // ─────────────────────────────────────────
        public void EndPlayerTurn()
        {
            if (currentState != TurnState.PlayerTurn) return;
            playerController.SetPlayerTurn(false);
            isGuksoPending = false;
            cylinderSystem.ResetTarget();

            Debug.Log("[플레이어 턴 종료]");
            StartVillainTurn();
        }

        // ─────────────────────────────────────────
        // 빌런 턴 시작
        // ─────────────────────────────────────────
        public void StartVillainTurn()
        {
            currentState = TurnState.VillainTurn;
            Debug.Log("─── [빌런 턴 시작] ───");

            // 핸드 보충
            villainDeckManager.DrawToHand();

            uiManager.SetTurnText("빌런 턴");
            uiManager.UpdateAll();

            StartCoroutine(villainAI.ExecuteTurn());
        }

        // ─────────────────────────────────────────
        // 빌런 턴 종료 (VillainAI에서 호출)
        // ─────────────────────────────────────────
        public void EndVillainTurn()
        {
            Debug.Log("[빌런 턴 종료]");
            if (CheckWin()) return;
            StartPlayerTurn();
        }

        // ─────────────────────────────────────────
        // 승패 판정
        // 반환: true = 게임 종료
        // ─────────────────────────────────────────
        public bool CheckWin()
        {
            // 이미 게임 종료 상태면 중복 호출 방지
            if (currentState == TurnState.GameOver) return true;

            if (playerHP <= 0)
            {
                EndGame(false, WinType.Bullet);
                return true;
            }
            if (villainHP <= 0)
            {
                WinType wt = lastVillainDamageSource == DamageSource.Mental
                    ? WinType.Mental : WinType.Bullet;
                EndGame(true, wt);
                return true;
            }
            return false;
        }

        // ─────────────────────────────────────────
        // 게임 종료
        // ─────────────────────────────────────────
        public void EndGame(bool isWin, WinType winType = WinType.Bullet)
        {
            currentState = TurnState.GameOver;
            playerController.SetPlayerTurn(false);

            if (isWin)
            {
                if (winType == WinType.Bullet)
                {
                    Debug.Log("[게임 종료] 총알 승리!");
                    uiManager.ShowGameOver(true, WinType.Bullet,
                        "총알 승리",
                        "빌런이 쓰러진다.\n총이 바닥에 떨어지는 소리.\n침묵.");
                }
                else
                {
                    Debug.Log("[게임 종료] 멘탈 승리!");
                    uiManager.ShowGameOver(true, WinType.Mental,
                        "멘탈 승리",
                        "빌런이 먼저 총을 내려놓는다.\n총 한 발 없이 이긴 밤.");
                }
            }
            else
            {
                Debug.Log("[게임 종료] 패배.");
                uiManager.ShowGameOver(false, WinType.Bullet,
                    "패배",
                    "총성 하나.\n암전.\n.");
            }
        }

        // ─────────────────────────────────────────
        // 플레이어 데미지
        // ─────────────────────────────────────────
        public void DamagePlayer(int amount)
        {
            int prev = playerHP;
            playerHP = Mathf.Max(0, playerHP - amount);
            Debug.Log($"[플레이어] HP {prev} → {playerHP}");
            uiManager.AppendLog($"플레이어 HP -{amount}  (현재: {playerHP})");
            uiManager.UpdatePlayerHP(playerHP);
            CheckWin();
        }

        // ─────────────────────────────────────────
        // 플레이어 체력 회복
        // ─────────────────────────────────────────
        public void HealPlayer(int amount)
        {
            if (playerHP >= playerMaxHP)
            {
                Debug.Log("[플레이어] 이미 최대 체력. 회복 효과 없음.");
                uiManager.AppendLog("체력 회복 실패 — 이미 최대 체력.");
                return;
            }
            int prev = playerHP;
            playerHP = Mathf.Min(playerMaxHP, playerHP + amount);
            Debug.Log($"[플레이어] HP 회복 {prev} → {playerHP}");
            uiManager.AppendLog($"체력 회복! HP {prev} → {playerHP}");
            uiManager.UpdatePlayerHP(playerHP);
        }

        // ─────────────────────────────────────────
        // 빌런 스탯 변경 (HP와 멘탈 항상 동기화)
        // ─────────────────────────────────────────
        public void SetVillainStat(int value, DamageSource source = DamageSource.Bullet)
        {
            int prev = villainHP;
            villainHP = Mathf.Max(0, value);
            villainMental = villainHP;
            if (prev != villainHP)
                lastVillainDamageSource = source;

            Debug.Log($"[빌런] HP/멘탈 {prev} → {villainHP}  (원인: {source})");
            uiManager?.UpdateVillainHP(villainHP);
            uiManager?.UpdateVillainMental(villainMental, villainAI ? villainAI.GetMentalState() : MentalState.CALM);
        }

        // 빌런 총알 데미지
        public void DamageVillain(int amount, DamageSource source = DamageSource.Bullet)
        {
            uiManager.AppendLog($"빌런 HP -{amount}  (현재: {villainHP - amount})");
            SetVillainStat(villainHP - amount, source);
            CheckWin();
        }

        // 빌런 멘탈 데미지 (HP도 같이 감소)
        public void DamageVillainMental(int amount)
        {
            uiManager.AppendLog($"빌런 멘탈 -{amount}  (현재: {villainMental - amount})");
            DamageVillain(amount, DamageSource.Mental);
        }
    }
}
