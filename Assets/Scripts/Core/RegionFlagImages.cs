using System.Collections.Generic;
using UnityEngine;

namespace MukJump.Core
{
    public static class RegionFlagImages
    {
        static readonly Dictionary<string, Sprite> sprites = new();
        public static Sprite Get(string region)
        {
            string path = DeviceRegion.IconResource(region);
            if (sprites.TryGetValue(path, out var sprite) && sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>(path);
            if (texture == null) texture = Resources.Load<Texture2D>(DeviceRegion.IconResource(null));
            if (texture == null) return null;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(.5f, .5f), 100f);
            sprite.name = "RegionFlag-" + DeviceRegion.Normalize(region);
            sprites[path] = sprite;
            return sprite;
        }
    }
}
