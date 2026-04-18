# 阶段三 TODO：长期规划层 / Strategic Planning

配套的首批实现计划见 `docs/phase-3-plan.md`。

## 1. 目标

阶段三的目标不是继续扩动作，而是让 agent 能在阶段二已经稳定的可执行层之上，做跨战斗、跨楼层、跨资源周期的规划。

阶段三默认建立在下面这个已完成闭环之上：

1. 通过 `context -> actions -> execute -> delta` 完成稳定执行
2. 通过细粒度观察接口读取局面、地图、奖励、商店、篝火和卡牌信息
3. 在尽量低 token 的前提下，只查询当前规划真正需要的状态

## 2. 阶段三范围

阶段三要解决的核心问题：

- 地图节点如何选
- 路线如何规划
- 奖励如何基于套牌与资源做长期判断
- 战斗内资源如何受后续风险约束
- 商店 / 篝火 / 事件 / 宝箱如何服务整局目标

阶段三不新增新的底层动作系统。它主要消费阶段二已经稳定的执行接口。

## 3. 当前输入契约

阶段三默认依赖下面这些接口：

- `context`
- `observation/compact`
- `observation/delta`
- `actions`
- `knowledge/current`
- `run`
- `player/summary`
- `player/deck`
- `map/summary`
- `event`
- `shop`
- `rest-site`
- `rewards`
- `card-reward`
- `treasure`

## 4. 核心任务

### 4.1 定义 Run Planner 输入模型

需要补一层面向规划的低 token 聚合视图，至少包含：

- 当前 act / floor / 房间类型
- 当前血量、金币、药水、遗物、套牌统计
- 当前地图可达分支
- 短期威胁指标
- 长期收益指标

建议：

- 不要默认把 `player/deck` 全量文本塞给规划器
- 先做 deck summary / build summary / route summary 这类聚合结构
- 把“适合规划的摘要”和“调试级完整信息”分开

### 4.2 做地图与路线规划接口

需要输出：

- 当前地图所有可达候选的价值评估
- 每个候选的风险 / 奖励解释
- 推荐节点
- 推荐理由

建议：

- 先从单步 `choose_map_node` 评分器开始
- 再扩到 2-3 步前瞻
- 不要一开始就做整 act 全图搜索

### 4.3 做奖励与商店评估接口

至少覆盖：

- `card_reward`
- `shop_purchase`
- `choose_rest_option`
- `claim_treasure_relic`

需要输出：

- 当前候选项排序
- 放弃当前收益的理由
- 对当前 build 的直接影响
- 对后续路线 / 战斗稳定性的影响

### 4.4 把战斗内策略和战斗外规划解耦

需要明确分层：

- 战斗内短期策略：在当前回合里怎样打
- 战斗外长期规划：这层楼 / 这局 run 想要什么

要求：

- 战斗策略可以消费长期规划给出的资源约束
- 但不能直接依赖地图 / 商店具体 UI 对象
- 长期规划不能反向依赖底层战斗执行细节

### 4.5 做规划输出的 agent 友好接口

建议补充的新接口：

- `/api/v1/planning/run-summary`
- `/api/v1/planning/map-options`
- `/api/v1/planning/reward-eval`
- `/api/v1/planning/shop-eval`
- `/api/v1/planning/rest-eval`

建议：

- 输出明确推荐项和简短理由
- 输出可复用的打分字段
- 继续保持 low-token first

## 5. 建议实现顺序

1. 先做 `run-summary` 聚合视图
2. 再做 `map-options` 单步评分
3. 再做 `card_reward / treasure / rest-site / shop` 的候选评估
4. 再做战斗内资源约束输入
5. 最后做多步路线规划

## 6. 验收标准

满足下面条件，可以认为阶段三进入可用状态：

- AI 可以在地图上给出明确节点偏好并执行
- AI 可以对卡牌奖励、遗物、商店和篝火做稳定选择
- 长期规划结果能影响战斗内资源使用
- 常见规划决策不需要读取全量状态
- CLI + server 查询路径仍然保持低 token

## 7. 非目标

阶段三当前不追求：

- 完整最优搜索
- 全局最优套牌构筑器
- 强化学习训练框架
- 自动 self-play 基础设施

这些可以放到阶段三之后的研究工作里。
