using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace C6Lab.Experiments.Editor
{
    /// <summary>Prepared asset silhouette only: no paper grain in the clay material.</summary>
    public static class LabClayStickerBaker
    {
        public const string Folder="Assets/Lab/Resources/GourdPaint/ClaySticker";
        public const int Size=512;
        static readonly string[] Names={"Broad","Long","Cluster"};
        [MenuItem("C6 Lab/Experiments/Surface Sticker/Bake Clay Data")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            foreach(string name in Names)
            {
                string source=LabSurfaceStickerPrototype.Folder+"/Assets/"+name+"Paper.png";
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(source);
                if(texture==null || !texture.isReadable) throw new InvalidOperationException("Import the original sticker first: "+source);
                var pixels=new Color32[Size*Size];var dist=new float[pixels.Length];
                for(int y=0;y<Size;y++) for(int x=0;x<Size;x++)
                {
                    float alpha=texture.GetPixelBilinear((x+.5f)/Size,(y+.5f)/Size).a;
                    byte a=(byte)Mathf.RoundToInt(alpha*255);
                    pixels[y*Size+x]=new Color32(a,0,0,a);dist[y*Size+x]=alpha>=.3f?2048f:0;
                }
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++) Relax(x,y,-1,-1);
                for(int y=Size-1;y>=0;y--)for(int x=Size-1;x>=0;x--)Relax(x,y,1,1);
                for(int i=0;i<pixels.Length;i++)
                {
                    var p=pixels[i];float d=Mathf.Clamp01(dist[i]/32f);
                    p.g=(byte)Mathf.RoundToInt(d*255);p.b=(byte)Mathf.RoundToInt(Mathf.SmoothStep(0,1,Mathf.Clamp01(dist[i]/10f))*255);pixels[i]=p;
                }
                var data=new Texture2D(Size,Size,TextureFormat.RGBA32,false,true);
                try { data.SetPixels32(pixels);data.Apply(); File.WriteAllBytes(Folder+"/"+name+"Data.png",data.EncodeToPNG()); }
                finally{UnityEngine.Object.DestroyImmediate(data);}
                string path=Folder+"/"+name+"Data.png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;
                importer.sRGBTexture=false;importer.isReadable=false;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
                void Relax(int x,int y,int dx,int dy)
                {
                    int i=y*Size+x;
                    Update(x+dx,y,1);Update(x,y+dy,1);Update(x+dx,y+dy,1.41421356f);Update(x-dx,y+dy,1.41421356f);
                    void Update(int nx,int ny,float cost) { if(nx>=0&&nx<Size&&ny>=0&&ny<Size)dist[i]=Mathf.Min(dist[i],dist[ny*Size+nx]+cost); }
                }
            }
            using(var hash=SHA256.Create())
            using(var stream=new MemoryStream())
            {
                byte[] version=System.Text.Encoding.UTF8.GetBytes("clay-data-v1-alpha-distance-rounded-height");stream.Write(version,0,version.Length);
                foreach(string name in Names) { byte[] bytes=File.ReadAllBytes(Folder+"/"+name+"Data.png");stream.Write(bytes,0,bytes.Length); }
                string id=string.Concat(hash.ComputeHash(stream.ToArray()).Select(b=>b.ToString("x2")));
                File.WriteAllText(Folder+"/Version.txt",id+"\n");AssetDatabase.ImportAsset(Folder+"/Version.txt",ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
