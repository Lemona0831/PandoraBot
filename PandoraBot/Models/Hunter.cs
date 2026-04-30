using System;
using System.Collections.Generic;
using System.Text;

namespace PandoraBot.Models
{
    public class Hunter
    {
        // 1. 시스템 식별 데이터 (A, B열)
        public string UserId { get; set; } = "";      // 유저 고유 ID (A열)
        public string CharacterName { get; set; } = ""; // 캐릭터 이름 (B열)

        // 2. 특성치 데이터 (C ~ H열)
        public int Strength { get; set; }     // 근력 (C열)
        public int Dexterity { get; set; }    // 민첩성 (D열)
        public int Constitution { get; set; } // 체력 (E열)
        public int Intelligence { get; set; } // 지능 (F열)
        public int Wisdom { get; set; }       // 지혜 (G열)
        public int Charisma { get; set; }     // 매력 (H열)

        // 3. 전투 상태 데이터 (I, J열)
        public int MaxHp { get; set; }     // 최대 체력 (I열)
        public int CurrentHp { get; set; } // 현재 체력 (J열)

        // [캡슐화된 로직] 수정치 계산 기능 (이미지의 -1, -3 같은 값들)
        // 예를 들어 (특성치 - 10) / 2 같은 공식이 있다면 여기에 넣으면 됩니다.
        public int GetModifier(int statValue)
        {
            return statValue switch
            {
                >= 18 => 3,
                >= 16 => 2,
                >= 13 => 1,
                >= 9 => 0,
                >= 6 => -1,
                >= 4 => -2,
                _ => -3
            };
        }
    }
}
