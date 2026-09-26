# SciToolbox

> 原生 **WPF / C# / .NET 8** 桌面应用，把 **15 个公开学术数据库** 聚合成一个统一的检索入口。
> 不登录、不注册、不采集任何用户数据；所有本地数据都在你自己电脑上。

[English](README_EN.md) · [使用指南](docs/使用指南.md) · [许可证](LICENSE)

---

## 它解决什么问题

做生物 / 医学 / 组学研究时，一次查证常常要在 UniProt、PDB、PubMed、NCBI、Ensembl、KEGG、GO、GBIF 等一堆站点之间来回跳转，每个站点一套界面、一套筛选逻辑、一套导出方式。

SciToolbox 把这些数据库的公开 API 收进同一个界面：统一检索、统一详情结构、统一跨库跳转、统一本地收藏与导出。全程不需要账号，也不上传任何个人信息。

## 主要能力

| 能力 | 说明 |
| --- | --- |
| **三栏工作台** | 侧边栏（工具导航）→ 中栏（检索与结果）→ 右栏（详情），栏宽可拖拽，侧边栏可整体收起 |
| **15 个数据源** | 按"分类与物种 / 基因与序列 / 蛋白与结构 / 功能与通路 / 文献"五大类组织，各有独立配色 |
| **Accession 智能路由** | 粘贴 `P12345`、`1ABC`、`TP53`、DOI、`GO:xxxx`、`ENSxxxx`、`PFxxxx` 等，自动判断类型并直达对应数据库；有歧义时弹窗列出候选与置信度 |
| **全库搜索** | 一个关键词并行检索全部 15 个库，按分类分组返回，单库失败不影响其他库 |
| **跨库互链** | 详情里的关联条目可直接跳到对应数据库，右栏保留浏览历史可前后翻 |
| **收藏 / 历史 / 项目集合** | 纯本地存储；支持备注、过滤、删除可撤销；集合可按课题归组并导出 CSV / JSON |
| **批量操作** | 多选结果后批量复制编号、收藏、加入集合、导出表格 |
| **跨库并排对比** | 把条目加入对比栏，统一字段并排查看 |
| **文献导出** | BibTeX / RIS / FASTA，复制或写入文件，兼容 Zotero、EndNote、Mendeley |
| **离线与流量友好** | 断网检测并前置拦截、磁盘结果缓存、指数退避重试、NCBI 请求节流与排队提示 |
| **中英双语** | 设置页一键切换，界面即时生效 |
| **浅深色主题** | 支持跟随系统，运行中切换系统主题也会即时响应 |

完整功能说明与操作细节见 **[使用指南](docs/使用指南.md)**。

## 数据来源

| 分类 | 工具 | 接口 |
| --- | --- | --- |
| 分类与物种 | GTDB 官方分类 | gtdb-api.ecogenomic.org |
| 分类与物种 | NCBI 分类检索 | eutils.ncbi.nlm.nih.gov |
| 分类与物种 | BacDive 菌株 | api.bacdive.dsmz.de |
| 分类与物种 | MGnify 微生物组 | www.ebi.ac.uk/metagenomics |
| 分类与物种 | GBIF 物种 | api.gbif.org |
| 基因与序列 | NCBI 基因/序列 | eutils.ncbi.nlm.nih.gov |
| 基因与序列 | Ensembl 基因 | rest.ensembl.org |
| 蛋白与结构 | UniProt 蛋白质 | rest.uniprot.org |
| 蛋白与结构 | RCSB PDB 结构 | data.rcsb.org |
| 蛋白与结构 | AlphaFold 结构 | alphafold.ebi.ac.uk |
| 蛋白与结构 | Pfam / InterPro | www.ebi.ac.uk/interpro |
| 功能与通路 | KEGG 在线检索 | rest.kegg.jp |
| 功能与通路 | GO 术语查询 | www.ebi.ac.uk/QuickGO |
| 文献 | PubMed 文献 | eutils.ncbi.nlm.nih.gov |
| 文献 | Europe PMC 文献 | www.ebi.ac.uk/europepmc |

应用内始终保留"数据来源"标注，并遵守各 API 的使用条款与频率限制。

## 运行环境

- Windows 10 / 11（x64）
- 使用自包含发布版（`publish\SciToolbox.exe`）**无需安装 .NET 运行时**
- 从源码构建需要 .NET 8 SDK

## 获取与运行

从发布页下载自包含单文件 `SciToolbox.exe`，双击即可运行，无需安装。

从源码构建：

```powershell
git clone <仓库地址>
cd SciToolbox

# 调试运行
dotnet run --project src\SciToolbox

# Release 构建
.\build.ps1 -Release

# 发布自包含单文件 exe（产物：publish\SciToolbox.exe）
.\build.ps1 -Publish
```

## 键盘快捷键

| 按键 | 作用 |
| --- | --- |
| `Ctrl` + `K` | 打开命令面板，输入工具名直达；支持 `uniprot:P12345` 语法 |
| `Ctrl` + `F` | 聚焦当前库的检索框 |
| `Ctrl` + `,` | 打开设置 |
| `Ctrl` + `/` | 键盘快捷键速查 |
| `Ctrl` + `\` | 显示 / 隐藏侧边栏 |
| `Ctrl` + `[` / `]` | 详情上一条 / 下一条 |
| `↑` / `↓` / `Home` / `End` | 在结果列表中移动高亮 |
| `Enter` | 打开高亮的结果行 |
| `Ctrl` + 点击 | 结果行多选 |
| `Ctrl` + `Enter` | 保存备注 |
| `Esc` | 关闭当前弹窗 |

## 项目结构

```
SciToolbox/
├── SciToolbox.sln
├── build.ps1                    # 构建 / 发布脚本
├── README.md                    # 中文说明（本文件）
├── README_EN.md                 # English readme
├── LICENSE                      # GPL-3.0
├── docs/
│   ├── 使用指南.md              # 中文详细使用文档
│   └── USER_GUIDE_EN.md         # English user guide
└── src/SciToolbox/
    ├── App.xaml(.cs)            # 入口、主题初始化、全局异常处理、命令行诊断模式
    ├── MainWindow.xaml(.cs)     # 三栏 shell、顶栏导航、快捷键、覆盖层
    ├── Resources/AppIcon.ico    # 应用图标（exe / 任务栏 / 界面内共用）
    ├── Core/                    # 与界面无关的核心层
    │   ├── Models.cs  Json.cs  Errors.cs  Log.cs
    │   ├── ApiClient.cs  ResponseCache.cs  Prefs.cs
    │   ├── Theme.cs  Localization.cs  DomainMaps.cs  RelativeTime.cs
    │   ├── ProviderHelpers.cs  ToolRegistry.cs  AccessionRouter.cs
    │   ├── Stores.cs  CollectionStore.cs  ComparisonStore.cs
    │   └── Util.cs              # 剪贴板 / Toast / 导出 / 网络监控
    ├── Providers/               # 15 个数据源适配器
    ├── ViewModels/              # MVVM 视图模型
    └── Views/                   # 样式、模板、转换器、覆盖层控件
```

## 开发与验证

改完界面后建议按这个顺序验证：

```powershell
.\build.ps1 -Release
Remove-Item "$env:LOCALAPPDATA\SciToolbox\binding.log","$env:LOCALAPPDATA\SciToolbox\crash.log" -ErrorAction SilentlyContinue
publish\SciToolbox.exe --smoke --trace
```

`--smoke` 会遍历所有页面并生成样例数据，强制每个界面模板都被实例化；`--trace` 把绑定与资源错误写到 `%LOCALAPPDATA%\SciToolbox\binding.log`。两个日志文件都不出现，才说明界面是干净的。

其他诊断参数：

| 参数 | 作用 |
| --- | --- |
| `--demo <toolId> <query>` | 启动后自动导航、检索并打开首条详情，便于稳定复现 |
| `--selftest <toolId> <query>` | 无界面跑一次检索 + 详情，结果写 `selftest.json` |

## 数据与隐私

- 无登录、无账户体系，不采集也不上传任何用户个人数据
- 查询历史、收藏、项目集合全部保存在本机 `%LOCALAPPDATA%\SciToolbox\prefs.json`，可随时导出或一键清除
- 缓存只记录"请求 → 响应"，不含任何用户或设备标识
- 所有网络请求都是到各公开数据库官方 HTTPS 接口的直连，没有中间服务器

## 许可

本项目以 **GPL-3.0** 授权，详见 [LICENSE](LICENSE)。
