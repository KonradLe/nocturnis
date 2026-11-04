using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Krypta.Editor
{
 // Run after NocturnisArtBuild.Prepare. Never regenerates the scene or KryptaAssets.
 public static class NocturnisCryptQA
 {
  const string SessionKey="Nocturnis.CryptQA",Output="Logs/Crypt20261024";
  const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
  const float Tile=1.8f,SurfaceTolerance=.08f;
  static readonly int[] Zones={Campaign.Crypt,Campaign.Crypt2,Campaign.Crypt3,Campaign.Kings,Campaign.Heart,Campaign.Cellar};
  static readonly Vector2[] Samples={Vector2.zero,new Vector2(-.65f,-.65f),new Vector2(.65f,-.65f),new Vector2(-.65f,.65f),new Vector2(.65f,.65f)};
  static readonly int[] Dx={1,-1,0,0},Dz={0,0,1,-1};
  static readonly MethodInfo LoadZone=typeof(KryptaGame).GetMethod("LoadZone",Private);
  static readonly PropertyInfo State=typeof(KryptaGame).GetProperty("State");
  static readonly FieldInfo World=typeof(KryptaGame).GetField("world",Private),Player=typeof(KryptaGame).GetField("player",Private);
  static bool registered,initialized,finished;
  static int zoneIndex,stage,shotIndex;
  static double started,next;
  static Report report;
  static ZoneResult current;

  [Serializable]sealed class Report
  {
   public bool passed;
   public string startedUtc,finishedUtc,unityVersion,graphicsDevice,message;
   public int assertions,artisanModels,artisanMeshes,artisanVertices;
   public bool floorWearAvailable;
   public List<string> runtimeErrors=new List<string>();
   public List<ZoneResult> zones=new List<ZoneResult>();
  }
  [Serializable]sealed class ZoneResult
  {
   public int id,walkableCells,reachableCells,floorSamples,floorMeshes,projectedTriangles,decorationObjects;
   public int artisanObjects,wearMeshes,rosetteMeshes;
   public string name;
   public float minimumSurfaceY=999,maximumSurfaceY=-999;
   public List<ShotResult> screenshots=new List<ShotResult>();
  }
  [Serializable]sealed class ShotResult
  {
   public string file;
   public Vector3 focus;
   public int width,height,pngBytes,pixelRange;
  }
  struct Triangle
  {
   public Vector3 a,b,c;
   public float denominator;
   public Triangle(Vector3 p,Vector3 q,Vector3 r)
   {a=p;b=q;c=r;denominator=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);}
   public bool Height(float x,float z,out float y)
   {
    float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/denominator;
    float v=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/denominator;
    y=u*a.y+v*b.y+(1-u-v)*c.y;
    return u>=-.0001f && v>=-.0001f && u+v<=1.0001f;
   }
  }

  [InitializeOnLoadMethod]static void Restore()
  {
   if(SessionState.GetBool(SessionKey,false))Register();
  }
  public static void Run()
  {
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Run crypt QA from edit mode.");
   Directory.CreateDirectory(Output);
   EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");
   SessionState.SetBool(SessionKey,true);
   Register();
   EditorApplication.isPlaying=true;
  }
  static void Register()
  {
   if(registered)return;
   registered=true;finished=false;initialized=false;zoneIndex=stage=shotIndex=0;
   started=EditorApplication.timeSinceStartup;next=0;KryptaGame.Testing=true;
   report=new Report{startedUtc=DateTime.UtcNow.ToString("o"),unityVersion=Application.unityVersion,graphicsDevice=SystemInfo.graphicsDeviceName};
   EditorApplication.update+=Tick;Application.logMessageReceived+=Log;
  }
  static void Log(string message,string stack,LogType type)
  {
   if(type==LogType.Exception || type==LogType.Error || type==LogType.Assert)
    report.runtimeErrors.Add(message+"\n"+stack);
  }
  static void Check(bool condition,string message)
  {
   report.assertions++;
   if(!condition)throw new InvalidOperationException(message);
  }
  static void Tick()
  {
   if(finished || !EditorApplication.isPlaying)return;
   if(EditorApplication.timeSinceStartup-started>240){Finish(false,"Timeout while collecting underground geometry and screenshots.");return;}
   if(EditorApplication.timeSinceStartup<next)return;
   next=EditorApplication.timeSinceStartup+.12;
   var game=UnityEngine.Object.FindFirstObjectByType<KryptaGame>();
   if(!game || !game.View)return;
   try
   {
    KryptaGame.Testing=true;
    if(!initialized)
    {
     Check(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null,"Screenshots require a graphics device; omit -nographics.");
     Check(LoadZone!=null && State!=null && World!=null && Player!=null,"Runtime world inspection is available.");
     CheckPalette();
     game.SelectHero(0);
     Check(game.SelectedHero==0,"Preview uses the first KayKit hero.");
     State.SetValue(game,KryptaGame.Phase.Paused);Time.timeScale=0;
     // Freeze LateUpdate's camera follow as well as gameplay; screenshots use the actual game camera.
     game.enabled=false;var hud=game.GetComponent<KryptaHUD>();if(hud)hud.enabled=false;
     game.View.enabled=false;game.View.cullingMask=~(1<<30);
     initialized=true;
    }
    if(zoneIndex>=Zones.Length)
    {
     Check(report.runtimeErrors.Count==0,"No Unity errors or exceptions during crypt review.");
     Finish(true,"Six underground zones: connected routes, five structural-floor samples per walkable tile, clear passages, original artisan models and floor wear in every zone, valid assets and eighteen rendered camera views.");
     return;
    }
    if(stage==0)
    {
     State.SetValue(game,KryptaGame.Phase.Paused);
     LoadZone.Invoke(game,new object[]{Zones[zoneIndex]});
     State.SetValue(game,KryptaGame.Phase.Paused);Time.timeScale=0;
     current=new ZoneResult{id=game.Zone,name=Campaign.Names[game.Zone]};report.zones.Add(current);
     shotIndex=0;stage=1;next=EditorApplication.timeSinceStartup+.3;
    }
    else if(stage==1)
    {
     ValidateZone(game);stage=2;
    }
    else
    {
     Capture(game,shotIndex++);
     if(shotIndex==3){zoneIndex++;stage=0;}
    }
   }
   catch(Exception error){Finish(false,error.ToString());}
  }
  static void CheckPalette()
  {
   var palette=Resources.Load<NocturnisCryptPalette>("CryptPalette");
   Check(palette,"CryptPalette is available as a runtime resource.");
   foreach(var field in typeof(NocturnisCryptPalette).GetFields(BindingFlags.Public|BindingFlags.Instance))
   {
    if(field.FieldType!=typeof(GameObject))continue;
    var prefab=(GameObject)field.GetValue(palette);
    Check(prefab,"Palette model is assigned: "+field.Name);
    Check(prefab.GetComponentsInChildren<MeshFilter>(true).Length>0,"Palette model contains geometry: "+field.Name);
    ValidateMaterials(prefab.transform,"palette/"+field.Name,true);
   }
   var artisan=Resources.Load<NocturnisArtisanKit>("ArtisanKit");
   Check(artisan,"ArtisanKit is available as a runtime resource.");
   foreach(var prefab in new[]{artisan.relicShrine,artisan.brokenPillar,artisan.urnNiche})
   {
    Check(prefab,"An original artisan model is assigned.");report.artisanModels++;
    ValidateMaterials(prefab.transform,"artisan/"+prefab.name,true);
    var filters=prefab.GetComponentsInChildren<MeshFilter>(true);
    Check(filters.Length>0,"Artisan model contains source geometry: "+prefab.name);
    foreach(var filter in filters)
    {
     Mesh mesh=filter.sharedMesh;
     Check(AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/Generated/Artisan20261024/Meshes/",StringComparison.Ordinal),"Artisan model uses its generated original mesh: "+prefab.name);
     Check(mesh.isReadable,"Artisan source geometry is available for verification: "+prefab.name);
     Vector3[] positions=mesh.vertices,normals=mesh.normals;Vector2[] uv=mesh.uv;int[] triangles=mesh.triangles;
     Check(positions.Length>=30 && mesh.bounds.size.sqrMagnitude>.01f,"Artisan source geometry has visible extent: "+prefab.name);
     Check(normals.Length==positions.Length,"Artisan mesh has normals for every vertex: "+prefab.name);
     Check(uv.Length==positions.Length,"Artisan mesh has UV coordinates for every vertex: "+prefab.name);
     bool geometryValid=true,normalsValid=true,uvValid=true,indicesValid=triangles.Length>0 && triangles.Length%3==0;
     for(int i=0;i<positions.Length;i++)
     {
      Vector3 v=positions[i],n=normals[i];Vector2 t=uv[i];
      geometryValid &= Finite(v.x) && Finite(v.y) && Finite(v.z);
      normalsValid &= Finite(n.x) && Finite(n.y) && Finite(n.z) && n.sqrMagnitude>.5f;
      uvValid &= Finite(t.x) && Finite(t.y);
     }
     foreach(int index in triangles)indicesValid &= index>=0 && index<positions.Length;
     Check(geometryValid && normalsValid && uvValid && indicesValid,"Artisan mesh vertices, normals, UVs and indices are valid: "+prefab.name);
     var renderer=filter.GetComponent<MeshRenderer>();
     Check(renderer.sharedMaterials.Length==mesh.subMeshCount,"Artisan material slots match its submeshes: "+prefab.name);
     report.artisanMeshes++;report.artisanVertices+=positions.Length;
    }
   }
   var wear=Resources.Load<Texture2D>("Artisan/FloorWear");
   Check(wear,"FloorWear is available as a runtime texture.");report.floorWearAvailable=true;
   var importer=AssetImporter.GetAtPath("Assets/Resources/Artisan/FloorWear.png") as TextureImporter;
   Check(importer,"FloorWear has an editor texture importer.");
   Check(importer.textureType==TextureImporterType.Default && importer.alphaSource==TextureImporterAlphaSource.FromInput && importer.alphaIsTransparency,"FloorWear preserves source alpha for transparent overlays.");
   Check(importer.mipmapEnabled && importer.maxTextureSize==1024 && importer.wrapMode==TextureWrapMode.Clamp && importer.textureCompression==TextureImporterCompression.Compressed && importer.sRGBTexture,"FloorWear uses the required mipmap, size, clamp, compression and color settings.");
  }
  static bool Finite(float value){return !float.IsNaN(value) && !float.IsInfinity(value);}
  static void ValidateMaterials(Transform root,string label,bool inactive=false)
  {
   foreach(var filter in root.GetComponentsInChildren<MeshFilter>(inactive))
   {
    Check(filter.sharedMesh && filter.sharedMesh.vertexCount>0,"Nonempty mesh: "+label+"/"+filter.name);
    var renderer=filter.GetComponent<MeshRenderer>();
    Check(renderer && renderer.sharedMaterials.Length>0,"Mesh renderer has material slots: "+label+"/"+filter.name);
    foreach(var material in renderer.sharedMaterials)
     Check(material && material.shader && material.shader.isSupported && material.shader.name!="Hidden/InternalErrorShader","Supported material: "+label+"/"+filter.name);
   }
  }
  static Transform FindRoot(Transform world,string name)
  {
   foreach(var child in world.GetComponentsInChildren<Transform>())if(child.name==name)return child;
   return null;
  }
  static int Cell(float value){return Mathf.FloorToInt(value/Tile+.5f);}
  static void ValidateZone(KryptaGame game)
  {
   var world=(Transform)World.GetValue(game);
   Check(world && world.gameObject.activeInHierarchy,"Current zone world is active.");
   Transform floor=FindRoot(world,"Posadzka katakumb");
   Check(floor,"New floor is present in zone "+game.Zone);
   ValidateMaterials(floor,"floor zone "+game.Zone);
   CheckFloorArt(floor,game.Zone);
   var buckets=BuildFloorIndex(floor);
   var cells=new List<Vector2Int>();
   for(int x=0;x<DungeonGrid.Width;x++)for(int z=0;z<DungeonGrid.Height;z++)
   {
    if(!game.MapFloor(x,z))continue;
    cells.Add(new Vector2Int(x,z));
    foreach(var offset in Samples)
    {
     float y;bool covered=FloorHeight(buckets,x,z,x*Tile+offset.x,z*Tile+offset.y,out y);
     current.floorSamples++;
     Check(covered,"Walkable surface is missing in zone "+game.Zone+" at "+x+","+z+" offset "+offset);
     Check(Mathf.Abs(y)<=SurfaceTolerance,"Walkable surface height "+y+" exceeds tolerance in zone "+game.Zone+" at "+x+","+z);
     current.minimumSurfaceY=Mathf.Min(current.minimumSurfaceY,y);current.maximumSurfaceY=Mathf.Max(current.maximumSurfaceY,y);
    }
   }
   current.walkableCells=cells.Count;Check(cells.Count>0,"Zone has navigable floor.");
   CheckConnected(game,cells);
   CheckActorFloor(game,buckets,game.SpawnPosition,"spawn");CheckActorFloor(game,buckets,game.ExitPosition,"exit");
   CheckActorFloor(game,buckets,game.HeroPosition,"hero");
   foreach(var enemy in game.enemies)if(enemy.hp>0)CheckActorFloor(game,buckets,enemy.Position,"enemy "+enemy.label);
   Transform details=FindRoot(world,"Dekoracje katakumb");
   Check(details && details.childCount>0,"Decorations are present in zone "+game.Zone);
   ValidateMaterials(details,"decorations zone "+game.Zone);
   foreach(Transform child in details)
   {
    CheckClearance(child,cells);
    if(child.name.StartsWith("Nisza [",StringComparison.Ordinal))
    {
     bool original=false;
     foreach(var filter in child.GetComponentsInChildren<MeshFilter>())
      if(AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith("Assets/Generated/Artisan20261024/Meshes/",StringComparison.Ordinal))
      {
       var renderer=filter.GetComponent<MeshRenderer>();
       Check(filter.sharedMesh.vertexCount>0 && renderer && renderer.enabled && filter.gameObject.activeInHierarchy,"Placed artisan geometry is present and rendered: "+child.name);
       original=true;
      }
     // Prefab saving may use the filename for its root name. The shared mesh asset
     // identifies original geometry independently of the presentation label.
     if(original)current.artisanObjects++;
    }
   }
   Check(current.artisanObjects>0,"Every zone contains at least one original artisan model: "+game.Zone);
   foreach(Transform child in world)
    if(child.name.StartsWith("Mur katakumb [",StringComparison.Ordinal) || child.name.StartsWith("Filar katakumb [",StringComparison.Ordinal))
    {ValidateMaterials(child,"masonry zone "+game.Zone);CheckClearance(child,cells);}
   Check(report.runtimeErrors.Count==0,"No runtime errors while loading zone "+game.Zone);
  }
  static void CheckFloorArt(Transform floor,int zone)
  {
   Transform wear=FindRoot(floor,"Ślady zużycia");
   Check(wear,"Floor-wear geometry is present in zone "+zone);
   var filters=wear.GetComponentsInChildren<MeshFilter>();current.wearMeshes=filters.Length;
   Check(current.wearMeshes>0,"Floor wear contains rendered geometry in zone "+zone);
   var texture=Resources.Load<Texture2D>("Artisan/FloorWear");
   foreach(var filter in filters)
   {
    var renderer=filter.GetComponent<MeshRenderer>();bool textured=false;
    foreach(var material in renderer.sharedMaterials)if(material.mainTexture==texture)textured=true;
    Check(textured,"Floor-wear geometry uses the original texture in zone "+zone);
   }
   Transform rosettes=FindRoot(floor,"Kamienne rozety");
   if(rosettes)
   {
    current.rosetteMeshes=rosettes.GetComponentsInChildren<MeshFilter>().Length;
    Check(current.rosetteMeshes>0,"Stone rosettes contain rendered geometry in zone "+zone);
   }
  }
  static Dictionary<int,List<Triangle>> BuildFloorIndex(Transform root)
  {
   var buckets=new Dictionary<int,List<Triangle>>();
   var filters=root.GetComponentsInChildren<MeshFilter>();
   foreach(var filter in filters)
   {
    // Transparent wear and raised inlays must not conceal holes in the structural floor check.
    if(filter.name!="Łączona posadzka" && filter.name!="Ciągłe podparcie")continue;
    current.floorMeshes++;
    Mesh mesh=filter.sharedMesh;Check(mesh.isReadable,"Floor mesh remains readable for geometry verification: "+filter.name);
    Vector3[] points=mesh.vertices;int[] indices=mesh.triangles;
    for(int i=0;i<points.Length;i++)points[i]=filter.transform.TransformPoint(points[i]);
    for(int i=0;i<indices.Length;i+=3)
    {
     var triangle=new Triangle(points[indices[i]],points[indices[i+1]],points[indices[i+2]]);
     if(Mathf.Abs(triangle.denominator)<.000001f)continue;
     current.projectedTriangles++;
     float minX=Mathf.Min(triangle.a.x,Mathf.Min(triangle.b.x,triangle.c.x)),maxX=Mathf.Max(triangle.a.x,Mathf.Max(triangle.b.x,triangle.c.x));
     float minZ=Mathf.Min(triangle.a.z,Mathf.Min(triangle.b.z,triangle.c.z)),maxZ=Mathf.Max(triangle.a.z,Mathf.Max(triangle.b.z,triangle.c.z));
     for(int x=Mathf.Max(0,Cell(minX));x<=Mathf.Min(DungeonGrid.Width-1,Cell(maxX));x++)
      for(int z=Mathf.Max(0,Cell(minZ));z<=Mathf.Min(DungeonGrid.Height-1,Cell(maxZ));z++)
      {
       int key=x+z*DungeonGrid.Width;List<Triangle> list;
       if(!buckets.TryGetValue(key,out list)){list=new List<Triangle>();buckets.Add(key,list);}
       list.Add(triangle);
      }
    }
   }
   Check(current.floorMeshes>0,"New floor contains structural geometry.");
   Check(current.projectedTriangles>0,"Floor triangles have a nonzero horizontal footprint.");
   return buckets;
  }
  static bool FloorHeight(Dictionary<int,List<Triangle>> buckets,int x,int z,float px,float pz,out float y)
  {
   y=float.NegativeInfinity;List<Triangle> triangles;
   if(!buckets.TryGetValue(x+z*DungeonGrid.Width,out triangles))return false;
   bool found=false;
   foreach(var triangle in triangles)
   {float height;if(triangle.Height(px,pz,out height)){y=Mathf.Max(y,height);found=true;}}
   return found;
  }
  static void CheckActorFloor(KryptaGame game,Dictionary<int,List<Triangle>> buckets,Vector3 p,string label)
  {
   int x=Cell(p.x),z=Cell(p.z);float height;
   Check(game.MapFloor(x,z),label+" is on navigation in zone "+game.Zone);
   Check(FloorHeight(buckets,x,z,p.x,p.z,out height) && Mathf.Abs(height)<=SurfaceTolerance,label+" is supported by the new floor in zone "+game.Zone);
  }
  static void CheckConnected(KryptaGame game,List<Vector2Int> cells)
  {
   var seen=new bool[DungeonGrid.Width,DungeonGrid.Height];var queue=new Queue<Vector2Int>();
   int sx=Cell(game.SpawnPosition.x),sz=Cell(game.SpawnPosition.z);
   Check(game.MapFloor(sx,sz),"Spawn is a walkable navigation tile.");queue.Enqueue(new Vector2Int(sx,sz));seen[sx,sz]=true;
   while(queue.Count>0)
   {
    var cell=queue.Dequeue();current.reachableCells++;
    for(int i=0;i<4;i++)
    {
     int x=cell.x+Dx[i],z=cell.y+Dz[i];
     if(!game.MapFloor(x,z) || seen[x,z])continue;
     seen[x,z]=true;queue.Enqueue(new Vector2Int(x,z));
    }
   }
   Check(current.reachableCells==cells.Count,"Every walkable tile connects to the spawn in zone "+game.Zone);
   Check(seen[Cell(game.ExitPosition.x),Cell(game.ExitPosition.z)],"Exit is reachable from the spawn in zone "+game.Zone);
  }
  static void CheckClearance(Transform decoration,List<Vector2Int> cells)
  {
   Bounds bounds;Check(ModelBounds.TryGet(decoration.gameObject,out bounds),"Decoration has visible bounds: "+decoration.name);
   current.decorationObjects++;
   if(bounds.max.y<=.2f || bounds.size.y<=.2f)return;
   bool clear=true;Vector2Int overlap=default(Vector2Int);
   foreach(var cell in cells)
   {
    float x=cell.x*Tile,z=cell.y*Tile,half=Tile*.5f-.02f;
    if(bounds.max.x>x-half && bounds.min.x<x+half && bounds.max.z>z-half && bounds.min.z<z+half)
    {clear=false;overlap=cell;break;}
   }
   Check(clear,"Tall decoration overlaps navigable tile "+overlap+": "+decoration.name);
  }
  static Vector3 CameraFocus(int zone,int index)
  {
   Vector2Int[] points=zone==Campaign.Heart ? new[]{new Vector2Int(12,5),new Vector2Int(12,25),new Vector2Int(12,35)} : new[]{new Vector2Int(5,5),new Vector2Int(8,20),new Vector2Int(16,34)};
   return new Vector3(points[index].x*Tile,0,points[index].y*Tile);
  }
  static void Capture(KryptaGame game,int index)
  {
   Vector3 focus=CameraFocus(game.Zone,index);Check(game.MapFloor(Cell(focus.x),Cell(focus.z)),"Screenshot focus is on a walkable tile.");
   var actor=(ActorVisual)Player.GetValue(game);actor.Root.transform.position=focus;actor.PreviewIdle(.5f);
   Shader.SetGlobalVector("_NocturnisHero",new Vector4(focus.x,focus.y,focus.z,1));
   Camera camera=game.View;camera.enabled=false;camera.cullingMask=~(1<<30);camera.orthographicSize=9;camera.aspect=16f/9;
   camera.transform.position=focus+new Vector3(9,15,-11);camera.transform.LookAt(focus);
   var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGB32);target.Create();
   var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);RenderTexture previous=RenderTexture.active,oldTarget=camera.targetTexture;
   try
   {
    camera.targetTexture=target;camera.Render();RenderTexture.active=target;
    pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();
    var colors=pixels.GetPixels32();int minimum=255,maximum=0;
    for(int i=0;i<colors.Length;i+=251)
    {
     int light=Mathf.Max(colors[i].r,Mathf.Max(colors[i].g,colors[i].b));minimum=Mathf.Min(minimum,light);maximum=Mathf.Max(maximum,light);
    }
    Check(maximum-minimum>25,"Rendered view contains visible scene geometry, zone "+game.Zone+" view "+index);
    string file="zone-"+game.Zone.ToString("00")+"-view-"+(index+1)+".png";byte[] png=pixels.EncodeToPNG();
    Directory.CreateDirectory(Output);File.WriteAllBytes(Path.Combine(Output,file),png);
    current.screenshots.Add(new ShotResult{file=file,focus=focus,width=1920,height=1080,pngBytes=png.Length,pixelRange=maximum-minimum});
   }
   finally
   {
    camera.targetTexture=oldTarget;RenderTexture.active=previous;target.Release();
    UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);
   }
  }
  static void Finish(bool success,string message)
  {
   if(finished)return;finished=true;
   SessionState.SetBool(SessionKey,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
   report.passed=success;report.message=message;report.finishedUtc=DateTime.UtcNow.ToString("o");
   Directory.CreateDirectory(Output);File.WriteAllText(Path.Combine(Output,"result.json"),JsonUtility.ToJson(report,true));
   File.WriteAllText(Path.Combine(Output,"result.txt"),(success ? "PASS: " : "FAIL: ")+report.assertions+" assertions. "+message);
   Debug.Log((success ? "PASS: " : "FAIL: ")+message);EditorApplication.Exit(success ? 0 : 1);
  }
 }
}
