using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurfaceWetness.WaterGun
{
    // One transparent renderer per source renderer, shared by all registered slots.
    // Meshes and bones are shared: this does not BakeMesh or duplicate geometry buffers.
    public sealed class WetRenderPair : IDisposable
    {
        private sealed class Group
        {
            public Renderer Source,Proxy;
            public GameObject Root;
            public Material Skip;
            public Material[] Materials;
            public bool[] Enabled;
            public WetTransparencyRenderer Sync;
            public int Users;
        }
        private static readonly Dictionary<Renderer,Group> groups=new Dictionary<Renderer,Group>();
        private Group group;
        private readonly int slot;
        private readonly Material opaque;
        private Material translucent;

        private WetRenderPair(Group group,int slot,Material opaque)
        {
            this.group=group;this.slot=slot;this.opaque=opaque;
            translucent=new Material(opaque){name="WaterGun translucent / "+slot};
            group.Materials[slot]=translucent;group.Proxy.sharedMaterials=group.Materials;group.Users++;
        }
        public static WetRenderPair Acquire(Renderer source,int slot,Material opaque)
        {
            Group group;
            if(!groups.TryGetValue(source,out group))
            {
                group=new Group{Source=source,Root=new GameObject("WaterGun transparency")};
                try
                {
                    group.Root.transform.SetParent(source.transform,false);
                    group.Root.layer=source.gameObject.layer;
                    var skin=source as SkinnedMeshRenderer;
                    if(skin!=null)
                    {
                        var proxy=group.Root.AddComponent<SkinnedMeshRenderer>();
                        proxy.sharedMesh=skin.sharedMesh;proxy.bones=skin.bones;proxy.rootBone=skin.rootBone;
                        proxy.localBounds=skin.localBounds;proxy.quality=skin.quality;proxy.updateWhenOffscreen=skin.updateWhenOffscreen;
                        group.Proxy=proxy;
                    }
                    else
                    {
                        group.Root.AddComponent<MeshFilter>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
                        group.Proxy=group.Root.AddComponent<MeshRenderer>();
                    }
                    group.Proxy.shadowCastingMode=ShadowCastingMode.Off;
                    group.Skip=new Material(opaque){name="WaterGun unused transparent slot"};
                    group.Skip.DisableKeyword("_WET_TRANSPARENT");group.Skip.DisableKeyword("_WET_OPAQUE_ONLY");
                    group.Skip.EnableKeyword("_WET_TRANSLUCENT_ONLY");group.Skip.renderQueue=3000;
                    group.Materials=new Material[source.sharedMaterials.Length];
                    group.Enabled=new bool[group.Materials.Length];
                    for(int i=0;i<group.Materials.Length;i++)group.Materials[i]=group.Skip;
                    group.Sync=group.Root.AddComponent<WetTransparencyRenderer>();group.Sync.Source=source;group.Sync.Proxy=group.Proxy;group.Sync.SlotCount=group.Materials.Length;
                    groups.Add(source,group);
                }
                catch
                {
                    if(group.Skip!=null)WetSurfaceState.Release(group.Skip);
                    WetSurfaceState.Release(group.Root);throw;
                }
            }
            return new WetRenderPair(group,slot,opaque);
        }
        public void Apply(bool enabled,int layerOrder)
        {
            opaque.EnableKeyword("_WET_OPAQUE_ONLY");opaque.DisableKeyword("_WET_TRANSLUCENT_ONLY");
            opaque.renderQueue=2000;opaque.SetOverrideTag("RenderType","Opaque");
            translucent.CopyPropertiesFromMaterial(opaque);
            translucent.DisableKeyword("_WET_OPAQUE_ONLY");translucent.EnableKeyword("_WET_TRANSLUCENT_ONLY");
            translucent.renderQueue=3000-Mathf.Clamp(layerOrder,0,499);
            translucent.SetOverrideTag("RenderType","Transparent");
            group.Enabled[slot]=enabled;UpdateVisibility();
        }
        public void Dispose()
        {
            if(group==null)return;
            group.Materials[slot]=group.Skip;
            group.Enabled[slot]=false;
            if(group.Proxy!=null)group.Proxy.sharedMaterials=group.Materials;
            if(translucent!=null)WetSurfaceState.Release(translucent);translucent=null;
            if(--group.Users==0)
            {
                groups.Remove(group.Source);
                if(group.Root!=null)WetSurfaceState.Release(group.Root);
                if(group.Skip!=null)WetSurfaceState.Release(group.Skip);
            }
            else UpdateVisibility();
            group=null;
        }
        private void UpdateVisibility()
        {
            if(group.Sync==null)return;
            bool enabled=false;foreach(bool value in group.Enabled)enabled|=value;
            group.Sync.HasTransparentSlots=enabled;group.Sync.Synchronize();
        }
    }
}
