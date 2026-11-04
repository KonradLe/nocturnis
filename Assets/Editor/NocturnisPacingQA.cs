using UnityEditor;
using UnityEditor.SceneManagement;
namespace Krypta.Editor
{
 public static class NocturnisPacingQA
 {
  public static void Run()
  {
   // RuntimeInitializeOnLoad in the bot sees this after the domain reload.
   SessionState.SetBool("Nocturnis.PacingQA",true);
   EditorSceneManager.OpenScene("Assets/Scenes/Krypta.unity");
   EditorApplication.isPlaying=true;
  }
 }
}
