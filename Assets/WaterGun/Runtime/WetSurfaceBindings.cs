using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurfaceWetness.WaterGun
{
    public sealed class WetSurfaceBindings : IDisposable
    {
        private static readonly Dictionary<GameObject,WetSurfaceBindings> owners=new Dictionary<GameObject,WetSurfaceBindings>();
        private GameObject ownedRoot;
        // Keep rejected registrations too, so repaired support can invalidate an empty binding.
        private readonly List<WetCharacterCatalog.Entry> registrations=new List<WetCharacterCatalog.Entry>();
        private readonly List<string> registrationKeys=new List<string>();
        public sealed class Surface
        {
            public WetSurfaceProfile Profile;
            public WetSingleSurface Binding;
            public WetCharacterCatalog.Entry Entry;
            public WetGeometry Geometry;
            public bool[] Allowed;
        }
        public struct PathHit { public Surface Surface;public WetRayHit Hit; }
        private sealed class HitComparer : IComparer<PathHit>
        {
            public int Compare(PathHit a,PathHit b)
            {
                int result=a.Hit.distance.CompareTo(b.Hit.distance);
                return result!=0 ? result : StringComparer.Ordinal.Compare(a.Surface.Entry.Key,b.Surface.Entry.Key);
            }
        }
        private static readonly HitComparer comparer=new HitComparer();
        private PathHit[] candidates;
        public PathHit[] Path { get; private set; }
        public int PathCount { get; private set; }
        private sealed class GeometryGroup
        {
            public WetGeometry Geometry;
            public bool[] Allowed;
            public readonly Dictionary<int,Surface> Slots=new Dictionary<int,Surface>();
        }
        public readonly List<Surface> Surfaces=new List<Surface>();
        private readonly Dictionary<Renderer,GeometryGroup> groups=new Dictionary<Renderer,GeometryGroup>();
        public string Report { get; private set; }
        public WetSurfaceBindings(WetCharacterCatalog.Snapshot catalog,WetSurfaceProfile[] profiles,Material update,Material coverage)
        {
            var entries=new Dictionary<string,WetCharacterCatalog.Entry>();foreach(var e in catalog.Entries)entries[e.Key]=e;
            var report=new System.Text.StringBuilder();var keys=new HashSet<string>();
            try
            {
                if(catalog.Root!=null)
                {
                    if(owners.ContainsKey(catalog.Root))throw new InvalidOperationException("이 캐릭터에는 다른 WaterGun이 이미 연결되어 있습니다");
                    owners.Add(catalog.Root,this);ownedRoot=catalog.Root;
                }
                foreach(var p in profiles)
                {
                    if(p==null || (!p.Enabled && !p.BlockOnly))continue;
                    WetCharacterCatalog.Entry entry;
                    entry=null;
                    if(!string.IsNullOrEmpty(p.Key))entries.TryGetValue(p.Key,out entry);
                    registrationKeys.Add(p.Key);registrations.Add(entry);
                    if(entry==null){report.AppendLine("미연결: "+p.Key);continue;}
                    if(!keys.Add(p.Key)){report.AppendLine("중복 표면: "+p.Key);continue;}
                    var failure=p.BlockOnly ? WetCharacterCatalog.CheckGeometry(entry) : entry.Support!=WetShaderAdapter.Supported ? entry.Support : null;
                    if(failure!=null){report.AppendLine(p.Key+": "+failure);continue;}
                    GeometryGroup group;
                    if(!groups.TryGetValue(entry.Renderer,out group))
                    {
                        group=new GeometryGroup{Geometry=new WetGeometry(entry.Renderer),Allowed=new bool[entry.SubMeshCount]};
                        groups.Add(entry.Renderer,group);
                    }
                    var surface=new Surface{Profile=p,Entry=entry,Geometry=group.Geometry,Allowed=new bool[entry.SubMeshCount]};
                    surface.Allowed[entry.Slot]=true;
                    if(!p.BlockOnly)surface.Binding=new WetSingleSurface(entry,update,coverage,512,group.Geometry);
                    group.Slots.Add(entry.Slot,surface);group.Allowed[entry.Slot]=true;Surfaces.Add(surface);
                }
                Report="연결 "+Surfaces.Count+" 표면 / geometry "+groups.Count+"\n"+report;
                candidates=new PathHit[Surfaces.Count];Path=new PathHit[Surfaces.Count];
            }
            catch{Dispose();throw;}
        }
        public bool IsCurrent()
        {
            foreach(var s in Surfaces)
            {
                if(s.Binding!=null ? !s.Binding.IsCurrent() : !s.Geometry.IsCurrent())return false;
                if(s.Entry.Renderer.sharedMaterials.Length<=s.Entry.Slot)return false;
            }
            return true;
        }
        public bool IsCurrent(WetCharacterCatalog.Snapshot catalog)
        {
            if(catalog==null || catalog.Root!=ownedRoot)return false;
            for(int i=0;i<registrationKeys.Count;i++)
            {
                WetCharacterCatalog.Entry current=null;
                foreach(var entry in catalog.Entries)if(entry.Key==registrationKeys[i]){current=entry;break;}
                var previous=registrations[i];
                if(previous==null || current==null){if(previous!=current)return false;continue;}
                if(previous.Renderer!=current.Renderer || previous.Support!=current.Support ||
                    previous.HasUv0!=current.HasUv0 || previous.Readable!=current.Readable ||
                    previous.VertexCount!=current.VertexCount || previous.SubMeshCount!=current.SubMeshCount)return false;
            }
            return IsCurrent();
        }
        public void Update(float delta,bool dry)
        {
            foreach(var s in Surfaces)
            {
                if(s.Binding==null)continue;
                var p=s.Profile;if(dry && p.Drying)s.Binding.State.Dry(delta,p.DrySeconds);
                s.Binding.Apply(p.Darkening,p.Smoothness,p.Opacity,p.WetOpacity,p.Order);
            }
        }
        public bool Trace(Ray ray,float range,out WetRayHit hit,out Surface selected)
        {
            hit=default(WetRayHit);selected=null;float closest=range;
            foreach(var group in groups.Values)
            {
                WetRayHit candidate;
                if(group.Geometry.Trace(ray,closest,group.Allowed,out candidate))
                {closest=candidate.distance;hit=candidate;selected=group.Slots[group.Geometry.Slot(candidate.triangleIndex)];}
            }
            return selected!=null;
        }
        public int TracePath(Ray ray,float range,float depth,bool transfer)
        {
            PathCount=0;int count=0;
            foreach(var s in Surfaces)
            {
                WetRayHit hit;
                if(s.Geometry.Trace(ray,range,s.Allowed,out hit))candidates[count++]=new PathHit{Surface=s,Hit=hit};
            }
            if(count==0)return 0;
            Array.Sort(candidates,0,count,comparer);
            Path[PathCount++]=candidates[0];var current=candidates[0];
            if(!transfer)return PathCount;
            for(int i=1;i<count;i++)
            {
                var p=current.Surface.Profile;
                if(p.BlockOnly || p.StopAfterWet || !p.Transmit)break;
                var next=candidates[i];var np=next.Surface.Profile;
                if(next.Hit.distance-candidates[0].Hit.distance>Mathf.Max(0,depth))break;
                // Stops are physical barriers even if a pose inverts configured layer order.
                if(!np.BlockOnly && !np.StopAfterWet && np.Order<p.Order)continue;
                Path[PathCount++]=next;current=next;
            }
            return PathCount;
        }
        public void SupplyPath(Vector3 direction,float radius,float hardness,float amount)
        {
            for(int i=0;i<PathCount;i++)
            {
                if(amount<=0)break;
                var item=Path[i];
                if(item.Surface.Binding!=null)item.Surface.Binding.Deposit(item.Hit,direction,radius,hardness,amount);
                amount*=Mathf.Clamp01(item.Surface.Profile.TransferMultiplier);
            }
        }
        public int GeometryCount => groups.Count;
        public void ClearPath(){PathCount=0;}
        public void Dispose()
        {
            foreach(var s in Surfaces)s.Binding?.Dispose();Surfaces.Clear();PathCount=0;
            foreach(var g in groups.Values)g.Geometry.Dispose();groups.Clear();
            if(!ReferenceEquals(ownedRoot,null)){owners.Remove(ownedRoot);ownedRoot=null;}
        }
    }
}
