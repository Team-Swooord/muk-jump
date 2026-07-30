namespace MukJump.Core
{
    /// 영구 성장 전용 화면에서 작은 서예 글꼴이 축소 렌더링으로 뭉개지지 않게 하는
    /// 타이포그래피 최소 크기. 배치 코드는 요청 크기를 유지하되 이 하한보다 작아질 수 없다.
    public static class PermanentGrowthTypography
    {
        public static int Resolve(string elementName, int requestedSize)
        {
            int minimum = elementName switch
            {
                "Title" => 72,
                "Subtitle" => 36,
                "Balance" => 40,
                "InkTreeRootLabel" => 28,
                "Name" => 44,
                "Level" => 32,
                "Description" => 34,
                "Effect" => 32,
                "Cost" => 30,
                "DetailName" => 46,
                "DetailLevel" => 32,
                "DetailDescription" => 32,
                "CurrentEffect" => 32,
                "NextEffect" => 32,
                "DetailCost" => 30,
                "PermanentHint" => 30,
                _ when elementName != null &&
                       elementName.StartsWith(
                           "GrowthNodeLabel",
                           System.StringComparison.Ordinal) => 28,
                _ => requestedSize,
            };
            return System.Math.Max(requestedSize, minimum);
        }

        public static void ApplyLayout(
            UnityEngine.UI.Text text,
            string elementName)
        {
            if (text == null || text.rectTransform == null)
                return;

            ApplyVisualWeight(text, elementName);

            UnityEngine.Vector2 minimum = elementName switch
            {
                "Name" => new UnityEngine.Vector2(190f, 54f),
                "Level" => new UnityEngine.Vector2(190f, 40f),
                "Description" => new UnityEngine.Vector2(190f, 44f),
                "Effect" => new UnityEngine.Vector2(190f, 42f),
                "Cost" => new UnityEngine.Vector2(96f, 40f),
                "DetailName" => new UnityEngine.Vector2(210f, 58f),
                "DetailLevel" => new UnityEngine.Vector2(210f, 38f),
                "DetailDescription" => new UnityEngine.Vector2(390f, 80f),
                "CurrentEffect" => new UnityEngine.Vector2(500f, 42f),
                "NextEffect" => new UnityEngine.Vector2(500f, 42f),
                "DetailCost" => new UnityEngine.Vector2(270f, 42f),
                "Balance" => new UnityEngine.Vector2(360f, 64f),
                "PermanentHint" => new UnityEngine.Vector2(780f, 60f),
                _ when elementName != null &&
                       elementName.StartsWith(
                           "GrowthNodeLabel",
                           System.StringComparison.Ordinal) =>
                    new UnityEngine.Vector2(42f, 38f),
                _ => UnityEngine.Vector2.zero,
            };
            if (minimum == UnityEngine.Vector2.zero)
                return;

            UnityEngine.Vector2 size = text.rectTransform.sizeDelta;
            size.x = UnityEngine.Mathf.Max(size.x, minimum.x);
            size.y = UnityEngine.Mathf.Max(size.y, minimum.y);
            text.rectTransform.sizeDelta = size;

            UnityEngine.Vector2 position =
                text.rectTransform.anchoredPosition;
            switch (elementName)
            {
                case "Name":
                    position.x = UnityEngine.Mathf.Sign(position.x) * 125f;
                    position.y = 82f;
                    break;
                case "Level":
                    position.x = UnityEngine.Mathf.Sign(position.x) * 125f;
                    position.y = 33f;
                    break;
                case "Description":
                    position.x = UnityEngine.Mathf.Sign(position.x) * 125f;
                    position.y = -9f;
                    break;
                case "Effect":
                    position.x = UnityEngine.Mathf.Sign(position.x) * 125f;
                    position.y = -53f;
                    break;
                case "DetailName":
                    position = new UnityEngine.Vector2(-215f, 88f);
                    break;
                case "DetailLevel":
                    position = new UnityEngine.Vector2(-215f, 40f);
                    break;
                case "DetailDescription":
                    position = new UnityEngine.Vector2(120f, 88f);
                    break;
                case "CurrentEffect":
                    position = new UnityEngine.Vector2(-85f, -3f);
                    break;
                case "NextEffect":
                    position = new UnityEngine.Vector2(-85f, -45f);
                    break;
            }
            text.rectTransform.anchoredPosition = position;

            if (elementName == "Name" ||
                elementName == "Level" ||
                elementName == "Description" ||
                elementName == "Effect" ||
                elementName == "Cost")
            {
                text.horizontalOverflow =
                    UnityEngine.HorizontalWrapMode.Overflow;
            }
        }

        /// 단일 Std 굵기 서체의 합성 Bold가 축소된 세로 화면에서 사라지지 않도록
        /// 글자색과 같은 외곽선을 얇게 겹쳐 실제 획 두께를 보강한다.
        static void ApplyVisualWeight(
            UnityEngine.UI.Text text,
            string elementName)
        {
            var outline =
                text.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null)
                outline =
                    text.gameObject.AddComponent<UnityEngine.UI.Outline>();

            float distance = elementName switch
            {
                "Title" => 2.5f,
                "Subtitle" => 2f,
                "Balance" => 2f,
                "Name" or "DetailName" => 1.8f,
                _ => 1.5f,
            };
            float alpha = elementName switch
            {
                "Title" => 0.86f,
                "Subtitle" => 0.8f,
                "Balance" => 0.84f,
                _ => 0.72f,
            };
            UnityEngine.Color textColor = text.color;
            outline.effectColor = new UnityEngine.Color(
                textColor.r,
                textColor.g,
                textColor.b,
                alpha);
            outline.effectDistance =
                new UnityEngine.Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }
    }
}
