using System.Collections.Generic;

namespace MukJump.Core
{
    public enum GameplayTutorialTopic
    {
        DrawInk,
        LandingPlatform,
        Obstacles,
        Weather,
        MapZones,
    }

    /// 첫 플레이 안내와 옵션의 다시 보기가 같은 문구를 사용하게 하는 불변 데이터다.
    public readonly struct GameplayTutorialPage
    {
        public GameplayTutorialPage(
            GameplayTutorialTopic topic,
            string title,
            string description,
            string spriteResourcePath)
        {
            Topic = topic;
            Title = title;
            Description = description;
            SpriteResourcePath = spriteResourcePath;
        }

        public GameplayTutorialTopic Topic { get; }
        public string Title { get; }
        public string Description { get; }
        public string SpriteResourcePath { get; }
    }

    public static class GameplayTutorialCatalog
    {
        static readonly GameplayTutorialPage[] pages =
        {
            new(
                GameplayTutorialTopic.DrawInk,
                "한 획이 발판이 돼요",
                "먹방울은 1초마다 뛰어요. 내려올 곳에 먹선을 그리세요.\n먹선은 곧 마르고, 넘치면 오래된 선부터 지워져요.",
                "MukJump/UI/Growth/growth_platform"),
            new(
                GameplayTutorialTopic.LandingPlatform,
                "기울기와 길이를 읽어요",
                "그린 먹선은 착지 발판이 돼요.\n선의 기울기는 점프 방향을, 길이는 점프 힘을 바꿔요.",
                "MukJump/UI/Growth/growth_jump"),
            new(
                GameplayTutorialTopic.Obstacles,
                "붉은 먹은 위험해요",
                "화면 가장자리 붉은 먹벽은 상승 힘을 끊어요.\n먹가시·낙묵석에 맞거나 추락하면 체력이 한 칸 줄어요.",
                "MukJump/UI/Growth/growth_guard"),
            new(
                GameplayTutorialTopic.Weather,
                "바람을 읽어요",
                "위 풍향표로 횡풍을 예상하세요.\n푸른 풍맥 발판과 상승기류는 오를 틈을 만들어요.",
                "MukJump/UI/Growth/growth_ink_regen"),
            new(
                GameplayTutorialTopic.MapZones,
                "산수화도 함께 변해요",
                "높이 오를수록 날씨와 산수화 맵이 바뀌어요.\n먹떼와 함께 최고 고도에 도전하세요.",
                "MukJump/UI/Growth/growth_scroll"),
        };

        public static IReadOnlyList<GameplayTutorialPage> Pages => pages;
        public static int Count => pages.Length;
        public static GameplayTutorialPage Get(int index) => pages[index];
    }
}
