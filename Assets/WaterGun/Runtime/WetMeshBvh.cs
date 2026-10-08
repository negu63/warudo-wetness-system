namespace SurfaceWetness.WaterGun {
using System.Collections.Generic;
using UnityEngine;

public struct WetRayHit
{
    public Renderer renderer;
    public Vector3 point, normal;
    public Vector2 textureCoord;
    public float distance;
    public int triangleIndex;
}

// Fixed topology: build once, refit bounds after skinning. Original triangles and UVs remain intact.
public sealed class WetMeshBvh
{
    public const int DefaultLeafTriangleCapacity=64;
    private static readonly Unity.Profiling.ProfilerMarker ReadMarker=new Unity.Profiling.ProfilerMarker("WaterGun.BVHReadVertices");
    private static readonly Unity.Profiling.ProfilerMarker LeafMarker=new Unity.Profiling.ProfilerMarker("WaterGun.BVHLeafBounds");
    private static readonly Unity.Profiling.ProfilerMarker ParentMarker=new Unity.Profiling.ProfilerMarker("WaterGun.BVHParentBounds");
    private struct Node { public Vector3 min,max; public int left,right,start,count; }
    private readonly List<Vector3> vertices;
    private readonly Vector3[] vertexCache;
    private readonly Vector2[] uv;
    private readonly int[] triangles, order, stack=new int[128];
    private readonly Node[] nodes;
    private readonly int[] leaves, parents;
    private readonly int[] leafStarts,leafCounts,leafVertices;
    public bool UseCompactLeafBounds=true;
    public int OriginalLeafVertexVisits => triangles.Length;
    public int CompactLeafVertexVisits => leafVertices.Length;
    public int LeafTriangleCapacity { get; private set; }
    public int NodeCount => nodes.Length;
    public WetMeshBvh(Mesh mesh,int leafTriangleCapacity=DefaultLeafTriangleCapacity)
    {
        if(leafTriangleCapacity<1 || leafTriangleCapacity>128)throw new System.ArgumentOutOfRangeException(nameof(leafTriangleCapacity));
        LeafTriangleCapacity=leafTriangleCapacity;
        vertices=new List<Vector3>(mesh.vertexCount);mesh.GetVertices(vertices);
        vertexCache=new Vector3[vertices.Count];vertices.CopyTo(vertexCache);
        uv=mesh.uv;if(uv.Length!=mesh.vertexCount)uv=new Vector2[mesh.vertexCount];
        triangles=mesh.triangles;order=new int[triangles.Length/3];
        for(int i=0;i<order.Length;i++)order[i]=i;
        var building=new List<Node>();Build(building,0,order.Length);nodes=building.ToArray();
        var leafList=new List<int>();var parentList=new List<int>();
        for(int i=nodes.Length-1;i>=0;i--)
            if(nodes[i].count>0)leafList.Add(i);else if(nodes[i].left>=0)parentList.Add(i);
        leaves=leafList.ToArray();parents=parentList.ToArray();
        leafStarts=new int[leaves.Length];leafCounts=new int[leaves.Length];
        var compact=new List<int>();var scratch=new int[leafTriangleCapacity*3];
        for(int leaf=0;leaf<leaves.Length;leaf++)
        {
            var node=nodes[leaves[leaf]];int count=0;
            leafStarts[leaf]=compact.Count;
            for(int i=node.start;i<node.start+node.count;i++)
            for(int corner=0;corner<3;corner++)
            {
                int vertex=triangles[order[i]*3+corner];bool exists=false;
                for(int j=0;j<count;j++)if(scratch[j]==vertex){exists=true;break;}
                if(!exists){scratch[count++]=vertex;compact.Add(vertex);}
            }
            leafCounts[leaf]=count;
        }
        leafVertices=compact.ToArray();
    }
    private Vector3 Center(int triangle)
    {
        int i=triangle*3;return (vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3;
    }
    private int Build(List<Node> building,int start,int count)
    {
        int index=building.Count;building.Add(default(Node));
        var node=new Node{start=start,count=count,left=-1,right=-1};BoundsFor(ref node);
        if(count>LeafTriangleCapacity)
        {
            Vector3 min=Center(order[start]),max=min;
            for(int i=start+1;i<start+count;i++){var c=Center(order[i]);min=Vector3.Min(min,c);max=Vector3.Max(max,c);}
            var size=max-min;int axis=size.x>size.y ? (size.x>size.z?0:2) : (size.y>size.z?1:2);
            float middle=(min[axis]+max[axis])*.5f;int split=start;
            for(int i=start;i<start+count;i++)if(Center(order[i])[axis]<middle){int value=order[split];order[split++]=order[i];order[i]=value;}
            // Bound depth even for degenerate or highly asymmetric triangle layouts.
            if(split-start<count/5 || start+count-split<count/5)split=start+count/2;
            node.left=Build(building,start,split-start);node.right=Build(building,split,start+count-split);node.count=0;
        }
        building[index]=node;return index;
    }
    private void BoundsFor(ref Node node)
    {
        float minX=float.PositiveInfinity,minY=minX,minZ=minX;
        float maxX=float.NegativeInfinity,maxY=maxX,maxZ=maxX;
        for(int i=node.start;i<node.start+node.count;i++)
        {
            int t=order[i]*3;
            for(int j=0;j<3;j++)
            {
                var p=vertices[triangles[t+j]];
                if(p.x<minX)minX=p.x;if(p.y<minY)minY=p.y;if(p.z<minZ)minZ=p.z;
                if(p.x>maxX)maxX=p.x;if(p.y>maxY)maxY=p.y;if(p.z>maxZ)maxZ=p.z;
            }
        }
        node.min=new Vector3(minX,minY,minZ);node.max=new Vector3(maxX,maxY,maxZ);
    }
    public void Refit(Mesh mesh)
    {
        if(mesh.vertexCount!=vertices.Count)throw new System.InvalidOperationException("Wet mesh topology changed; rebuild BVH.");
        if(order.Length==0)return;
        using(ReadMarker.Auto())
        {
            mesh.GetVertices(vertices);
            if(UseCompactLeafBounds)vertices.CopyTo(vertexCache);
        }
        using(LeafMarker.Auto())
        for(int leaf=0;leaf<leaves.Length;leaf++)
        {
            int i=leaves[leaf];var node=nodes[i];
            if(UseCompactLeafBounds)CompactBoundsFor(ref node,leaf);else BoundsFor(ref node);
            nodes[i]=node;
        }
        using(ParentMarker.Auto())
        foreach(int i in parents)
        {
            var node=nodes[i];
            {
                var a=nodes[node.left];var b=nodes[node.right];
                node.min=new Vector3(a.min.x<b.min.x?a.min.x:b.min.x,a.min.y<b.min.y?a.min.y:b.min.y,a.min.z<b.min.z?a.min.z:b.min.z);
                node.max=new Vector3(a.max.x>b.max.x?a.max.x:b.max.x,a.max.y>b.max.y?a.max.y:b.max.y,a.max.z>b.max.z?a.max.z:b.max.z);
            }
            nodes[i]=node;
        }
    }
    private void CompactBoundsFor(ref Node node,int leaf)
    {
        int start=leafStarts[leaf],end=start+leafCounts[leaf];
        var first=vertexCache[leafVertices[start]];
        float minX=first.x,minY=first.y,minZ=first.z,maxX=minX,maxY=minY,maxZ=minZ;
        for(int i=start+1;i<end;i++)
        {
            var p=vertexCache[leafVertices[i]];
            if(p.x<minX)minX=p.x;if(p.y<minY)minY=p.y;if(p.z<minZ)minZ=p.z;
            if(p.x>maxX)maxX=p.x;if(p.y>maxY)maxY=p.y;if(p.z>maxZ)maxZ=p.z;
        }
        node.min=new Vector3(minX,minY,minZ);node.max=new Vector3(maxX,maxY,maxZ);
    }
#if UNITY_EDITOR
    // Outside measured frames: verify deduplication changes neither leaf nor parent bounds.
    public int VerifyCompactBounds(Mesh mesh)
    {
        bool original=UseCompactLeafBounds;
        try
        {
            UseCompactLeafBounds=false;Refit(mesh);var reference=(Node[])nodes.Clone();
            UseCompactLeafBounds=true;Refit(mesh);int mismatch=0;
            for(int i=0;i<nodes.Length;i++)
                if(!nodes[i].min.Equals(reference[i].min)||!nodes[i].max.Equals(reference[i].max))mismatch++;
            return mismatch;
        }
        finally{UseCompactLeafBounds=original;}
    }
#endif
    private static bool Intersects(Node node,Vector3 origin,Vector3 direction,float distance)
    {
        float near=0,far=distance;
        for(int axis=0;axis<3;axis++)
        {
            float d=direction[axis];
            if(Mathf.Abs(d)<1e-12f){if(origin[axis]<node.min[axis]||origin[axis]>node.max[axis])return false;continue;}
            float a=(node.min[axis]-origin[axis])/d,b=(node.max[axis]-origin[axis])/d;
            if(a>b){float swap=a;a=b;b=swap;}
            near=Mathf.Max(near,a);far=Mathf.Min(far,b);if(near>far)return false;
        }
        return true;
    }
    public bool Raycast(Renderer renderer,Ray ray,float maxDistance,out WetRayHit hit, int[] triangleSlots=null, bool[] allowedSlots=null)
    {
        hit=default(WetRayHit);if(order.Length==0)return false;
        var inverse=renderer.transform.worldToLocalMatrix;
        var origin=inverse.MultiplyPoint3x4(ray.origin);var direction=inverse.MultiplyVector(ray.direction);
        int pending=1;stack[0]=0;float closest=maxDistance;bool found=false;
        while(pending>0)
        {
            var node=nodes[stack[--pending]];if(!Intersects(node,origin,direction,closest))continue;
            if(node.count==0){stack[pending++]=node.right;stack[pending++]=node.left;continue;}
            for(int i=node.start;i<node.start+node.count;i++)
            {
                int triangle=order[i],t=triangle*3;int ia=triangles[t],ib=triangles[t+1],ic=triangles[t+2];
                if(allowedSlots!=null && !allowedSlots[triangleSlots[triangle]])continue;
                var a=vertices[ia];var e1=vertices[ib]-a;var e2=vertices[ic]-a;
                var p=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,p);
                if(Mathf.Abs(det)<1e-12f)continue;
                float invDet=1/det;var offset=origin-a;float u=Vector3.Dot(offset,p)*invDet;
                if(u<-.000001f||u>1.000001f)continue;
                var q=Vector3.Cross(offset,e1);float v=Vector3.Dot(direction,q)*invDet;
                if(v<-.000001f||u+v>1.000001f)continue;
                float distance=Vector3.Dot(e2,q)*invDet;if(distance<0||distance>closest)continue;
                // Vector3.normalized drops vectors below 1e-5, including valid tiny triangles.
                var normal=inverse.transpose.MultiplyVector(Vector3.Cross(e1,e2));
                float normalLength=normal.magnitude;if(normalLength<=0)continue;
                normal/=normalLength;
                if(Vector3.Dot(normal,ray.direction)>=-.00001f)continue;
                closest=distance;found=true;
                hit=new WetRayHit{renderer=renderer,point=ray.GetPoint(distance),normal=normal,distance=distance,
                    triangleIndex=triangle,textureCoord=uv[ia]*(1-u-v)+uv[ib]*u+uv[ic]*v};
            }
        }
        return found;
    }
}

}
