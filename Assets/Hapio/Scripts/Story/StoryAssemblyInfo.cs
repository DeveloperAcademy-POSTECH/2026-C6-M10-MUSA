using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Hapio.Story.PlayModeTests")]
// The PlayMode suite shortens the #306 start gate through the internal GameStartTiming (GameTestStartTiming).
[assembly: InternalsVisibleTo("Hapio.PlayModeTests")]
// The EditMode suite checks the result hold limits through the internal GameResultHold.
[assembly: InternalsVisibleTo("Hapio.EditModeTests")]
