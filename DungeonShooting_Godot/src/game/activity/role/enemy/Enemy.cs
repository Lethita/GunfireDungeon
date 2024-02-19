
using System;
using System.Collections.Generic;
using Config;
using EnemyState;
using Godot;

/// <summary>
/// 高级敌人，可以携带武器
/// </summary>
[Tool]
public partial class Enemy : Role
{
    /// <summary>
    /// 目标是否在视野内
    /// </summary>
    public bool TargetInView { get; set; } = true;
    
    /// <summary>
    /// 敌人身上的状态机控制器
    /// </summary>
    public StateController<Enemy, AIStateEnum> StateController { get; private set; }
    
    /// <summary>
    /// 视野检测射线, 朝玩家打射线, 检测是否碰到墙
    /// </summary>
    [Export, ExportFillNode]
    public RayCast2D ViewRay { get; set; }

    /// <summary>
    /// 导航代理
    /// </summary>
    [Export, ExportFillNode]
    public NavigationAgent2D NavigationAgent2D { get; set; }

    /// <summary>
    /// 导航代理中点
    /// </summary>
    [Export, ExportFillNode]
    public Marker2D NavigationPoint { get; set; }

    /// <summary>
    /// 不通过武发射子弹的开火点
    /// </summary>
    [Export, ExportFillNode]
    public Marker2D FirePoint { get; set; }
    
    /// <summary>
    /// 当前敌人所看向的对象, 也就是枪口指向的对象
    /// </summary>
    public ActivityObject LookTarget { get; set; }

    /// <summary>
    /// 攻击锁定目标时间
    /// </summary>
    public float LockingTime { get; set; } = 1f;
    
    /// <summary>
    /// 锁定目标已经走过的时间
    /// </summary>
    public float LockTargetTime { get; set; } = 0;
    
    /// <summary>
    /// 敌人属性
    /// </summary>
    public EnemyRoleState EnemyRoleState { get; private set; }

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
        
        IsAi = true;
        
        StateController = AddComponent<StateController<Enemy, AIStateEnum>>();

        AttackLayer = PhysicsLayer.Obstacle | PhysicsLayer.Player;
        EnemyLayer = PhysicsLayer.Player;
        Camp = CampEnum.Camp2;

        RoleState.MoveSpeed = 20;

        MaxHp = 20;
        Hp = 20;

        //注册Ai状态机
        StateController.Register(new AiNormalState());
        StateController.Register(new AiTailAfterState());
        StateController.Register(new AiFollowUpState());
        StateController.Register(new AiLeaveForState());
        StateController.Register(new AiSurroundState());
        StateController.Register(new AiFindAmmoState());
        StateController.Register(new AiAttackState());
        StateController.Register(new AiAstonishedState());
        StateController.Register(new AiNotifyState());
        
        //默认状态
        StateController.ChangeStateInstant(AIStateEnum.AiNormal);

        //NavigationAgent2D.VelocityComputed += OnVelocityComputed;
    }

    protected override RoleState OnCreateRoleState()
    {
        var roleState = new EnemyRoleState();
        EnemyRoleState = roleState;
        var enemyBase = GetEnemyAttribute(ActivityBase.Id).Clone();
        _enemyAttribute = enemyBase;

        MaxHp = enemyBase.Hp;
        Hp = enemyBase.Hp;
        roleState.CanPickUpWeapon = enemyBase.CanPickUpWeapon;
        roleState.MoveSpeed = enemyBase.MoveSpeed;
        roleState.Acceleration = enemyBase.Acceleration;
        roleState.Friction = enemyBase.Friction;
        roleState.ViewRange = enemyBase.ViewRange;
        roleState.TailAfterViewRange = enemyBase.TailAfterViewRange;
        roleState.BackViewRange = enemyBase.BackViewRange;
        
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
        CreateGold();
        
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

        if (EnemyRoleState.CanPickUpWeapon)
        {
            //拾起武器操作
            EnemyPickUpWeapon();
        }
    }

    /// <summary>
    /// 创建散落的金币
    /// </summary>
    protected void CreateGold()
    {
        var goldList = Utils.GetGoldList(RoleState.Gold);
        foreach (var id in goldList)
        {
            var o = ObjectManager.GetActivityObject<Gold>(id);
            o.Position = Position;
            o.Throw(0,
                Utils.Random.RandomRangeInt(50, 110),
                new Vector2(Utils.Random.RandomRangeInt(-20, 20), Utils.Random.RandomRangeInt(-20, 20)),
                0
            );
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
    /// 返回地上的武器是否有可以拾取的, 也包含没有被其他敌人标记的武器
    /// </summary>
    public bool CheckUsableWeaponInUnclaimed()
    {
        foreach (var unclaimedWeapon in World.Weapon_UnclaimedWeapons)
        {
            //判断是否能拾起武器, 条件: 相同的房间
            if (unclaimedWeapon.AffiliationArea == AffiliationArea)
            {
                if (!unclaimedWeapon.IsTotalAmmoEmpty())
                {
                    if (!unclaimedWeapon.HasSign(SignNames.AiFindWeaponSign))
                    {
                        return true;
                    }
                    else
                    {
                        //判断是否可以移除该标记
                        var enemy = unclaimedWeapon.GetSign<Enemy>(SignNames.AiFindWeaponSign);
                        if (enemy == null || enemy.IsDestroyed) //标记当前武器的敌人已经被销毁
                        {
                            unclaimedWeapon.RemoveSign(SignNames.AiFindWeaponSign);
                            return true;
                        }
                        else if (!enemy.IsAllWeaponTotalAmmoEmpty()) //标记当前武器的敌人已经有新的武器了
                        {
                            unclaimedWeapon.RemoveSign(SignNames.AiFindWeaponSign);
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }
    
    /// <summary>
    /// 寻找可用的武器
    /// </summary>
    public Weapon FindTargetWeapon()
    {
        Weapon target = null;
        var position = Position;
        foreach (var weapon in World.Weapon_UnclaimedWeapons)
        {
            //判断是否能拾起武器, 条件: 相同的房间, 或者当前房间目前没有战斗, 或者不在战斗房间
            if (weapon.AffiliationArea == AffiliationArea)
            {
                //还有弹药
                if (!weapon.IsTotalAmmoEmpty())
                {
                    //查询是否有其他敌人标记要拾起该武器
                    if (weapon.HasSign(SignNames.AiFindWeaponSign))
                    {
                        var enemy = weapon.GetSign<Enemy>(SignNames.AiFindWeaponSign);
                        if (enemy == this) //就是自己标记的
                        {

                        }
                        else if (enemy == null || enemy.IsDestroyed) //标记当前武器的敌人已经被销毁
                        {
                            weapon.RemoveSign(SignNames.AiFindWeaponSign);
                        }
                        else if (!enemy.IsAllWeaponTotalAmmoEmpty()) //标记当前武器的敌人已经有新的武器了
                        {
                            weapon.RemoveSign(SignNames.AiFindWeaponSign);
                        }
                        else //放弃这把武器
                        {
                            continue;
                        }
                    }

                    if (target == null) //第一把武器
                    {
                        target = weapon;
                    }
                    else if (target.Position.DistanceSquaredTo(position) >
                             weapon.Position.DistanceSquaredTo(position)) //距离更近
                    {
                        target = weapon;
                    }
                }
            }
        }

        return target;
    }

    /// <summary>
    /// 获取武器攻击范围 (最大距离值与最小距离的中间值)
    /// </summary>
    /// <param name="weight">从最小到最大距离的过渡量, 0 - 1, 默认 0.5</param>
    public float GetWeaponRange(float weight = 0.5f)
    {
        if (WeaponPack.ActiveItem != null)
        {
            var attribute = WeaponPack.ActiveItem.Attribute;
            return Mathf.Lerp(Utils.GetConfigRangeStart(attribute.Bullet.DistanceRange), Utils.GetConfigRangeEnd(attribute.Bullet.DistanceRange), weight);
        }

        return 0;
    }

    /// <summary>
    /// 返回目标点是否在视野范围内
    /// </summary>
    public bool IsInViewRange(Vector2 target)
    {
        var isForward = IsPositionInForward(target);
        if (isForward)
        {
            if (GlobalPosition.DistanceSquaredTo(target) <= EnemyRoleState.ViewRange * EnemyRoleState.ViewRange) //没有超出视野半径
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 返回目标点是否在跟随状态下的视野半径内
    /// </summary>
    public bool IsInTailAfterViewRange(Vector2 target)
    {
        var isForward = IsPositionInForward(target);
        if (isForward)
        {
            if (GlobalPosition.DistanceSquaredTo(target) <= EnemyRoleState.TailAfterViewRange * EnemyRoleState.TailAfterViewRange) //没有超出视野半径
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 调用视野检测, 如果被墙壁和其它物体遮挡, 则返回true
    /// </summary>
    public bool TestViewRayCast(Vector2 target)
    {
        ViewRay.Enabled = true;
        ViewRay.TargetPosition = ViewRay.ToLocal(target);
        ViewRay.ForceRaycastUpdate();
        return ViewRay.IsColliding();
    }

    /// <summary>
    /// 调用视野检测完毕后, 需要调用 TestViewRayCastOver() 来关闭视野检测射线
    /// </summary>
    public void TestViewRayCastOver()
    {
        ViewRay.Enabled = false;
    }

    /// <summary>
    /// AI 拾起武器操作
    /// </summary>
    private void EnemyPickUpWeapon()
    {
        //这几个状态不需要主动拾起武器操作
        var state = StateController.CurrState;
        if (state == AIStateEnum.AiNormal || state == AIStateEnum.AiNotify || state == AIStateEnum.AiAstonished || state == AIStateEnum.AiAttack)
        {
            return;
        }
        
        //拾起地上的武器
        if (InteractiveItem is Weapon weapon)
        {
            if (WeaponPack.ActiveItem == null) //手上没有武器, 无论如何也要拾起
            {
                TriggerInteractive();
                return;
            }

            //没弹药了
            if (weapon.IsTotalAmmoEmpty())
            {
                return;
            }
            
            var index = WeaponPack.FindIndex((we, i) => we.ActivityBase.Id == weapon.ActivityBase.Id);
            if (index != -1) //与武器背包中武器类型相同, 补充子弹
            {
                if (!WeaponPack.GetItem(index).IsAmmoFull())
                {
                    TriggerInteractive();
                }

                return;
            }

            // var index2 = Holster.FindWeapon((we, i) =>
            //     we.Attribute.WeightType == weapon.Attribute.WeightType && we.IsTotalAmmoEmpty());
            var index2 = WeaponPack.FindIndex((we, i) => we.IsTotalAmmoEmpty());
            if (index2 != -1) //扔掉没子弹的武器
            {
                ThrowWeapon(index2);
                TriggerInteractive();
                return;
            }
            
            // if (Holster.HasVacancy()) //有空位, 拾起武器
            // {
            //     TriggerInteractive();
            //     return;
            // }
        }
    }
    
    /// <summary>
    /// 获取锁定目标的剩余时间
    /// </summary>
    public float GetLockRemainderTime()
    {
        var weapon = WeaponPack.ActiveItem;
        if (weapon == null)
        {
            return LockingTime - LockTargetTime;
        }
        return weapon.Attribute.AiAttackAttr.LockingTime - LockTargetTime;
    }

    public override void LookTargetPosition(Vector2 pos)
    {
        LookTarget = null;
        base.LookTargetPosition(pos);
    }
    
    /// <summary>
    /// 执行移动操作
    /// </summary>
    public void DoMove()
    {
        // //计算移动
        // NavigationAgent2D.MaxSpeed = EnemyRoleState.MoveSpeed;
        // var nextPos = NavigationAgent2D.GetNextPathPosition();
        // NavigationAgent2D.Velocity = (nextPos - Position - NavigationPoint.Position).Normalized() * RoleState.MoveSpeed;
        
        AnimatedSprite.Play(AnimatorNames.Run);
        //计算移动
        var nextPos = NavigationAgent2D.GetNextPathPosition();
        BasisVelocity = (nextPos - Position - NavigationPoint.Position).Normalized() * RoleState.MoveSpeed;
    }

    /// <summary>
    /// 执行站立操作
    /// </summary>
    public void DoIdle()
    {
        AnimatedSprite.Play(AnimatorNames.Idle);
        BasisVelocity = Vector2.Zero;
    }
    
    /// <summary>
    /// 更新房间中标记的目标位置
    /// </summary>
    public void UpdateMarkTargetPosition()
    {
        if (LookTarget != null)
        {
            AffiliationArea.RoomInfo.MarkTargetPosition[LookTarget.Id] = LookTarget.Position;
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

    // private void OnVelocityComputed(Vector2 velocity)
    // {
    //     if (Mathf.Abs(velocity.X) >= 0.01f && Mathf.Abs(velocity.Y) >= 0.01f)
    //     {
    //         AnimatedSprite.Play(AnimatorNames.Run);
    //         BasisVelocity = velocity;
    //     }
    // }
}
