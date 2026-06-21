using Raylib_cs;
using static Raylib_cs.Raylib;

SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.Msaa4xHint);
InitWindow(1280, 720, "BuildToSurviveMonsters");
SetTargetFPS(60);

var game = new Game();
game.Init();

while (!WindowShouldClose())
{
    float dt = GetFrameTime();
    game.Update(dt);
    game.Draw();
}

CloseWindow();
