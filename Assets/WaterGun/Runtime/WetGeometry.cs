using System;
using UnityEngine;
using Unity.Profiling;

namespace SurfaceWetness.WaterGun
{
    // Owned once per Renderer; all material slots and rays share this frame snapshot.
    public sealed class WetGeometry : IDisposable
    {
        public readonly Renderer Renderer;
        public readonly Mesh Source;
        public Mesh Mesh { get; private set; }
        public int RefreshCount { get; private set; }
        private readonly SkinnedMeshRenderer skin;
        private readonly bool ownsMesh;
        private readonly WetMeshBvh bvh;
        private readonly int[] triangleSlots;
        private int frame=-1;
        private static readonly ProfilerMarker BakeMarker=new ProfilerMarker("WaterGun.BakeMesh");
        private static readonly ProfilerMarker TraceMarker=new ProfilerMarker("WaterGun.Trace");
        private static readonly ProfilerMarker RefitMarker=new ProfilerMarker("WaterGun.BVHRefit");
        public WetGeometry(Renderer renderer)
        {
            Renderer=renderer;skin=renderer as SkinnedMeshRenderer;ownsMesh=skin!=null;
            Source=skin!=null ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if(Source==null || !Source.isReadable)throw new InvalidOperationException("Readable geometry required");
            for(int s=0;s<Source.subMeshCount;s++)
                if(Source.GetTopology(s)!=MeshTopology.Triangles)throw new InvalidOperationException("All submeshes must be triangles");
            try
            {
                if(skin!=null){Mesh=new Mesh{name="WaterGun baked geometry"};Mesh.MarkDynamic();skin.BakeMesh(Mesh,false);}
                else Mesh=Source;
                triangleSlots=new int[Mesh.triangles.Length/3];int cursor=0;
                for(int s=0;s<Mesh.subMeshCount;s++)for(int i=0;i<(int)Mesh.GetIndexCount(s)/3;i++)triangleSlots[cursor++]=s;
                if(cursor!=triangleSlots.Length)throw new InvalidOperationException("Triangle/submesh mapping mismatch");
                bvh=new WetMeshBvh(Mesh);
            }
            catch{Dispose();throw;}
        }
        public bool IsCurrent()
        {
            if(Renderer==null)return false;
            var current=skin!=null ? skin.sharedMesh : Renderer.GetComponent<MeshFilter>()?.sharedMesh;
            return current==Source && current!=null && current.vertexCount==Mesh.vertexCount && current.subMeshCount==Mesh.subMeshCount;
        }
        public bool Trace(Ray ray,float range,bool[] allowed,out WetRayHit hit)
        {
            hit=default(WetRayHit);
            if(Renderer==null || !Renderer.enabled || !Renderer.gameObject.activeInHierarchy)return false;
            if(frame!=Time.frameCount)
            {
                frame=Time.frameCount;
                if(skin!=null){using(BakeMarker.Auto())skin.BakeMesh(Mesh,false);using(RefitMarker.Auto())bvh.Refit(Mesh);}
                RefreshCount++;
            }
            using(TraceMarker.Auto())return bvh.Raycast(Renderer,ray,range,out hit,triangleSlots,allowed);
        }
        public int Slot(int triangle) => triangleSlots[triangle];
        public void Dispose(){if(ownsMesh && Mesh!=null)WetSurfaceState.Release(Mesh);Mesh=null;}
    }
}
