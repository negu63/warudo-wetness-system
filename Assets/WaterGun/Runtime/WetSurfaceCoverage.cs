namespace SurfaceWetness.WaterGun {
using UnityEngine;

// Evaluate a world-space brush while rasterizing every surface triangle in UV0.
public static class WetSurfaceCoverage
{
    private static readonly Unity.Profiling.ProfilerMarker Marker=new Unity.Profiling.ProfilerMarker("WaterGun.Brush");
    public static void Render(Mesh mesh, Material material, RenderTexture target, Vector3 incoming,
        Matrix4x4 localToWorld, Vector3 center, float radius, float hardness, int submesh)
    {
        using(var scope=Marker.Auto())
        {
        var previous=RenderTexture.active;
        try
        {
            Graphics.SetRenderTarget(target);
            GL.Clear(false,true,Color.clear);
            material.SetVector("_LocalIncoming",new Vector4(incoming.x,incoming.y,incoming.z,0));
            material.SetMatrix("_SurfaceLocalToWorld",localToWorld);
            material.SetVector("_BrushWorldCenter",new Vector4(center.x,center.y,center.z,0));
            material.SetFloat("_BrushWorldRadius",Mathf.Max(0.001f,radius));
            material.SetFloat("_BrushHardness",Mathf.Clamp01(hardness));
            material.SetPass(0);
            Graphics.DrawMeshNow(mesh,Matrix4x4.identity,submesh);
        }
        finally { RenderTexture.active=previous; }
        }
    }
}

}
