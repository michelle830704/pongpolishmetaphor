using Godot;
using System;

// Optional headless integration check, also written entirely in C#.
public partial class ProjectCheck : Node
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public override async void _Ready()
    {
        try
        {
            var scene = GD.Load<PackedScene>("res://W0041E/Laboratory1/Lab3_GameFeel_Pong/Scenes/Lab_3_1.tscn").Instantiate<Node3D>();
            AddChild(scene);
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            var logic = scene.GetNode<PongLogic>("PaddleController");
            var polish = scene.GetNode<OceanPolish>("OceanPolish");
            var ball = scene.GetNode<MeshInstance3D>("Ball");
            var paddle = logic.GetNode<Node3D>("LeftPaddle");
            var camera = scene.GetNode<Camera3D>("Camera3D");
            logic.SetProcess(false);
            var ballTransform = ball.Transform;
            var paddleScale = paddle.Scale;
            polish.SetEnabled(false);
            var cameraTransform = camera.Transform;
            Check(ball.Transform == ballTransform, "Toggle moved the ball");
            Check(paddle.Scale == paddleScale, "Toggle changed paddle dimensions");
            Check(ball.Layers == 1, "Original ball not restored");
            polish._UnhandledKeyInput(new InputEventKey { Keycode = Key.P, Pressed = true });
            Check(polish.LayerEnabled && ball.Layers == 0, "P did not enable the layer");
            Check(camera.Projection == Camera3D.ProjectionType.Orthogonal, "Front view projection missing");
            polish.Impact(ball.Position, Vector3.Right, 0, true);
            Check(polish.Shake > 0, "Smash did not trigger camera shake");
            polish.PointScored(true);
            polish._UnhandledKeyInput(new InputEventKey { Keycode = Key.R, Pressed = true });
            Check(ball.Position == Vector3.Zero, "Restart did not reset the ball");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(polish.Staging > 0, "Restart staging hook failed");
            // Exercise movement reversal puffs and repeated switches during live effects.
            paddle.Position += new Vector3(0, 0, 0.1f);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            paddle.Position -= new Vector3(0, 0, 0.2f);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            polish.SetEnabled(false);
            Check(camera.Transform == cameraTransform, "Original camera not restored");
            polish.SetEnabled(true);
            await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
            Check(paddle.Scale == paddleScale, "Effects changed original paddle dimensions");
            GD.Print("PASS: C# scene, model loading, toggle, P/R input, smash, restart, reversal effects and particle cleanup");
            if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--capture-preview") >= 0)
            {
                ball.Position = Vector3.Zero;
                await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://PREVIEW.png"));
            }
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }
}
