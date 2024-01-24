﻿
using System.Collections.Generic;
using Godot;

/// <summary>
/// 默认实现地牢房间规则
/// </summary>
public class DefaultDungeonRule : DungeonRule
{
    //用于排除上一级房间
    private List<RoomInfo> excludePrevRoom = new List<RoomInfo>();
    private int chainTryCount = 0;
    private int chainMaxTryCount = 3;
    
    public DefaultDungeonRule(DungeonGenerator generator) : base(generator)
    {
    }

    public override bool CanOverGenerator()
    {
        return Generator.BattleRoomInfos.Count >= Config.BattleRoomCount && Generator.EndRoomInfos.Count > 0;
    }

    public override RoomInfo GetConnectPrevRoom(RoomInfo prevRoomInfo, DungeonRoomType nextRoomType)
    {
        if (nextRoomType == DungeonRoomType.Inlet)
        {
            return null;
        }
        else if (nextRoomType == DungeonRoomType.Boss)
        {
            return Generator.FindMaxLayerRoom(excludePrevRoom);
        }
        else if (nextRoomType == DungeonRoomType.Outlet || nextRoomType == DungeonRoomType.Reward || nextRoomType == DungeonRoomType.Shop || nextRoomType == DungeonRoomType.Event)
        {
            return prevRoomInfo;
        }
        else if (nextRoomType == DungeonRoomType.Battle)
        {
            if (chainTryCount < chainMaxTryCount)
            {
                if (prevRoomInfo != null && prevRoomInfo.Layer >= Config.MaxLayer - 1) //层数太高, 下一个房间生成在低层级
                {
                    return Generator.RoundRoomLessThanLayer(Mathf.Max(1, Config.MaxLayer / 2));
                }
                else
                {
                    return prevRoomInfo;
                }
            }
            else
            {
                return Random.RandomChoose(Generator.RoomInfos);
            }
        }
        else
        {
            return Random.RandomChoose(Generator.RoomInfos);
        }
    }

    public override DungeonRoomType GetNextRoomType(RoomInfo prevRoomInfo)
    {
        if (Generator.StartRoomInfo == null) //生成第一个房间
        {
            return DungeonRoomType.Inlet;
        }
        // else if (Generator.BattleRoomInfos.Count == 0) //奖励房间
        // {
        //     return DungeonRoomType.Reward;
        // }
        else if (prevRoomInfo.RoomType == DungeonRoomType.Boss) //boss房间后生成结束房间
        {
            return DungeonRoomType.Outlet;
        }
        else if (Generator.BattleRoomInfos.Count >= Config.BattleRoomCount) //战斗房间已满
        {
            if (Generator.BossRoomInfos.Count < Config.BossRoomCount) //最后一个房间是boss房间
            {
                if (RoomGroup.BossList.Count == 0) //没有预设boss房间
                {
                    return DungeonRoomType.Battle;
                }
                //生成boss房间
                return DungeonRoomType.Boss;
            }
        }
        return DungeonRoomType.Battle;
    }

    public override void GenerateRoomSuccess(RoomInfo prevRoomInfo, RoomInfo roomInfo)
    {
        if (roomInfo.RoomType == DungeonRoomType.Boss) //boss房间
        {
            roomInfo.CanRollback = true;
            excludePrevRoom.Clear();
        }
        else if (roomInfo.RoomType == DungeonRoomType.Battle)
        {
            chainTryCount = 0;
            chainMaxTryCount = Random.RandomRangeInt(1, 3);
        }
    }

    public override void GenerateRoomFail(RoomInfo prevRoomInfo, DungeonRoomType roomType)
    {
        if (roomType == DungeonRoomType.Boss)
        {
            //生成boss房间成功
            excludePrevRoom.Add(prevRoomInfo);
            if (excludePrevRoom.Count >= Generator.RoomInfos.Count)
            {
                //全都没找到合适的, 那就再来一遍
                excludePrevRoom.Clear();
            }
        }
        else if (roomType == DungeonRoomType.Outlet)
        {
            //生成结束房间失败, 那么只能回滚boss房间
            if (prevRoomInfo != null)
            {
                var bossPrev = prevRoomInfo.Prev;
                Generator.RollbackRoom(prevRoomInfo);
                Generator.SetPrevRoom(bossPrev);
            }
        }
        else if (roomType == DungeonRoomType.Battle)
        {
            chainTryCount++;
        }
    }
}