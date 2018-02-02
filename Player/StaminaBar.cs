using Godot;
using System;

public class StaminaBar : Node2D
{
	Vector2 stamina = new Vector2(0, 0);
	Player player;
	Sprite[] tokens = new Sprite[0];

    public override void _Ready()
    {
        player = (Player)GetParent().GetParent().GetParent();
		stamina = player.Stamina;
		Sprite tokenTemplate = (Sprite)FindNode("Token");
		
		tokens = new Sprite[(int)stamina.y];
		for (int i = 0; i < (int)stamina.y; i++)
		{
			tokens[i] = (Sprite)tokenTemplate.Duplicate();
			AddChild(tokens[i]);
			tokens[i].SetName("Stamina Token " + (i+1));
			tokens[i].SetOffset(new Vector2(20*i, 0));
			
			AnimationPlayer a = (AnimationPlayer)tokens[i].GetChildren()[0];
			a.Play("Idle Bob");
			a.Advance(i/20f);
		}
		
		tokenTemplate.Free();
    }

    public override void _Process(float delta)
    {
		stamina = player.Stamina + new Vector2((int)player.Fatigue, 0);
		if (stamina.x > stamina.y) { stamina.x = stamina.y; }
		else if (stamina.x < (int)player.Fatigue) { stamina.x = (int)player.Fatigue; }
		
		//first case, max stamina has been increased. New token is needed, and array must be corrected before other code can run.
		if (stamina.y > tokens.Length)
		{
			Sprite[] temp = new Sprite[(int)stamina.y];
			for (int i = 0; i < stamina.y; i++)
			{
				if (i < tokens.Length) { temp[i] = tokens[i]; }
				else
				{
					temp[i] = (Sprite)tokens[0].Duplicate();
					AddChild(temp[i]);
					temp[i].SetName("Stamina Token " + (i+1));
					temp[i].SetOffset(new Vector2(20*i, 0));
					temp[i].Frame = 9;
					AnimationPlayer a = (AnimationPlayer)temp[i].GetChildren()[0];
					a.Play("Pop In");
				}
			}
			tokens = temp;
		}
		
		//in this case, stamina has dropped and tokens need to be removed
		if (stamina.x < stamina.y && stamina.x < tokens.Length)
		{
			for (int i = (int)stamina.x; i < (int)stamina.y; i++)
			{
				AnimationPlayer a = (AnimationPlayer)tokens[i].GetChildren()[0];
				if (a.GetCurrentAnimation() != "Pop Out") { a.Play("Pop Out"); }
			}
		}
		
		//Fatigue check, do fatigue animations here to show player that there tokens are getting locked
		if (player.Fatigue > 0)
		{
			for (int i = 0; i < (int)player.Fatigue; i++)
			{
				AnimationPlayer a = (AnimationPlayer)tokens[i].GetChildren()[0];
				if (a.GetCurrentAnimation() != "Fatigue Out") { a.Play("Fatigue Out"); }
			}
		}
		else
		{
			for (int i = 0; i < stamina.y; i++)
			{
				AnimationPlayer a = (AnimationPlayer)tokens[i].GetChildren()[0];
				if (a.GetCurrentAnimation() == "Fatigue Out") { a.Play("Fatigue In"); }
				if (a.GetCurrentAnimation() == "Fatigue In" && tokens[i].Frame == 0)
				{
					for(int j = 0; j < stamina.y; j++)
					{
						a = (AnimationPlayer)tokens[j].GetChildren()[0];
						a.Play("Idle Bob");
						AnimationPlayer b = (AnimationPlayer)tokens[0].GetChildren()[0];
						a.Advance(b.GetCurrentAnimationPosition() + j/20f);
					}
				}
			}
		}
		
		//in this case, tokens need to be back brought on screen (no new ones need to be created though)
		for (int i = 0; i < stamina.x; i++)
		{
			AnimationPlayer a = (AnimationPlayer)tokens[i].GetChildren()[0];
			if (a.GetCurrentAnimation() != "Idle Bob")
			{
				if (a.GetCurrentAnimation() == "Pop Out") { a.Play("Pop In"); }
				else if (a.GetCurrentAnimation() == "Pop In" && tokens[i].Frame == 0)
				{
					a.Play("Idle Bob");
					if (i > (int)player.Fatigue)
					{
						AnimationPlayer b = (AnimationPlayer)tokens[(int)player.Fatigue].GetChildren()[0];
						a.Advance(b.GetCurrentAnimationPosition() + (i - (int)player.Fatigue) / 20f);
					}
				}
			}
		}
    }
}
