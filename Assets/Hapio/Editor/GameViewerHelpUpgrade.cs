using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hapio.Editor
{
    /// <summary>Capture authored scenery only. No gameplay object or board backing enters the viewer.</summary>
    public static class GameViewerHelpUpgrade
    {
        [MenuItem("Hapio/UI/Rebuild Sealed Gourd Backgrounds")]
        public static void BakeBackgrounds()
        {
            var bindings=Object.FindFirstObjectByType<GameBattleBindings>();
            var stages=Object.FindFirstObjectByType<GameStageBackgrounds>();
            if(Application.isPlaying||bindings==null||stages==null)
                throw new System.InvalidOperationException("Open the saved Battle scene outside Play mode first.");
            const string folder="Assets/Hapio/Art/UI/SealedGourd";
            Directory.CreateDirectory(folder);
            var textures=new Texture2D[3];
            for(int stage=1;stage<=3;stage++)
            {
                var set=stages.Resolve(stage);
                var root=new GameObject("Temporary viewer scenery");
                var cameraObject=new GameObject("Temporary viewer camera");
                var camera=cameraObject.AddComponent<Camera>();
                var rt=new RenderTexture(804,1748,24,RenderTextureFormat.ARGB32);
                var previous=RenderTexture.active;
                Texture2D capture=null;
                try
                {
                    var seen=new HashSet<MeshRenderer>();
                    foreach(var source in new[]{set.studio.gameObject,set.ground})
                    {
                        if(source==null)continue;
                        foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true))
                        {
                            if(!seen.Add(renderer)||!renderer.enabled||set.boardBacking!=null&&renderer.transform.IsChildOf(set.boardBacking.transform))continue;
                            bool visible=true;
                            for(var t=renderer.transform;t!=source.transform&&t!=null;t=t.parent)
                                if(!t.gameObject.activeSelf){visible=false;break;}
                            if(!visible)continue;
                            // Inactive stage cards have no transient mesh until their explicit refresh.
                            renderer.GetComponent<C6Lab.LabEnvironmentCard>()?.Refresh();
                            var mesh=renderer.GetComponent<MeshFilter>();if(mesh==null||mesh.sharedMesh==null)continue;
                            var copy=new GameObject(renderer.name,typeof(MeshFilter),typeof(MeshRenderer));
                            copy.layer=30;copy.transform.SetParent(root.transform,false);
                            copy.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);
                            copy.transform.localScale=renderer.transform.lossyScale;
                            copy.GetComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                            EditorUtility.CopySerialized(renderer,copy.GetComponent<MeshRenderer>());
                            var properties=new MaterialPropertyBlock();renderer.GetPropertyBlock(properties);
                            copy.GetComponent<MeshRenderer>().SetPropertyBlock(properties);
                        }
                    }
                    camera.CopyFrom(bindings.battleCamera);camera.enabled=false;
                    camera.transform.SetPositionAndRotation(bindings.battleCamera.transform.position,bindings.battleCamera.transform.rotation);
                    camera.rect=new Rect(0,0,1,1);camera.aspect=402f/874f;camera.cullingMask=1<<30;
                    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                    capture=new Texture2D(804,1748,TextureFormat.RGB24,false);
                    capture.ReadPixels(new Rect(0,0,804,1748),0,0);capture.Apply();
                    string path=folder+"/Stage"+stage+".png";File.WriteAllBytes(path,capture.EncodeToPNG());
                    AssetDatabase.ImportAsset(path);
                    var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                    importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
                    importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
                    importer.SaveAndReimport();textures[stage-1]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                finally
                {
                    RenderTexture.active=previous;camera.targetTexture=null;
                    Object.DestroyImmediate(capture);Object.DestroyImmediate(rt);
                    Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(root);
                }
            }
            const string artPath="Assets/Hapio/Resources/Hapio/UI/SealedGourdArt.asset";
            var art=AssetDatabase.LoadAssetAtPath<GameSealedGourdArt>(artPath);
            if(art==null){art=ScriptableObject.CreateInstance<GameSealedGourdArt>();AssetDatabase.CreateAsset(art,artPath);}
            art.stageBackgrounds=textures;
            EditorUtility.SetDirty(art);AssetDatabase.SaveAssets();
        }
    }
}
