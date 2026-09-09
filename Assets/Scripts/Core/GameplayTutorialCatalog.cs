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
        InkBudget,
        AutoJump,
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
                "선 그리기",
                "먹방울은 자동으로 뛰어요.\n내려올 곳에 선을 그려주세요.\n기울기는 점프 방향을, 길이는 힘을 정해요.",
                "MukJump/UI/Growth/growth_platform"),
            new(
                GameplayTutorialTopic.InkBudget,
                "먹 게이지",
                "아래 게이지는 남은 먹이에요.\n선은 시간이 지나면 사라져요.\n먹이 부족하면 오래된 선부터 지워져요.",
                "MukJump/UI/Growth/growth_ink_capacity"),
            new(
                GameplayTutorialTopic.Obstacles,
                "체력",
                "장애물에 닿거나 떨어지면 체력이 1칸 줄어요.\n분신까지 모두 쓰러지면 게임이 끝나요.",
                "MukJump/UI/Growth/growth_guard"),
        };

        public static IReadOnlyList<GameplayTutorialPage> Pages => pages;
        public static int Count => pages.Length;
        public static GameplayTutorialPage Get(int index) => pages[index];
    }
}
