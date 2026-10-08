using UnityEngine;

namespace SurfaceWetness.WaterGun
{
    // Also marks internal renderers so the character catalog never offers them as surfaces.
    public sealed class WetTransparencyRenderer : MonoBehaviour
    {
        public Renderer Source,Proxy;
        public int SlotCount;
        public bool HasTransparentSlots;
        private MaterialPropertyBlock block;
        private void LateUpdate(){Synchronize();}
        public void Synchronize()
        {
            if(Source==null || Proxy==null)return;
            gameObject.layer=Source.gameObject.layer;
            Proxy.enabled=Source.enabled && HasTransparentSlots;Proxy.receiveShadows=Source.receiveShadows;
            if(!Proxy.enabled)return;
            Proxy.lightProbeUsage=Source.lightProbeUsage;Proxy.reflectionProbeUsage=Source.reflectionProbeUsage;
            Proxy.probeAnchor=Source.probeAnchor;Proxy.lightmapIndex=Source.lightmapIndex;
            Proxy.lightmapScaleOffset=Source.lightmapScaleOffset;
            var skin=Source as SkinnedMeshRenderer;
            var proxySkin=Proxy as SkinnedMeshRenderer;
            if(skin!=null && proxySkin!=null && skin.sharedMesh!=null)
            {
                proxySkin.localBounds=skin.localBounds;
                for(int i=0;i<skin.sharedMesh.blendShapeCount;i++)proxySkin.SetBlendShapeWeight(i,skin.GetBlendShapeWeight(i));
            }
            if(block==null)block=new MaterialPropertyBlock();
            Source.GetPropertyBlock(block);Proxy.SetPropertyBlock(block);
            for(int i=0;i<SlotCount;i++)
            {
                Source.GetPropertyBlock(block,i);Proxy.SetPropertyBlock(block,i);
            }
        }
    }
}
