# 阶段二 TODO：动作层 / Action Execution

## 1. 目标

阶段二的目标不是让 AI 立刻“很会玩”，而是先让它可以稳定、合法地操作游戏。

阶段二要建立的闭环是:

1. 先通过阶段一接口读取 `context`
2. 再读取当前屏幕的 `actions`
3. 选择一个动作并执行
4. 用 `delta observation` 回读执行结果
5. 如果动作失败或界面切换，再进入恢复逻辑

## 2. 当前输入契约

阶段二默认依赖下面这些阶段一接口:

- `context`
- `compact observation`
- `actions`
- `observation/delta`
- `knowledge/current`

它们现在已经足够让动作层避免直接依赖大部分原始游戏对象。

## 3. 优先级

### P2-1 战斗动作闭环

先打通:

- `play_card`
- `use_potion`
- `discard_potion`
- `end_turn`

要求:

- 能按 `actions` 中的参数结构执行
- 能处理合法目标选择
- 执行后能通过 `delta observation` 看出结果

### P2-2 局外单步选择

再打通:

- `choose_map_node`
- `choose_event_option`
- `choose_rest_option`
- `shop_purchase`
- `claim_reward`
- `select_card_reward`
- `claim_treasure_relic`
- `proceed`

要求:

- 输入参数与 `actions` 返回保持一致
- 出错时能给出明确失败原因

### P2-3 多步交互

最后处理:

- `select_card`
- `confirm_selection`
- `cancel_selection`
- `skip_selection`
- 需要二次确认或多步选取的战斗效果

要求:

- 明确当前交互模式
- 避免动作发到错误 screen
- 动作失败后能恢复到可读状态

## 4. 核心任务

### 4.1 定义动作执行 API

需要确定:

- 本地 CLI 入口怎么调用动作
- server 是否直接提供写接口
- 参数格式是否复用 `actions` 的结构

建议:

- 先做独立的本地执行入口
- 参数命名完全复用 `actions`
- 不引入第二套动作命名

当前仓库已落地的起步实现:

- server 写接口: `POST /api/v1/actions/execute`
- CLI 执行入口: `./sts exec ACTION [INDEX] [TARGET]`
- 执行响应会直接返回一条关联的 `delta observation`
- 当前 canonical 参数收敛为 `action + index? + target?`
- 旧字段名仍兼容，但不再是推荐写法

### 4.2 做合法性检查

每个动作执行前要检查:

- 当前 `stateType` 是否匹配
- 索引是否仍然有效
- target 是否仍然存在
- 玩家是否还能操作
- 当前 screen 是否允许该动作

### 4.3 做动作结果回读

每个动作执行后至少要记录:

- 请求的动作名和参数
- 是否执行成功
- 如果失败，失败原因
- 对应的 `delta observation`
- 如有必要，落一份完整调试快照

### 4.4 做恢复策略

需要覆盖:

- 动作执行时屏幕刚切换
- 目标在动画过程中失效
- 手里牌顺序变化导致索引失效
- 需要 confirm 但模型直接发了 proceed

恢复策略至少包括:

- 重新读 `context`
- 重新读 `actions`
- 判断是否需要重试
- 明确哪些错误禁止自动重试

## 5. 建议实现顺序

1. 增加动作执行总入口
2. 先只支持战斗 `play_card` 和 `end_turn`
3. 再补 `use_potion` 和 `discard_potion`
4. 再补地图 / 奖励 / 事件 / 商店 / 篝火 / 宝箱
5. 最后补卡牌选择、多步交互、恢复逻辑

当前已完成到:

- 动作执行总入口
- 战斗 `play_card / use_potion / discard_potion / end_turn`
- 地图 / 奖励 / 事件 / 商店 / 篝火 / 宝箱 / 基础卡牌选择
- 战斗内 hand selection 的 `select_card / confirm_selection`
- 执行日志落盘并关联 `delta observation`
- 失败响应内建恢复建议和调试快照

当前仍待补强:

- 游戏内完整 smoke test 和边界动作回归

## 6. 验收标准

满足下面条件，可以认为阶段二进入可用状态:

- AI 可以读取 `actions` 并成功执行至少一条动作
- 战斗里能连续打牌并结束回合
- 局外至少能完成地图选择、奖励选择和事件选择
- 每个动作执行后都能读到合理的 `delta observation`
- 常见非法动作不会导致状态机失控

## 6.1 当前结论

阶段二当前结论：`已完成`

依据：

- 已在真实游戏中完成完整战斗执行
- 已在真实游戏中完成奖励领取、卡牌选择和地图前进
- `CLI -> server -> execute -> delta` 闭环已实战验证
- 当前 canonical 参数已收敛为 `action + index? + target?`
- 执行失败时可返回恢复建议、当前动作面和调试快照路径

本阶段后续不再继续堆功能，后续工作转入阶段三规划层。

## 7. 非目标

阶段二当前不追求:

- 最优决策质量
- 长期路线规划
- 套牌构筑策略
- 跨楼层资源规划

这些属于阶段三。
