using Godot;
using System;

public class Player : KinematicBody2D
{
	PlayerState State = new OnGroundState();
	public Vector2 Velocity = new Vector2(0, 0);
	[Export] public Vector2 Stamina = new Vector2(6, 6);
	[Export] public float Fatigue = 0;
	float StaminaTimer = 0;
	
	public enum dir {LEFT = -1, RIGHT = 1};
	public dir Facing = dir.RIGHT;

    public override void _Ready()
    { State.SetPlayerReference(this); State.Enter(); }
	
	public override void _Process(float delta)
	{
		Sprite spr = (Sprite)FindNode("Sprite");
		if (Facing == dir.LEFT) {spr.SetFlipH(true);} else {spr.SetFlipH(false);}
		spr.SetOffset(new Vector2(16 * (int)Facing, -32));
	}

    public override void _PhysicsProcess(float delta)
    {
		Vector2 oldStamina = Stamina;
		float oldFatigue = Fatigue;
		
		PlayerState NextState = State.FixedUpdate(delta);
		if (NextState != null)
		{
			State.Exit();
			State = NextState;
			State.SetPlayerReference(this);
			State.Enter();
		}
		
		//Manage stamina regen and properly reseting variables like fatigue	
		StaminaSystemUpkeep(delta, oldFatigue, oldStamina);
    }
	
	
	void StaminaSystemUpkeep(float delta, float oldFatigue, Vector2 oldStamina)
	{
		//if Fatigue gets reset, current stamina = max
		if (oldFatigue != Fatigue && Fatigue == 0) { Stamina.x = Stamina.y; }
		//You always have at least 1/3 your max stamina. Fatigue cant reduce past that
		if (Fatigue > Stamina.y * 2f / 3f) { Fatigue = Stamina.y * 2f / 3f; }
		
		//automatically reset stamina timer whenever the current stamina drops
		if (oldStamina.x > Stamina.x) { StaminaTimer = 0; }
		else { StaminaTimer += delta; }
		
		//if the max stamina increases heal the player up to full
		if (oldStamina.y < Stamina.y) { Fatigue = 0; Stamina.x = Stamina.y; StaminaTimer = 10; }
		
		//use int casts on fatigue so I can ignore the remainder.
		//regen stamina tokens after some time has passed. Only the first token's time increases with fatigue, the rest are on half second timers
		if (Stamina.x < Stamina.y - (int)Fatigue && StaminaTimer > 1.5f + (int)Fatigue/2f)
		{ Stamina.x++; StaminaTimer -= 0.5f; }
		
		//cap stamina so it doesn't get bigger than the max adjusted for fatigue	
		if (Stamina.x > Stamina.y - (int)Fatigue) { Stamina.x = Stamina.y - (int)Fatigue; }
	}
	
	public bool HaveStamina()
	{
		if (Stamina.x > 0) { return true; }
		else { return false; }
	}
}


//-----------------------------------------------------------------------------------------------------------------------------------
public class OnGroundState : PlayerState
{
	public override void Enter()
	{
		animation.Play("Idle");
		player.Velocity.y = 0;
		airTime = 0;
	}
	protected float airTime = 0;
	
	public override PlayerState FixedUpdate(float delta)
	{
		if (Input.IsActionPressed("CommandAttack"))
		{ return new BlockState(); }
		
		if (Input.IsActionPressed("CommandHook"))
		{ return new HookAimState(); }
		
		Vector2 vel = player.Velocity;
		airTime += delta;
		
		if (Input.IsActionPressed("CommandLeft") && !Input.IsActionPressed("CommandRight"))
		{
			player.Facing = Player.dir.LEFT;
			if (vel.x < 0) {vel.x -= 250 * delta;}
			else {vel.x -= 400 * delta;}
			
		}
		if (Input.IsActionPressed("CommandRight") && !Input.IsActionPressed("CommandLeft"))
		{
			player.Facing = Player.dir.RIGHT;
			if (vel.x > 0) {vel.x += 250 * delta;}
			else {vel.x += 400 * delta;}
		}
		if (Mathf.Abs(vel.x) > 200) { vel.x = 200 * Mathf.Sign(vel.x); }
		
		if ((!Input.IsActionPressed("CommandLeft") && !Input.IsActionPressed("CommandRight")) ||
			(Input.IsActionPressed("CommandLeft") && Input.IsActionPressed("CommandRight")) )
		{
			float s = Mathf.Sign(vel.x);
			vel.x = Mathf.Abs(vel.x) - 550*delta;
			if (vel.x < 0) { vel.x = 0; }
			else { vel.x *= s; }
			
			if (Input.IsActionPressed("CommandDown")) { return new DuckState(); }
		}
		
		if (Input.IsActionJustPressed("CommandRoll") && player.HaveStamina())
		{ return new RollState(); }
		
		if (Input.IsActionJustPressed("CommandJump"))
		{ return new JumpState(); }
		
		if (player.IsOnFloor() || player.TestMove(player.GetTransform(), LockPlayerToSlope45DegreeOrLess(delta)))
		{ airTime = 0; player.MoveAndCollide(LockPlayerToSlope45DegreeOrLess(delta)); }
		
		if (airTime > 0.15) { return new InAirState(); }
		
		player.MoveAndSlide(vel, new Vector2(0, -1));
		player.Velocity = vel;
		return null;
	}
}

public class HookState : PlayerState
{
	protected RayCast2D Hook = null;
	protected Line2D Rope = null;
	protected System.Collections.Generic.Stack<Vector2> RotationPoints = new System.Collections.Generic.Stack<Vector2>();
	protected System.Collections.Generic.Stack<Vector2> RopeNorms = new System.Collections.Generic.Stack<Vector2>();
	protected float Slack = 0;
	protected bool DestroyHook = true;
	
	public void Init(float slack)
	{ Slack = slack; }
	public void Init(float slack, System.Collections.Generic.Stack<Vector2> rotationPoints)
	{ Slack = slack; RotationPoints = rotationPoints; }
	public void Init(float slack, System.Collections.Generic.Stack<Vector2> rotationPoints, System.Collections.Generic.Stack<Vector2> ropeNorms)
	{ Slack = slack; RotationPoints = rotationPoints; RopeNorms = ropeNorms; }
	
	public override void Enter()
	{
		Hook = (RayCast2D)player.FindNode("Hook");
		Rope = (Line2D)player.FindNode("Rope");
		
		if (Hook == null)
		{
			Hook = new RayCast2D();
			Hook.SetName("Hook");
			Hook.SetPosition(new Vector2(0, 0));
			Hook.SetEnabled(false);
			
			Rope = new Line2D();
			Rope.SetName("Rope");
			Rope.SetWidth(1);
			Rope.SetDefaultColor(new Color(1, 1, 1));
			//Rope.SetTexture(ResourceLoader.Load("res://Player/HookReticule.png"));
			Rope.AddPoint(new Vector2(0, 0));
			//Rope.SetVisible(false);
			Rope.SetBeginCapMode(2);
			Rope.SetEndCapMode(2);
			Rope.SetJointMode(2);
			
			player.AddChild(Hook);
			Hook.AddChild(Rope);
			
			Hook.SetOwner(player);
			Rope.SetOwner(player);
		}
	}
	
	public override void Exit()
	{ if (DestroyHook) { Hook.Free(); GD.Print("Hook Destroyed"); } }
	
	protected Vector2 GetLocalRotationPoint()
	{ return RotationPoints.Peek() - Hook.GetGlobalPosition(); }
	
	protected float TotalRopeLength()
	{
		Vector2[] temp = new Vector2[RotationPoints.Count];
		RotationPoints.CopyTo(temp, 0);
		
		float total = GetLocalRotationPoint().Length();
		
		for (int i = 0; i < RotationPoints.Count - 1; i++)
		{ total += (temp[i] - temp[i + 1]).Length(); }
		
		return total;
	}
	
	protected void RopeRotationPointUpdate()
	{
		Hook.SetCastTo(GetLocalRotationPoint());
		Hook.ForceRaycastUpdate();
		
		if (Hook.IsColliding())
		{
			//add a rotation point
			float difference = (Hook.GetCollisionPoint() - RotationPoints.Peek()).Length();
			if (difference > 1)
			{
				Vector2 norm = (Hook.GetGlobalPosition() - Hook.GetCollisionPoint()).Normalized();
				norm = new Vector2(norm.y, -norm.x);
				if (norm.Dot(Hook.GetCollisionNormal()) < 0)
				{ norm *= -1; }
				
				RopeNorms.Push(norm);
				RotationPoints.Push(Hook.GetCollisionPoint());
			}
		}
		
		if (RotationPoints.Count > 1 && RopeNorms.Count > 0)
		{
			//remove a rotation point
			if (RopeNorms.Peek().Dot(GetLocalRotationPoint()) < 0)
			{ RotationPoints.Pop(); RopeNorms.Pop(); }
		}
		
		//Update rotation point and rope feedback
		Hook.SetCastTo(GetLocalRotationPoint());
		Hook.ForceRaycastUpdate();
		
		for (int i = 0; i < RotationPoints.Count; i++)
		{
			Vector2[] temp = new Vector2[RotationPoints.Count];
			RotationPoints.CopyTo(temp, 0);
			if (Rope.GetPointCount() <= i + 1)
			{ Rope.AddPoint(temp[i] - Hook.GetGlobalPosition()); }
			else
			{ Rope.SetPointPosition(i + 1, temp[i] - Hook.GetGlobalPosition()); }
		}
		for (int i = RotationPoints.Count + 1; i < Rope.GetPointCount(); i++)
		{ Rope.RemovePoint(i); }
	}
}

public class HookAimState : HookState
{
	Node2D reticule; Sprite reticuleSpr;
	
	public override void Enter()
	{
		DestroyHook = false;
		
		player.Velocity *= 0.5f;
		player.MoveAndSlide(player.Velocity);
		
		reticule = new Node2D();
		reticuleSpr = new Sprite();
		
		reticule.SetName("Reticule");
		reticule.SetPosition(64 * (player.GetGlobalMousePosition() - player.GetGlobalPosition()).Normalized());
		reticuleSpr.SetTexture((Texture)ResourceLoader.Load("res://Player/HookReticule.png"));
		reticuleSpr.SetRotation(Mathf.PI / 2 + reticule.GetPosition().Angle());
		
		player.AddChild(reticule);
		reticule.AddChild(reticuleSpr);
		base.Enter();
	}
	
	public override PlayerState FixedUpdate(float delta)
	{
		player.MoveAndSlide(player.Velocity);
		player.Velocity.x *= 0.9f;
		player.Velocity.y += 100 * delta;
		
		reticule.SetPosition(64 * (player.GetGlobalMousePosition() - player.GetGlobalPosition()).Normalized());
		reticuleSpr.SetRotation(Mathf.PI / 2 + reticule.GetPosition().Angle());
		
		if (!Input.IsActionPressed("CommandHook")) { return new HookThrowState(); }
		return null;
	}
	
	public override void Exit()
	{ reticule.Free(); base.Exit(); }
}

public class HookThrowState : HookState
{
	public override void Enter()
	{
		base.Enter();
		Hook.SetEnabled(true);
		Hook.SetCastTo(16 * (Hook.GetGlobalMousePosition() - Hook.GetGlobalPosition()).Normalized());
		
		Rope.AddPoint(Hook.GetCastTo());
	}
	
	public override PlayerState FixedUpdate(float delta)
	{
		player.Velocity.y += 100 * delta;
		player.MoveAndSlide(player.Velocity);
		
		Rope.RemovePoint(Rope.GetPointCount() - 1);
		Rope.AddPoint(Hook.GetCastTo());
		
		if (Hook.IsColliding())
		{
			DestroyHook = false;
			Hook.SetCastTo(Hook.GetCollisionPoint() - Hook.GetGlobalPosition());
			Rope.RemovePoint(Rope.GetPointCount() - 1);
			Rope.AddPoint(Hook.GetCastTo());
			RotationPoints.Push(Hook.GetCollisionPoint());
			
			HookState h = new HookWalkState();
			h.Init(Slack, RotationPoints);
			return h;
		}
		if (Hook.GetCastTo().Length() > 8 * 32)
		{ DestroyHook = true; return new OnGroundState(); }
		
		Hook.SetCastTo(Hook.GetCastTo() + delta * 1000 * Hook.GetCastTo().Normalized());
		
		return null;
	}
}

public class HookSwingState : HookState
{
	Vector2 vel = new Vector2(0, 0);
	bool stopPass = false;
	
	public override void Enter()
	{
		base.Enter();
		if (Slack == 0) { Slack = Hook.GetCastTo().Length(); }
	}
	
	public override PlayerState FixedUpdate(float delta)
	{
		//Update RotationPoints on the rope
		RopeRotationPointUpdate();
		Vector2 oldPos = Hook.GetGlobalPosition();
		float radius = GetLocalRotationPoint().Length();
		
		//Apply forces (gravity and centripital accleration) in a rope like fashion (faking tension)
		float ang = Mathf.Atan2(Hook.GetCastTo().x, Hook.GetCastTo().y);
		Vector2 gravity = new Vector2(0, 150);
		Vector2 centripitalAcceleration = new Vector2(0, 0);
		
		if ((vel.Length() >= Mathf.Sqrt(150 * Hook.GetCastTo().Length()) || RotationPoints.Peek().y < Hook.GetGlobalPosition().y)
			&& Mathf.Abs(Slack - TotalRopeLength()) < 1)
		{
			float c = vel.LengthSquared() / Hook.GetCastTo().Length();
			centripitalAcceleration = new Vector2(c * Mathf.Sin(ang), c * Mathf.Cos(ang));
		}
		
		Vector2 acceleration = gravity + centripitalAcceleration;
		if (vel.Length() > 10 * radius) { vel = vel.Normalized() * 10 * radius; GD.Print("Angular Speed Limit"); }
		player.MoveAndCollide((acceleration * delta * delta) + (vel * delta));
		
		//Manage Player Inputs
		if (Input.IsActionJustPressed("CommandJump"))
		{
			DestroyHook = true;
			return new InAirState();
		}
//		if (player.IsColliding())
//		{
//			DestroyHook = false;
//			HookState next = new HookWalkState();
//			next.Init(Slack, RotationPoints, RopeNorms)
//			return next;
//		}
		
		float t = TotalRopeLength();
		if (Slack - t < 0)
		{ player.MoveAndCollide(-GetLocalRotationPoint().Normalized() * (Slack - t)); }
		
		vel = (Hook.GetGlobalPosition() - oldPos) / delta;
		//GD.Print(-GetLocalRotationPoint().x);
		
		if (Slack - TotalRopeLength() < 1)
		{
			if ((Input.IsActionPressed("CommandLeft") || Input.IsActionPressed("CommandRight")) &&
				!Input.IsActionPressed("CommandHook") && RotationPoints.Peek().y < Hook.GetGlobalPosition().y)
			{
				Vector2 swingDir = -GetLocalRotationPoint().Normalized();
				if (Input.IsActionPressed("CommandLeft") && !Input.IsActionPressed("CommandRight"))
				{
					swingDir = new Vector2(-swingDir.y, swingDir.x);
					if ((swingDir + vel).Length() < 10 * radius)
					{ player.MoveAndSlide(swingDir); }
				}
				if (Input.IsActionPressed("CommandRight") && !Input.IsActionPressed("CommandLeft"))
				{
					swingDir = new Vector2(swingDir.y, -swingDir.x);
					if ((swingDir + vel).Length() < 100)
					{ player.MoveAndSlide(swingDir); }
				}
			}
			else
			{
				//GD.Print(Mathf.Abs(GetLocalRotationPoint().x));
				if (Input.IsActionPressed("CommandHook") && Math.Abs(GetLocalRotationPoint().x) < 1 && !stopPass)
				{
					//I should base this off of position rather than speed, otherwise I'll stop the player in weird spots
					//Vector2 stop = -GetLocalRotationPoint().Normalized();
					//if (vel.x < 0) { stop = new Vector2(stop.y, -stop.x); }
					//else if (vel.x > 0) { stop = new Vector2(-stop.y, stop.x); }
					//else { stop *= 0; }
					//stop *= vel.Length() * 0.25f;
					
					Random rand = new Random();
					GD.Print("Stop; ", vel.Length());
					Vector2 stop = -vel * 0.75f * Math.Abs(100 - vel.Length()) / 100;
					player.MoveAndSlide(stop);
					stopPass = true;
				}
				else if (Mathf.Abs(GetLocalRotationPoint().x) > 1 && stopPass)
				{ stopPass = false; }
				
				if (vel.Length() < 20 && vel.x != 0 && Mathf.Abs(GetLocalRotationPoint().x) < 1)
				{ player.MoveAndSlide(-vel); }
				
				if (Mathf.Abs(vel.x) < 1)
				{
					if (Input.IsActionPressed("CommandUp") && !Input.IsActionPressed("CommandDown"))
					{ Slack -= 25 * delta; }
					if (Input.IsActionPressed("CommandDown") && !Input.IsActionPressed("CommandUp"))
					{ Slack += 25 * delta; }
				}
			}
		}
		vel = (Hook.GetGlobalPosition() - oldPos) / delta;
		return null;
	}
	
	public override void Exit()
	{ player.Velocity = vel + new Vector2(0, -50); base.Exit(); }
}

public class HookWalkState : HookState
{
	public override void Enter()
	{
		base.Enter();
		if (Slack == 0) { Slack = Hook.GetCastTo().Length(); }
	}
	
	public override PlayerState FixedUpdate(float delta)
	{
		player.Velocity = new Vector2(0, 0);
		DestroyHook = false;
		HookState next = new HookSwingState();
		next.Init(Slack, RotationPoints, RopeNorms);
		return next;
	}
}

public class BlockState : PlayerState
{
	float airTime = 0;
	
	public override void Enter()
	{ animation.Play("Block"); }
	
	public override PlayerState FixedUpdate(float delta)
	{
		if (!Input.IsActionPressed("CommandAttack")) { return new OnGroundState(); }
		if (Input.IsActionPressed("CommandDown")) { return new DuckBlockState(); }
		if (Input.IsActionJustPressed("CommandJump")) { return new JumpBlockState(); }
		
		
		if (player.HaveStamina())
		{
			if (Input.IsActionJustPressed("CommandItem")) { return new PokeHoldState(); }
			if (Input.IsActionJustPressed("CommandLamp")) { return new SwingHoldState(); }
			if (Input.IsActionJustPressed("CommandHook")) { return new ParryState(); }
		}
		
		Vector2 vel = player.Velocity;
		
		if (Input.IsActionPressed("CommandLeft") && !Input.IsActionPressed("CommandRight"))
		{
			if (vel.x < 0) {vel.x -= 250 * delta;}
			else {vel.x -= 400 * delta;}
		}
		if (Input.IsActionPressed("CommandRight") && !Input.IsActionPressed("CommandLeft"))
		{
			if (vel.x > 0) {vel.x += 250 * delta;}
			else {vel.x += 400 * delta;}
		}
		
		int speedLimit = 100;
		if (Mathf.Sign(vel.x) == (int)player.Facing) { speedLimit = 175; }
		if (Mathf.Abs(vel.x) > speedLimit) { vel.x = speedLimit * Mathf.Sign(vel.x); }
		
		if ((Input.IsActionPressed("CommandRight") && Input.IsActionPressed("CommandLeft")) ||
			(!Input.IsActionPressed("CommandRight") && !Input.IsActionPressed("CommandLeft")) )
		{
			float s = Mathf.Sign(vel.x);
			vel.x = Mathf.Abs(vel.x) - 550*delta;
			if (vel.x < 0) { vel.x = 0; }
			else { vel.x *= s; }
		}
		
		if (player.TestMove(player.GetTransform(), LockPlayerToSlope45DegreeOrLess(delta)))
		{player.MoveAndCollide(LockPlayerToSlope45DegreeOrLess(delta));}
		
		if (Input.IsActionJustPressed("CommandRoll"))
		{ return new RollState(); }
		
		player.MoveAndSlide(vel);
		player.Velocity = vel;
		
		if (player.IsOnFloor()) { airTime = 0; }
		if (airTime > 0.15) { return new InAirBlockState(); }
		airTime += delta;
		
		return null;
	}
}

public class AttackState : PlayerState
{
	public int HyperArmour = 0;
	public int Damage = 0;
	public int KnockBack = 0;

	public override void Enter()
	{ player.Velocity *= 0.1f; }

	public override PlayerState FixedUpdate(float delta)
	{
		player.MoveAndSlide(player.Velocity);
		player.Velocity *= 0.96f;
		
		if (!animation.IsPlaying()) { return new BlockState(); }
		return null;
	}
}

public class PokeHoldState : AttackState
{
	float timer = 0;
	
	public override void Enter() { animation.Play("Poke Hold"); base.Enter(); }
	
	public override PlayerState FixedUpdate(float delta)
	{
		base.FixedUpdate(delta);
		
		if ((!animation.IsPlaying() && !Input.IsActionPressed("CommandItem")) || timer > 0.5) 
		{
			AttackState a = new PokeState();
			a.Damage = Damage + (int)(20 * timer / 0.5f);
			a.KnockBack = KnockBack + (int)(50 * timer / 0.5f);
			return a;
		}
		
		timer += delta;
		return null;
	}
}

public class SwingHoldState : AttackState
{
	float timer = 0;
	
	public override void Enter() { animation.Play("Swing Hold"); base.Enter(); }
	
	public override PlayerState FixedUpdate(float delta)
	{
		base.FixedUpdate(delta);
		
		if ((!animation.IsPlaying() && !Input.IsActionPressed("CommandLamp")) || timer > 0.75) 
		{
			AttackState a = new SwingState();
			a.Damage = Damage + (int)(50 * timer / 0.75f);
			a.KnockBack = KnockBack + (int)(20 * timer / 0.75f);
			return a;
		}
		
		timer += delta;
		return null;
	}
}

public class PokeState : AttackState
{
	public override void Enter()
	{
		animation.Play("Poke");
		player.Stamina.x--;
		player.Fatigue += 0.05f;
	}
}

public class SwingState : AttackState
{
	public override void Enter()
	{
		animation.Play("Swing");
		player.Stamina.x -= 2;
		player.Fatigue += 0.075f;
	}
}

public class ParryState : AttackState
{
	public override void Enter()
	{
		animation.Play("Parry");
		player.Stamina.x--;
		player.Fatigue += 0.05f;
		base.Enter();
	}
}


public class DuckState : PlayerState
{
	public override void Enter()
	{ animation.Play("Duck"); }
	
	public override PlayerState FixedUpdate(float delta)
	{
		if (player.TestMove(player.GetTransform(), LockPlayerToSlope45DegreeOrLess(delta)))
		{player.MoveAndCollide(LockPlayerToSlope45DegreeOrLess(delta));}
		
		if (!player.IsOnFloor() && !player.TestMove(player.GetTransform(), new Vector2(0, 8)))
		{ return new InAirState(); }
		
		player.Velocity *= 0.96f;
		
		player.MoveAndSlide(player.Velocity);
		
		if (!Input.IsActionPressed("CommandDown")) { return new OnGroundState(); }
		if (Input.IsActionJustPressed("CommandJump"))
		{
			CollisionPolygon2D hitbox = (CollisionPolygon2D)player.FindNode("Hitbox");
			hitbox.SetDisabled(true);
			player.SetPosition(player.GetPosition() + new Vector2(0, 1));
			hitbox.SetDisabled(false);
		}
		
		return null;
	}
}

public class DuckBlockState : DuckState
{
	public override void Enter()
	{ animation.Play("Block Duck"); }
	
	public override PlayerState FixedUpdate(float delta)
	{
		PlayerState next = base.FixedUpdate(delta);
		if (!Input.IsActionPressed("CommandDown")) { return new BlockState(); }
		if (next != null) { if (next.GetType() == new InAirState().GetType()) { return new InAirBlockState(); } }
		return next;
	}
}

public class JumpState : PlayerState
{
	public override void Enter()
	{
		player.Velocity.y = -200;
		player.MoveAndSlide(player.Velocity);
		player.Fatigue += 0.025f;
	}
	
	public override PlayerState FixedUpdate(float delta)
	{ return new InAirState(); }
}

public class JumpBlockState : JumpState
{
	float timer = 0;
	public override void Enter()
	{ animation.Play("Block Jump"); timer = 0; base.Enter(); player.Velocity.y = 0; }
	
	public override PlayerState FixedUpdate(float delta)
	{
		//player.Velocity.y *= 0.9f;
		player.MoveAndSlide(player.Velocity);
		if (timer > 0.25) { return new InAirBlockState(); }
		
		timer += delta;
		return null;
	}
}

//moves the player about 100 pixels (about 3 tiles) along the ground at a fast pace
public class RollState : PlayerState
{
	float timer = 0;
	
	public override void Enter()
	{
		player.Stamina.x--;
		player.Fatigue += 0.05f;
		
		if ((Input.IsActionPressed("CommandRight") && Input.IsActionPressed("CommandLeft")) ||
		   (!Input.IsActionPressed("CommandRight") && !Input.IsActionPressed("CommandLeft")) )
		{ player.Velocity = new Vector2((int)player.Facing * 500, 0); }
		else if (Input.IsActionPressed("CommandRight"))
		{ player.Velocity = new Vector2(500, 0); }
		else if (Input.IsActionPressed("CommandLeft"))
		{ player.Velocity = new Vector2(-500, 0); }
		
		
		timer = 0;
		animation.Play("Duck");
	}
	
	public override PlayerState FixedUpdate(float delta)
	{
		if (player.TestMove(player.GetTransform(), LockPlayerToSlope45DegreeOrLess(delta)))
		{player.MoveAndCollide(LockPlayerToSlope45DegreeOrLess(delta));}
		
		player.MoveAndSlide(player.Velocity);
		if (timer > 0.2) { return new RollCooldownState(); }
		else { timer += delta; return null; }
	}
	
	public override void Exit()
	{
		player.Velocity = new Vector2(player.Velocity.x * 0.25f, 0);
		animation.Play("Idle");
	}
}

public class RollCooldownState : OnGroundState
{
	float timer = 0;
	
	public override PlayerState FixedUpdate(float delta)
	{
		timer += delta;
		PlayerState next = base.FixedUpdate(delta);
		
		if (timer > 0.5) { return new OnGroundState(); }
		if (next != null) { if (next.GetType() != new RollState().GetType()) { return next; } }
		return null;
	}
}

public class InAirState : PlayerState
{
	public override void Enter()
	{ animation.Play("Idle"); }
	
	public override PlayerState FixedUpdate(float delta)
	{
		Vector2 vel = player.Velocity;
		vel.y += 450 * delta;
		
		if (Input.IsActionPressed("CommandLeft") && !Input.IsActionPressed("CommandRight"))
		{
			player.Facing = Player.dir.LEFT;
			if (vel.x < 0) {vel.x -= 100 * delta;}
			else {vel.x -= 200 * delta;}
			
		}
		if (Input.IsActionPressed("CommandRight") && !Input.IsActionPressed("CommandLeft"))
		{
			player.Facing = Player.dir.RIGHT;
			if (vel.x > 0) {vel.x += 100 * delta;}
			else {vel.x += 200 * delta;}
		}
		if (Mathf.Abs(vel.x) > 200) { vel.x = 200 * Mathf.Sign(vel.x); }
		
		if ((!Input.IsActionPressed("CommandLeft") && !Input.IsActionPressed("CommandRight")) ||
			(Input.IsActionPressed("CommandLeft") && Input.IsActionPressed("CommandRight")) )
		{
			float s = Mathf.Sign(vel.x);
			vel.x = Mathf.Abs(vel.x) - 150*delta;
			if (vel.x < 0) { vel.x = 0; }
			else { vel.x *= s; }
		}
		
		player.MoveAndSlide(vel, new Vector2(0, -1));
		player.Velocity = vel;
		
		if (player.IsOnFloor())
		{ return new OnGroundState();}
		
		if (Input.IsActionPressed("CommandHook"))
		{ return new HookAimState(); }
		
		return null;
	}
}

public class InAirBlockState : InAirState
{
	public override void Enter()
	{ animation.Play("Block"); }
	
	public override PlayerState FixedUpdate(float delta)
	{
		Player.dir d = player.Facing;
		PlayerState p = base.FixedUpdate(delta);
		player.Facing = d;
		
		if (p != null) { if (p.GetType() == new OnGroundState().GetType()) { return new BlockState(); } }
		return null;
	}
}

public class PlayerState
{
	protected Player player;
	protected AnimationPlayer animation;
	
	public void SetPlayerReference(Player Player)
	{
		player = Player;
		animation = (AnimationPlayer)player.FindNode("AnimationPlayer");
	}
	
	protected Vector2 LockPlayerToSlope45DegreeOrLess(float delta)
	{ return new Vector2(0, 1 + Mathf.Abs(player.Velocity.x) * delta); }
	
	public PlayerState()
	{ /*GD.Print(System.ComponentModel.TypeDescriptor.GetClassName(this), " Constructed");*/ }
	
	//Initialize State
	public virtual void Enter()
	{ /*GD.Print(System.ComponentModel.TypeDescriptor.GetClassName(this), " Entered\n");*/ }
	
	//Clean up any loose ends here
	public virtual void Exit()
	{ /*GD.Print(System.ComponentModel.TypeDescriptor.GetClassName(this), " Exiting");*/ }	
	
	//Runs Every Process
	public virtual PlayerState ProcessUpdate(float delta)
	{ return null; }
	
	//Runs Every Physics Process
	public virtual PlayerState FixedUpdate(float delta)
	{ return null; }
}