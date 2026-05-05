using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;

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
        public Text cylinderCount_Text;         // "실탄 N발 / 공탄 M발" 상단 텍스트
        public Text cylinder_Text;              // 6칸 슬롯 상태 표시
        public GameObject slotSelectionPanel;   // 탄 확인 슬롯 선택 패널
        public Button[] slotSelectButtons;      // 1~6번 슬롯 버튼 (6개)

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
        // Inspector 연결 — 행동 기록 패널
        // ─────────────────────────────────────────
        [Header("행동 기록 패널")]
        public Text actionLogText;              // 나 / 상대 행동 요약 텍스트

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

        // 행동 기록 (턴마다 초기화)
        private readonly List<string> _playerActions = new List<string>();
        private readonly List<string> _villainActions = new List<string>();

        // ─────────────────────────────────────────

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (slotSelectionPanel != null) slotSelectionPanel.SetActive(false);

            // 발사/턴 종료 버튼 리스너 연결
            if (shootButton != null)
                shootButton.onClick.AddListener(() => playerController.OnShootButtonClicked());
            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(() => playerController.OnEndTurnButtonClicked());
            if (restartButton != null)
                restartButton.onClick.AddListener(() => { GameManager.Instance.StartGame(); });

            // 슬롯 선택 버튼 리스너 연결 (0~5 인덱스)
            if (slotSelectButtons != null)
            {
                for (int i = 0; i < slotSelectButtons.Length; i++)
                {
                    int captured = i; // 클로저 캡처용
                    if (slotSelectButtons[i] != null)
                        slotSelectButtons[i].onClick.AddListener(() => playerController.OnSlotSelected(captured));
                }
            }
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
            UpdateCylinder(gm.cylinderSystem.slots, gm.cylinderSystem.currentIndex, gm.cylinderSystem.slotStates);
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
        public void UpdateCylinder(bool[] slots, int currentIdx, CylinderSlotState[] slotStates)
        {
            // 상단: 실탄/공탄 수
            if (cylinderCount_Text != null)
            {
                int liveCount = 0;
                for (int i = currentIdx; i < slots.Length; i++)
                    if (slots[i] && slotStates[i] != CylinderSlotState.Consumed)
                        liveCount++;
                int remaining = slots.Length - currentIdx;
                int blankCount = remaining - liveCount;
                cylinderCount_Text.text = $"실탄 {liveCount}발 / 공탄 {blankCount}발";
            }

            // 6칸 슬롯 상태
            if (cylinder_Text == null) return;
            var gm = GameManager.Instance;
            cylinder_Text.text = "실린더: " + gm.cylinderSystem.GetCylinderDisplayString();
        }

        // 이전 시그니처 호환용 (slotStates 없이 호출되는 경우)
        public void UpdateCylinder(bool[] slots, int currentIdx)
        {
            var gm = GameManager.Instance;
            UpdateCylinder(slots, currentIdx, gm.cylinderSystem.slotStates);
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

                if (txt != null)
                {
                    txt.text = card.cardName;
                    txt.fontSize = 30;
                    txt.resizeTextForBestFit = false;
                }

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
        // 플레이어 입력 전체 활성/비활성 (턴 전환 시 사용)
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

        // ShootButton 단독 제어 (발사 1회 후 비활성화용)
        public void SetShootButtonActive(bool active)
        {
            if (shootButton != null) shootButton.interactable = active;
        }

        // EndTurnButton 단독 제어 (턴 종료 조건 충족 시 활성화용)
        public void SetEndTurnButtonActive(bool active)
        {
            if (endTurnButton != null) endTurnButton.interactable = active;
        }

        // ─────────────────────────────────────────
        // 탄 확인 슬롯 선택 패널 제어
        // 별도 패널 없이 핸드 영역에 1~6번 슬롯 버튼을 동적 생성
        // ─────────────────────────────────────────
        public void ShowSlotSelectionPanel()
        {
            if (handPanel == null || cardButtonPrefab == null) return;

            var cyl = GameManager.Instance.cylinderSystem;

            // 기존 카드 버튼 제거
            foreach (Transform child in handPanel)
                Destroy(child.gameObject);

            // 슬롯 선택 버튼 1~6 생성
            for (int i = 0; i < 6; i++)
            {
                var btnObj = Instantiate(cardButtonPrefab, handPanel);
                var btn = btnObj.GetComponent<Button>();
                var txt = btnObj.GetComponentInChildren<Text>();

                bool isConsumed = cyl.slotStates[i] == CylinderSlotState.Consumed;
                string stateLabel = GetSlotStateLabel(cyl, i);

                if (txt != null)
                {
                    txt.text = $"{i + 1}번 칸\n{stateLabel}";
                    txt.fontSize = 24;
                    txt.resizeTextForBestFit = false;
                }

                if (btn != null)
                {
                    btn.interactable = !isConsumed;
                    int captured = i;
                    btn.onClick.AddListener(() => playerController.OnSlotSelected(captured));
                }
            }
        }

        // 슬롯 상태 레이블 반환 (슬롯 선택 버튼 표시용)
        private string GetSlotStateLabel(CylinderSystem cyl, int i)
        {
            switch (cyl.slotStates[i])
            {
                case CylinderSlotState.Consumed:  return "(소모됨)";
                case CylinderSlotState.Revealed:  return cyl.slots[i] ? "[실탄]" : "[공탄]";
                case CylinderSlotState.Peeked:    return "[!]";
                default:                          return "[?]";
            }
        }

        public void HideSlotSelectionPanel()
        {
            // handPanel은 UpdatePlayerHand()에서 갱신됨 — 별도 처리 불필요
            // Inspector 연결된 패널이 있으면 함께 숨김
            if (slotSelectionPanel != null) slotSelectionPanel.SetActive(false);
        }

        // ─────────────────────────────────────────
        // 행동 기록 패널 — 나 / 상대 행동 요약
        // ─────────────────────────────────────────

        /// <summary>플레이어 행동 1건 추가</summary>
        public void AddPlayerAction(string action)
        {
            _playerActions.Add(action);
            RefreshActionLog();
        }

        /// <summary>빌런 행동 1건 추가</summary>
        public void AddVillainAction(string action)
        {
            _villainActions.Add(action);
            RefreshActionLog();
        }

        /// <summary>새 턴 시작 시 행동 기록 초기화</summary>
        public void ClearActionLog()
        {
            _playerActions.Clear();
            _villainActions.Clear();
            RefreshActionLog();
        }

        private void RefreshActionLog()
        {
            if (actionLogText == null) return;

            var sb = new StringBuilder();

            sb.AppendLine("━━ 나 ━━");
            if (_playerActions.Count == 0)
                sb.AppendLine("  (대기 중)");
            else
                foreach (var a in _playerActions)
                    sb.AppendLine($"  · {a}");

            sb.AppendLine();

            sb.AppendLine("━━ 빌런 ━━");
            if (_villainActions.Count == 0)
                sb.AppendLine("  (대기 중)");
            else
                foreach (var a in _villainActions)
                    sb.AppendLine($"  · {a}");

            actionLogText.text = sb.ToString();
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
