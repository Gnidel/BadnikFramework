# Cutscenes

`CutsceneController` supports video cutscenes now and real-time cutscenes later.

## Scene setup

Add a `Node` with the `CutsceneController.cs` script and assign:

- `VideoPlayer`: optional `VideoStreamPlayer`. Its `Finished` signal automatically completes the cutscene.
- `PauseOverlay`: optional `Control` containing the pause UI. It is shown only while paused.
- `ResumeButton`: optional button that resumes playback.
- `SkipButton`: optional button that finishes the cutscene as skipped.
- `NextScene`: optional destination scene path, for example `res://Games/Example game/Cutscenes/Credits/Credits.tscn`.

The `any_pause` action pauses and resumes the cutscene. The controller remains processable while the scene tree is paused, so the pause UI remains interactive.

For a real-time cutscene, omit `VideoPlayer` and call `Complete()` when the timeline reaches its end. Connect to the `CutsceneFinished(bool skipped)` signal for custom behavior. If `NextScene` is empty, no scene transition is performed.

When a destination scene is configured, it is requested through Godot's threaded resource loader. The existing `LoadingScreen` is used when present; otherwise the packed scene is changed to directly after loading.