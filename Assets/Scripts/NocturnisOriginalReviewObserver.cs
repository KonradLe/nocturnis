using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Krypta
{
    // Explicit review flag only. Observes the real player; never changes game state.
    public sealed class NocturnisOriginalReviewObserver : MonoBehaviour
    {
        [Serializable] public sealed class Snapshot
        {
            public int frame, zone, hero, enemies, configuration, castSequence, castSlot, screenWidth, screenHeight;
            public string phase, clip, model, castNotice, menuPage, menuConfirmation;
            public float realTime, gameTime, health, resource, dashReady, pulseReady, defenseReady, clipTime;
            public Vector3 position, nearestEnemy, enemyScreen, mouseWorld;
            public float nearestEnemyDistance, nearestEnemyHealth, totalEnemyHealth;
            public bool originalFloor, armor, castSuccess, testing, equipmentOpen, bookOpen, journalOpen;
            public bool pointerOverHUD, mouseWorldWalkable, hasDestination;
            public float buffRemaining;
            public int[] floorModules;
            public int activeProfile;
            public bool largeText, cameraShake;
            public string keySkill, keyInteract;
        }
        static readonly FieldInfo Player = typeof(KryptaGame).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo Destination = typeof(KryptaGame).GetField("hasDestination", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo OverHUD = typeof(KryptaGame).GetMethod("OverHUD", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo MenuPage = typeof(KryptaHUD).GetField("menuPage", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo MenuConfirmation = typeof(KryptaHUD).GetField("menuConfirmation", BindingFlags.Instance | BindingFlags.NonPublic);
        string output;
        float next;
        bool reportedError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartObserver()
        {
            var args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-original-review") < 0 && Array.IndexOf(args, "-gothic-ui-review") < 0) return;
            int index = Array.IndexOf(args, "-original-review-path");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Review requires an explicit -original-review-path.");
            var observer = new GameObject("Opt-in original asset review observer").AddComponent<NocturnisOriginalReviewObserver>();
            observer.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(observer.output));
            DontDestroyOnLoad(observer.gameObject);
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + .08f;
            try
            {
                var game = FindFirstObjectByType<KryptaGame>();
                if (!game || !game.View) return;
                var actor = Player.GetValue(game) as ActorVisual;
                if (actor == null) return;
                var config = Resources.Load<KryptaAssets>("KryptaAssets");
                var hud = game.GetComponent<KryptaHUD>();
                var state = new Snapshot {
                    frame = Time.frameCount, zone = game.Zone, hero = game.SelectedHero,
                    activeProfile = game.ActiveProfile,
                    largeText = PlayerPrefs.GetInt("Nocturnis.LargeText", 0) == 1,
                    cameraShake = PlayerPrefs.GetInt("Nocturnis.CameraShake", 1) == 1,
                    keySkill = NocturnisInput.Text("skill"), keyInteract = NocturnisInput.Text("interact"),
                    phase = game.State.ToString(), realTime = Time.unscaledTime, gameTime = Time.time,
                    health = game.Health, resource = game.Resource, dashReady = game.DashReady,
                    pulseReady = game.PulseReady, defenseReady = game.DefenseReady,
                    position = game.HeroPosition, clip = actor.CurrentClip, clipTime = actor.CurrentClipTime,
                    configuration = config.version, model = config.heroes[game.SelectedHero].model.name,
                    screenWidth = Screen.width, screenHeight = Screen.height, testing = KryptaGame.Testing,
                    equipmentOpen = game.EquipmentOpen, bookOpen = game.BookOpen, journalOpen = game.JournalOpen,
                    menuPage = hud ? MenuPage.GetValue(hud).ToString() : "",
                    menuConfirmation = hud ? MenuConfirmation.GetValue(hud).ToString() : "",
                    pointerOverHUD = (bool)OverHUD.Invoke(game, null), hasDestination = (bool)Destination.GetValue(game),
                    armor = game.Status.Count("armor", Time.time) > 0,
                    castSequence = game.CastNoticeSequence, castSlot = game.LastCastSlot,
                    castNotice = game.CastNotice, castSuccess = game.CastNoticeSucceeded,
                    buffRemaining = game.SkillActiveRemaining(3),
                    nearestEnemyDistance = float.MaxValue,
                };
                float mouseDistance;
                var mouseRay = game.View.ScreenPointToRay(Input.mousePosition);
                if (new Plane(Vector3.up, Vector3.zero).Raycast(mouseRay, out mouseDistance))
                {
                    state.mouseWorld = mouseRay.GetPoint(mouseDistance);
                    state.mouseWorldWalkable = game.MapFloor(Mathf.FloorToInt(state.mouseWorld.x / 1.8f + .5f), Mathf.FloorToInt(state.mouseWorld.z / 1.8f + .5f));
                }
                foreach (var enemy in game.enemies)
                {
                    if (enemy.hp <= 0) continue;
                    state.enemies++; state.totalEnemyHealth += enemy.hp;
                    float distance = Vector3.Distance(game.HeroPosition, enemy.Position);
                    if (distance >= state.nearestEnemyDistance) continue;
                    state.nearestEnemyDistance = distance; state.nearestEnemy = enemy.Position;
                    state.nearestEnemyHealth = enemy.hp;
                    state.enemyScreen = game.View.WorldToScreenPoint(enemy.Position + Vector3.up * .5f);
                }
                var floor = FindFirstObjectByType<NocturnisCryptGeometry>();
                if (floor) { state.originalFloor = floor.usesOriginalFloor; state.floorModules = floor.moduleCounts; }
                string json = JsonUtility.ToJson(state);
                File.WriteAllText(output + ".tmp", json);
                if (File.Exists(output)) File.Delete(output);
                File.Move(output + ".tmp", output);
                File.AppendAllText(output + ".jsonl", json + "\n");
            }
            catch (Exception error)
            {
                if (!reportedError) { reportedError = true; Debug.LogError("Original review observer: " + error); }
            }
        }
    }
}
