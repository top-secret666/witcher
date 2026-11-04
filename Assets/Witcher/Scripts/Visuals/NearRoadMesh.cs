using UnityEngine;
using UnityEngine.UI;

namespace Witcher.Visuals
{
    /// <summary>
    /// UI graphic that draws the near road as a perspective strip mesh built by <see cref="NearRoadLayer"/>.
    /// Uses the road texture directly on the GPU (mipmaps, trilinear, anisotropic, V-repeat), so the
    /// road scrolls smoothly without the shimmer of the old CPU point-sampled image.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class NearRoadMesh : MaskableGraphic
    {
        public Texture texture;
        public NearRoadLayer owner;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            if (owner == null) { vh.Clear(); return; }
            owner.FillRoadMesh(vh, rectTransform.rect, color);
        }
    }
}
