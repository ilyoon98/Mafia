using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace NoirRoulette
{
    // UI 전체 관리 — 텍스트 갱신, 카드 버튼 동적 생성, 게임오버 패널
    public class UIManager : MonoBehaviour
    {
        // ─────────────────────────────────────────
        // 싱글톤
        // ─────────────────────────────────────────
        public static UIManager Instance { get; private set; }

        // ─────────────────────────────────────────
        // Inspector 연결 — 빌런 영역
        // ─────────────────────────────────────────
        [Header("빌런 UI")]
        public Text villainHP_Text;
        public Text villainMental_Text;
        public Text villainState_Text;
        public Text villainHand_Text;

        // ─────────────────────────────────────────
        // Inspector 연결 — 실린더 영역
        // ─────────────────────────────────────────
        [Header("실린더 UI")]
        public Text cylinder_Text;

        // ─────────────────────────────────────────
        // Inspector 연결 — 플레이어 영역
        // ─────────────────────────────────────────
        [Header("플레이어 UI")]
        public Text playerHP_Text;
        public Transform handPanel;             // 카드 버튼 동적 생성 부모
        public Button shootButton;
        public Button endTurnButton;
        public GameObject cardButtonPrefab;     // Button + Text 구성 프리팹

        // ─────────────────────────────────────────
        // Inspector 연결 — 공통
        // ─────────────────────────────────────────
        [Header("공통 UI")]
        public Text turnText;
        public Text logText;                    // ScrollView 안의 로그 텍스트

        // ─────────────────────────────────────────
        // Inspector 연결 — 게임오버 패널
        // ─────────────────────────────────────────
        [Header("게임오버 패널")]
        public GameObject gameOverPanel;
        public Text resultTitle_Text;
        public Text resultScript_Text;
        public Button restartButton;

        // ─────────────────────────────────────────
        // 참조
        // ─────────────────────────────────────────
        [Header("참조")]
        public PlayerController playerController;

        private string logBuffer = "";
        private const int maxLogLines = 30;     // 로그 최대 줄 수

        // ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            // 발사/턴 종료 버튼 리스너 연결
            if (shootButton != null)
                shootButton.onClick.AddListener(() => playerController.OnShootButtonClicked());
            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(() => playerController.OnEndTurnButtonClicked());
            if (restartButton != null)
                restartButton.onClick.AddListener(() => { GameManager.Instance.StartGame(); });
        }

        // ─────────────────────────────────────────
        // 전체 UI 갱신 (편의 메서드)
        // ─────────────────────────────────────────
        public void UpdateAll()
        {
            var gm = GameManager.Instance;
            UpdatePlayerHP(gm.playerHP);
            UpdateVillainHP(gm.villainHP);
            UpdateVillainMental(gm.villainMental, gm.villainAI ? gm.villainAI.GetMentalState() : MentalState.CALM);
            UpdateCylinder(gm.cylinderSystem.slots, gm.cylinderSystem.currentIndex);
            UpdateVillainHandCount(gm.villainDeckManager.hand.Count);
        }

        // ─────────────────────────────────────────
        // 플레이어 HP 갱신
        // ─────────────────────────────────────────
        public void UpdatePlayerHP(int hp)
        {
            if (playerHP_Text == null) return;
            string desc = hp >= 3 ? "조직원2+본인" : hp == 2 ? "조직원1+본인" : hp == 1 ? "본인만" : "사망";
            playerHP_Text.text = $"플레이어  HP: {hp}  ({desc})";
        }

        // ─────────────────────────────────────────
        // 빌런 HP 갱신
        // ─────────────────────────────────────────
        public void UpdateVillainHP(int hp)
        {
            if (villainHP_Text == null) return;
            villainHP_Text.text = $"빌런  HP: {hp}";
        }

        // ─────────────────────────────────────────
        // 빌런 멘탈 + 상태 갱신
        // ─────────────────────────────────────────
        public void UpdateVillainMental(int mental, MentalState state)
        {
            if (villainMental_Text != null)
                villainMental_Text.text = $"멘탈: {mental}";
            if (villainState_Text != null)
                villainState_Text.text = $"상태: {state}";
        }

        // ─────────────────────────────────────────
        // 실린더 표시 갱신
        // ─────────────────────────────────────────
        public void UpdateCylinder(bool[] slots, int currentIdx)
        {
            if (cylinder_Text == null) return;
            System.Text.StringBuilder sb = new System.Text.StringBuilder("실린더: ");
            for (int i = 0; i < slots.Length; i++)
            {
                string mark = slots[i] ? "실" : "공";
                if (i == currentIdx) sb.Append($"[▶{mark}]");
                else sb.Append($"[{mark}]");
            }
            cylinder_Text.text = sb.ToString();
        }

        // ─────────────────────────────────────────
        // 빌런 핸드 수 표시
        // ─────────────────────────────────────────
        public void UpdateVillainHandCount(int count)
        {
            if (villainHand_Text != null)
                villainHand_Text.text = $"빌런 핸드: {count}장";
        }

        // ─────────────────────────────────────────
        // 플레이어 핸드 카드 버튼 동적 생성
        // ─────────────────────────────────────────
        public void UpdatePlayerHand(List<CardData> hand)
        {
            if (handPanel == null || cardButtonPrefab == null) return;

            // 기존 버튼 모두 제거
            foreach (Transform child in handPanel)
                Destroy(child.gameObject);

            // 카드 수만큼 버튼 생성
            foreach (var card in hand)
            {
                var btnObj = Instantiate(cardButtonPrefab, handPanel);
                var btn = btnObj.GetComponent<Button>();
                var txt = btnObj.GetComponentInChildren<Text>();

                if (txt != null) txt.text = card.cardName;

                // 클로저 캡처 방지용 로컬 변수
                CardData captured = card;
                btn?.onClick.AddListener(() =>
                {
                    playerController.UseCard(captured);
                });
            }
        }

        // ─────────────────────────────────────────
        // 로그 텍스트 추가
        // ─────────────────────────────────────────
        public void AppendLog(string msg)
        {
            logBuffer += msg + "\n";

            // 최대 줄 수 초과 시 오래된 줄 제거
            string[] lines = logBuffer.Split('\n');
            if (lines.Length > maxLogLines)
            {
                logBuffer = string.Join("\n", lines, lines.Length - maxLogLines, maxLogLines);
            }

            if (logText != null)
                logText.text = logBuffer;
        }

        // ─────────────────────────────────────────
        // 턴 텍스트 갱신
        // ─────────────────────────────────────────
        public void SetTurnText(string text)
        {
            if (turnText != null) turnText.text = text;
        }

        // ─────────────────────────────────────────
        // 플레이어 입력 활성/비활성 (버튼 interactable)
        // ─────────────────────────────────────────
        public void SetPlayerInputActive(bool active)
        {
            if (shootButton != null) shootButton.interactable = active;
            if (endTurnButton != null) endTurnButton.interactable = active;

            // 핸드 패널 버튼들도 일괄 처리
            if (handPanel != null)
            {
                foreach (Transform child in handPanel)
                {
                    var btn = child.GetComponent<Button>();
                    if (btn != null) btn.interactable = active;
                }
            }
        }

        // ─────────────────────────────────────────
        // 게임오버 패널 표시
        // ─────────────────────────────────────────
        public void ShowGameOver(bool isWin, WinType winType, string title, string script)
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (resultTitle_Text != null) resultTitle_Text.text = title;
            if (resultScript_Text != null) resultScript_Text.text = script;
        }
    }
}
