using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace C6Lab.Tests
{
 public sealed class LabGourdCrackTests
 {
  [TestCase(1000,0)] [TestCase(980,1)] [TestCase(660,2)] [TestCase(330,3)] [TestCase(0,3)]
  public void SharedHpDeterminesStage(int hp,int expected)
   =>Assert.That(LabGourdCrack.StageForHp(hp,1000),Is.EqualTo(expected));
  [UnityTearDown]
  public IEnumerator UnloadSavedSceneBeforeOtherPhysicsFixtures()
  {
   foreach (string name in new[] { "Lab", "LabGourdCrack" })
   {
    Scene scene = SceneManager.GetSceneByName(name);
    if (!scene.IsValid() || !scene.isLoaded) continue;
    Scene cleanup = SceneManager.CreateScene("Gourd test cleanup");
    SceneManager.SetActiveScene(cleanup);
    yield return SceneManager.UnloadSceneAsync(scene);
   }
  }

  #if UNITY_EDITOR
  [UnityTest]
  public IEnumerator MainSceneCrackModeHostProjectilesDriveCracksFinishAndResultRestart()
  {
   yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
   yield return null;
   var hud = Object.FindFirstObjectByType<LabHud>();
   Assert.That(hud, Is.Not.Null);
   LabNetwork network = hud.network;
   Assert.That(network, Is.Not.Null);
   var c = network.target.GetComponent<LabGourdCrack>();
   Assert.That(c, Is.Not.Null, "The ordinary Scene retains the crack component and its Inspector references.");
   var controller = Object.FindFirstObjectByType<LabSceneController>();
   Assert.That(controller, Is.Not.Null);
   LabConfig savedConfig = network.config;
   LabConfig savedBoardConfig = controller.config;
   LabConfig fixture = Object.Instantiate(savedConfig);
   // Fixed DEV fixture: generating unlocked Relay orbs avoids random Yin/Yang draws. The
   // saved Host, board gesture, projectile collision and result UI paths remain in use.
   float[] tuning = fixture.CaptureDeveloperValues();
   SetTuning(tuning, "Gourd Mode (0 paint, 1 crack)", 1f);
   SetTuning(tuning, "Monster Max HP", 60f);
   SetTuning(tuning, "Damage per Hit", 20f);
   SetTuning(tuning, "Battle Duration (seconds)", 30f);
   SetTuning(tuning, "Orb Generation Cost", 1f);
   SetTuning(tuning, "Item Lifetime (seconds)", 30f);
   SetTuning(tuning, "Item Unlock Swipes Min", 0f);
   SetTuning(tuning, "Item Unlock Swipes Max", 0f);
   SetTuning(tuning, "Relay x2 Damage Multiplier", 1f);
   SetTuning(tuning, "Yin Chance (%)", 0f);
   SetTuning(tuning, "Yang Chance (%)", 0f);
   SetTuning(tuning, "Relay Orb Chance (%)", 100f);
   SetTuning(tuning, "Secret Box Chance (%)", 0f);
   SetTuning(tuning, "Potion Chance (%)", 0f);
   SetTuning(tuning, "Bomb Chance (%)", 0f);
   SetTuning(tuning, "Spawn Pop Duration (seconds)", 0f);
   SetTuning(tuning, "Spawn Minimum Speed (board widths/s)", 0f);
   SetTuning(tuning, "Spawn Maximum Speed (board widths/s)", 0f);
   SetTuning(tuning, "Drum Drift Speed (board widths/s)", 0f);
   SetTuning(tuning, "Throw Origin Radius", 4f);
   SetTuning(tuning, "Throw Origin Height Offset", -.5f);
   SetTuning(tuning, "Throw Minimum Upward Speed", 2f);
   SetTuning(tuning, "Throw Lateral Gain", 2.5f);
   SetTuning(tuning, "Throw Upward Gain", 2.1f);
   SetTuning(tuning, "Throw Forward Gain", 4.5f);
   SetTuning(tuning, "Throw Maximum World Speed", 18f);
   SetTuning(tuning, "Throw Gravity", 9.81f);
   SetTuning(tuning, "Throw Power Exponent", 1f);
   SetTuning(tuning, "Throw Loft Offset (degrees)", 0f);
   SetTuning(tuning, "Throw Air Damping", 0f);
   // The gourd's sabotage bombs would add unplanned orbs to the boards checked here.
   SetTuning(tuning, "Gourd Bomb Interval Min (seconds, 0 = off)", 0f);
   SetTuning(tuning, "Gourd Bomb Interval Max (seconds, 0 = off)", 0f);
   Assert.That(fixture.TryApplyDeveloperValues(tuning, out string error), Is.True, error);
   network.config = fixture;
   controller.config = fixture;
   controller.ReconfigureBoard();
   network.port = 17781;
   var approvedDamages = new System.Collections.Generic.List<int>();
   System.Action<int> countHit = approvedDamages.Add;
   network.LocalThrowHit += countHit;
   try
   {
    Assert.That(network.StartDeveloperSolo(), Is.True, network.Notice);
    yield return null;
    Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Crack));
    Assert.That(c.network, Is.SameAs(network));
    Assert.That(c.target, Is.SameAs(network.target));
    Assert.That(c.gourdPrefab, Is.SameAs(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
     "Assets/Lab/Art/Target2/Prefabs/Target2_Intact.prefab")));
    Assert.That(c.crackMaterial, Is.SameAs(UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
     "Assets/Lab/Art/GourdCrack/GourdCrack.mat")));
    Assert.That(c.IsReady, Is.True, c.InitializationError);
    Assert.That(c.InitializationError, Is.Null.Or.Empty);
    Assert.That(c.VisualInstance, Is.SameAs(network.target.MountedVisual));
    Assert.That(c.VisualInstance.activeSelf, Is.True);
    Assert.That(c.VisualInstance.transform.parent, Is.SameAs(network.target.VisualMount));
    Assert.That(c.crackMaterial.shader.isSupported, Is.True);
    var shells = c.VisualInstance.GetComponentsInChildren<Renderer>(true)
     .Where(renderer => renderer.sharedMaterials.Contains(c.crackMaterial)).ToArray();
    Assert.That(shells, Is.Not.Empty);
    Assert.That(shells.Any(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy), Is.True);
    Assert.That(network.target.Hitbox.enabled, Is.True);
    Assert.That(network.target.Hitbox.GetComponent<MeshRenderer>().enabled, Is.False,
     "Only the diagnostic cylinder rendering is replaced; its hitbox must stay enabled.");
    Assert.That(c.VisualInstance.GetComponentsInChildren<Collider>(true), Is.Empty);
    Assert.That(c.VisualInstance.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
    Assert.That(c.VisualInstance.GetComponentsInChildren<Animator>(true).All(animator => !animator.enabled), Is.True);

    var finish = c.GetComponent<LabGourdFinish>();
    Assert.That(finish, Is.Not.Null);
    Assert.That(finish.enabled, Is.True);
    var prospectiveVictory = new LabSnapshot { sessionId = "disabled-check", round = 1, phase = LabPhase.Victory, hp = 0 };
    finish.enabled = false;
    Assert.That(LabGourdFinish.DelayVictory(network, prospectiveVictory), Is.False,
     "A disabled presentation cannot hold the ordinary victory UI indefinitely.");
    finish.enabled = true;

    Assert.That(network.IsHost, Is.True);
    Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
    Assert.That(network.Snapshot.hp, Is.EqualTo(60));
    Assert.That(c.Stage, Is.Zero);
    string firstSession = network.Snapshot.sessionId;
    Camera battleCamera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
     .Single(camera => camera.name == "BattleCamera");
    CaptureMainBattle(battleCamera, "main-playing.png");

    for (int hit = 1; hit <= 3; hit++)
    {
     // Each independent shot starts with the authored physical target at rest. Existing
     // hanging-target tests cover continued swing and spin between successive impacts.
     network.target.GetComponent<LabHangingTarget>().ResetSwing();
     network.Generate();
     LabOrbState orb = network.Snapshot.orbs.Single();
     Assert.That(orb.kind, Is.EqualTo(LabOrbKind.Relay));
     Assert.That(orb.lockSwipes, Is.Zero);
     LaunchThroughSavedBoard(network.board, orb.id, hit * 10d);
     Assert.That(network.Snapshot.orbs.Single().inFlight, Is.True,
      "The board's gesture must reach Host launch approval before any HP changes.");
     LabProjectile projectile = network.projectileRoot.GetComponentsInChildren<LabProjectile>()
      .Single(candidate => candidate.ProjectileId == orb.id);
     Assert.That(projectile.GetComponent<SphereCollider>(), Is.Not.Null);
     Assert.That(projectile.IsResolved, Is.False);
     int previousHp = network.Snapshot.hp;
     for (int step = 0; step < 150 && network.Snapshot.hp == previousHp; step++)
      yield return new WaitForFixedUpdate();
     Assert.That(network.Snapshot.hp, Is.EqualTo(60 - hit * 20),
      "A real Host-owned sphere/cylinder collision must publish the next shared HP.");
     Assert.That(network.LastShotReport, Does.StartWith("SHOT HIT"));
     Assert.That(network.Snapshot.orbs.Any(candidate => candidate.id == orb.id), Is.False);
     Assert.That(approvedDamages.Count, Is.EqualTo(hit), "The same collision must award damage once.");
     Assert.That(approvedDamages, Is.All.EqualTo(20));
     yield return null; // Let the presentation observe the Host-published snapshot.
     Assert.That(c.Stage, Is.EqualTo(hit));
     var properties = new MaterialPropertyBlock();
     foreach (Renderer shell in shells)
     {
      shell.GetPropertyBlock(properties);
      Assert.That(properties.GetFloat("_CrackStage"), Is.EqualTo(hit));
     }
     if (hit == 2) CaptureMainBattle(battleCamera, "main-cracks-stage-2.png");
     Assert.That(hud.resultControls.activeSelf, Is.False,
      "Nonterminal hits and an unfinished final presentation must keep results hidden.");
     if (hit < 3)
     {
      Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
      Assert.That(finish.IsFinishing, Is.False);
     }
    }

    Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Victory),
     "Gameplay must end immediately even while the local finishing visual is running.");
    Assert.That(finish.IsFinishing, Is.True);
    Assert.That(finish.Complete, Is.False);
    Assert.That(LabGourdFinish.DelayVictory(network, network.Snapshot), Is.True);
    float deadline = Time.realtimeSinceStartup + LabGourdFinish.Duration + 1f;
    while (!finish.Complete && Time.realtimeSinceStartup < deadline) yield return null;
    Assert.That(finish.Complete, Is.True, "A ready presentation must finish within its bounded duration.");
    yield return null;
    Assert.That(LabGourdFinish.DelayVictory(network, network.Snapshot), Is.False);
    Assert.That(hud.resultControls.activeInHierarchy, Is.True);
    Assert.That(hud.resultStatusText.text, Is.EqualTo("VICTORY"));
    Assert.That(c.VisualInstance.activeSelf, Is.False);
    Assert.That(network.target.VisualMount.Find("Gourd Left Half").gameObject.activeSelf, Is.True);
    Assert.That(network.target.VisualMount.Find("Gourd Right Half").gameObject.activeSelf, Is.True);
    Assert.That(network.target.VisualMount.Find("Small Yokai Spirit").gameObject.activeSelf, Is.True);
    CaptureMainBattle(battleCamera, "main-finish.png");
    int completedChildCount = network.target.VisualMount.childCount;
    yield return null;
    Assert.That(network.target.VisualMount.childCount, Is.EqualTo(completedChildCount),
     "Repeated authoritative victory snapshots must not duplicate finish objects.");
    Assert.That(hud.resultRestartButton.interactable, Is.True);
    hud.resultRestartButton.onClick.Invoke();
    yield return null;
    yield return null; // Destroy() of temporary halves completes at the end of the frame.
    Assert.That(network.Snapshot.sessionId, Is.Not.EqualTo(firstSession));
    Assert.That(network.Snapshot.phase, Is.EqualTo(LabPhase.Playing));
    Assert.That(network.Snapshot.hp, Is.EqualTo(60));
    Assert.That(network.Snapshot.orbs, Is.Empty);
    Assert.That(network.Snapshot.players.Single().stamina, Is.EqualTo(fixture.StaminaStart).Within(.001f));
    Assert.That(c.Stage, Is.Zero);
    Assert.That(finish.IsFinishing, Is.False);
    Assert.That(c.VisualInstance.activeSelf, Is.True);
    Assert.That(network.target.VisualMount.Find("Gourd Left Half"), Is.Null);
    Assert.That(network.target.VisualMount.Find("Gourd Right Half"), Is.Null);
    Assert.That(network.target.VisualMount.Find("Small Yokai Spirit"), Is.Null);
    Assert.That(hud.resultControls.activeSelf, Is.False);

    network.Stop();
    yield return null;
    Assert.That(network.config.GourdMode, Is.EqualTo(LabGourdMode.Paint));
    Assert.That(network.target.GetComponent<LabGourdPaint>().IsReady, Is.True);
    Assert.That(c.enabled, Is.False);
    Assert.That(hud.roomListPanel.activeInHierarchy, Is.True,
     "Leaving a Solo round returns to the automatic room browser.");
   }
   finally
   {
    network.LocalThrowHit -= countHit;
    if (network.IsConnected) network.Stop();
    network.config = savedConfig;
    controller.config = savedBoardConfig;
    controller.ReconfigureBoard();
    Object.Destroy(fixture);
   }
  }

  [UnityTest]
  public IEnumerator MissingGourdReferencesDoNotBlockVictoryUi()
  {
   var target = LabTarget.CreateCylinder(null, Vector3.zero, .6f, 2.7f);
   var networkObject = new GameObject("Incomplete gourd test network");
   var network = networkObject.AddComponent<LabNetwork>();
   network.target = target;
   var c = target.gameObject.AddComponent<LabGourdCrack>();
   c.target = target;
   c.network = network;
   var finish = target.gameObject.AddComponent<LabGourdFinish>();
   try
   {
    yield return null;
    Assert.That(c.IsReady, Is.False);
    Assert.That(c.InitializationError, Is.Not.Empty);
    var victory = new LabSnapshot { sessionId = "unconfigured", round = 1, phase = LabPhase.Victory, hp = 0 };
    Assert.That(LabGourdFinish.DelayVictory(network, victory), Is.False,
     "An absent gourd reference must leave the ordinary victory screen available.");
    finish.ObserveSnapshot(victory, .6f);
    Assert.That(finish.IsFinishing, Is.False);
    Assert.That(target.Hitbox.enabled, Is.True);
    Assert.That(target.Hitbox.GetComponent<MeshRenderer>().enabled, Is.True,
     "A failed visual replacement must preserve the diagnostic fallback target.");
   }
   finally
   {
    Object.Destroy(target.gameObject);
    Object.Destroy(networkObject);
   }
  }

  static void CaptureMainBattle(Camera camera, string fileName)
  {
   const string directory = "Library/Issue166ModeCaptures/Crack";
   Directory.CreateDirectory(directory);
   var texture = new RenderTexture(540, 960, 24);
   var image = new Texture2D(540, 960, TextureFormat.RGB24, false);
   RenderTexture previousTarget = camera.targetTexture;
   RenderTexture previousActive = RenderTexture.active;
   try
   {
    camera.targetTexture = texture;
    camera.Render();
    RenderTexture.active = texture;
    image.ReadPixels(new Rect(0, 0, 540, 960), 0, 0);
    image.Apply();
    File.WriteAllBytes(Path.Combine(directory, fileName), image.EncodeToPNG());
   }
   finally
   {
    camera.targetTexture = previousTarget;
    RenderTexture.active = previousActive;
    texture.Release();
    Object.Destroy(texture);
    Object.Destroy(image);
   }
  }

  static void LaunchThroughSavedBoard(LabOrbBoard board, string orbId, double now)
  {
   Assert.That(board.TryGetMotion(orbId, out Vector2 position01, out _), Is.True);
   Rect bounds = board.CenterBounds;
   Vector2 start = ToWorld(bounds, position01);
   Vector2 aim = ToWorld(bounds, new Vector2(.5f, .25f));
   Assert.That(board.TryBeginDragAtWorld(start, now), Is.True);
   Assert.That(board.DragToWorld(aim, now + 1d), Is.True);
   Vector2 release = aim + Vector2.up * (bounds.width * .2f);
   Assert.That(board.EndDragAtWorld(release, now + 1.04d), Is.True);
  }

  static Vector2 ToWorld(Rect bounds, Vector2 position01) => new Vector2(
   Mathf.Lerp(bounds.xMin, bounds.xMax, position01.x),
   Mathf.Lerp(bounds.yMin, bounds.yMax, position01.y));

  static void SetTuning(float[] values, string label, float value)
  {
   int index = Enumerable.Range(0, LabConfig.DeveloperFields.Count)
    .Single(candidate => LabConfig.DeveloperFields[candidate].Label == label);
   values[index] = value;
  }

  [UnityTest]
  public IEnumerator MainSceneConfigSelectedCrackPresentationUsesStagesAndOneBoundedFinish()
  {
   yield return SceneManager.LoadSceneAsync("Lab", LoadSceneMode.Single);
   yield return null;
   var network = Object.FindFirstObjectByType<LabHud>().network;
   var fixture = Object.Instantiate(network.config);
   float[] values = fixture.CaptureDeveloperValues();
   SetTuning(values, "Gourd Mode (0 paint, 1 crack)", 1f);
   SetTuning(values, "Gourd Bomb Interval Min (seconds, 0 = off)", 0f);
   SetTuning(values, "Gourd Bomb Interval Max (seconds, 0 = off)", 0f);
   Assert.That(fixture.TryApplyDeveloperValues(values, out string error), Is.True, error);
   try
   {
    Assert.That(network.ConfigureHostSession(fixture, 2), Is.True, network.Notice);
    yield return null;
    var c = network.target.GetComponent<LabGourdCrack>();
    var paint = network.target.GetComponent<LabGourdPaint>();
    Assert.That(c.IsReady, Is.True, c.InitializationError);
    Assert.That(paint.enabled, Is.False);
    Assert.That(c.target.Hitbox.enabled, Is.True);
    Assert.That(c.VisualInstance.GetComponentsInChildren<Collider>(true), Is.Empty);
    var renderers = c.VisualInstance.GetComponentsInChildren<Renderer>();
    Assert.That(renderers.Any(r => r.sharedMaterials.Contains(c.crackMaterial)), Is.True);
    var camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(v => v.name == "BattleCamera");
    for (int stage = 0; stage <= 3; stage++)
    {
     c.SetStage(stage);
     Assert.That(c.Stage, Is.EqualTo(stage));
     CaptureMainBattle(camera, "preview-stage-" + stage + ".png");
    }
    var finish = c.GetComponent<LabGourdFinish>();
    var victory = new LabSnapshot { sessionId = "finish-fixture", round = 1, phase = LabPhase.Victory, hp = 0 };
    Assert.That(LabGourdFinish.DelayVictory(network, victory), Is.True);
    finish.ObserveSnapshot(victory, .6f);
    Assert.That(finish.IsFinishing, Is.True);
    Assert.That(finish.Complete, Is.False);
    Assert.That(c.target.VisualMount.Find("Gourd Left Half").gameObject.activeSelf, Is.True);
    Assert.That(c.target.VisualMount.Find("Gourd Right Half").gameObject.activeSelf, Is.True);
    Assert.That(c.target.VisualMount.Find("Small Yokai Spirit").gameObject.activeSelf, Is.True);
    finish.Advance(1f);
    Assert.That(finish.Complete, Is.True);
    Assert.That(LabGourdFinish.DelayVictory(network, victory), Is.False);
    int childCount = c.target.VisualMount.childCount;
    finish.ObserveSnapshot(victory, 0f);
    Assert.That(c.target.VisualMount.childCount, Is.EqualTo(childCount), "Repeated snapshot fixture cannot duplicate finish objects.");
    Assert.That(LabGourdFinish.DelayVictory(network, new LabSnapshot { phase = LabPhase.Defeat }), Is.False);
    CaptureMainBattle(camera, "preview-finish.png");
    finish.ObserveSnapshot(null, 0f);
    Assert.That(finish.IsFinishing, Is.False);
    Assert.That(c.VisualInstance.activeSelf, Is.True);
    yield return null;
    Assert.That(c.Stage, Is.Zero, "A lobby without a snapshot resets damage presentation.");
   }
   finally
   {
    network.Stop();
    Object.Destroy(fixture);
   }
  }
  #endif
 }
}
