
using System;
using Godot;

namespace AiState;

/// <summary>
/// Ai 寻找弹药, 进入该状态需要在参数中传入目标武器对象
/// </summary>
public class AiFindAmmoState : StateBase<AiRole, AIStateEnum>
{
    /// <summary>
    /// 目标武器
    /// </summary>
    public Weapon TargetWeapon;

    //导航目标点刷新计时器
    private float _navigationUpdateTimer = 0;
    private float _navigationInterval = 1f;
    
    private float _tailAfterTimer = 0;
    private Role _attackTarget;

    public AiFindAmmoState() : base(AIStateEnum.AiFindAmmo)
    {
    }

    public override void Enter(AIStateEnum prev, params object[] args)
    {
        if (args.Length == 0)
        {
            throw new Exception("进入 AiStateEnum.AiFindAmmo 状态必须要把目标武器当成参数传过来");
        }

        if (args.Length >= 2)
        {
            _attackTarget = (Role)args[1];
        }
        else
        {
            _attackTarget = null;
        }
        
        SetTargetWeapon((Weapon)args[0]);
        _navigationUpdateTimer = _navigationInterval;
        _tailAfterTimer = 0;

        //标记武器
        TargetWeapon.SetSign(SignNames.AiFindWeaponSign, Master);
    }

    public override void Process(float delta)
    {
        if (!Master.IsAllWeaponTotalAmmoEmpty()) //已经有弹药了
        {
            RunNextState();
            return;
        }

        //更新目标位置
        if (_navigationUpdateTimer <= 0)
        {
            //每隔一段时间秒更改目标位置
            _navigationUpdateTimer = _navigationInterval;
            var position = TargetWeapon.GlobalPosition;
            Master.NavigationAgent2D.TargetPosition = position;
        }
        else
        {
            _navigationUpdateTimer -= delta;
        }

        if (TargetWeapon.IsDestroyed || TargetWeapon.IsTotalAmmoEmpty()) //已经被销毁, 或者弹药已经被其他角色捡走
        {
            //再去寻找其他武器
            SetTargetWeapon(Master.FindTargetWeapon());

            if (TargetWeapon == null) //也没有其他可用的武器了
            {
                RunNextState();
            }
        }
        else if (TargetWeapon.Master == Master) //已经被自己拾起
        {
            RunNextState();
        }
        else if (TargetWeapon.Master != null) //武器已经被其他角色拾起!
        {
            //再去寻找其他武器
            SetTargetWeapon(Master.FindTargetWeapon());

            if (TargetWeapon == null) //也没有其他可用的武器了
            {
                RunNextState();
            }
        }
        else
        {
            if (Master.LookTarget != null)
            {
                //检测目标没有超出跟随视野距离
                var isInTailAfterRange = Master.TargetInViewRange;
                if (isInTailAfterRange)
                {
                    _tailAfterTimer = 0;
                }
                else
                {
                    _tailAfterTimer += delta;
                }
            }

            //向武器移动
            if (!Master.NavigationAgent2D.IsNavigationFinished())
            {
                //移动
                Master.DoMove();
            }
            else
            {
                //站立
                Master.DoIdle();
            }
        }
    }

    private void RunNextState()
    {
        if (_attackTarget != null)
        {
            ChangeState(AIStateEnum.AiTailAfter, _attackTarget);
        }
        else if (Master.LookTarget != null)
        {
            ChangeState(_tailAfterTimer > 10 ? AIStateEnum.AiNormal : AIStateEnum.AiTailAfter);
        }
        else
        {
            ChangeState(AIStateEnum.AiNormal);
        }
    }

    private void SetTargetWeapon(Weapon weapon)
    {
        TargetWeapon = weapon;
        if (weapon != null)
        {
            //设置目标点
            Master.NavigationAgent2D.TargetPosition = TargetWeapon.GlobalPosition;
        }
    }
    
    public override void DebugDraw()
    {
        if (TargetWeapon != null)
        {
            Master.DrawLine(Vector2.Zero, Master.ToLocal(TargetWeapon.GlobalPosition), Colors.Purple);

            if (Master.LookTarget != null)
            {
                if (_tailAfterTimer <= 0)
                {
                    Master.DrawLine(Vector2.Zero, Master.ToLocal(Master.LookTarget.GetCenterPosition()), Colors.Orange);
                }
                else if (_tailAfterTimer <= 10)
                {
                    Master.DrawLine(Vector2.Zero, Master.ToLocal(Master.LookTarget.GetCenterPosition()), Colors.Blue);
                }
            }
        }
    }
}