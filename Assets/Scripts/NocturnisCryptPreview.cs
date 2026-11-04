using System;
using UnityEngine;

namespace Krypta
{
 public sealed partial class KryptaGame
 {
  static readonly int[] CryptPreviewZones={Campaign.Crypt,Campaign.Crypt2,Campaign.Crypt3,Campaign.Kings,Campaign.Heart,Campaign.Cellar};
  bool cryptPreview;
  bool wholeActPreview;
  static bool previewLaunchConsumed;
  static readonly int[] ActPreviewZones={Campaign.Road,Campaign.Town,Campaign.Fields,Campaign.Forest,Campaign.Cellar,Campaign.Ravine,Campaign.Cemetery,Campaign.Chapel,Campaign.Crypt,Campaign.Cloister,Campaign.Crypt2,Campaign.Cistern,Campaign.Crypt3,Campaign.Galleries,Campaign.Kings,Campaign.Ossuary,Campaign.Forge,Campaign.Threshold,Campaign.Heart};

  // Explicit launcher argument only. Normal campaign startup and save keys stay unchanged.
  void TryStartCryptPreview()
  {
   string[] args=Environment.GetCommandLineArgs();
   wholeActPreview=Array.IndexOf(args,"-act1-preview")>=0;
   if(Array.IndexOf(args,"-crypt-preview")<0 && !wholeActPreview)return;
   cryptPreview=true;
   Testing=true;
   // The launcher enters preview once. Returning to the menu reloads the scene
   // and must not immediately send the player back into Oakhaven.
   if(previewLaunchConsumed)return;
   previewLaunchConsumed=true;
   // Set before Begin/ResetArtisans: both the checkpoint and shared stash use their QA namespace.
   Testing=true;
   int zone=wholeActPreview?Campaign.Town:Campaign.Crypt,argument=Array.IndexOf(args,"-crypt-zone"),requested;
   if(argument>=0 && argument+1<args.Length && int.TryParse(args[argument+1],out requested) && Campaign.Underground(requested))zone=requested;
   Begin();
   OpenCryptPreviewZone(zone);
  }

  // Return true only when a zone switch consumed this frame's input.
  bool CryptPreviewInput()
  {
   if(!cryptPreview || (State!=Phase.Playing && State!=Phase.Dead && State!=Phase.Won))return false;
   if(wholeActPreview && Input.GetKeyDown(KeyCode.F8)){
    Vector3 position=HeroPosition;SelectedHero=(SelectedHero+1)%5;CreateHero();player.Root.transform.position=position;Resume();ResetCombatActions();Resource=50;Shadow=60;return true;
   }
   int direction=Input.GetKeyDown(KeyCode.F6) ? -1 : Input.GetKeyDown(KeyCode.F7) ? 1 : 0;
   if(direction==0)return false;
   var zones=wholeActPreview?ActPreviewZones:CryptPreviewZones;
   int index=Array.IndexOf(zones,Zone);
   index=index<0 ? direction>0 ? 0 : zones.Length-1 : (index+direction+zones.Length)%zones.Length;
   OpenCryptPreviewZone(zones[index]);
   return true;
  }

  void OpenCryptPreviewZone(int zone)
  {
   Testing=true;
   Resume();
   // Clear transient combat timers when inspecting another room, including after a death.
   strikeAt=pulseAt=defenseAt=dashAt=dashEnd=hurtUntil=graceAt=0;
   for(int i=0;i<extraAt.Length;i++)extraAt[i]=0;
   Resource=50;Shadow=60;killStreak=0;
   LoadZone(zone);
   Health=MaxHealth;
   ShowNotice("PODGLĄD • "+Campaign.Names[zone]+"\nF6: poprzednia lokacja  •  F7: następna lokacja");
   if(wholeActPreview)ShowNotice("PODGLĄD AKTU I • "+Campaign.Names[zone]+"\nF6 / F7: lokacja  •  F8: następna klasa");
   NoticeUntil=Time.time+12;
  }
 }
}
