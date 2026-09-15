using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C6.Prototype.Orbs;
using UnityEngine;
using UnityEngine.EventSystems;

namespace C6.Prototype.Attack
{
    /// <summary>Explicit desktop development evidence. Never a substitute for physical iPhone drags.</summary>
    [DisallowMultipleComponent]
    public sealed class T06Probe : MonoBehaviour
    {
        [SerializeField] private T06AttackController controller;
        public void Configure(T06AttackController owner) => controller = owner;
#if DEVELOPMENT_BUILD && UNITY_STANDALONE && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachOnlyForExplicitDevelopmentProbe()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-c6T06ProbeDirectory") < 0) return;
            var owner = FindAnyObjectByType<T06AttackController>();
            if (owner == null) return;
            var probe = owner.GetComponent<T06Probe>();
            if (probe == null) probe = owner.gameObject.AddComponent<T06Probe>();
            probe.Configure(owner);
        }

        private ProbeReport report;
        private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        private readonly List<ShotEvidence> shots = new List<ShotEvidence>();
        private readonly List<HitEvidence> hits = new List<HitEvidence>();
        private readonly List<string> runtimeErrors = new List<string>();
        private string output, role, host, port;
        private bool quit, networkProbe, hooked;

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, "-c6T06ProbeDirectory") < 0) yield break;
            report = new ProbeReport { startedAtUtc = DateTime.UtcNow.ToString("O"), buildGuid = Application.buildGUID };
            string error = null;
            try { Initialize(args); }
            catch (Exception exception) { error = Describe(exception); }
            if (error != null) { Finish(error); yield break; }
            // Only this explicit development probe continues when its window loses focus.
            Application.runInBackground = true;
            Application.logMessageReceived += ObserveLog;
            controller.Attack.Changed += ObserveState;
            controller.Attack.ValidHit += ObserveHit;
            hooked = true;

            // Flatten nested coroutines so exceptions from any probe phase become a FAIL
            // report rather than silently stopping an iterator and leaving false evidence.
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            while (stack.Count > 0 && error == null)
            {
                object yielded = null;
                bool more = false;
                try
                {
                    if (runtimeErrors.Count > 0) throw new InvalidOperationException("Runtime error: " + runtimeErrors[0]);
                    more = stack.Peek().MoveNext();
                    if (more) yielded = stack.Peek().Current;
                    else stack.Pop();
                }
                catch (Exception exception) { error = Describe(exception); }
                if (error != null) break;
                if (!more) continue;
                if (yielded is IEnumerator nested) stack.Push(nested);
                else yield return yielded;
            }
            Finish(error);
        }

        private void Initialize(string[] args)
        {
            quit = Array.IndexOf(args, "-c6T06ProbeQuit") >= 0;
            networkProbe = Array.IndexOf(args, "-c6T06NetworkProbe") >= 0;
            string requested = Argument(args, "-c6T06ProbeDirectory", null);
            Require(!string.IsNullOrWhiteSpace(requested) && Path.IsPathRooted(requested), "Probe directory must be absolute.");
            string candidate = Path.GetFullPath(requested);
            Require(!File.Exists(candidate), "Probe path is already a file.");
            Require(!Directory.Exists(candidate) || !Directory.EnumerateFileSystemEntries(candidate).Any(),
                "Use a new empty probe directory to preserve earlier evidence.");
            Directory.CreateDirectory(candidate);
            output = candidate;
            role = Argument(args, "-c6T06Role", "host").ToLowerInvariant();
            Require(role == "host" || role == "client" || role == "observer", "Role must be host, client or observer.");
            Require(role != "client" || networkProbe, "Client input requires the explicit -c6T06NetworkProbe flag.");
            host = Argument(args, "-c6T06Host", "127.0.0.1");
            port = Argument(args, "-c6T06Port", role == "observer" ? "7777" : "25066");
            Require(ushort.TryParse(port, out ushort parsedPort) && parsedPort > 0, "Invalid probe port.");
            if (controller == null) controller = GetComponent<T06AttackController>();
            Require(controller != null && controller.Attack != null, "Missing configured T06 attack controller/session.");
            report.role = role;
            report.hostAddress = host;
            report.port = port;
            report.networkProbe = networkProbe;
            report.mode = role == "observer" ? "READ_ONLY_NETWORK_OBSERVER" : networkProbe ? "DEV_NETWORK_DEBUG_POINTER" : "DEV_HOST_DEBUG_POINTER";
        }

        private IEnumerator Run()
        {
            for (int i = 0; i < 8; i++) yield return null;
            report.screenWidth = Screen.width; report.screenHeight = Screen.height; report.safeArea = Screen.safeArea;
            report.deviceModel = SystemInfo.deviceModel; report.operatingSystem = SystemInfo.operatingSystem;
            report.unityVersion = Application.unityVersion; report.applicationVersion = Application.version;
            report.lowerViewport = controller.Layout.BottomPixelRect;
            var config = controller.Layout.Config;
            report.attackZone = OrbGestureEngine.AttackZone(report.lowerViewport, config.AttackZoneHeightFraction);
            report.upperFraction = config.UpperFraction;
            report.horizontalSwipeFraction = config.HorizontalSwipeFraction;
            report.attackZoneHeightFraction = config.AttackZoneHeightFraction;
            report.projectileSpeed = config.ProjectileSpeed; report.projectileLifetime = config.ProjectileLifetime;
            report.projectileRadius = config.ProjectileRadius; report.launchOrigin = config.LaunchOrigin;
            report.launchWidth = config.LaunchWidth; report.launchAim = config.LaunchAim;
            report.configMaxHp = config.MonsterMaxHp;
            report.configBaseDamage = config.BaseDamage;
            report.canvasCount = FindObjectsByType<Canvas>().Length;
            report.audioListenerCount = FindObjectsByType<AudioListener>().Length;
            report.eventSystemCount = FindObjectsByType<EventSystem>().Length;
            Require(report.canvasCount == 1 && report.audioListenerCount == 1 && report.eventSystemCount == 1,
                "Duplicate or missing presentation loop.");
            bool started = role == "host" ? controller.StartDevelopmentHost(port) : controller.JoinDevelopmentHost(host, port);
            Require(started, "The explicit development network attempt was refused.");
            yield return WaitFor(() => controller.Attack.Connected && controller.Attack.Snapshot != null && controller.Views.Count == 6,
                20f, "Development session did not connect with six local fixture views.");
            report.actualHost = controller.Attack.IsHost;
            report.localPlayerId = controller.Attack.LocalPlayerId;
            Require(report.actualHost == (role == "host"), "Actual network authority does not match the requested probe role.");
            if (role != "host") Require(report.localPlayerId != 0 && FindObjectsByType<Rigidbody>().Length == 0,
                "A client must have a remote owner ID and no simulated Rigidbody.");
            if (networkProbe) yield return WaitFor(() => controller.Attack.Snapshot.orbs.Length == 12, 30f,
                "Both participants did not register their six-fixture development sets.");
            for (int i = 0; i < 4; i++) yield return null;
            report.fixture = Snapshot();
            report.sessionId = report.fixture.sessionId;
            report.initialRoundId = report.fixture.roundId;
            report.initialLocalOrbIds = controller.Views.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            Require(report.initialLocalOrbIds.Length == 6 && report.fixture.orbs.Count(orb => orb.owner == report.localPlayerId
                && orb.kind == (int)OrbKind.Combined) == 5, "The explicit local fixture is not five Combined plus one Raw.");
            yield return Capture("fixture.png");
            if (role == "observer") yield return ObservePhysicalSession();
            else if (networkProbe) yield return NetworkSequence();
            else yield return SingleHostSequence();

            report.touchBegins = controller.TouchBegins;
            report.touchLaunches = controller.TouchLaunches;
            report.finalRigidbodyCount = FindObjectsByType<Rigidbody>().Length;
            Require(report.touchBegins == 0 && report.touchLaunches == 0,
                "A debug or observer run cannot be mislabeled when actual/queued Touch occurs locally.");
            Require(role != "observer" || report.debugPointerBegins == 0, "Observer unexpectedly injected input.");
            int expectedShots = role == "observer" ? 0 : !networkProbe ? 5 : role == "host" ? 2 : 3;
            Require(shots.Count == expectedShots && report.debugPointerBegins == expectedShots, "Unexpected number of injected/approved probe actions.");
            Require(hits.Count == (role == "host" ? 5 : 0), "Host physics result events differ from the expected five-hit probe; clients cannot apply hits.");
            if (role != "host") Require(FindObjectsByType<Rigidbody>().Length == 0, "Client created an authoritative physics body.");
            report.pngsComplete = CompletePng(Path.Combine(output, "fixture.png")) && CompletePng(Path.Combine(output, "result.png"))
                && (role == "observer" || networkProbe || CompletePng(Path.Combine(output, "flight.png")));
            Require(report.pngsComplete, "A required PNG did not finish saving.");
            // A network result is recorded before either endpoint disconnects. The Host gives
            // the client longer to receive and save its own independently observed result.
            if (networkProbe && role == "host") yield return Pause(3f);
            else if (networkProbe) yield return Pause(1f);
            controller.EndDevelopmentTest();
            yield return WaitFor(() => !controller.Attack.Connected && controller.Views.Count == 0
                && FindObjectsByType<HostProjectile3D>().Length == 0, 8f, "Confirmed End did not clear owned session physics/views.");
            // The service intentionally preserves an empty terminal snapshot for status UI.
            // That display receipt is not a live registry, authority, connection or projectile.
            var terminal = controller.Attack.Snapshot;
            report.endCleanup = !controller.Gestures.HasActivePointer && !controller.Gestures.HasPending
                && controller.Attack.Registry == null && controller.Attack.Authority == null
                && controller.Attack.ActiveProjectileCount == 0
                && (terminal == null || ((terminal.state == "Ended" || terminal.state == "NetworkError")
                    && terminal.orbs != null && terminal.orbs.Length == 0
                    && terminal.projectiles != null && terminal.projectiles.Length == 0));
            report.terminal = terminal == null ? null : Snapshot();
            Require(report.endCleanup, "End left a pointer, pending decision, authority, registry or nonempty live snapshot.");
        }

        private IEnumerator SingleHostSequence()
        {
            Require(report.fixture.hp == 100 && report.fixture.totalHits == 0, "Single-host probe requires a fresh default-HP round.");
            yield return FireOne(80, true);
            report.afterFirstHit = Snapshot();
            Require(report.afterFirstHit.hp == 80 && report.afterFirstHit.totalHits == 1, "First actual collision did not apply one 20-damage hit.");
            for (int expectedHp = 60; expectedHp >= 0; expectedHp -= 20) yield return FireOne(expectedHp, false);
            report.final = Snapshot();
            Require(report.final.hp == 0 && report.final.totalHits == 5 && report.final.state == "TargetCleared" && !controller.CanInteract,
                "Five actual collisions did not clear the target and stop interaction.");
            yield return Capture("result.png");
            controller.ResetDevelopmentRound();
            yield return WaitFor(() => controller.Attack.Snapshot.roundId > report.initialRoundId && controller.Views.Count == 6,
                5f, "Explicit development reset did not create a fresh round.");
            report.afterReset = Snapshot();
            report.resetVerified = report.afterReset.hp == 100 && report.afterReset.totalHits == 5 && report.afterReset.resets == 1
                && report.afterReset.state == "Playing" && !controller.Views.Keys.Intersect(report.initialLocalOrbIds).Any()
                && FindObjectsByType<HostProjectile3D>().Length == 0;
            Require(report.resetVerified, "Explicit reset failed to restore HP, fresh IDs, or clear physics.");
        }

        private IEnumerator NetworkSequence()
        {
            if (role == "host")
            {
                Require(report.fixture.hp == 100, "Network host probe requires a fresh target.");
                yield return FireOne(80, false);
                yield return FireOne(60, false);
                yield return WaitFor(() => controller.Attack.Snapshot.hp == 0 && controller.Attack.Snapshot.totalHits == 5,
                    45f, "The remote client did not contribute its three actual host-simulated hits.");
            }
            else
            {
                yield return WaitFor(() => controller.Attack.Snapshot.hp == 60 && controller.Attack.Snapshot.totalHits == 2,
                    30f, "Client did not receive the two Host hits before its turn.");
                yield return FireOne(40, false);
                yield return FireOne(20, false);
                yield return FireOne(0, false);
            }
            report.final = Snapshot();
            var consumed = report.final.orbs.Where(orb => orb.state == (int)OrbAuthorityState.Consumed).ToArray();
            report.networkOwnerSplitVerified = consumed.Count(orb => orb.owner == 0) == 2
                && consumed.Count(orb => orb.owner != 0) == 3 && report.final.orbs.Select(orb => orb.owner).Distinct().Count() == 2;
            Require(report.final.sessionId == report.sessionId && report.final.roundId == report.initialRoundId
                && report.final.hp == 0 && report.final.totalHits == 5 && report.final.state == "TargetCleared"
                && report.networkOwnerSplitVerified, "Final network result or attacker split differs from the 2+3 scenario.");
            report.finalRigidbodyCount = FindObjectsByType<Rigidbody>().Length;
            yield return Capture("result.png");
            report.finalSavedAtUtc = DateTime.UtcNow.ToString("O");
        }

        private IEnumerator ObservePhysicalSession()
        {
            // This role never calls Begin/Move/EndPointer, never requests launch, and never
            // resets the Host target. It only receives the Host's ordinary confirmed state.
            report.observerStartingTotalHits = report.fixture.totalHits;
            Require(report.observerStartingTotalHits == 0, "Observer must join before the twenty-hit physical attempt begins.");
            yield return WaitFor(() => controller.Attack.Snapshot != null && controller.Attack.Snapshot.totalHits >= 20,
                20f * 60f, "Observer did not receive 20 confirmed Host hits within twenty minutes.");
            report.final = Snapshot();
            Require(report.final.sessionId == report.sessionId, "Observer session changed during the physical-device attempt.");
            Require(report.final.totalHits >= 20 && report.final.resets >= 3 && FindObjectsByType<Rigidbody>().Length == 0,
                "Observer did not receive twenty hits with explicit target resets and zero local physics.");
            report.observerReceivedTwentyHits = true;
            report.finalRigidbodyCount = FindObjectsByType<Rigidbody>().Length;
            yield return Capture("result.png");
            report.finalSavedAtUtc = DateTime.UtcNow.ToString("O");
            report.observerWaitedForHostEnd = true;
            Debug.Log("C6_T06_OBSERVER_TWENTY_RECEIVED session=" + report.final.sessionId
                + " round=" + report.final.roundId + " totalHits=" + report.final.totalHits
                + " resets=" + report.final.resets + " waitingForHostEnd=true inputInjection=0");
            // Preserve the Host's result screen until the person has inspected it and
            // pressed END there. Receiving twenty hits must not disconnect the observer
            // automatically, because that would turn the iPhone's result into a network error.
            yield return WaitFor(() => !controller.Attack.Connected, 20f * 60f,
                "Twenty hits were captured, but the Host did not end the session within twenty minutes.");
            report.observerPeerDisconnected = !controller.Attack.Connected;
            report.observerPeerDisconnectedAtUtc = DateTime.UtcNow.ToString("O");
            report.observerDisconnectMessage = controller.Attack.Connection.Message;
        }

        private IEnumerator FireOne(int expectedHp, bool captureFlight)
        {
            Require(role != "observer", "Observer cannot inject input.");
            Require(controller.CanInteract, "The confirmed battle state does not permit input.");
            var orb = controller.Attack.Snapshot.orbs.Where(value => value.owner == controller.Attack.LocalPlayerId
                && value.kind == (int)OrbKind.Combined && value.state == (int)OrbAuthorityState.Idle)
                .OrderBy(value => value.pos.x).FirstOrDefault();
            Require(orb != null && controller.Views.ContainsKey(orb.id), "No confirmed local Combined fixture remains.");
            int pointerId = -6006 - report.debugPointerBegins;
            Vector2 start = controller.GetViewScreenPosition(orb.id);
            Rect zone = OrbGestureEngine.AttackZone(controller.Layout.BottomPixelRect, controller.Layout.Config.AttackZoneHeightFraction);
            Vector2 destination = new Vector2(start.x, zone.yMin + Mathf.Min(5f, zone.height * .5f));
            int hpBefore = controller.Attack.Snapshot.hp;
            Require(hpBefore == expectedHp + 20, "Unexpected HP before the next controlled probe shot.");
            Require(controller.BeginPointer(pointerId, start, false), "Debug pointer could not select the expected Combined view.");
            report.debugPointerBegins++;
            controller.MovePointer(pointerId, destination);
            controller.EndPointer(pointerId, destination);
            yield return WaitFor(() => !controller.Views.ContainsKey(orb.id) && controller.Attack.LastResult != null
                && controller.Attack.LastResult.orbId == orb.id && controller.Attack.LastResult.known, 5f, "Host did not confirm the launch request.");
            Require(controller.Attack.LastResult.accepted, "Host rejected the probe launch: " + controller.Attack.LastResult.reason);
            var shot = new ShotEvidence
            {
                orbId = orb.id, owner = orb.owner, source = "DEBUG_POINTER", pointerId = pointerId,
                requestId = controller.Attack.LastResult.requestId, approved = true, removed2D = !controller.Views.ContainsKey(orb.id),
                hpBefore = hpBefore, rigidbodyCountAtApproval = FindObjectsByType<Rigidbody>().Length,
                stateAtApproval = controller.Attack.Snapshot.orbs.Single(value => value.id == orb.id).state
            };
            shots.Add(shot);
            if (role == "host") Require(shot.stateAtApproval == (int)OrbAuthorityState.Projectile
                && controller.Attack.Snapshot.hp == hpBefore, "Host approval did not preserve an actual flight phase before damage.");
            else Require(shot.rigidbodyCountAtApproval == 0, "Client approval created a simulated Rigidbody.");
            if (captureFlight)
            {
                var projectile = FindObjectsByType<HostProjectile3D>().Single(value => value.OrbId == orb.id);
                report.flightInitialProjection = Projection(projectile, "launch-approval");
                report.flight = Snapshot();
                // Capture real movement after at least 0.10 seconds of physics. The launch
                // approval snapshot/projection is retained separately even if it begins below
                // the viewport; a merely existing Rigidbody is not a useful flight photo.
                yield return WaitFor(() => projectile == null || projectile.HasCompleted || projectile.ElapsedPhysicsTime >= .10f,
                    2f, "The projectile did not advance to the mid-flight capture time.");
                Require(projectile != null && !projectile.HasCompleted, "The projectile ended before mid-flight could be captured.");
                yield return Capture("flight.png", projectile);
                Require(report.flightCaptureHadActiveProjectile, "The flight capture was taken after the actual projectile ended.");
                Require(report.flightCaptureProjection != null && report.flightCaptureProjection.elapsedPhysicsTime >= .10f
                    && report.flightCaptureProjection.sphereFullyWithinViewport,
                    "The actual mid-flight sphere is not fully visible in the named battle camera viewport.");
            }
            yield return WaitFor(() => controller.Attack.Snapshot != null && controller.Attack.Snapshot.hp == expectedHp,
                5f, "Approved projectile did not produce its expected actual Host collision.");
            var consumed = controller.Attack.Snapshot.orbs.Single(value => value.id == orb.id);
            shot.hpAfter = controller.Attack.Snapshot.hp;
            shot.finalState = consumed.state;
            Require(consumed.state == (int)OrbAuthorityState.Consumed && consumed.owner == orb.owner,
                "The same logical projectile ID and attacker did not survive through consumption.");
            yield return null;
        }

        private IEnumerator Capture(string filename, HostProjectile3D flightProjectile = null)
        {
            string path = Path.Combine(output, filename);
            Require(!File.Exists(path), "Refusing to overwrite a probe image.");
            yield return new WaitForEndOfFrame();
            if (filename == "flight.png")
            {
                report.flightCaptureHadActiveProjectile = flightProjectile != null && !flightProjectile.HasCompleted;
                Require(report.flightCaptureHadActiveProjectile, "The actual projectile ended before the rendered capture frame.");
                report.flightCaptureProjection = Projection(flightProjectile, "mid-flight-screenshot");
                report.midFlight = Snapshot();
            }
            ScreenCapture.CaptureScreenshot(path);
            yield return WaitFor(() => CompletePng(path), 10f, "PNG did not finish saving: " + filename);
        }

        private ProjectionEvidence Projection(HostProjectile3D projectile, string stage)
        {
            // Diagnostics only: gameplay launch still receives normalized X and an explicit
            // world basis. It never consumes Camera.main, these pixels, or this viewport.
            var camera = controller.Layout.BattleCamera;
            Require(camera != null && projectile != null && projectile.HitCollider != null, "Missing projection diagnostic references.");
            var collider = projectile.HitCollider;
            Vector3 scale = collider.transform.lossyScale;
            float radius = collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Vector3 renderCenter = collider.transform.TransformPoint(collider.center);
            Vector3 screen = camera.WorldToScreenPoint(renderCenter);
            Vector3 horizontal = camera.WorldToScreenPoint(renderCenter + camera.transform.right * radius);
            Vector3 vertical = camera.WorldToScreenPoint(renderCenter + camera.transform.up * radius);
            float radiusX = Mathf.Abs(horizontal.x - screen.x), radiusY = Mathf.Abs(vertical.y - screen.y);
            var bounds = new Rect(screen.x - radiusX, screen.y - radiusY, radiusX * 2f, radiusY * 2f);
            Rect viewport = camera.pixelRect;
            return new ProjectionEvidence
            {
                stage = stage, orbId = projectile.OrbId, cameraName = camera.name,
                elapsedPhysicsTime = projectile.ElapsedPhysicsTime,
                bodyWorldPosition = projectile.Body.position, renderedSphereCenter = renderCenter,
                sphereCenterScreen = screen, bodyCenterScreen = camera.WorldToScreenPoint(projectile.Body.position),
                colliderRadiusWorld = radius, radiusPixelsX = radiusX, radiusPixelsY = radiusY,
                viewportPixels = viewport, sphereBoundsPixels = bounds,
                centerWithinViewport = screen.z > camera.nearClipPlane && viewport.Contains(new Vector2(screen.x, screen.y)),
                sphereIntersectsViewport = screen.z > camera.nearClipPlane && viewport.Overlaps(bounds),
                sphereFullyWithinViewport = screen.z > camera.nearClipPlane && bounds.xMin >= viewport.xMin
                    && bounds.xMax <= viewport.xMax && bounds.yMin >= viewport.yMin && bounds.yMax <= viewport.yMax,
                expectedLowerOrbRadiusPixels = Screen.width * controller.Layout.Config.OrbRadiusScreenFraction
            };
        }

        private IEnumerator WaitFor(Func<bool> condition, float seconds, string failure)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                if (runtimeErrors.Count > 0) throw new InvalidOperationException("Runtime error: " + runtimeErrors[0]);
                if (controller.Attack.Connection.State == C6.Prototype.Networking.DirectConnectionState.Failed)
                    throw new InvalidOperationException("Network failure: " + controller.Attack.Connection.Message);
                yield return null;
            }
            Require(condition(), failure);
        }

        private static IEnumerator Pause(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }

        private AttackSnapshot Snapshot()
        {
            Require(controller.Attack.Snapshot != null, "No confirmed snapshot is available.");
            return JsonUtility.FromJson<AttackSnapshot>(JsonUtility.ToJson(controller.Attack.Snapshot));
        }

        private void ObserveState()
        {
            var state = controller.Attack.Snapshot;
            if (state == null || checkpoints.Count >= 128) return;
            var last = checkpoints.Count == 0 ? null : checkpoints[checkpoints.Count - 1];
            if (last != null && last.round == state.roundId && last.hp == state.hp && last.totalHits == state.totalHits && last.state == state.state) return;
            checkpoints.Add(new Checkpoint { atUtc = DateTime.UtcNow.ToString("O"), sessionId = state.sessionId,
                round = state.roundId, revision = state.revision, hp = state.hp, totalHits = state.totalHits,
                resets = state.resets, state = state.state, localRigidbodyCount = FindObjectsByType<Rigidbody>().Length });
        }

        private void ObserveHit(AttackHitResult hit)
        {
            if (hit == null || hits.Count >= 64) return;
            hits.Add(new HitEvidence { orbId = hit.OrbId, attacker = hit.AttackerPlayerId, round = hit.RoundId,
                hpBefore = hit.HpBefore, hpAfter = hit.HpAfter, damage = hit.Damage, applied = hit.Applied });
        }

        private void ObserveLog(string message, string stackTrace, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && runtimeErrors.Count < 16)
                runtimeErrors.Add(message == null ? "Unknown runtime error" : message.Substring(0, Math.Min(600, message.Length)));
        }

        private void Finish(string error)
        {
            if (hooked && controller != null && controller.Attack != null)
            {
                controller.Attack.Changed -= ObserveState;
                controller.Attack.ValidHit -= ObserveHit;
            }
            Application.logMessageReceived -= ObserveLog;
            hooked = false;
            if (error == null && runtimeErrors.Count > 0) error = runtimeErrors[0];
            if (error != null && controller != null && controller.Attack != null)
            {
                try { controller.EndDevelopmentTest(); }
                catch (Exception cleanupError) { error += " / cleanup: " + Describe(cleanupError); }
            }
            if (report != null)
            {
                if (controller != null) { report.touchBegins = controller.TouchBegins; report.touchLaunches = controller.TouchLaunches; }
                report.result = error == null ? "PASS" : "FAIL";
                report.completedAtUtc = DateTime.UtcNow.ToString("O");
                report.detail = error ?? (role == "observer"
                    ? "Received confirmed Host hit/reset state without injecting input. Physical finger evidence must come from device logs and the user's observation."
                    : "Actual desktop development runtime, real Host physics, explicit debug pointer calls. PNG completeness is separate from visual review.");
                report.checkpoints = checkpoints.ToArray(); report.shots = shots.ToArray(); report.localHostHits = hits.ToArray();
                report.runtimeErrors = runtimeErrors.ToArray();
                if (output != null)
                {
                    try
                    {
                        using (var stream = new FileStream(Path.Combine(output, "probe.json"), FileMode.CreateNew, FileAccess.Write))
                        using (var writer = new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report, true));
                    }
                    catch (Exception exception) { error = "Could not save probe report: " + Describe(exception); }
                }
            }
            if (error == null) Debug.Log("C6_T06_PROBE PASS role=" + role + " output=" + output);
            else Debug.LogError("C6_T06_PROBE FAIL " + error);
            if (quit) Application.Quit(error == null ? 0 : 1);
        }

        private static string Argument(string[] args, string key, string fallback)
        {
            int index = Array.IndexOf(args, key);
            if (index < 0) return fallback;
            if (index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                throw new ArgumentException("Missing value for " + key);
            return args[index + 1];
        }
        private static string Describe(Exception exception) => exception.GetType().Name + ": " + exception.Message;
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static bool CompletePng(string path)
        {
            try
            {
                using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (file.Length < 20) return false;
                    byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
                    foreach (byte value in signature) if (file.ReadByte() != value) return false;
                    file.Seek(-12, SeekOrigin.End);
                    byte[] ending = { 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 };
                    foreach (byte value in ending) if (file.ReadByte() != value) return false;
                    return true;
                }
            }
            catch (IOException) { return false; }
        }

        [Serializable] private sealed class Checkpoint
        {
            public string atUtc, sessionId, state;
            public uint round;
            public ulong revision;
            public int hp, totalHits, resets, localRigidbodyCount;
        }
        [Serializable] private sealed class HitEvidence
        {
            public string orbId;
            public ulong attacker;
            public uint round;
            public int hpBefore, hpAfter, damage;
            public bool applied;
        }
        [Serializable] private sealed class ShotEvidence
        {
            public string orbId, source, requestId;
            public ulong owner;
            public int pointerId, hpBefore, hpAfter, stateAtApproval, finalState, rigidbodyCountAtApproval;
            public bool approved, removed2D;
        }
        [Serializable] private sealed class ProjectionEvidence
        {
            public string stage, orbId, cameraName;
            public float elapsedPhysicsTime, colliderRadiusWorld, radiusPixelsX, radiusPixelsY, expectedLowerOrbRadiusPixels;
            public Vector3 bodyWorldPosition, renderedSphereCenter, sphereCenterScreen, bodyCenterScreen;
            public Rect viewportPixels, sphereBoundsPixels;
            public bool centerWithinViewport, sphereIntersectsViewport, sphereFullyWithinViewport;
        }
        [Serializable] private sealed class ProbeReport
        {
            public string task = "T06", buildNumber = "8", physicalFingerValidation = "NOT_RUN_BY_THIS_PROBE";
            public string buildGuid, startedAtUtc, completedAtUtc, finalSavedAtUtc, mode, role, hostAddress, port, sessionId, result, detail,
                deviceModel, operatingSystem, unityVersion, applicationVersion, observerPeerDisconnectedAtUtc, observerDisconnectMessage;
            public int screenWidth, screenHeight, canvasCount, audioListenerCount, eventSystemCount, configMaxHp, configBaseDamage,
                debugPointerBegins, touchBegins, touchLaunches, finalRigidbodyCount, observerStartingTotalHits;
            public ulong localPlayerId;
            public uint initialRoundId;
            public Rect safeArea, lowerViewport, attackZone;
            public Vector3 launchOrigin, launchAim;
            public float upperFraction, horizontalSwipeFraction, attackZoneHeightFraction, projectileSpeed, projectileLifetime, projectileRadius, launchWidth;
            public bool networkProbe, actualHost, pngsComplete, endCleanup, resetVerified, networkOwnerSplitVerified,
                flightCaptureHadActiveProjectile, observerReceivedTwentyHits, observerWaitedForHostEnd, observerPeerDisconnected;
            public string[] initialLocalOrbIds, runtimeErrors;
            public AttackSnapshot fixture, flight, midFlight, afterFirstHit, final, afterReset, terminal;
            public ProjectionEvidence flightInitialProjection, flightCaptureProjection;
            public Checkpoint[] checkpoints;
            public ShotEvidence[] shots;
            public HitEvidence[] localHostHits;
        }
#endif
    }
}
