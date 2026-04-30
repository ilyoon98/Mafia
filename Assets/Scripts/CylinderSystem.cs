using UnityEngine;
using System.Text;

namespace NoirRoulette
{
    // 발사 대상
    public enum ShootTarget { Self, Opponent }

    public class CylinderSystem : MonoBehaviour
    {
        // 실린더 6칸 (true = 실탄, false = 공탄)
        public bool[] slots = new bool[6];

        // 현재 발사할 칸 인덱스 (0~5)
        public int currentIndex = 0;

        // 발사 대상 (기본: 자기 자신)
        public ShootTarget shootTarget = ShootTarget.Self;

        // 잼 카드 발동 여부 (다음 Fire() 불발 처리)
        public bool isJammed = false;

        // 현재 실린더의 기준 실탄 수 (재장전 시 재사용)
        private int baseBulletCount = 1;

        // ─────────────────────────────────────────
        // 장전 — bulletCount개 실탄을 랜덤 배치
        // ─────────────────────────────────────────
        public void Load(int bulletCount = 1)
        {
            baseBulletCount = Mathf.Clamp(bulletCount, 0, 6);
            slots = new bool[6];
            currentIndex = 0;
            isJammed = false;
            shootTarget = ShootTarget.Self;

            // 랜덤 위치에 실탄 배치
            bool[] placed = new bool[6];
            int placed_count = 0;
            while (placed_count < baseBulletCount)
            {
                int pos = Random.Range(0, 6);
                if (!placed[pos])
                {
                    placed[pos] = true;
                    slots[pos] = true;
                    placed_count++;
                }
            }

            LogCylinderState("장전");
        }

        // ─────────────────────────────────────────
        // 발사 — 현재 칸 소모, 다음 칸으로 이동
        // 반환: true = 실탄 명중, false = 공탄
        // ─────────────────────────────────────────
        public bool Fire()
        {
            // 잼 처리: 불발, 탄 소모 없음
            if (isJammed)
            {
                isJammed = false;
                Debug.Log($"[실린더] 잼! {currentIndex + 1}번 칸 불발. 탄 소모 없음.");
                UIManager.Instance?.AppendLog("잼! 불발 처리됨. (탄 소모 없음)");
                ResetTarget();
                return false;
            }

            bool isLive = slots[currentIndex];
            string slotType = isLive ? "실탄" : "공탄";
            string targetStr = shootTarget == ShootTarget.Self ? "자기 자신" : "상대";
            Debug.Log($"[발사] {currentIndex + 1}번 칸 [{slotType}] → {targetStr}");
            UIManager.Instance?.AppendLog($"발사! [{slotType}] {currentIndex + 1}번 칸 → {targetStr}");

            // 칸 소모 후 다음으로 이동
            slots[currentIndex] = false;
            currentIndex++;

            // 6칸 소모 또는 남은 칸에 실탄이 없으면 자동 재장전
            if (currentIndex >= 6 || !HasLiveBulletRemaining())
            {
                Debug.Log("[실린더] 실탄 없음 → 자동 재장전");
                UIManager.Instance?.AppendLog("실린더 소모 완료 — 자동 재장전!");
                Load(baseBulletCount);
            }

            ResetTarget();
            return isLive;
        }

        // ─────────────────────────────────────────
        // 실린더 섞기 — 현재 칸 이후의 탄만 재배치
        // ─────────────────────────────────────────
        public void Shuffle()
        {
            int remaining = 6 - currentIndex;
            for (int i = remaining - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int ai = currentIndex + i;
                int aj = currentIndex + j;
                bool temp = slots[ai];
                slots[ai] = slots[aj];
                slots[aj] = temp;
            }
            Debug.Log("[실린더] 셔플 완료. 탄 정보 초기화.");
            UIManager.Instance?.AppendLog("실린더 섞기! 기존 탄 정보 무효화.");
            LogCylinderState("셔플 후");
        }

        // ─────────────────────────────────────────
        // 실탄 1발 추가 후 섞기
        // ─────────────────────────────────────────
        public void AddBullet()
        {
            // 남은 칸 중 공탄 칸 찾아 실탄 추가
            for (int i = currentIndex; i < 6; i++)
            {
                if (!slots[i])
                {
                    slots[i] = true;
                    baseBulletCount = Mathf.Min(6, baseBulletCount + 1);
                    Debug.Log($"[실린더] 실탄 추가! {i + 1}번 칸 → 셔플.");
                    UIManager.Instance?.AppendLog("실탄 1발 추가됨! (셔플)");
                    Shuffle();
                    return;
                }
            }
            Debug.Log("[실린더] 실탄 추가 실패 — 빈 칸 없음 (만탄).");
            UIManager.Instance?.AppendLog("실린더 만탄 — 실탄 추가 불가.");
        }

        // ─────────────────────────────────────────
        // 특정 칸 탄 확인 (탄 소모 없음)
        // ─────────────────────────────────────────
        public bool PeekSlot(int index)
        {
            if (index < 0 || index >= 6) return false;
            bool isLive = slots[index];
            string type = isLive ? "실탄" : "공탄";
            Debug.Log($"[탄 확인] {index + 1}번 칸 → [{type}]");
            UIManager.Instance?.AppendLog($"탄 확인: {index + 1}번 칸 [{type}]");
            return isLive;
        }

        // ─────────────────────────────────────────
        // 허공 쏘기 — 현재 탄 소모, 실탄/공탄만 확인
        // ─────────────────────────────────────────
        public bool FireBlank()
        {
            bool isLive = slots[currentIndex];
            string type = isLive ? "실탄" : "공탄";
            Debug.Log($"[허공 쏘기] {currentIndex + 1}번 칸 [{type}] 소모 (허공 발사).");
            UIManager.Instance?.AppendLog($"허공 쏘기! {currentIndex + 1}번 칸 [{type}] 확인 후 소모.");

            slots[currentIndex] = false;
            currentIndex++;
            if (currentIndex >= 6)
            {
                Debug.Log("[실린더] 자동 재장전");
                Load(baseBulletCount);
            }
            return isLive;
        }

        // ─────────────────────────────────────────
        // 실린더 넘기기 — 현재 칸의 탄을 6번째 칸으로 이동
        // ─────────────────────────────────────────
        public void SkipToLast()
        {
            bool current = slots[currentIndex];
            slots[currentIndex] = false;
            slots[5] = current;
            string type = current ? "실탄" : "공탄";
            Debug.Log($"[실린더 넘기기] {currentIndex + 1}번 칸 [{type}] → 6번째 칸으로 이동.");
            UIManager.Instance?.AppendLog($"실린더 넘기기! 현재 [{type}] → 6번째 칸.");
            LogCylinderState("넘기기 후");
        }

        // ─────────────────────────────────────────
        // 잼 — 다음 Fire() 불발 예약
        // ─────────────────────────────────────────
        public void JamNext()
        {
            isJammed = true;
            Debug.Log("[잼] 다음 발사 불발 예약.");
            UIManager.Instance?.AppendLog("잼! 다음 발사는 불발 처리.");
        }

        // 남은 칸에 실탄이 하나라도 있는지 확인
        private bool HasLiveBulletRemaining()
        {
            for (int i = currentIndex; i < 6; i++)
                if (slots[i]) return true;
            return false;
        }

        // 발사 대상 변경
        public void SetTarget(ShootTarget target)
        {
            shootTarget = target;
            string targetStr = target == ShootTarget.Self ? "자기 자신" : "상대";
            Debug.Log($"[실린더] 발사 대상 → {targetStr}");
        }

        // 발사 대상 리셋
        public void ResetTarget()
        {
            shootTarget = ShootTarget.Self;
        }

        // ─────────────────────────────────────────
        // 디버그: 실린더 전체 상태 Console 출력
        // ─────────────────────────────────────────
        public void LogCylinderState(string prefix = "")
        {
            StringBuilder sb = new StringBuilder($"[실린더] {prefix} : ");
            for (int i = 0; i < 6; i++)
            {
                string mark = slots[i] ? "실탄" : "공탄";
                string cur = (i == currentIndex) ? "◀현재" : "";
                sb.Append($"{i + 1}:{mark}{cur}  ");
            }
            Debug.Log(sb.ToString());
        }

        // DebugPanel용 실린더 상태 문자열 반환
        public string GetCylinderDisplayString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                string mark = slots[i] ? "실" : "공";
                if (i == currentIndex)
                    sb.Append($"[▶{mark}]");
                else
                    sb.Append($"[{mark}]");
            }
            return sb.ToString();
        }
    }
}
