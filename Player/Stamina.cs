/*
using Godot;
using System;

public class StaminaBar : Node2D
{
	Vector2 stamina = new Vector2(0, 0);
	Player player;
	Sprite[] tokens = new Sprite[0];
	Sprite tokenTemplate;
	float timeStamp = 0;

    public override void _Ready()
    {
        player = (Player)GetParent().GetParent().GetParent();
		stamina = player.Stamina;
		tokenTemplate = (Sprite)FindNode("Token");
		
		tokens = new Sprite[(int)stamina.y];
		for (int i = 0; i < (int)stamina.y; i++)
		{
			tokens[i] = (Sprite)tokenTemplate.Duplicate();
			AddChild(tokens[i]);
			tokens[i].SetOffset(new Vector2(20*i, 0));
			
			AnimationPlayer a = (AnimationPlayer)tokens[i].GetChildren()[0];
			a.Play("Idle Bob");
			a.Advance(i/20f);
		}
    }

    public override void _Process(float delta)
    {
		//if (Input.IsActionJustPressed("CommandUp")) {stamina.x--;}
		stamina = player.Stamina;
		
		//in this case, stamina has dropped and tokens need to be removed
		if (stamina.x < stamina.y && stamina.x < tokens.Length)
		{
			for (int i = (int)stamina.x; i < (int)stamina.y; i++)
			{
				AnimationPlayer a = (AnimationPlayer)tokens[i].GetChildren()[0];
				a.Play("Pop");
			}
		}
    }
}
*/