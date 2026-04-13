# 阶段二可行性验证：执行战斗动作

## 1. 验证目标

阶段二关注的问题是:

- AI 能不能合法出牌
- 能不能选目标
- 能不能处理需要交互的牌
- 能不能结束回合
- 能不能在失败时识别并恢复

## 2. 当前已确认的能力

已确认存在以下公开命令类:

- `MegaCrit.Sts2.Core.Commands.CardCmd`
- `MegaCrit.Sts2.Core.Commands.CardSelectCmd`
- `MegaCrit.Sts2.Core.Commands.PlayerCmd`
- `MegaCrit.Sts2.Core.Commands.PotionCmd`
- `MegaCrit.Sts2.Core.Commands.RelicCmd`

已确认存在下列关键方法:

- `CardCmd.AutoPlay(...)`
- `CardCmd.Discard(...)`
- `CardCmd.Exhaust(...)`
- `CardSelectCmd.FromHand(...)`
- `CardSelectCmd.FromChooseACardScreen(...)`
- `PlayerCmd.EndTurn(...)`

这说明游戏内部并不是只能依赖模拟鼠标和键盘来操作，而是存在命令式入口。

## 3. 结论

阶段二当前判断为: `中高可行`

原因:

- 已确认可以通过命令类执行部分核心动作
- 已确认有结束回合和选牌相关接口
- 已确认动作系统和战斗状态系统在命名上是解耦的，方便做 AI 动作层

## 4. 当前风险

阶段二的主要风险不是“完全没有 API”，而是“复杂交互未必一次打通”。

重点风险包括:

- 某些卡牌需要目标选择、二段确认或额外界面交互
- 某些动作可能要求特定的 `PlayerChoiceContext`
- 某些公开方法虽然存在，但调用前后顺序可能有约束
- 动作是否需要等待队列清空、动画完成或状态切换，还需要实测

## 5. 不反编译前的下一步

优先做下面这些验证:

1. 找到最简单的一类普通攻击牌出牌流程
2. 验证 `EndTurn` 在本地战斗中是否可稳定调用
3. 验证单目标牌、多目标牌、无需目标牌三类最小样本
4. 记录动作执行前需要的上下文对象
5. 验证动作执行后是否能从状态层观察到一致结果

## 6. 何时才需要反编译

只有在下面情况出现时，才对阶段二做定向反编译:

- 方法签名存在，但不知道如何构造调用上下文
- 某张牌的执行流程依赖内部选择器或状态机
- 动作执行经常失败，但日志无法说明失败原因
- 需要确认命令系统内部是否还依赖额外校验

反编译范围应限制在:

- `CardCmd`
- `CardSelectCmd`
- `PlayerCmd`
- `PlayerChoiceContext`
- 与出牌和目标选择直接相关的少量类型

## 7. 当前推荐结论

阶段二不需要现在就展开深挖，但可以在阶段一完成最小快照后立刻启动最小动作闭环验证。
