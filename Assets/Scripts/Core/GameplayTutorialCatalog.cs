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
                "먹방울은 1초마다 자동으로 뛰어요.\n내려올 곳을 손가락으로 그리면 먹선이 발판이 돼요.\n아래 먹 게이지는 지금 그릴 수 있는 총량이에요.\n먹선은 시간이 지나면 마르고, 넘치면 오래된 선부터 지워져요.",
                "MukJump/UI/Growth/growth_platform"),
            new(
                GameplayTutorialTopic.LandingPlatform,
                "기울기와 길이를 읽어요",
                "먹방울이 선 위에 내려오면 다시 뛰어올라요.\n선의 기울기는 점프 방향을 바꿔요.\n긴 선은 더 멀리 오를 수 있지만 먹도 더 많이 써요.\n캐릭터 바로 곁의 선은 안전을 위해 발판이 되지 않아요.",
                "MukJump/UI/Growth/growth_jump"),
            new(
                GameplayTutorialTopic.Obstacles,
                "붉은 먹은 위험해요",
                "붉은 장애물에 닿으면 체력 한 칸을 잃어요.\n화면 한쪽의 느낌표와 붉은 먹빛이 깜빡이면 해태가 그 높이를 가로질러요.\n기본 체력은 1칸이며, 성장하면 최대 5칸이에요.\n아래로 떨어져도 한 칸을 잃고 다시 튀어 올라오며, 마지막 먹방울이 쓰러지면 끝나요.",
                "MukJump/UI/Growth/growth_guard"),
            new(
                GameplayTutorialTopic.Weather,
                "바람을 읽어요",
                "화면 위 풍향표가 지금 부는 바람을 알려줘요.\n횡풍은 공중의 먹방울을 천천히 옆으로 밀어요.\n푸른 풍맥 발판은 높은 곳으로 오를 틈을 만들어요.\n강한 상승기류가 오면 선을 아끼며 다음 착지를 준비하세요.",
                "MukJump/UI/Growth/growth_ink_regen"),
            new(
                GameplayTutorialTopic.MapZones,
                "산수화도 함께 변해요",
                "높이 오를수록 날씨와 산수화 풍경이 달라져요.\n새 구간에는 다른 배치의 발판과 장애물이 나타나요.\n분신을 얻으면 하나가 쓰러져도 남은 먹방울로 계속해요.\n오래 살아남아 나만의 최고 고도를 새로 쓰세요.",
                "MukJump/UI/Growth/growth_scroll"),
        };

        public static IReadOnlyList<GameplayTutorialPage> Pages => pages;
        public static int Count => pages.Length;
        public static GameplayTutorialPage Get(int index) => pages[index];
    }
}
