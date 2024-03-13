﻿
using Godot;

/// <summary>
/// 血量上限buff, 心之容器 + 1
/// </summary>
[Tool]
public partial class BuffProp0002 : BuffActivity
{
    public override void OnPickUpItem()
    {
        Master.MaxHp += 2;
        Master.Hp += 2;
    }

    public override void OnRemoveItem()
    {
        Master.MaxHp -= 2;
    }
}