using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace UnnamedProject
{
    class Zombie
    {
        public Vector3 Position;
        public float Health = 100f;
        public bool IsAlive = true;

        public Zombie(Vector3 startPos) => Position = startPos;

        public void Update(Vector3 playerPos, List<Zombie> allZombies)
        {
            if (!IsAlive) return;
            Vector3 toPlayer = Vector3.Normalize(playerPos - Position);
            Vector3 newPos = Position + toPlayer * 0.06f;

            // Collision with player (radius 0.4)
            float distToPlayer = Vector3.Distance(newPos, playerPos);
            if (distToPlayer < 0.8f)
                newPos = Position + toPlayer * 0.02f;

            // Collision with other zombies
            foreach (var other in allZombies)
            {
                if (!other.IsAlive || other == this) continue;
                float distToOther = Vector3.Distance(newPos, other.Position);
                if (distToOther < 0.6f)
                {
                    Vector3 away = Vector3.Normalize(newPos - other.Position);
                    if (away.LengthSquared() > 0)
                        newPos += away * 0.03f;
                }
            }

            Position = newPos;
            Position.X = Math.Clamp(Position.X, -49f, 49f);
            Position.Z = Math.Clamp(Position.Z, -49f, 49f);
        }

        public void Draw()
        {
            if (!IsAlive) return;
            Raylib.DrawCube(Position + new Vector3(0, 1f, 0), 0.8f, 1.8f, 0.6f, Color.DarkGreen);
            Raylib.DrawCube(Position + new Vector3(-0.5f, 1.2f, 0), 0.3f, 0.8f, 0.3f, Color.DarkGreen);
            Raylib.DrawCube(Position + new Vector3(0.5f, 1.2f, 0), 0.3f, 0.8f, 0.3f, Color.DarkGreen);
            Raylib.DrawCube(Position + new Vector3(0, 1.7f, 0), 0.5f, 0.5f, 0.5f, Color.Gray);
        }

        public void TakeDamage(float damage)
        {
            Health -= damage;
            if (Health <= 0) IsAlive = false;
        }
    }

    class Program
    {
        static void Main()
        {
            const int screenWidth = 1280;
            const int screenHeight = 720;
            Raylib.InitWindow(screenWidth, screenHeight, "Build to Survive");
            Raylib.DisableCursor();
            Raylib.SetTargetFPS(60);

            Camera3D camera = new Camera3D();
            camera.Up = new Vector3(0f, 1f, 0f);
            camera.FovY = 70f;
            camera.Projection = CameraProjection.Perspective;

            float cameraAngleX = 0f, cameraAngleY = 0f;
            const float mouseSensitivity = 0.003f;

            Vector3 playerPos = new Vector3(0f, 0f, 0f);
            float playerYVelocity = 0f;
            bool isGrounded = true;
            float limbSwingTimer = 0f;

            const float normalSpeed = 0.15f, sprintSpeed = 0.28f;
            const float gravity = -0.015f, jumpForce = 0.35f;

            List<Zombie> zombies = new();
            for (int i = 0; i < 8; i++)
            {
                float angle = (float)(i * Math.PI * 2 / 8);
                Vector3 spawnPos = new Vector3((float)Math.Cos(angle) * 25f, 0, (float)Math.Sin(angle) * 25f);
                zombies.Add(new Zombie(spawnPos));
            }

            float punchCooldown = 0f;
            const float punchRange = 3.5f, punchDamage = 50f;
            int zombiesKilled = 0;

            while (!Raylib.WindowShouldClose())
            {
                punchCooldown -= Raylib.GetFrameTime();

                Vector2 mouseDelta = Raylib.GetMouseDelta();
                cameraAngleX -= mouseDelta.X * mouseSensitivity;
                cameraAngleY -= mouseDelta.Y * mouseSensitivity;
                cameraAngleY = Math.Clamp(cameraAngleY, -1.2f, 1.2f);

                Vector3 forward = new Vector3((float)Math.Sin(cameraAngleX), 0f, (float)Math.Cos(cameraAngleX));
                Vector3 right = new Vector3((float)Math.Sin(cameraAngleX - Math.PI / 2f), 0f, (float)Math.Cos(cameraAngleX - Math.PI / 2f));

                float currentSpeed = normalSpeed;
                if (Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift))
                    currentSpeed = sprintSpeed;

                Vector3 moveDirection = Vector3.Zero;
                bool isMoving = false;
                if (Raylib.IsKeyDown(KeyboardKey.W) || Raylib.IsKeyDown(KeyboardKey.Up)) moveDirection += forward;
                if (Raylib.IsKeyDown(KeyboardKey.S) || Raylib.IsKeyDown(KeyboardKey.Down)) moveDirection -= forward;
                if (Raylib.IsKeyDown(KeyboardKey.A) || Raylib.IsKeyDown(KeyboardKey.Left)) moveDirection -= right;
                if (Raylib.IsKeyDown(KeyboardKey.D) || Raylib.IsKeyDown(KeyboardKey.Right)) moveDirection += right;

                if (moveDirection != Vector3.Zero)
                {
                    moveDirection = Vector3.Normalize(moveDirection);
                    playerPos += moveDirection * currentSpeed;
                    isMoving = true;
                }

                if (isMoving && isGrounded)
                {
                    float swingSpeed = currentSpeed == sprintSpeed ? 16f : 10f;
                    limbSwingTimer += Raylib.GetFrameTime() * swingSpeed;
                }
                else limbSwingTimer = 0f;

                if (isGrounded)
                {
                    playerYVelocity = 0f;
                    if (Raylib.IsKeyPressed(KeyboardKey.Space))
                    {
                        playerYVelocity = jumpForce;
                        isGrounded = false;
                    }
                }
                else playerYVelocity += gravity;

                playerPos.Y += playerYVelocity;
                if (playerPos.Y <= 0f) { playerPos.Y = 0f; isGrounded = true; }

                playerPos.X = Math.Clamp(playerPos.X, -49f, 49f);
                playerPos.Z = Math.Clamp(playerPos.Z, -49f, 49f);

                // Player collision with zombies
                foreach (var zombie in zombies)
                {
                    if (!zombie.IsAlive) continue;
                    float dist = Vector3.Distance(playerPos, zombie.Position);
                    if (dist < 0.8f && dist > 0.01f)
                    {
                        Vector3 pushBack = Vector3.Normalize(playerPos - zombie.Position) * 0.05f;
                        playerPos += pushBack;
                    }
                }

                // Punch attack
                if (Raylib.IsMouseButtonPressed(MouseButton.Left) && punchCooldown <= 0f)
                {
                    Vector3 punchDir = new Vector3(
                        (float)Math.Sin(cameraAngleX) * (float)Math.Cos(cameraAngleY),
                        (float)Math.Sin(cameraAngleY),
                        (float)Math.Cos(cameraAngleX) * (float)Math.Cos(cameraAngleY)
                    );
                    Vector3 punchOrigin = playerPos + new Vector3(0, 1.5f, 0);

                    foreach (var zombie in zombies)
                    {
                        if (!zombie.IsAlive) continue;
                        float dist = Vector3.Distance(punchOrigin, zombie.Position + new Vector3(0, 1f, 0));
                        if (dist < punchRange)
                        {
                            zombie.TakeDamage(punchDamage);
                            if (!zombie.IsAlive) zombiesKilled++;
                        }
                    }
                    punchCooldown = 0.4f;
                }

                foreach (var zombie in zombies) zombie.Update(playerPos, zombies);

                camera.Position = playerPos + new Vector3(0f, 1.65f, 0f);
                camera.Target = camera.Position + new Vector3(
                    (float)(Math.Sin(cameraAngleX) * Math.Cos(cameraAngleY)),
                    (float)Math.Sin(cameraAngleY),
                    (float)(Math.Cos(cameraAngleX) * Math.Cos(cameraAngleY))
                );

                Raylib.BeginDrawing();
                Raylib.ClearBackground(Color.SkyBlue);

                Raylib.BeginMode3D(camera);

                // Grass ground (no grid/wireframe)
                Raylib.DrawCube(new Vector3(0f, -0.5f, 0f), 100f, 1f, 100f, new Color(34, 139, 34, 255));
                Raylib.DrawCube(new Vector3(0f, -0.6f, 0f), 100f, 0.1f, 100f, new Color(50, 100, 30, 255));

                foreach (var zombie in zombies) zombie.Draw();

                Raylib.EndMode3D();

                Raylib.DrawText("BUILD TO SURVIVE: MONSTERS", 25, 25, 20, Color.White);
                Raylib.DrawText("WASD: Move | SHIFT: Sprint | SPACE: Jump | MOUSE: Look | LEFT CLICK: Punch", 25, 60, 14, Color.White);

                int aliveZombies = 0;
                foreach (var z in zombies) if (z.IsAlive) aliveZombies++;
                Raylib.DrawText($"Zombies: {aliveZombies} | Killed: {zombiesKilled}", 25, 85, 14, Color.Red);

                if (punchCooldown > 0f)
                    Raylib.DrawText("PUNCHING!", screenWidth / 2 - 50, screenHeight / 2, 20, Color.Red);

                Raylib.EndDrawing();
            }

            Raylib.CloseWindow();
        }
    }
}
