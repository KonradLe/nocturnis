using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Krypta.Editor
{
    public static class KryptaDiagnostics
    {
        [MenuItem("Krypta/Zapisz pozycje modeli %#F9")]
        public static void Dump()
        {
            var log=new StringBuilder();
            foreach (string name in new[] {"Bohater","Posadzka","pillar","Szkielet","Kamera izometryczna"})
            {
                GameObject obj=GameObject.Find(name);
                if (!obj) { log.AppendLine(name+": missing"); continue; }
                log.AppendLine(name+" position="+obj.transform.position+" scale="+obj.transform.lossyScale+" rotation="+obj.transform.eulerAngles);
                int count=0;
                foreach (Renderer r in obj.GetComponentsInChildren<Renderer>())
                {
                    if (count++ > 3) break;
                    log.AppendLine(r.name+" position="+r.transform.position+" scale="+r.transform.lossyScale+" bounds="+r.bounds);
                }
                foreach (MeshFilter mesh in obj.GetComponentsInChildren<MeshFilter>())
                    log.AppendLine("mesh "+mesh.name+" localBounds="+mesh.sharedMesh.bounds+" vertices="+mesh.sharedMesh.vertexCount+" readable="+mesh.sharedMesh.isReadable);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/model-positions.txt",log.ToString());
            Debug.Log(log.ToString());
        }
    }
}
