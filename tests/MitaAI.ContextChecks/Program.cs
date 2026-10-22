using MitaAI.Plugin;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    checks++;
}
bool Unknown(ContextValue value, string reason) => !value.IsKnown && value.Raw == null && value.Reason == reason;
ContextValue Room(out int sources, params RoomObservation[] observations) => GameContextRules.Room(observations, out sources);

// Scene identity.
var scene = GameContextRules.Scene(true, "Scene 3 - WeTogether");
Check(scene.IsKnown && scene.Raw == "Scene 3 - WeTogether" && scene.Reason == "", "loaded scene name is kept raw");
Check(Unknown(GameContextRules.Scene(false, "Scene 3 - WeTogether"), "scene-not-loaded"), "unloaded scene is unknown");
Check(Unknown(GameContextRules.Scene(true, null), "missing"), "missing scene name is unknown");
Check(Unknown(GameContextRules.Scene(true, " "), "empty"), "blank scene name is unknown");

// Room values are preserved exactly, never mapped.
foreach (string raw in new[] { "Kitchen", "kitchen", " Room ", "Туалет", "room_2", new string('x', GameContextRules.MaxRawLength) })
{
    var room = Room(out int sources, new RoomObservation(raw, true));
    Check(room.IsKnown && room.Raw == raw && sources == 1, "room value kept raw: " + raw.Length);
}
Check(Unknown(Room(out _, new RoomObservation(null, true)), "missing"), "null room is unknown");
Check(Unknown(Room(out _, new RoomObservation("", true)), "empty"), "empty room is unknown");
Check(Unknown(Room(out _, new RoomObservation(" \t", true)), "empty"), "whitespace room is unknown");
Check(Unknown(Room(out _, new RoomObservation("Kit\nchen", true)), "invalid"), "control characters are invalid");
Check(Unknown(Room(out _, new RoomObservation(new string('x', GameContextRules.MaxRawLength + 1), true)), "invalid"), "overlong room is invalid");
Check(Unknown(Room(out _, new RoomObservation("Kitchen", true, ReadFailed: true)), "read-failed"), "failed read is unknown");

// Source selection.
Check(Unknown(Room(out int none), "no-source") && none == 0, "no controller is unknown");
Check(Unknown(Room(out int inactive, new RoomObservation("Kitchen", false)), "no-source") && inactive == 0, "ineligible controller is ignored");
var withInactive = Room(out int one, new RoomObservation("Hall", false), new RoomObservation("Kitchen", true), new RoomObservation(null, false));
Check(withInactive.Raw == "Kitchen" && one == 1, "single eligible controller wins over ineligible copies");
Check(Unknown(Room(out int two, new RoomObservation("Kitchen", true), new RoomObservation("Hall", true)), "ambiguous-sources") && two == 2,
    "conflicting controllers are ambiguous");
Check(Unknown(Room(out _, new RoomObservation("Kitchen", true), new RoomObservation("Kitchen", true)), "ambiguous-sources"),
    "duplicate agreeing controllers are still ambiguous");
Check(Unknown(Room(out _, new RoomObservation("Kitchen", true), new RoomObservation(null, true)), "ambiguous-sources"),
    "one valid and one missing controller is ambiguous");

// Snapshot keeps scene and room independent.
var roomOnly = new GameContextSnapshot(ContextValue.Unknown("missing"), ContextValue.Known("Kitchen"), 1, 12.5f);
Check(!roomOnly.Scene.IsKnown && roomOnly.PlayerRoom.Raw == "Kitchen", "known room survives unknown scene");
var sceneOnly = new GameContextSnapshot(scene, ContextValue.Unknown("no-source"), 0, 3f);
Check(sceneOnly.Scene.Raw == "Scene 3 - WeTogether" && !sceneOnly.PlayerRoom.IsKnown, "known scene does not imply a room");
var unavailable = GameContextSnapshot.Unavailable("not-main-thread");
Check(Unknown(unavailable.Scene, "not-main-thread") && Unknown(unavailable.PlayerRoom, "not-main-thread")
    && unavailable.RoomSources == 0 && float.IsNaN(unavailable.CapturedAt), "unavailable snapshot is fully unknown");

// Diagnostic line.
string line = new GameContextSnapshot(scene, ContextValue.Known("Room \"2\"\\"), 1, 0).ToLogLine();
Check(line == "MITAAI_CONTEXT scene=\"Scene 3 - WeTogether\" room=\"Room \\\"2\\\"\\\\\" roomSource=Location34_Communication.roomPlayer roomSources=1",
    "log line quotes raw values");
Check(sceneOnly.ToLogLine() == "MITAAI_CONTEXT scene=\"Scene 3 - WeTogether\" room=unknown roomReason=no-source roomSource=Location34_Communication.roomPlayer roomSources=0",
    "log line reports unknown with reason");
Check(unavailable.ToLogLine().Contains("scene=unknown sceneReason=not-main-thread"), "log line reports unknown scene");
Check(!new GameContextSnapshot(scene, ContextValue.Known("a\u0007b"), 1, 0).ToLogLine().Contains('\u0007'), "log line escapes control characters");

// Capture gate: owner thread and throttled refresh.
var gate = new ContextCaptureGate(0.5f);
Check(gate.OnOwnerThread, "creating thread may capture");
bool otherThread = true;
var thread = new Thread(() => otherThread = gate.OnOwnerThread);
thread.Start();
thread.Join();
Check(!otherThread, "other threads may not capture");
Check(gate.ShouldRefresh(10f), "first capture refreshes");
Check(!gate.ShouldRefresh(10.2f), "repeat within interval reuses the snapshot");
Check(gate.ShouldRefresh(10.5f), "capture after interval refreshes");
Check(gate.ShouldRefresh(4f), "clock moving backwards refreshes");
Check(gate.ShouldRefresh(float.NaN) && !gate.ShouldRefresh(4.1f), "non-finite time refreshes without replacing the last refresh time");
Check(gate.ShouldRefresh(4.2f, 42), "scene change bypasses cache even inside the interval");
Check(!gate.ShouldRefresh(4.3f, 42), "same new scene reuses its cached snapshot");
Check(gate.ShouldRefresh(4.4f, 0), "returning to an earlier scene also refreshes");

Console.WriteLine($"PASS: {checks} game-context snapshot checks (Unity collector not exercised).");
