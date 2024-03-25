
using System;
using System.Collections.Generic;
using Config;
using AiState;
using Godot;

/// <summary>
/// 敌人，可以携带武器
/// </summary>
[Tool]
public partial class Enemy : AiRole
{
    /// <summary>
    /// 敌人属性
    /// </summary>
    private ExcelConfig.EnemyBase _enemyAttribute;

    private static bool _init = false;
    private static Dictionary<string, ExcelConfig.EnemyBase> _enemyAttributeMap =
        new Dictionary<string, ExcelConfig.EnemyBase>();
    
    /// <summary>
    /// 初始化敌人属性数据
    /// </summary>
    public static void InitEnemyAttribute()
    {
        if (_init)
        {
            return;
        }

        _init = true;
        foreach (var enemyAttr in ExcelConfig.EnemyBase_List)
        {
            if (enemyAttr.Activity != null)
            {
                if (!_enemyAttributeMap.TryAdd(enemyAttr.Activity.Id, enemyAttr))
                {
                    Debug.LogError("发现重复注册的敌人属性: " + enemyAttr.Id);
                }
            }
        }
    }

    /// <summary>
    /// 根据 ActivityBase.Id 获取对应敌人的属性数据
    /// </summary>
    public static ExcelConfig.EnemyBase GetEnemyAttribute(string itemId)
    {
        if (itemId == null)
        {
            return null;
        }
        if (_enemyAttributeMap.TryGetValue(itemId, out var attr))
        {
            return attr;
        }

        throw new Exception($"敌人'{itemId}'没有在 EnemyBase 表中配置属性数据!");
    }
    
    public override void OnInit()
    {
        base.OnInit();

        AttackLayer = PhysicsLayer.Obstacle | PhysicsLayer.Player;
        EnemyLayer = PhysicsLayer.Player;
        Camp = CampEnum.Camp2;

        RoleState.MoveSpeed = 20;

        MaxHp = 20;
        Hp = 20;
    }

    protected override RoleState OnCreateRoleState()
    {
        var roleState = new RoleState();
        var enemyBase = GetEnemyAttribute(ActivityBase.Id).Clone();
        _enemyAttribute = enemyBase;

        MaxHp = enemyBase.Hp;
        Hp = enemyBase.Hp;
        roleState.CanPickUpWeapon = enemyBase.CanPickUpWeapon;
        roleState.MoveSpeed = enemyBase.MoveSpeed;
        roleState.Acceleration = enemyBase.Acceleration;
        roleState.Friction = enemyBase.Friction;
        ViewRange = enemyBase.ViewRange;
        TailAfterViewRange = enemyBase.TailAfterViewRange;
        BackViewRange = enemyBase.BackViewRange;
        AttackInterval = enemyBase.AttackInterval;
        
        roleState.Gold = Mathf.Max(0, Utils.Random.RandomConfigRange(enemyBase.Gold));
        return roleState;
    }

    public override void EnterTree()
    {
        if (!World.Enemy_InstanceList.Contains(this))
        {
            World.Enemy_InstanceList.Add(this);
        }
    }

    public override void ExitTree()
    {
        World.Enemy_InstanceList.Remove(this);
    }

    protected override void OnDie()
    {
        //扔掉所有武器
        ThrowAllWeapon();

        var effPos = Position + new Vector2(0, -Altitude);
        //血液特效
        var blood = ObjectManager.GetPoolItem<AutoDestroyParticles>(ResourcePath.prefab_effect_enemy_EnemyBlood0001_tscn);
        blood.Position = effPos - new Vector2(0, 12);
        blood.AddToActivityRoot(RoomLayerEnum.NormalLayer);
        blood.PlayEffect();
        
        var realVelocity = GetRealVelocity();
        //创建敌人碎片
        var count = Utils.Random.RandomRangeInt(3, 6);
        for (var i = 0; i < count; i++)
        {
            var debris = Create(Ids.Id_enemy_dead0001);
            debris.PutDown(effPos, RoomLayerEnum.NormalLayer);
            debris.MoveController.AddForce(Velocity + realVelocity);
        }
        
        //创建金币
        Gold.CreateGold(Position, RoleState.Gold);
        
        //派发敌人死亡信号
        EventManager.EmitEvent(EventEnum.OnEnemyDie, this);
        Destroy();

    }

    protected override void Process(float delta)
    {
        base.Process(delta);
        if (IsDie)
        {
            return;
        }
        
        //看向目标
        if (LookTarget != null && MountLookTarget)
        {
            var pos = LookTarget.Position;
            LookPosition = pos;
            //脸的朝向
            var gPos = Position;
            if (pos.X > gPos.X && Face == FaceDirection.Left)
            {
                Face = FaceDirection.Right;
            }
            else if (pos.X < gPos.X && Face == FaceDirection.Right)
            {
                Face = FaceDirection.Left;
            }
            //枪口跟随目标
            MountPoint.SetLookAt(pos);
        }

        if (RoleState.CanPickUpWeapon)
        {
            //拾起武器操作
            DoPickUpWeapon();
        }
    }

    public override bool IsAllWeaponTotalAmmoEmpty()
    {
        if (!_enemyAttribute.CanPickUpWeapon)
        {
            return false;
        }
        return base.IsAllWeaponTotalAmmoEmpty();
    }

    protected override void OnHit(ActivityObject target, int damage, float angle, bool realHarm)
    {
        //受到伤害
        var state = StateController.CurrState;
        if (state == AIStateEnum.AiNormal)
        {
            LookTarget = target;
            //判断是否进入通知状态
            if (World.Enemy_InstanceList.FindIndex(enemy =>
                    enemy != this && !enemy.IsDie && enemy.AffiliationArea == AffiliationArea &&
                    enemy.StateController.CurrState == AIStateEnum.AiNormal) != -1)
            {
                //进入惊讶状态, 然后再进入通知状态
                StateController.ChangeState(AIStateEnum.AiAstonished, AIStateEnum.AiNotify);
            }
            else
            {
                //进入惊讶状态, 然后再进入跟随状态
                StateController.ChangeState(AIStateEnum.AiAstonished, AIStateEnum.AiTailAfter);
            }
        }
        else if (state == AIStateEnum.AiLeaveFor)
        {
            LookTarget = target;
            StateController.ChangeState(AIStateEnum.AiAstonished, AIStateEnum.AiTailAfter);
        }
        else if (state == AIStateEnum.AiFindAmmo)
        {
            if (LookTarget == null)
            {
                LookTarget = target;
                var findAmmo = (AiFindAmmoState)StateController.CurrStateBase;
                StateController.ChangeState(AIStateEnum.AiAstonished, AIStateEnum.AiFindAmmo, findAmmo.TargetWeapon);
            }
        }
    }

    /// <summary>
    /// 从标记出生时调用, 预加载波不会调用
    /// </summary>
    public virtual void OnBornFromMark()
    {
        //罚站 0.7 秒
        StateController.Enable = false;
        this.CallDelay(0.7f, () => StateController.Enable = true);
    }
}
