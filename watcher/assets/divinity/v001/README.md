# 神格双掌展开：第一版素材工作台

2026-10-24。目标是在“美化”的原人物身份上制作“重塑”式双臂舒展、双掌向上、悬浮的神格待机姿态。最新硬性条件：不能因动作修改丢失原有光影。

**最新：首版已部署并通过一场普通战斗验证。** 只取 v002 纹理的手臂/衣袖区域，原脸、身体、腿、飘带及法阵继续由原 Spine 绘制；没有把整张生成图替换原模型。详情与回滚见 `../../../pose/README.md`。以下保留素材制作与早期试验记录。

| 文件/来源 | 用途 | 状态 |
|---|---|---|
| beautified-divinity-open-arms-concept.png | AI 辅助生成的双掌展开关键造型；1024×1536，ARGB，透明边角 | 已生成、已检查；作为新形象和补绘参考，不冒充 Spine 动画 |
| beautified-divinity-body-v002.png | 分离光环与法杖后的人物层候选 | 保留服装内部绘制的明暗；外部光效由原游戏节点负责 |
| beautified-divinity-body-v003.png | 再次强调清理外轮廓的候选 | 留作迭代对比，不因文件较新自动选为正式版 |
| asset-qa.json | 尺寸、格式、SHA256、透明/半透明/不透明采样数 | 原始 RGB 预览可能显示透明像素中的光晕色，是否可见应以 alpha 与引擎合成为准 |
| prompt.txt | 本次生成的完整提示词 | 内置 image_gen；输入1锁定身份和服装，输入2仅提供姿态 |
| ../../../../artifacts/watcher-menu-20261024-224744/references/beautified-divinity-idle.png | 引擎导出的原美化神格待机 | 原始身份参考，非生成图片 |
| ../../../../artifacts/watcher-menu-20261024-224744/references/reimagined-divinity.png | 重塑作者的原始神格形象 | 仅借鉴双臂展开与悬浮姿态，不替换原人物 |
| ../../../../artifacts/watcher-divinity-baseline-20261024-224822 | 四个相关 Mod 原文件及逐文件 SHA256 | 已复制并核对，不覆盖原件 |

## 必须保留的外观与光影

脸、可见眼睛、遮眼长发、面纱、星空纹上衣、紫色外衣、服装花纹、腿部服装、金色龙首法杖都以美化原图为准。生成图中的静态光晕是造型预览，不能替代或重复覆盖游戏中的动态效果。

接入优先改变原骨架的手臂和法杖位置，保留原网格、UV、材质和角色节点。已有光影需要以当前已启用的两 Mod 组合为基线检查：

| 层 | 当前实现 | 动作修改约束 |
|---|---|---|
| 人物、服装、法杖的绘制与原骨架光环 | 原 Spine 资源、附件与材质 | 不整体隐藏/更换人物，不删附件，不把动态光影烘焙成单张图片 |
| 天人背后白色人像/光环及姿态粒子 | YUWatcherForms.DevaEffects，独立挂在 NCreatureVisuals 后层 | 不改 ShowDevaEffects、不移动到前景、不替换或关闭其节点 |
| 七帧神格眼睛背景、角色周围眼睛 | YUWatcherForms.DivinityEyesBackground | 保留控制器、透明度与帧动画，不随手臂动作重建背景 |
| 瞄准、出牌、命中及姿态切换特效 | 已启用的作者特效系统 | 新动作不接管选牌/出牌逻辑，不改变现有开关、伤害和时序 |
| 受击、死亡、血量换档、退出神格 | 原美化控制器与骨架池 | 在动作边界回退，不让新待机姿势覆盖这些行为 |

后续补绘以空掌、手腕和展开后的衣袖为主。若原骨架已经能完成某处动作，优先复用；生成整身图不直接作为最终整身替换，否则容易丢失原骨架动作和光影层。

原骨架试摆结果：`artifacts/watcher-menu-20261024-230523/references/beautified-open-arms-rig-prototype.png`（相对工程根目录）。复用作者 IK 求解器后，法杖可直立悬浮，原头后光环仍可见，但长袖被拉成横板，肩部出现断开感，手型也不能达到自然摊掌。此候选明确不验收、不部署；需要补画衣袖/手掌并重绑局部，而不能仅将骨骼转到目标角度。

## 验证与已知问题

导出已取得可读的原图、图集描述、重塑分层骨架描述和 127 根美化骨骼的信息。早期测试导出文件成功后，进程关闭时出现 `0xC0000005`；不能算整条引擎测试通过。前两份骨骼清单名称为空，后续已改为读取实际 `get_bone_name`。所有测试只进入菜单/独立离屏视口，不出发、不放弃既有对局；设置和偏好按进入时备份恢复，临时插件归档移出 mods。

`watcher-menu-20261024-225915` 与 `watcher-menu-20261024-230523` 的 `process-result.json` 均记录退出码 -1073741819；这些测试不是 PASS。已尝试主线程释放临时 Spine 对象/数组和清空延迟回收，退出异常仍未解决。`Test-Selection.ps1 -Assets` 保持严格检查退出码，不可放宽成假通过。正常模式原有选人检查与此实验资产模式分开。

本轮没有修改作者 DLL/PCK/动作覆盖文件。最终独立插件已正式部署；正常战斗记录 `artifacts/native-compatibility-20261024-233858` 为最终依据，早期失败记录不能替代它，也不能据此声称最终插件失败。

## 来源与迁移

- 美化人物的战斗资源来源：安装包 README 明确标注 Erowatcher（工坊 3784906423）；本机形态包为 YUWatcherForms（3817002306）。
- Watcher Beautified 工坊页标注其美术基于 @anaertailin 的同人作品：https://steamcommunity.com/sharedfiles/filedetails/?id=3747800917 。
- 姿态参考：https://steamcommunity.com/sharedfiles/filedetails/?id=3818575110 。
- 已查阅 Spine 官方网格/权重说明：https://esotericsoftware.com/spine-meshes 、https://esotericsoftware.com/spine-weights 。两套骨架不是同一结构，不能直接复制同名动作后宣称兼容。

新电脑复制整个 STS2ModCompat（含 tools/artifacts），使用 GamePath 参数；`watcher/scripts/Snapshot-DivinitySources.ps1` 可建立当地基线。后续导出/预览采用原生 `Export-Atlas.gd`、`Preview-Pose.gd`；早期 `Test-Selection.ps1 -Assets` 入口已移除，源码归档供追溯。第三方参考与备份仅供本地工作，不默认公开再分发。
