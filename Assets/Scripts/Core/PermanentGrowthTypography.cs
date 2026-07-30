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
                           System.StringComparison.Ordinal) => 24,
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

            ApplyContainerLayout(text, elementName);
            ApplyVisualWeight(text, elementName);
            ApplyEnhanceButtonLayout(text, elementName);

            UnityEngine.Vector2 minimum = elementName switch
            {
                "Name" => new UnityEngine.Vector2(174f, 48f),
                "Level" => new UnityEngine.Vector2(174f, 34f),
                "Description" => new UnityEngine.Vector2(174f, 36f),
                "Effect" => new UnityEngine.Vector2(174f, 34f),
                "Cost" => new UnityEngine.Vector2(96f, 32f),
                "DetailName" => new UnityEngine.Vector2(220f, 52f),
                "DetailLevel" => new UnityEngine.Vector2(220f, 34f),
                "DetailDescription" => new UnityEngine.Vector2(400f, 72f),
                "CurrentEffect" => new UnityEngine.Vector2(520f, 36f),
                "NextEffect" => new UnityEngine.Vector2(520f, 36f),
                "DetailCost" => new UnityEngine.Vector2(500f, 36f),
                "Balance" => new UnityEngine.Vector2(360f, 64f),
                "PermanentHint" => new UnityEngine.Vector2(780f, 60f),
                _ when elementName != null &&
                       elementName.StartsWith(
                           "GrowthNodeLabel",
                           System.StringComparison.Ordinal) =>
                    new UnityEngine.Vector2(26f, 28f),
                _ => UnityEngine.Vector2.zero,
            };
            if (minimum == UnityEngine.Vector2.zero)
                return;

            UnityEngine.Vector2 size = text.rectTransform.sizeDelta;
            if (UsesFixedGrid(elementName))
            {
                size = minimum;
            }
            else
            {
                size.x = UnityEngine.Mathf.Max(size.x, minimum.x);
                size.y = UnityEngine.Mathf.Max(size.y, minimum.y);
            }
            text.rectTransform.sizeDelta = size;

            UnityEngine.Vector2 position =
                text.rectTransform.anchoredPosition;
            switch (elementName)
            {
                case "Name":
                    position.x = UnityEngine.Mathf.Sign(position.x) * 132f;
                    position.y = 84f;
                    break;
                case "Level":
                    position.x = UnityEngine.Mathf.Sign(position.x) * 132f;
                    position.y = 37f;
                    break;
                case "Description":
                    position.x = UnityEngine.Mathf.Sign(position.x) * 132f;
                    position.y = -4f;
                    break;
                case "Effect":
                    position.x = UnityEngine.Mathf.Sign(position.x) * 132f;
                    position.y = -45f;
                    break;
                case "DetailName":
                    position = new UnityEngine.Vector2(-215f, 98f);
                    break;
                case "DetailLevel":
                    position = new UnityEngine.Vector2(-215f, 52f);
                    break;
                case "DetailDescription":
                    position = new UnityEngine.Vector2(110f, 92f);
                    break;
                case "CurrentEffect":
                    position = new UnityEngine.Vector2(-80f, 12f);
                    break;
                case "NextEffect":
                    position = new UnityEngine.Vector2(-80f, -31f);
                    break;
                case "DetailCost":
                    position = new UnityEngine.Vector2(-55f, -88f);
                    break;
            }
            text.rectTransform.anchoredPosition = position;

            if (elementName == "Name" ||
                elementName == "Level" ||
                elementName == "Description" ||
                elementName == "Effect")
            {
                text.alignment = UnityEngine.TextAnchor.MiddleLeft;
                text.horizontalOverflow =
                    UnityEngine.HorizontalWrapMode.Wrap;
                text.verticalOverflow =
                    UnityEngine.VerticalWrapMode.Truncate;
            }
            else if (elementName == "DetailName" ||
                     elementName == "DetailLevel" ||
                     elementName == "DetailDescription" ||
                     elementName == "CurrentEffect" ||
                     elementName == "NextEffect" ||
                     elementName == "DetailCost")
            {
                text.alignment = UnityEngine.TextAnchor.MiddleLeft;
                text.horizontalOverflow =
                    UnityEngine.HorizontalWrapMode.Wrap;
                text.verticalOverflow =
                    UnityEngine.VerticalWrapMode.Truncate;
            }

            ApplyCostLayout(text, elementName);
            ApplyNodeLabelLayout(text, elementName);
            ApplyDetailDecorationLayout(text, elementName);
        }

        static bool UsesFixedGrid(string elementName)
        {
            if (elementName == "Name" ||
                elementName == "Level" ||
                elementName == "Description" ||
                elementName == "Effect" ||
                elementName == "Cost" ||
                elementName == "DetailName" ||
                elementName == "DetailLevel" ||
                elementName == "DetailDescription" ||
                elementName == "CurrentEffect" ||
                elementName == "NextEffect" ||
                elementName == "DetailCost")
                return true;

            return elementName != null &&
                   elementName.StartsWith(
                       "GrowthNodeLabel",
                       System.StringComparison.Ordinal);
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
                _ when elementName != null &&
                       elementName.StartsWith(
                           "GrowthNodeLabel",
                           System.StringComparison.Ordinal) => 1f,
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

        static void ApplyContainerLayout(
            UnityEngine.UI.Text text,
            string elementName)
        {
            if (elementName == "Name")
            {
                var paper = text.rectTransform.parent as
                    UnityEngine.RectTransform;
                var outline = paper?.parent as UnityEngine.RectTransform;
                var card = outline?.parent as UnityEngine.RectTransform;
                if (paper != null &&
                    outline != null &&
                    card != null &&
                    card.name.StartsWith(
                        "PermanentGrowth",
                        System.StringComparison.Ordinal))
                {
                    card.sizeDelta = new UnityEngine.Vector2(456f, 248f);
                    outline.sizeDelta = new UnityEngine.Vector2(456f, 248f);
                    paper.sizeDelta = new UnityEngine.Vector2(440f, 232f);
                }
            }
            else if (elementName == "DetailName")
            {
                var panel = text.rectTransform.parent as
                    UnityEngine.RectTransform;
                if (panel != null &&
                    panel.name == "SelectedGrowthDetail")
                {
                    panel.sizeDelta = new UnityEngine.Vector2(920f, 300f);
                }
            }
        }

        static void ApplyCostLayout(
            UnityEngine.UI.Text text,
            string elementName)
        {
            if (elementName != "Cost") return;
            var brush = text.rectTransform.parent as
                UnityEngine.RectTransform;
            if (brush == null || brush.name != "CostBrush") return;

            float direction =
                UnityEngine.Mathf.Sign(brush.anchoredPosition.x);
            if (UnityEngine.Mathf.Approximately(direction, 0f))
                direction = 1f;
            brush.anchoredPosition =
                new UnityEngine.Vector2(direction * 132f, -91f);
            brush.sizeDelta = new UnityEngine.Vector2(132f, 38f);

            bool hasDrop = brush.Find("CostDrop") != null;
            text.rectTransform.anchoredPosition =
                new UnityEngine.Vector2(hasDrop ? 12f : 0f, 0f);
            text.rectTransform.sizeDelta =
                new UnityEngine.Vector2(hasDrop ? 96f : 116f, 32f);
            text.alignment = UnityEngine.TextAnchor.MiddleCenter;
            text.horizontalOverflow =
                UnityEngine.HorizontalWrapMode.Wrap;
            text.verticalOverflow =
                UnityEngine.VerticalWrapMode.Truncate;
        }

        static void ApplyNodeLabelLayout(
            UnityEngine.UI.Text text,
            string elementName)
        {
            const string prefix = "GrowthNodeLabel";
            if (elementName == null ||
                !elementName.StartsWith(
                    prefix,
                    System.StringComparison.Ordinal))
                return;

            int separator = elementName.IndexOf(
                '_',
                prefix.Length);
            if (separator < 0) return;
            if (!int.TryParse(
                    elementName.Substring(
                        prefix.Length,
                        separator - prefix.Length),
                    out int branch) ||
                !int.TryParse(
                    elementName.Substring(separator + 1),
                    out int node))
                return;

            float direction =
                UnityEngine.Mathf.Sign(
                    text.rectTransform.anchoredPosition.x);
            if (UnityEngine.Mathf.Approximately(direction, 0f))
                direction = branch % 2 == 1 ? -1f : 1f;
            float progress =
                UnityEngine.Mathf.InverseLerp(1f, 6f, node);
            float x = direction *
                      UnityEngine.Mathf.Lerp(52f, 174f, progress);

            UnityEngine.Vector2 labelPosition =
                text.rectTransform.anchoredPosition;
            labelPosition.x = x;
            text.rectTransform.anchoredPosition = labelPosition;
            text.rectTransform.sizeDelta =
                new UnityEngine.Vector2(26f, 28f);
            text.alignment = UnityEngine.TextAnchor.MiddleCenter;
            text.horizontalOverflow =
                UnityEngine.HorizontalWrapMode.Wrap;
            text.verticalOverflow =
                UnityEngine.VerticalWrapMode.Truncate;

            var nodeRect = text.transform.parent.Find(
                    $"GrowthNode{branch}_{node}") as
                UnityEngine.RectTransform;
            if (nodeRect == null) return;
            UnityEngine.Vector2 nodePosition = nodeRect.anchoredPosition;
            nodePosition.x = x;
            nodeRect.anchoredPosition = nodePosition;
            nodeRect.sizeDelta = new UnityEngine.Vector2(28f, 28f);
        }

        static void ApplyDetailDecorationLayout(
            UnityEngine.UI.Text text,
            string elementName)
        {
            if (elementName != "DetailCost") return;
            UnityEngine.Transform panel = text.transform.parent;
            SetRect(panel.Find("HeaderDivider"), 20f, 33f, 760f, 2f);
            SetRect(panel.Find("EffectDivider"), -80f, -10f, 520f, 2f);
            SetRect(panel.Find("ButtonDivider"), 205f, -62f, 88f, 3f);
            SetRect(panel.Find("DetailCostIcon"), -350f, -88f, 30f, 30f);
        }

        static void ApplyEnhanceButtonLayout(
            UnityEngine.UI.Text text,
            string elementName)
        {
            if (elementName != "Label" ||
                text.transform.parent == null ||
                text.transform.parent.name != "EnhanceButton")
                return;

            var button = text.transform.parent as UnityEngine.RectTransform;
            if (button == null) return;
            button.anchoredPosition = new UnityEngine.Vector2(325f, -62f);
            button.sizeDelta = new UnityEngine.Vector2(220f, 88f);
            text.rectTransform.anchoredPosition = UnityEngine.Vector2.zero;
            text.rectTransform.sizeDelta = new UnityEngine.Vector2(184f, 70f);
            text.alignment = UnityEngine.TextAnchor.MiddleCenter;
        }

        static void SetRect(
            UnityEngine.Transform target,
            float x,
            float y,
            float width,
            float height)
        {
            if (target is not UnityEngine.RectTransform rect) return;
            rect.anchoredPosition = new UnityEngine.Vector2(x, y);
            rect.sizeDelta = new UnityEngine.Vector2(width, height);
        }
    }
}
