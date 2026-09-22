# One small edit still needed in `DoodleArenaGame.cs`

Aijia's health bar code (`OnGUI`, `DrawHud`, `DrawBar`, `DrawMiniHealth`) is already sitting in
`DoodleArenaGame.cs` (Aaron's file) — that's fine to leave as is. But her current `OnGUI` only
draws the HUD, not the actual game world. To make `DoodleArenaPresentation.cs` actually show up on
screen, the existing `OnGUI` method in `DoodleArenaGame.cs` needs to change from this:

```csharp
private void OnGUI()
{
    if (state == GameState.Playing ||
        state == GameState.BetweenWave ||
        state == GameState.GameOver ||
        state == GameState.Victory)
    {
        DrawHud();
    }
}
```

to this:

```csharp
private void OnGUI()
{
    if (Event.current.type != EventType.Repaint) return;
    float scale = Mathf.Min(Screen.width / WorldW, Screen.height / WorldH);
    Vector2 offset = new Vector2((Screen.width - WorldW * scale) * .5f, (Screen.height - WorldH * scale) * .5f);
    Matrix4x4 old = GUI.matrix;
    GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one * scale);
    DrawBackground();
    if (state == GameState.Title) DrawTitle();
    else DrawGame();
    GUI.matrix = old;
}
```

`DrawGame()` (defined in the new `DoodleArenaPresentation.cs`) already calls `DrawHud()` internally,
so nothing about the health bars needs to change — this just wires the rest of the visuals
(background, player, enemies, shots, title/game-over screens) back in.

Since this line lives in `DoodleArenaGame.cs`, it's really Aijia's edit to make on her own branch/PR
(she already touched this file once for the health bars) — not something to fold into someone
else's commit.
