using UnityEngine;

namespace NoirRoulette
{
    // 카드 효과 실행 — 13종 전부 static 메서드로 구현
    public static class CardEffect
    {
        // 카드 실행 진입점
        // isPlayer: true = 플레이어 사용, false = 빌런 사용
        // 반환: true = 효과 정상 실행, false = 조건 불충족 등 실패
        public static bool Execute(CardData card, bool isPlayer)
        {
            var gm = GameManager.Instance;
            var cyl = gm.cylinderSystem;
            var playerDeck = gm.playerDeckManager;
            var villainDeck = gm.villainDeckManager;
            var villain = gm.villainAI;
            string user = isPlayer ? "플레이어" : "빌런";

            Debug.Log($"[카드 사용] {user} → [{card.cardName}]");
            gm.uiManager.AppendLog($"{user}: [{card.cardName}] 사용");

            switch (card.cardType)
            {
                case CardType.조준:           return Do조준(gm, cyl, isPlayer);
                case CardType.급소:           return Do급소(gm, cyl, isPlayer);
                case CardType.실린더섞기:     return Do실린더섞기(cyl);
                case CardType.총알추가:       return Do총알추가(cyl);
                case CardType.카드뺏기:       return Do카드뺏기(isPlayer, playerDeck, villainDeck, gm);
                case CardType.허공쏘기:       return Do허공쏘기(gm, cyl, isPlayer);
                case CardType.탄확인:         return Do탄확인(cyl, isPlayer);
                case CardType.협박:           return Do협박(gm);
                case CardType.조롱:           return Do조롱(gm, isPlayer);
                case CardType.눈치채기:       return Do눈치채기(isPlayer, gm, villain);
                case CardType.체력회복:       return Do체력회복(isPlayer, gm);
                case CardType.잼:             return Do잼(cyl);
                case CardType.실린더넘기기:   return Do실린더넘기기(cyl);
                default:
                    Debug.LogWarning($"[카드] 미구현 카드 타입: {card.cardType}");
                    return false;
            }
        }

        // ──────────────────────────────────────────────────────
        // 공격계
        // ──────────────────────────────────────────────────────

        // 조준: 플레이어 사용 시 즉시 상대에게 발사. 빌런 사용 시 대상 예약(VillainShoot에서 발사).
        private static bool Do조준(GameManager gm, CylinderSystem cyl, bool isPlayer)
        {
            cyl.SetTarget(ShootTarget.Opponent);

            if (!isPlayer)
            {
                Debug.Log("[조준] 발사 대상 → 상대. (빌런 발사 단계에서 처리)");
                return true;
            }

            // 플레이어: 즉시 발사
            bool isLive = cyl.Fire();
            if (isLive)
            {
                gm.DamageVillain(1, DamageSource.Bullet);
                gm.uiManager.AppendLog("조준 명중! 빌런 HP -1");
                gm.uiManager.AddPlayerAction("[조준] → 명중! 빌런 HP-1");
            }
            else
            {
                gm.uiManager.AppendLog("공탄. 빌런 안전.");
                gm.uiManager.AddPlayerAction("[조준] → 공탄");
            }
            return true;
        }

        // 급소: 플레이어 사용 시 즉시 발사 — 명중=HP-2 / 공탄=내 HP-1.
        // 빌런 사용 시 대상 예약 + 급소 플래그(VillainShoot에서 처리).
        private static bool Do급소(GameManager gm, CylinderSystem cyl, bool isPlayer)
        {
            cyl.SetTarget(ShootTarget.Opponent);

            if (!isPlayer)
            {
                gm.villainAI.isGuksoPendingForVillain = true;
                Debug.Log("[급소] 발사 대상 → 상대. 명중=플레이어 HP-2 / 공탄=빌런 HP-1 예약. (빌런 발사 단계에서 처리)");
                return true;
            }

            // 플레이어: 즉시 발사
            bool isLive = cyl.Fire();
            if (isLive)
            {
                gm.DamageVillain(2, DamageSource.Bullet);
                gm.uiManager.AppendLog("급소 명중! 빌런 HP -2");
                gm.uiManager.AddPlayerAction("[급소] → 명중! 빌런 HP-2");
            }
            else
            {
                gm.DamagePlayer(1);
                gm.uiManager.AppendLog("급소 공탄... 내 HP -1.");
                gm.uiManager.AddPlayerAction("[급소] → 공탄 (내 HP-1)");
            }
            gm.isGuksoPending = false;
            return true;
        }

        // ──────────────────────────────────────────────────────
        // 방해계
        // ──────────────────────────────────────────────────────

        // 실린더 섞기
        private static bool Do실린더섞기(CylinderSystem cyl)
        {
            cyl.Shuffle();
            return true;
        }

        // 총알 추가
        private static bool Do총알추가(CylinderSystem cyl)
        {
            cyl.AddBullet();
            return true;
        }

        // 카드 뺏기: 상대 핸드에서 랜덤 1장 버림
        private static bool Do카드뺏기(bool isPlayer, DeckManager playerDeck, DeckManager villainDeck, GameManager gm)
        {
            DeckManager target = isPlayer ? villainDeck : playerDeck;
            if (target.hand.Count == 0)
            {
                Debug.Log("[카드 뺏기] 상대 핸드 비어있음 — 효과 없음.");
                gm.uiManager.AppendLog("카드 뺏기: 상대 핸드가 비어 효과 없음.");
                return false;
            }
            var removed = target.RemoveRandomCard();
            gm.uiManager.AppendLog($"카드 뺏기 성공! [{removed.cardName}] 버려짐.");
            // 플레이어 핸드 변경 시 UI 갱신
            gm.uiManager.UpdatePlayerHand(playerDeck.hand);
            return true;
        }

        // ──────────────────────────────────────────────────────
        // 정보계
        // ──────────────────────────────────────────────────────

        // 허공 쏘기: 현재 탄 소모, 실탄/공탄 확인 + 행동 기록
        private static bool Do허공쏘기(GameManager gm, CylinderSystem cyl, bool isPlayer)
        {
            bool isLive = cyl.FireBlank();
            string ammo = isLive ? "실탄" : "공탄";
            if (isPlayer)
                gm.uiManager.AddPlayerAction($"[허공쏘기] → {ammo}");
            else
                gm.uiManager.AddVillainAction($"[허공쏘기] → {ammo}");
            return true;
        }

        // 탄 확인: 빌런 전용 (플레이어는 PlayerController에서 슬롯 선택 UI로 처리)
        // 빌런 사용 시 랜덤 칸 선택 → Peeked(!) 표시
        private static bool Do탄확인(CylinderSystem cyl, bool isPlayer)
        {
            int remaining = 6 - cyl.currentIndex;
            if (remaining <= 0)
            {
                Debug.Log("[탄 확인] 확인할 칸 없음.");
                return false;
            }
            int offset = Random.Range(0, remaining);
            int slotIndex = cyl.currentIndex + offset;
            bool actualIsLive = cyl.slots[slotIndex];

            if (isPlayer)
            {
                // 플레이어 경로는 PlayerController.UseCard()에서 인터셉트하므로
                // 여기에 도달하는 경우는 없음 — 폴백으로 Revealed 처리
                cyl.PeekSlot(slotIndex, true);
            }
            else
            {
                // 빌런: 랜덤 칸 선택 → Peeked 표시 (UI에서 ! 로 표시)
                cyl.PeekSlot(slotIndex, false);

                // 블러핑 여부에 따라 진실 or 거짓 발언
                var villain = GameManager.Instance.villainAI;
                bool reportFalse = villain.isBluffing;
                bool reportedIsLive = reportFalse ? !actualIsLive : actualIsLive;
                string actualType   = actualIsLive   ? "실탄" : "공탄";
                string reportedType = reportedIsLive ? "실탄" : "공탄";

                villain.debug_lastTanHwakIn = $"실제:{actualType} / 발언:{reportedType}";
                Debug.Log($"[탄 확인] 빌런 {slotIndex + 1}번 칸 실제:[{actualType}] 블러핑:{reportFalse} 발언:[{reportedType}]");

                // 플레이어에게 보이는 발언 (본인이 확인한 칸에 대해서만 말할 수 있음)
                GameManager.Instance.uiManager.AppendLog(
                    $"빌런: \"{slotIndex + 1}번째 칸은 {reportedType}이야!\"");
                GameManager.Instance.uiManager.AddVillainAction(
                    $"[탄확인] {slotIndex + 1}번 칸 → \"{reportedType}이야!\"");
            }
            return true;
        }

        // ──────────────────────────────────────────────────────
        // 멘탈 공격계
        // ──────────────────────────────────────────────────────

        // 협박: 빌런 멘탈 1 감소
        private static bool Do협박(GameManager gm)
        {
            gm.DamageVillainMental(1);
            return true;
        }

        // 조롱: 직전 턴 빌런 실수 시만 사용 가능, 멘탈 2 감소
        private static bool Do조롱(GameManager gm, bool isPlayer)
        {
            if (isPlayer && !gm.villainLastTurnMistake)
            {
                Debug.Log("[조롱] 조건 불충족 — 빌런 직전 실수 없음.");
                gm.uiManager.AppendLog("조롱: 사용 불가 (빌런 직전 실수 없음).");
                return false;
            }
            gm.DamageVillainMental(2);
            return true;
        }

        // 눈치채기: 블러핑 간파 선언
        // 성공(블러핑 O) → 빌런 멘탈 -2
        // 실패(블러핑 X) → 내 HP -1
        private static bool Do눈치채기(bool isPlayer, GameManager gm, VillainAI villain)
        {
            if (!isPlayer) return false; // 빌런은 사용 안 함

            if (villain.isBluffing)
            {
                Debug.Log("[눈치채기] 성공! 블러핑 간파.");
                gm.uiManager.AppendLog("눈치채기 성공! 빌런 블러핑 들킴 → 멘탈 -2");
                gm.DamageVillainMental(2);
                gm.villainLastTurnMistake = true;
            }
            else
            {
                Debug.Log("[눈치채기] 실패! 블러핑 아님 → 플레이어 HP -1");
                gm.uiManager.AppendLog("눈치채기 실패! 블러핑 아님 → 내 HP -1");
                gm.DamagePlayer(1);
            }
            return true;
        }

        // ──────────────────────────────────────────────────────
        // 회복/방어계
        // ──────────────────────────────────────────────────────

        // 체력 회복: 플레이어 HP +1 (최대 3)
        private static bool Do체력회복(bool isPlayer, GameManager gm)
        {
            if (isPlayer)
            {
                if (gm.playerHP >= gm.playerMaxHP)
                {
                    gm.uiManager.AppendLog("체력 회복: 이미 최대 체력.");
                    return false;
                }
                gm.HealPlayer(1);
                return true;
            }
            // 빌런이 체력 회복 사용
            if (gm.villainHP >= 3)
            {
                gm.uiManager.AppendLog("[빌런] 체력 회복: 이미 최대.");
                gm.villainLastTurnMistake = true;
                return false;
            }
            gm.SetVillainStat(Mathf.Min(3, gm.villainHP + 1));
            gm.uiManager.AppendLog($"[빌런] 체력 회복! HP/멘탈 → {gm.villainHP}");
            return true;
        }

        // 잼: 다음 발사 불발 처리
        private static bool Do잼(CylinderSystem cyl)
        {
            cyl.JamNext();
            return true;
        }

        // 실린더 넘기기: 현재 탄 → 6번째 칸으로 이동
        private static bool Do실린더넘기기(CylinderSystem cyl)
        {
            cyl.SkipToLast();
            return true;
        }
    }
}
