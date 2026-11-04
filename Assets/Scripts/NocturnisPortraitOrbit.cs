using UnityEngine;

namespace Krypta
{
 // Coordinates are in the same 1280 x 720 canvas as the rest of the HUD.
 public static class NocturnisPortraitOrbit
 {
  public const float DefaultYaw=8f,DegreesPerGuiUnit=.65f;
  public static Rect MenuBounds {get{return new Rect(364,148,502,432);}}
  public static float DragYaw(float yaw,float horizontalGuiDelta)
  {return Mathf.Repeat(yaw+horizontalGuiDelta*DegreesPerGuiUnit,360f);}
  public static Vector2 WindowPointToCanvas(Vector2 point,int width,int height)
  {
   float factor=Mathf.Max(.0001f,Mathf.Min(width/1280f,height/720f));
   Vector2 margin=new Vector2((width-1280*factor)*.5f,(height-720*factor)*.5f);
   return (point-margin)/factor;
  }
 }
 public sealed partial class KryptaHUD
 {
  const int PortraitControlHint=0x4E4F5242;
  float portraitYaw=NocturnisPortraitOrbit.DefaultYaw;
  int portraitDragControl;
  bool portraitDragging;
  Vector2 portraitPointer,portraitPreviousPointer;
  public float PortraitYaw {get{return portraitYaw;}}
  public bool PortraitDragging {get{return portraitDragging;}}
  public int PortraitControlId {get{return portraitDragControl;}}
  bool PortraitOrbitVisible
  {
   get
   {
    return game && game.State==KryptaGame.Phase.Title && menuPage==MenuPage.Characters;
   }
  }
  bool PortraitOrbitAvailable
  {
   get{return PortraitOrbitVisible && menuConfirmation==MenuConfirmation.None &&
     gateAt<0 && !NocturnisCinematic.BlocksInput;}
  }
  void ApplyPortraitYaw()
  {
   if(model!=null && model.Root)
    model.Root.transform.rotation=Quaternion.Euler(0,PortraitOrbitVisible ? portraitYaw : NocturnisPortraitOrbit.DefaultYaw,0);
  }
  public void CancelPortraitDrag()
  {
   if(portraitDragControl!=0 && GUIUtility.hotControl==portraitDragControl)GUIUtility.hotControl=0;
   portraitDragging=false;portraitDragControl=0;
  }
  void ResetPortraitOrbit()
  {CancelPortraitDrag();portraitYaw=NocturnisPortraitOrbit.DefaultYaw;ApplyPortraitYaw();}
  void TickPortraitOrbit()
  {
   if(!portraitDragging)return;
   // MouseUp normally releases in OnGUI. This watchdog also covers losing
   // focus outside the player window, and a native release lost by IMGUI.
   if(!PortraitOrbitAvailable || !Application.isFocused ||
      (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0)))CancelPortraitDrag();
  }
  // Called before the HUD installs its scaling matrix. Capturing this point
  // here avoids depending on matrix/GUIClip conversions inside other widgets.
  void BeginPortraitOrbitGUI()
  {
   var e=Event.current;
   portraitPointer=NocturnisPortraitOrbit.WindowPointToCanvas(e.mousePosition,Screen.width,Screen.height);
   if(!portraitDragging)return;
   if(!PortraitOrbitAvailable){CancelPortraitDrag();return;}
   // Consume the release before any menu buttons are drawn, including a
   // release over Begin Journey after the drag left the preview rectangle.
   if(e.rawType==EventType.MouseUp && e.button==0)
   {CancelPortraitDrag();if(e.type!=EventType.Used)e.Use();}
   else if(e.rawType==EventType.Ignore || e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape)
    CancelPortraitDrag();
  }
  void DrawRotatablePortrait(Rect rect)
  {
   if(portrait)GUI.DrawTexture(rect,portrait,ScaleMode.ScaleToFit,true);
   int id=GUIUtility.GetControlID(PortraitControlHint,FocusType.Passive,rect);
   var e=Event.current;
   if(!GUI.enabled || !PortraitOrbitAvailable || !portrait || model==null)
   {CancelPortraitDrag();return;}
   if(portraitDragging && portraitDragControl!=id){CancelPortraitDrag();return;}
   switch(e.GetTypeForControl(id))
   {
    case EventType.MouseDown:
     if(e.button==0 && GUIUtility.hotControl==0 && rect.Contains(portraitPointer))
     {
      portraitDragControl=id;portraitDragging=true;portraitPreviousPointer=portraitPointer;
      GUIUtility.hotControl=id;e.Use();
     }
     break;
    case EventType.MouseDrag:
     if(e.button==0 && portraitDragging && GUIUtility.hotControl==id)
     {
      portraitYaw=NocturnisPortraitOrbit.DragYaw(portraitYaw,portraitPointer.x-portraitPreviousPointer.x);
      portraitPreviousPointer=portraitPointer;ApplyPortraitYaw();e.Use();
     }
     break;
    case EventType.MouseUp:
     if(e.button==0 && portraitDragging && GUIUtility.hotControl==id)
     {CancelPortraitDrag();e.Use();}
     break;
   }
  }
  void OnDisable(){CancelPortraitDrag();}
  void OnApplicationFocus(bool focused){if(!focused)CancelPortraitDrag();}
  void OnApplicationPause(bool paused){if(paused)CancelPortraitDrag();}
 }
}
