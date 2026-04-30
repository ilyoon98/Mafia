using UnityEngine;

namespace NoirRoulette
{
    // 카드 13종 타입 열거형
    public enum CardType
    {
        // 공격계
        조준, 급소,
        // 방해계
        실린더섞기, 총알추가, 카드뺏기,
        // 정보계
        허공쏘기, 탄확인,
        // 멘탈 공격계
        협박, 조롱, 눈치채기,
        // 회복/방어계
        체력회복, 잼, 실린더넘기기
    }

    // 카드 데이터 ScriptableObject
    // 우클릭 → Create → NoirRoulette → CardData 로 에셋 생성
    [CreateAssetMenu(fileName = "NewCard", menuName = "NoirRoulette/CardData")]
    public class CardData : ScriptableObject
    {
        [Header("카드 기본 정보")]
        public string cardName;             // 카드 표시 이름
        public CardType cardType;           // 카드 타입 (효과 분기에 사용)
        [TextArea] public string description; // 카드 설명 (UI 툴팁용)
        public int quantity = 1;            // 25장 덱 구성 시 포함 수량
    }
}
