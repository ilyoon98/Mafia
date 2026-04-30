using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace NoirRoulette
{
    // 덱/핸드 관리 — 플레이어/빌런 각각 1개 인스턴스로 사용
    public class DeckManager : MonoBehaviour
    {
        [Header("카드 에셋 (Inspector에서 13종 ScriptableObject 연결)")]
        public CardData[] allCardAssets;

        // 드로우 대기 중인 덱
        public List<CardData> deck = new List<CardData>();

        // 현재 손에 든 핸드
        public List<CardData> hand = new List<CardData>();

        // 핸드 최대 크기
        public int maxHandSize = 3;

        // ─────────────────────────────────────────
        // 덱 초기화 — allCardAssets × quantity로 25장 구성 후 셔플
        // ─────────────────────────────────────────
        public void BuildDeck()
        {
            deck.Clear();
            hand.Clear();

            foreach (var card in allCardAssets)
            {
                if (card == null) continue;
                for (int i = 0; i < card.quantity; i++)
                    deck.Add(card);
            }

            Shuffle();
            Debug.Log($"[덱] 초기화 완료. 총 {deck.Count}장.");
        }

        // ─────────────────────────────────────────
        // Fisher-Yates 셔플 + 덱 내용 Console 출력
        // ─────────────────────────────────────────
        public void Shuffle()
        {
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                var temp = deck[i];
                deck[i] = deck[j];
                deck[j] = temp;
            }
            LogDeckContents();
        }

        // ─────────────────────────────────────────
        // 핸드가 maxHandSize가 될 때까지 드로우
        // 덱 소진 시 allCardAssets로 재생성 후 셔플
        // ─────────────────────────────────────────
        public void DrawToHand()
        {
            while (hand.Count < maxHandSize)
            {
                if (deck.Count == 0)
                {
                    Debug.Log("[덱] 덱 소진 → 재생성 후 셔플.");
                    RebuildDeckFromAssets();
                    if (deck.Count == 0) break; // 에셋이 없으면 중단
                }

                var card = deck[0];
                deck.RemoveAt(0);
                hand.Add(card);
            }

            LogHandContents();
        }

        // 에셋에서 덱 재생성 (핸드는 유지, 셔플 포함)
        private void RebuildDeckFromAssets()
        {
            deck.Clear();
            foreach (var card in allCardAssets)
            {
                if (card == null) continue;
                for (int i = 0; i < card.quantity; i++)
                    deck.Add(card);
            }
            Shuffle();
            Debug.Log($"[덱] 재생성 완료. {deck.Count}장.");
        }

        // ─────────────────────────────────────────
        // 핸드에서 특정 카드 제거 (사용 시 호출)
        // ─────────────────────────────────────────
        public void DiscardCard(CardData card)
        {
            hand.Remove(card);
        }

        // ─────────────────────────────────────────
        // 핸드에서 랜덤 1장 제거 (카드 뺏기)
        // ─────────────────────────────────────────
        public CardData RemoveRandomCard()
        {
            if (hand.Count == 0) return null;
            int idx = Random.Range(0, hand.Count);
            CardData removed = hand[idx];
            hand.RemoveAt(idx);
            Debug.Log($"[카드 뺏기] 핸드에서 [{removed.cardName}] 제거됨.");
            return removed;
        }

        // ─────────────────────────────────────────
        // 디버그: 덱 전체 내용 Console 출력
        // ─────────────────────────────────────────
        public void LogDeckContents()
        {
            StringBuilder sb = new StringBuilder($"[덱 셔플] {deck.Count}장 순서: ");
            for (int i = 0; i < deck.Count; i++)
                sb.Append($"{i + 1}.{deck[i].cardName} / ");
            Debug.Log(sb.ToString());
        }

        // 디버그: 핸드 내용 Console 출력
        public void LogHandContents()
        {
            StringBuilder sb = new StringBuilder($"[핸드] {hand.Count}장: ");
            foreach (var card in hand)
                sb.Append($"[{card.cardName}] ");
            Debug.Log(sb.ToString());
        }

        // DebugPanel용 핸드 카드 이름 문자열 반환
        public string GetHandDisplayString()
        {
            if (hand.Count == 0) return "(없음)";
            StringBuilder sb = new StringBuilder();
            foreach (var card in hand)
                sb.Append($"[{card.cardName}] ");
            return sb.ToString().TrimEnd();
        }
    }
}
