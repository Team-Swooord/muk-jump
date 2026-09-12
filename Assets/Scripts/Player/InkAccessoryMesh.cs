using MukJump.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MukJump.Player
{
    /// 장식 한 장만 재사용하는 12×12 망. 원본 Sprite·Material·몸체를 변경하지 않는다.
    [DisallowMultipleComponent]
    public sealed class InkAccessoryMesh : MonoBehaviour
    {
        Mesh mesh;
        MeshRenderer view;
        Material material;
        MaterialPropertyBlock properties;
        Sprite source;
        Vector3[] vertices;
        float sampledTime = -1f, sampledStrength = -1f;
        InkSkinId displayed = (InkSkinId)(-1);
        static readonly int TextureId = Shader.PropertyToID("_MainTex"), ColorId = Shader.PropertyToID("_Color");
        public Mesh Mesh => mesh;
        public MeshRenderer Renderer => view;
        public void Hide() { if (view != null) view.enabled = false; }

        public bool Present(SpriteRenderer original, InkSkinId id, float time, float strength)
        {
            if (original == null || original.sprite == null || !InkAccessoryMotion.Supports(id)) { Hide(); return false; }
            if (view == null) Initialize();
            if (material == null) return false; // 그림자만 남지 않도록 원래 Sprite로 폴백한다.
            bool changed = source != original.sprite || displayed != id;
            if (changed)
            {
                source = original.sprite; displayed = id;
                var uv = new Vector2[vertices.Length]; Rect rect = source.textureRect;
                int n = InkAccessoryMotion.Segments;
                for (int y=0; y<=n; y++) for(int x=0; x<=n; x++)
                    uv[y*(n+1)+x] = new Vector2((rect.x+(float)x/n*rect.width)/source.texture.width,
                        (rect.y+(float)y/n*rect.height)/source.texture.height);
                mesh.uv = uv;
            }
            time = InkAccessoryMotion.SampleTime(time);
            if (changed || time != sampledTime || strength != sampledStrength)
            {
                sampledTime = time; sampledStrength = strength;
                int n = InkAccessoryMotion.Segments;
                for (int y=0; y<=n; y++) for(int x=0; x<=n; x++)
                {
                    Vector2 p = InkAccessoryMotion.Deform(id, new Vector2((float)x/n,(float)y/n), time, strength);
                    vertices[y*(n+1)+x] = (Vector2.Scale(p, source.rect.size) - source.pivot) / source.pixelsPerUnit;
                }
                mesh.vertices = vertices; mesh.RecalculateBounds();
            }
            transform.localScale = new Vector3(original.flipX ? -1 : 1, original.flipY ? -1 : 1, 1);
            view.sortingLayerID = original.sortingLayerID; view.sortingOrder = original.sortingOrder;
            properties.SetTexture(TextureId, source.texture); properties.SetColor(ColorId, original.color);
            view.SetPropertyBlock(properties); view.enabled = original.enabled;
            return true;
        }

        void Initialize()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            view = GetComponent<MeshRenderer>();
            if (view == null) view = gameObject.AddComponent<MeshRenderer>();
            view.shadowCastingMode = ShadowCastingMode.Off; view.receiveShadows = false;
            view.lightProbeUsage = LightProbeUsage.Off; view.reflectionProbeUsage = ReflectionProbeUsage.Off;
            // 재컴파일로 관리 필드가 비어도 이 객체가 소유한 망·재질은 다시 연결한다.
            mesh = filter.sharedMesh;
            string ownedName = "InkAccessoryMotion_" + GetEntityId();
            if (mesh == null || mesh.name != ownedName) mesh = new Mesh { name = ownedName, hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic(); filter.sharedMesh = mesh;
            material = view.sharedMaterial;
            if (material == null || material.name != ownedName)
            {
                var shader = Resources.Load<Shader>("MukJump/Shaders/InkAccessory");
                material = shader == null ? null : new Material(shader) { name = ownedName, hideFlags = HideFlags.DontSave };
            }
            view.sharedMaterial = material; properties = new MaterialPropertyBlock();
            int n = InkAccessoryMotion.Segments;
            vertices = new Vector3[(n+1)*(n+1)]; var triangles = new int[n*n*6];
            mesh.vertices = vertices;
            for (int y=0, j=0; y<n; y++) for(int x=0; x<n; x++)
            {
                int i = y*(n+1)+x;
                triangles[j++]=i; triangles[j++]=i+n+1; triangles[j++]=i+1;
                triangles[j++]=i+1; triangles[j++]=i+n+1; triangles[j++]=i+n+2;
            }
            mesh.triangles = triangles;
        }
        void OnDisable() => Hide();
        void OnDestroy()
        {
            if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
