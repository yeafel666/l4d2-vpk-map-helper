# 求生之路2 · 第三方战役开图指令助手

把第三方战役的 `.vpk` 文件拖进来，自动解析出每一关的 `map` 开图指令，并**按第一关到最后一关的顺序**排好 —— 不用再为了找关卡名翻半天文件夹。

> 作者：Yeafel ｜ 许可证：MIT ｜ 完全离线，不联网、不上传任何数据

---

## 为什么需要它

开 L4D2 服务器时，第三方战役（创意工坊）里的关卡要手动 `map <地图名>` 才能进。

而地图名是**打包在 `.vpk` 里的英文名**（`requiem_01`、`c1m1`…），关卡的显示名（Aleph、Ascension…）藏在战役的 `missions/*.txt` 里。想让玩家能进某一关，主玩就得自己把 VPK 解开翻目录。

本工具做三件事：

1. 读懂 Valve 的 **VPK** 打包格式，找出里面真正的 `.bsp` 地图
2. 解析战役的 `missions/*.txt`，拿到**作者写好的权威关卡顺序**
3. 输出可直接粘贴到服务器控制台的 `map` 指令

---

## 直接下载使用

到 [Releases](https://github.com/yeafel666/l4d2-vpk-map-helper/releases) 下载 `L4D2-MapHelper-v1.0.0.zip`（附件名仅支持 ASCII，包内就是 `L4D2开图助手.exe`），解压双击即可。

- Windows 10 / 11 自带的 .NET Framework 4.x 就能跑，**无需安装任何东西**
- 全程离线，只读取本地文件
- 单文件 exe，整个文件夹拷到别的电脑上照样能用

---

## 用法

### 方式一：拖拽（推荐）

把 `.vpk` 文件拖到程序窗口中间的白框里。

### 方式二：拖到程序图标上

把 `.vpk` 文件直接拖到 `L4D2开图助手.exe` 的图标上，程序会自动打开并解析。

### 方式三：按钮

点右下角「浏览添加...」用文件选择框挑。

### 一次拖多个 VPK（重要）

同一张大地图在创意工坊里常被拆成好几个 `.vpk`：

- 一次把多个 `.vpk` 一起拖进来即可，程序会自动合并解析、汇总去重
- 也可以把整个文件夹（比如 `left4dead2\addons`）拖进来，会自动扫描里面所有 `.vpk`
- 如果是 `xxx_dir.vpk` + `xxx_001.vpk` 的**分卷**形式，只要拖 `xxx_dir.vpk`，程序会自动去同目录把分卷一起用上
  - 万一只拖了分卷，程序会直接告诉你「地图目录在 xxx_dir.vpk 里」，照提示补上就行

拖进来后，中间的「已载入的 VPK 文件」列表会显示每个文件的解析状态（找到几张图 / 有没有问题），选错的可以「移除选中」。

---

## 输出示例

以创意工坊战役 *Prodeus Requiem* 为例：

```
序号  开图指令               关卡名                所属战役          来源文件
 1    map requiem_01         Aleph                 Prodeus Requiem   3492541870.vpk
 2    map requiem_02         Ascension             Prodeus Requiem   3492541870.vpk
 3    map requiem_03         Immortal Garden       Prodeus Requiem   3492541870.vpk
 4    map requiem_04         Night at the Museum   Prodeus Requiem   3492541870.vpk
 5    map requiem_05         Steel Ball Run        Prodeus Requiem   3492541870.vpk
```

- 点「复制全部开图指令」→ 每关一条 `map` 指令，直接粘到服务器控制台
- **双击任意一行** → 只复制那一关的指令
- 点「导出 TXT 报告」→ 存一份带完整信息的报告文件

---

## 排序规则

结果表按战役分组，组内就是从第一关到最后一关的顺序。排序依据按以下优先级降级：

| 优先级 | 依据 | 说明 |
|---|---|---|
| 1 | `missions/*.txt` 的权威序 | 战役作者在 `modes → coop` 里用数字键显式列出的顺序，**和游戏里显示的关卡顺序一致**，最准 |
| 2 | 地图命名规则 | `c1m1` / `c2m1` 这类章节号，或 `xxx_01` / `xxx_02` 这类尾部数字 |
| 3 | VPK 内存储偏移 | 打包时的先后顺序，一般也接近战役顺序 |

左下角下拉框可手动切换成「按关卡名」或「按 VPK 内顺序」。

---

## 原理备忘

### VPK 目录树是**三层嵌套**结构

```
扩展名\0 → 路径\0 → ("文件名\0" + 18字节条目头)* → \0 → 下一个路径 → … → \0 → 下一个扩展名 → … → \0(树结束)
```

把扩展名和路径当成一对是**错的** —— 同一个扩展名下可以有多个路径（`bsp` 下同时有 `maps` 和 `missions`），扁平模型会整体错位。

条目头固定 18 字节：

```
crc32(4) | preload_length(2) | archive_index(2) | archive_offset(4) | file_length(4) | suffix(2)=0xFFFF
```

数据区起点 = `头部长度 + treeSize`（v1 头 12 字节，v2 头 28 字节）。

### 分卷（split archive）

```
xxx_dir.vpk     ← 目录树在这里
xxx_001.vpk     ← 第一卷数据（裸数据，连 VPK 头都没有，偏移从 0 起算）
xxx_002.vpk     ← 第二卷数据
```

条目头里 `archive_index != 0x7fff` 表示数据在分卷里。

### VBSP 魔数校验

Source 引擎的 `.bsp` 文件必以 `VBSP` 开头，这是格式内不可伪造的特征。程序用它来挡掉「名字像地图但其实不是」的文件 —— 勾选「严格校验」时只认通过校验的条目。

---

## 从源码构建

无需安装任何 SDK，Windows 自带的 .NET Framework 编译器就够（**C# 5 语法限制**：无字符串插值、无 `?.`、无表达式体成员、无 `nameof`）：

```powershell
pwsh -File build.ps1
```

或者手动：

```powershell
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

# 1) 生成图标
& $csc /nologo /out:build\IconGen.exe src\..\tools\IconGen.cs
& build\IconGen.exe

# 2) 编译主程序
& $csc /nologo /target:winexe /out:build\L4D2MapHelper.exe /win32icon:build\l4d2icon.ico `
    src\MainForm.cs src\Vpk.cs src\KeyValues.cs src\MapCollector.cs src\Mission.cs src\Program.cs
```

- 编译 GUI 用 `/target:winexe`，加图标用 `/win32icon:xxx.ico`
- `.NET Framework 4.x` 没有 `Encoding.Latin1`，需要时用 `Encoding.GetEncoding(28591)`

---

## 目录结构

```
src/                主程序源码
  MainForm.cs       WinForms 界面
  Vpk.cs            VPK 头 / 目录树 / 分卷 / VBSP 校验
  KeyValues.cs      Valve KeyValues 文本解析（missions/*.txt 用）
  MapCollector.cs   地图收集、分组、排序
  Mission.cs        战役定义解析
  Program.cs        入口（含命令行参数支持）
tools/              开发辅助工具
  IconGen.cs        生成多分辨率 ICO
  SplitGen.cs       生成合成测试样本（分卷格式）
  Verify.cs         控制台版解析验证
  *.cs              逆向 VPK 格式时的临时探针，已完成使命，保留供参考
docs/使用说明.txt    随发布包附带的用户手册
build.ps1           一键构建脚本
```

---

## 许可

MIT © 2026 Yeafel
