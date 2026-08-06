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
                "먹방울은 1초마다 자동으로 뛰어요.\n내려올 곳에 선을 그리면 발판이 돼요.\n아래 게이지는 더 그릴 수 있는 먹의 양이에요.\n먹선은 시간이 지나면 오래된 것부터 사라져요.",
                "MukJump/UI/Growth/growth_platform"),
            new(
                GameplayTutorialTopic.LandingPlatform,
                "기울기와 길이를 읽어요",
                "먹방울이 선에 닿으면 다시 뛰어요.\n기울기는 방향을, 길이는 거리를 바꿔요.\n캐릭터 바로 곁의 선은 발판이 되지 않아요.",
                "MukJump/UI/Growth/growth_jump"),
            new(
                GameplayTutorialTopic.Obstacles,
                "붉은 먹은 위험해요",
                "붉은 장애물과 해태에 닿으면 체력 한 칸을 잃어요.\n본체는 성장 체력을 쓰고, 분신은 항상 한 칸이에요.\n떨어져도 체력 한 칸을 쓰고 다시 올라와요.\n모든 먹방울이 쓰러지면 끝나요.",
                "MukJump/UI/Growth/growth_guard"),
            new(
                GameplayTutorialTopic.Weather,
                "바람을 읽어요",
                "풍향표는 지금 부는 바람을 알려줘요.\n횡풍은 먹방울을 옆으로 밀어요.\n푸른 풍맥과 상승기류를 이용해 착지를 준비하세요.",
                "MukJump/UI/Growth/growth_ink_regen"),
            new(
                GameplayTutorialTopic.MapZones,
                "산수화도 함께 변해요",
                "높이 오르면 날씨·풍경·장애물 배치가 바뀌어요.\n분신이 남아 있으면 한 마리가 쓰러져도 계속해요.\n더 높이 올라 최고 기록에 도전하세요.",
                "MukJump/UI/Growth/growth_scroll"),
        };

        public static IReadOnlyList<GameplayTutorialPage> Pages => pages;
        public static int Count => pages.Length;
        public static GameplayTutorialPage Get(int index) => pages[index];
    }
}
