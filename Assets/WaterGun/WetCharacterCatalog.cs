using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace SurfaceWetness.WaterGun
{
    // Read-only discovery. Does not bake, modify materials, layers, bones or colliders.
    public static class WetCharacterCatalog
    {
        public sealed class Entry
        {
            public Renderer Renderer;
            public string Key, Path, DisplayName;
            public int Slot, SubMeshCount, VertexCount;
            public Material Material;
            public bool HasUv0, Readable;
            public string Support;
        }

        public sealed class Snapshot
        {
            public GameObject Root;
            public readonly List<Entry> Entries = new List<Entry>();
            public int RendererCount, CandidateCount;
            public string Report;
        }

        public static Snapshot Scan(GameObject root)
        {
            var result = new Snapshot{Root=root};
            if (root == null) { result.Report = "캐릭터 모델이 아직 없습니다"; return result; }
            var renderers = new List<Renderer>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                if ((renderer is SkinnedMeshRenderer || renderer is MeshRenderer) && renderer.GetComponent<WetTransparencyRenderer>()==null) renderers.Add(renderer);
            renderers.Sort((a,b) => StringComparer.Ordinal.Compare(Path(root.transform,a.transform),Path(root.transform,b.transform)));
            var report = new StringBuilder();
            foreach (var renderer in renderers)
            {
                result.RendererCount++;
                var skin = renderer as SkinnedMeshRenderer;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
                string path = Path(root.transform,renderer.transform);
                var materials = renderer.sharedMaterials;
                bool uv = mesh != null && mesh.HasVertexAttribute(VertexAttribute.TexCoord0);
                report.Append(renderer.name).Append(" [").Append(skin != null ? "Skinned" : "Static")
                    .Append("] 정점 ").Append(mesh != null ? mesh.vertexCount : 0)
                    .Append(" / submesh ").Append(mesh != null ? mesh.subMeshCount : 0)
                    .Append(" / ").Append(renderer.enabled && renderer.gameObject.activeInHierarchy ? "표시 중" : "숨김")
                    .AppendLine();
                if (materials.Length == 0) report.AppendLine("  재질 슬롯 없음");
                for (int slot=0;slot<materials.Length;slot++)
                {
                    var material = materials[slot];
                    string support = CheckSupport(mesh,uv,slot,material);
                    var entry = new Entry { Renderer=renderer, Path=path, Key=path+"#slot="+slot,
                        Slot=slot, Material=material, SubMeshCount=mesh != null ? mesh.subMeshCount : 0,
                        VertexCount=mesh != null ? mesh.vertexCount : 0, HasUv0=uv,
                        Readable=mesh != null && mesh.isReadable, Support=support };
                    result.Entries.Add(entry);
                    if (support == WetShaderAdapter.Supported) result.CandidateCount++;
                    report.Append("  #").Append(result.Entries.Count-1).Append(" 슬롯 ").Append(slot).Append(": ")
                        .Append(MaterialName(material)).Append(" | ")
                        .Append(material != null && material.shader != null ? material.shader.name : "셰이더 없음")
                        .Append(" | ").Append(support).AppendLine();
                }
            }
            var labelCounts=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var entry in result.Entries)
            {
                entry.DisplayName=entry.Renderer.name+" / "+MaterialName(entry.Material);
                if(entry.Renderer.sharedMaterials.Length>1)entry.DisplayName+=" (슬롯 "+entry.Slot+")";
                int count;labelCounts.TryGetValue(entry.DisplayName,out count);labelCounts[entry.DisplayName]=count+1;
            }
            foreach(var entry in result.Entries)
                if(labelCounts[entry.DisplayName]>1)entry.DisplayName+=" · "+Uri.UnescapeDataString(entry.Path);
            result.Report = report.Length == 0 ? "캐릭터 아래에 MeshRenderer/SkinnedMeshRenderer가 없습니다" : report.ToString();
            return result;
        }

        private static string MaterialName(Material material)=>material!=null
            ? material.GetTag("SurfaceWetnessSourceMaterialName",false,material.name) : "머티리얼 없음";

        public static string CheckGeometry(Entry entry)
        {
            var skin=entry.Renderer as SkinnedMeshRenderer;
            var mesh=skin!=null ? skin.sharedMesh : entry.Renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if(mesh==null)return "메쉬 없음";
            if(!mesh.isReadable)return "CPU 메쉬 읽기 불가";
            if(entry.Slot>=mesh.subMeshCount)return "submesh보다 많은 슬롯";
            for(int s=0;s<mesh.subMeshCount;s++)if(mesh.GetTopology(s)!=MeshTopology.Triangles)return "삼각형 topology 아님";
            return null;
        }
        private static string CheckSupport(Mesh mesh,bool uv,int slot,Material material)
        {
            if (mesh == null) return "메쉬 없음";
            if (!uv) return "UV0 없음";
            if (slot >= mesh.subMeshCount) return "submesh보다 많은 슬롯 · 연결 미지원";
            if (mesh.GetTopology(slot) != MeshTopology.Triangles) return "삼각형 topology 아님";
            for(int s=0;s<mesh.subMeshCount;s++)
                if(mesh.GetTopology(s)!=MeshTopology.Triangles)return "다른 submesh의 비삼각형 topology · 연결 미지원";
            if (!mesh.isReadable) return "CPU 메쉬 읽기 불가 · 연결 전 준비 필요";
            return WetShaderAdapter.Check(material);
        }

        public static string Path(Transform root,Transform target)
        {
            if (target == root) return ".";
            var parts = new List<string>();
            for (var node=target;node != null && node != root;node=node.parent)
            {
                // Index among siblings of the same name; unaffected by unrelated sibling insertion.
                int ordinal=0;
                if (node.parent != null)
                    for (int i=0;i<node.GetSiblingIndex();i++)
                        if (node.parent.GetChild(i).name == node.name) ordinal++;
                parts.Add(Uri.EscapeDataString(node.name)+"["+ordinal+"]");
            }
            parts.Reverse();return string.Join("/",parts.ToArray());
        }
    }
}
